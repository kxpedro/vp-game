using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>Oferta da Loja da Camara de Hemomancia: artefato, preco base da raridade e preco final (com desconto de skill).</summary>
public sealed record ShopOffer(ArtifactDefinition Definition, int BasePrice, int Price);

public static class ArtifactCatalog
{
    private const string CatalogPath = "res://data/artifacts/artifacts.json";
    private static readonly List<ArtifactDefinition> Definitions = new();
    private static readonly Dictionary<string, ArtifactDefinition> DefinitionsById = new();
    private static bool _loaded;
    private static float _basePowerMultiplier = 1.75f;

    // Loja (docs/design/economia-v2.md, secoes 6 e 9.4). Ordem = raridade crescente; indices alinhados com as tabelas abaixo.
    private static readonly string[] ShopRarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic" };
    private static readonly int[] DefaultShopPrices = EconomyDefaults.ShopPrices;
    // [raridade][visita 0..2]
    private static readonly float[][] DefaultShopRarityWeights =
    {
        new[] { 45.0f, 35.0f, 25.0f },
        new[] { 33.0f, 30.0f, 28.0f },
        new[] { 15.0f, 20.0f, 22.0f },
        new[] { 6.0f, 10.0f, 15.0f },
        new[] { 1.0f, 4.0f, 8.0f },
        new[] { 0.0f, 1.0f, 2.0f },
    };
    private const int ShopVisitCount = 3;
    private const int FirstSlotMaxRarityIndex = 1; // economy.shop.first_slot_max_rarity = Uncommon
    private const float MaxShopDiscount = 0.9f;

    /// <summary>
    /// Multiplicador de poder aplicado aos efeitos numericos dos artefatos comprados (economy.shop.power_multiplier,
    /// padrao 1,75 = "bossDropPowerMultiplier" do JSON). Mesmo valor de antes da loja (decisao D14).
    /// </summary>
    public static float ShopPowerMultiplier
    {
        get
        {
            EnsureLoaded();
            return Mathf.Max(CompendiumTuning.GetFloat("economy.shop.power_multiplier", _basePowerMultiplier), 0.0f);
        }
    }

    /// <summary>Se verdadeiro (economy.shop.exclude_owned, padrao 1), a loja nao oferece nem vende artefato que a run ja tem (D13).</summary>
    public static bool ShopExcludesOwned => CompendiumTuning.GetInt("economy.shop.exclude_owned", 1) != 0;

    /// <summary>
    /// Indice da visita atual a Loja (0..2), usado nos pesos de raridade. A Camara de Hemomancia carrega DEPOIS de
    /// RunState.CompleteBoss ter incrementado CurrentMapIndex, entao o mapa que acabou de terminar e CurrentMapIndex - 1.
    /// </summary>
    public static int CurrentShopVisitIndex => Math.Clamp(RunState.CurrentMapIndex - 1, 0, ShopVisitCount - 1);

    public static IReadOnlyList<ArtifactDefinition> All
    {
        get
        {
            EnsureLoaded();
            return Definitions;
        }
    }

    public static bool TryGet(string artifactId, out ArtifactDefinition definition)
    {
        EnsureLoaded();
        return DefinitionsById.TryGetValue(artifactId, out definition);
    }

    public static ArtifactRecord CreateRecord(ArtifactDefinition definition)
    {
        var effect = definition.Effects.Length > 0 ? definition.Effects[0] : new ArtifactEffectDefinition("", "", 0.0f);
        var value = definition.Effects.Length > 0
            ? CompendiumTuning.GetFloat($"{definition.Id}.effect.0.value", effect.Value)
            : 0.0f;

        return new ArtifactRecord(definition.Id, definition.SaveName, definition.Slot, effect.Type, value);
    }

    // Definição a partir do que o save guarda (Id ou SaveName legado de ArtifactRecord.Name / slot equipado).
    public static bool TryGetByRecordName(string idOrSaveName, out ArtifactDefinition definition)
    {
        EnsureLoaded();
        definition = null;
        if (string.IsNullOrWhiteSpace(idOrSaveName))
            return false;
        if (DefinitionsById.TryGetValue(idOrSaveName, out definition))
            return true;
        foreach (var candidate in Definitions)
        {
            if (candidate.SaveName == idOrSaveName)
            {
                definition = candidate;
                return true;
            }
        }
        return false;
    }

    // Nome exibido (idioma atual) para um ArtifactRecord.Name / slot equipado; sem definição, devolve o próprio texto.
    public static string DisplayNameFor(string idOrSaveName)
        => TryGetByRecordName(idOrSaveName, out var definition) ? definition.DisplayName : idOrSaveName ?? "";

    public static string DisplayNameFor(ArtifactRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.Id) && TryGet(record.Id, out var definition))
            return definition.DisplayName;
        return DisplayNameFor(record.Name);
    }

    /// <summary>Preco base (sem desconto) da raridade do artefato: economy.shop.price.[raridade] (250/420/700/1.100/1.700/2.400).</summary>
    public static int GetShopBasePrice(ArtifactDefinition definition)
    {
        var index = ShopRarityIndex(definition.Rarity);
        if (index < 0)
            index = 0;
        return CompendiumTuning.GetInt($"economy.shop.price.{ShopRarities[index].ToLowerInvariant()}", DefaultShopPrices[index]);
    }

    /// <summary>Desconto no preco da loja vindo da skill humanitas.collector_eye (0,05 por rank; limitado a 0,9).</summary>
    public static float GetShopDiscount() => Mathf.Clamp(PlayerSkillController.ShopPriceDiscount(), 0.0f, CompendiumTuning.GetFloat("economy.shop.max_discount", MaxShopDiscount));

    /// <summary>Preco final na loja: preco base da raridade com o desconto da skill (le o rank em CharacterSheet; estatico, serve a cena da Camara).</summary>
    public static int GetShopPrice(ArtifactDefinition definition)
    {
        var price = GetShopBasePrice(definition) * (1.0f - GetShopDiscount());
        return Math.Max(1, (int)Math.Round(price, MidpointRounding.AwayFromZero));
    }

    /// <summary>Sorteia o estoque da visita atual (CurrentShopVisitIndex); ownedIds nulo = artefatos da run em RunState.</summary>
    public static List<ShopOffer> RollShopStock(RandomNumberGenerator rng, IReadOnlyCollection<string> ownedIds = null)
        => RollShopStock(rng, CurrentShopVisitIndex, ownedIds);

    /// <summary>
    /// Estoque da Loja: 3 a 4 artefatos (economy.shop.stock_min/max, 50% de chance do 4o), sem repeticao no estoque,
    /// sem os ja possuidos (se economy.shop.exclude_owned) e com o PRIMEIRO slot sempre Common ou Uncommon. A raridade
    /// de cada slot sai da tabela da visita (economy.shop.rarity_weight.[raridade].[visita]; classes sem artefato
    /// elegivel tem o peso redistribuido); dentro da classe vale o campo "weight" do artefato. Nao usa o multiplicador
    /// de raridade do sorteio antigo do boss. Ofertas contam como vistas no diario de descobertas.
    /// </summary>
    /// <param name="visitIndex">0 a 2: mapa que acabou de terminar (veja CurrentShopVisitIndex).</param>
    public static List<ShopOffer> RollShopStock(RandomNumberGenerator rng, int visitIndex, IReadOnlyCollection<string> ownedIds = null)
    {
        EnsureLoaded();
        visitIndex = Math.Clamp(visitIndex, 0, ShopVisitCount - 1);
        var excludeOwned = ShopExcludesOwned;
        var owned = ownedIds ?? RunState.GetRunArtifactIds();
        var allowDuplicates = CompendiumTuning.GetInt("economy.shop.allow_duplicates_in_stock", 0) != 0;

        var pools = new List<ArtifactDefinition>[ShopRarities.Length];
        for (var i = 0; i < pools.Length; i++)
            pools[i] = new List<ArtifactDefinition>();
        foreach (var definition in Definitions)
        {
            var rarityIndex = ShopRarityIndex(definition.Rarity);
            if (rarityIndex < 0)
                continue;
            if (excludeOwned && ContainsId(owned, definition.Id))
                continue;
            pools[rarityIndex].Add(definition);
        }

        var count = CompendiumTuning.GetInt("economy.shop.stock_min", 3);
        var maxCount = Math.Max(CompendiumTuning.GetInt("economy.shop.stock_max", 4), count);
        var extraChance = CompendiumTuning.GetFloat("economy.shop.stock_extra_chance", 0.5f);
        for (var extra = count; extra < maxCount; extra++)
        {
            if (rng.Randf() >= extraChance)
                break;
            count++;
        }

        var stock = new List<ShopOffer>();
        for (var slot = 0; slot < count; slot++)
        {
            var selected = PickShopArtifact(rng, pools, visitIndex, slot == 0);
            if (selected == null)
                break;

            stock.Add(new ShopOffer(selected, GetShopBasePrice(selected), GetShopPrice(selected)));
            if (!allowDuplicates)
                pools[ShopRarityIndex(selected.Rarity)].Remove(selected);
            CodexProgress.MarkDiscovered(CodexCategory.Artifact, selected.Id);
        }

        return stock;
    }

    private static bool ContainsId(IReadOnlyCollection<string> ids, string id)
    {
        if (ids == null)
            return false;
        foreach (var candidate in ids)
            if (candidate == id)
                return true;
        return false;
    }

    private static int ShopRarityIndex(string rarity)
    {
        for (var i = 0; i < ShopRarities.Length; i++)
            if (ShopRarities[i].Equals(rarity, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static ArtifactDefinition PickShopArtifact(RandomNumberGenerator rng, List<ArtifactDefinition>[] pools, int visitIndex, bool firstSlot)
    {
        // Primeiro slot: so Common/Uncommon. Se o pool barato esgotou (caso extremo), cai para qualquer classe.
        var rarityIndex = PickShopRarity(rng, pools, visitIndex, firstSlot);
        if (rarityIndex < 0 && firstSlot)
            rarityIndex = PickShopRarity(rng, pools, visitIndex, false);
        if (rarityIndex < 0)
            return null;

        var pool = pools[rarityIndex];
        var totalWeight = 0.0f;
        foreach (var definition in pool)
            totalWeight += Mathf.Max(CompendiumTuning.GetFloat($"{definition.Id}.weight", definition.Weight), 0.0f);
        if (totalWeight <= 0.0f)
            return pool[rng.RandiRange(0, pool.Count - 1)];

        var roll = rng.RandfRange(0.0f, totalWeight);
        var cursor = 0.0f;
        foreach (var definition in pool)
        {
            cursor += Mathf.Max(CompendiumTuning.GetFloat($"{definition.Id}.weight", definition.Weight), 0.0f);
            if (roll <= cursor)
                return definition;
        }
        return pool[^1];
    }

    private static int PickShopRarity(RandomNumberGenerator rng, List<ArtifactDefinition>[] pools, int visitIndex, bool cheapOnly)
    {
        var firstSlotMaxRarity = CompendiumTuning.GetInt("economy.shop.first_slot_max_rarity", FirstSlotMaxRarityIndex);
        var weights = new float[pools.Length];
        var totalWeight = 0.0f;
        var firstAvailable = -1;
        for (var i = 0; i < pools.Length; i++)
        {
            if (pools[i].Count == 0 || (cheapOnly && i > firstSlotMaxRarity))
                continue;
            if (firstAvailable < 0)
                firstAvailable = i;
            weights[i] = Mathf.Max(CompendiumTuning.GetFloat(
                $"economy.shop.rarity_weight.{ShopRarities[i].ToLowerInvariant()}.{visitIndex}", DefaultShopRarityWeights[i][visitIndex]), 0.0f);
            totalWeight += weights[i];
        }

        // Sem peso nas classes disponiveis (ex.: so restou Mythic na visita 1): usa a mais barata disponivel.
        if (totalWeight <= 0.0f)
            return firstAvailable;

        var roll = rng.RandfRange(0.0f, totalWeight);
        var cursor = 0.0f;
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0.0f)
                continue;
            cursor += weights[i];
            if (roll <= cursor)
                return i;
        }
        for (var i = weights.Length - 1; i >= 0; i--)
            if (weights[i] > 0.0f)
                return i;
        return firstAvailable;
    }

    public static void Reload()
    {
        _loaded = false;
        _basePowerMultiplier = 1.75f;
        Definitions.Clear();
        DefinitionsById.Clear();
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;

        if (!FileAccess.FileExists(CatalogPath))
        {
            GD.PushError($"Artifact catalog not found: {CatalogPath}");
            return;
        }

        try
        {
            var json = FileAccess.GetFileAsString(CatalogPath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("bossDropPowerMultiplier", out var power) && power.TryGetSingle(out var multiplier))
                _basePowerMultiplier = Mathf.Max(multiplier, 0.0f);

            if (!document.RootElement.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            {
                GD.PushError("Artifact catalog is missing an artifacts array.");
                return;
            }

            foreach (var artifactElement in artifacts.EnumerateArray())
                AddDefinition(ParseDefinition(artifactElement));
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to load artifact catalog: {exception.Message}");
        }
    }

    private static ArtifactDefinition ParseDefinition(JsonElement element)
    {
        // Nome, descrição e flavor ficam em content.json (artifact.<id>.name/.desc/.flavor); "saveName" é só o
        // identificador legado persistido no save.
        return new ArtifactDefinition(
            ReadString(element, "id"),
            ReadString(element, "saveName"),
            ReadString(element, "icon"),
            ReadString(element, "rarity"),
            ReadString(element, "slot"),
            ReadInt(element, "weight"),
            ReadStringArray(element, "tags"),
            ReadEffects(element)
        );
    }

    private static void AddDefinition(ArtifactDefinition definition)
    {
        if (!definition.Slot.Equals("Passive", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            GD.PushWarning("Artifact catalog ignored an entry without id.");
            return;
        }

        if (DefinitionsById.ContainsKey(definition.Id))
        {
            GD.PushWarning($"Artifact catalog ignored duplicate id: {definition.Id}");
            return;
        }

        Definitions.Add(definition);
        DefinitionsById[definition.Id] = definition;
    }

    private static ArtifactDefinition RollOne(RandomNumberGenerator rng, List<ArtifactDefinition> pool)
    {
        var totalWeight = 0.0f;
        foreach (var definition in pool)
            totalWeight += GetSpawnWeight(definition);

        if (totalWeight <= 0.0f)
            return pool.Count > 0 ? pool[0] : null;

        var roll = rng.RandfRange(0.0f, totalWeight);
        var cursor = 0.0f;

        foreach (var definition in pool)
        {
            cursor += GetSpawnWeight(definition);
            if (roll <= cursor)
                return definition;
        }

        return pool[^1];
    }

    private static float GetSpawnWeight(ArtifactDefinition definition)
    {
        var baseWeight = CompendiumTuning.GetFloat($"{definition.Id}.weight", definition.Weight);
        return Mathf.Max(baseWeight * GetRarityMultiplier(definition.Rarity), 0.0f);
    }

    private static float GetRarityMultiplier(string rarity)
    {
        return rarity switch
        {
            "Common" => 1.0f,
            "Uncommon" => 0.65f,
            "Rare" => 0.35f,
            "Epic" => 0.18f,
            "Legendary" => 0.08f,
            _ => 1.0f
        };
    }

    private static ArtifactEffectDefinition[] ReadEffects(JsonElement element)
    {
        if (!element.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array)
            return Array.Empty<ArtifactEffectDefinition>();

        var definitions = new List<ArtifactEffectDefinition>();
        foreach (var effectElement in effects.EnumerateArray())
        {
            definitions.Add(new ArtifactEffectDefinition(
                ReadString(effectElement, "type"),
                ReadString(effectElement, "operation"),
                ReadFloat(effectElement, "value")
            ));
        }

        return definitions.ToArray();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var values = new List<string>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                values.Add(item.GetString() ?? "");
        }

        return values.ToArray();
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;
    }

    private static float ReadFloat(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetSingle(out var number)
            ? number
            : 0.0f;
    }
}

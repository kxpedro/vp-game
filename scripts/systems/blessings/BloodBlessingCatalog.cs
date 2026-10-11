using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class BloodBlessingCatalog
{
    private const string CatalogPath = "res://data/blessings/blood_blessings.json";
    private const float StirpsFamilySelectionWeight = 0.45f;
    private const float DirectAttackBlessingWeightMultiplier = 1.75f;
    private static readonly List<BloodBlessingDefinition> Definitions = new();
    private static readonly Dictionary<string, BloodBlessingDefinition> ById = new();
    private static readonly List<BloodBlessingFamily> Families = new();
    private static readonly List<BloodBlessingRarity> Rarities = new();
    private static bool _loaded;

    public static IReadOnlyList<BloodBlessingDefinition> All { get { EnsureLoaded(); return Definitions; } }
    public static IReadOnlyList<BloodBlessingFamily> AllFamilies { get { EnsureLoaded(); return Families; } }
    public static IReadOnlyList<BloodBlessingRarity> AllRarities { get { EnsureLoaded(); return Rarities; } }

    public static int GetRarityLevel(string rarityName)
    {
        EnsureLoaded();
        for (var i = 0; i < Rarities.Count; i++)
            if (Rarities[i].Id.Equals(rarityName, StringComparison.OrdinalIgnoreCase) ||
                Rarities[i].DisplayName.Equals(rarityName, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return 1;
    }

    public static int GetSummonedRatCapacity(BloodBlessingRecord record)
    {
        var rarityCapacity = GetRarityLevel(record.Rarity);
        var levelBonus = System.Math.Clamp(record.Level, 1, BloodBlessingProgression.MaximumLevel) - 1;
        return System.Math.Max(rarityCapacity + levelBonus, 1);
    }

    public static bool TryGet(string id, out BloodBlessingDefinition definition)
    {
        EnsureLoaded();
        return ById.TryGetValue(id, out definition);
    }

    public static List<BloodBlessingDefinition> RollChoices(RandomNumberGenerator rng, int count) => RollChoices(rng, count, out _);

    public static List<BloodBlessingOffer> RollOffers(RandomNumberGenerator rng, int count, out string selectedFamily, bool guaranteeBasicAttack = false)
    {
        var definitions = RollChoices(rng, count, out selectedFamily, guaranteeBasicAttack);
        var offers = new List<BloodBlessingOffer>();
        foreach (var definition in definitions)
            offers.Add(RollOffer(rng, definition));
        return offers;
    }

    /// <summary>
    /// Reamostra uma oferta pré-sorteada (porta de Bênção, RoomRewardCatalog.PlanBlessings) ao abri-la. Cada opção que
    /// ainda é elegível (mesmas regras de RollChoices: sem bênção já adquirida e pré-requisitos atendidos) fica no
    /// mesmo lugar, com a raridade já sorteada. As demais são trocadas por outras da mesma linhagem, sem repetir;
    /// sem bênção da linhagem sobrando, usa as de outra linhagem. Devolve menos opções só se não houver mais nenhuma.
    /// <paramref name="replacedCount"/> = quantas opções foram trocadas (para log/teste).
    /// </summary>
    public static List<BloodBlessingOffer> ReviewPlannedOffers(RandomNumberGenerator rng, IReadOnlyList<BloodBlessingOffer> planned, string family, bool guaranteeBasicAttack, out int replacedCount)
    {
        EnsureLoaded();
        replacedCount = 0;
        if (planned == null || planned.Count == 0)
            return new List<BloodBlessingOffer>();

        var slots = new BloodBlessingOffer[planned.Count];
        var missing = new List<int>();
        for (var i = 0; i < planned.Count; i++)
        {
            var offer = planned[i];
            if (offer?.Definition != null && IsEligible(offer.Definition))
                slots[i] = offer;
            else
                missing.Add(i);
        }
        replacedCount = missing.Count;

        var needBasicAttack = guaranteeBasicAttack && !HasBasicAttackOffer(slots);
        foreach (var slot in missing)
        {
            var pool = ReplacementPool(family, slots, true);
            if (pool.Count == 0)
                pool = ReplacementPool(family, slots, false);

            var candidates = pool;
            if (needBasicAttack)
            {
                var basicPool = pool.FindAll(definition => HasTag(definition, "basic_attack"));
                if (basicPool.Count == 0)
                    basicPool = ReplacementPool(family, slots, false).FindAll(definition => HasTag(definition, "basic_attack"));
                if (basicPool.Count > 0)
                {
                    candidates = basicPool;
                    needBasicAttack = false;
                }
            }
            if (candidates.Count == 0)
                continue;

            slots[slot] = RollOffer(rng, SelectWeighted(rng, candidates));
        }

        var result = new List<BloodBlessingOffer>(planned.Count);
        foreach (var offer in slots)
            if (offer != null)
                result.Add(offer);
        return result;
    }

    // Uma oferta com raridade sorteada (Stirps tem raridade fixa). Marca a bênção como vista no diário (Fonte/porta).
    private static BloodBlessingOffer RollOffer(RandomNumberGenerator rng, BloodBlessingDefinition definition)
    {
        var rarity = definition.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase)
            ? new BloodBlessingRarity("stirps", "Stirps", 1.0f, 1.0f)
            : RollRarity(rng);
        // Diario de descobertas: bencao oferecida na Fonte de Sangue (ou numa porta) conta como vista.
        CodexProgress.MarkDiscovered(CodexCategory.Blessing, definition.Id);
        return new BloodBlessingOffer(definition, rarity.DisplayName, rarity.PowerMultiplier);
    }

    // Candidatas para trocar uma opção: elegíveis, sem repetir as que já estão nas opções. Só da linhagem ou de qualquer uma.
    private static List<BloodBlessingDefinition> ReplacementPool(string family, IReadOnlyList<BloodBlessingOffer> slots, bool sameFamilyOnly)
    {
        var pool = new List<BloodBlessingDefinition>();
        foreach (var definition in Definitions)
        {
            if (sameFamilyOnly && !definition.Family.Equals(family, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsEligible(definition) || IsInSlots(definition.Id, slots)) continue;
            pool.Add(definition);
        }
        return pool;
    }

    private static bool IsInSlots(string id, IReadOnlyList<BloodBlessingOffer> slots)
    {
        foreach (var offer in slots)
            if (offer != null && offer.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool HasBasicAttackOffer(IReadOnlyList<BloodBlessingOffer> slots)
    {
        foreach (var offer in slots)
            if (offer != null && HasTag(offer.Definition, "basic_attack")) return true;
        return false;
    }

    public static List<BloodBlessingDefinition> RollChoices(RandomNumberGenerator rng, int count, out string selectedFamily, bool guaranteeBasicAttack = false)
    {
        EnsureLoaded();
        selectedFamily = "";
        var availableFamilies = new List<string>();
        var fallbackFamilies = new List<string>();
        foreach (var family in Families)
        {
            var available = 0;
            var hasBasicAttack = false;
            foreach (var definition in Definitions)
            {
                if (!definition.Family.Equals(family.DisplayName, StringComparison.OrdinalIgnoreCase) || !IsEligible(definition)) continue;
                available++;
                if (HasTag(definition, "basic_attack")) hasBasicAttack = true;
            }
            if (available > 0 && (!guaranteeBasicAttack || hasBasicAttack)) fallbackFamilies.Add(family.DisplayName);
            var isStirps = family.DisplayName.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
            if ((available >= count || (isStirps && available > 0)) && (!guaranteeBasicAttack || hasBasicAttack)) availableFamilies.Add(family.DisplayName);
        }
        if (availableFamilies.Count == 0) availableFamilies.AddRange(fallbackFamilies);
        if (availableFamilies.Count == 0) return new List<BloodBlessingDefinition>();
        selectedFamily = SelectFamily(rng, availableFamilies);
        var pool = new List<BloodBlessingDefinition>();
        foreach (var definition in Definitions)
            if (definition.Family.Equals(selectedFamily, StringComparison.OrdinalIgnoreCase) && IsEligible(definition))
                pool.Add(definition);

        var choices = new List<BloodBlessingDefinition>();
        if (guaranteeBasicAttack)
        {
            var basicPool = pool.FindAll(definition => HasTag(definition, "basic_attack"));
            if (basicPool.Count > 0)
            {
                var guaranteed = SelectWeighted(rng, basicPool);
                choices.Add(guaranteed);
                pool.Remove(guaranteed);
            }
        }
        while (choices.Count < count && pool.Count > 0)
        {
            var selected = SelectWeighted(rng, pool);
            choices.Add(selected);
            pool.Remove(selected);
        }
        return choices;
    }

    private static bool HasTag(BloodBlessingDefinition definition, string tag)
    {
        foreach (var entry in definition.Tags)
            if (entry.Equals(tag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static BloodBlessingDefinition SelectWeighted(RandomNumberGenerator rng, List<BloodBlessingDefinition> pool)
    {
        var total = 0.0f;
        foreach (var entry in pool) total += EffectiveSelectionWeight(entry);
        var roll = rng.RandfRange(0.0f, Mathf.Max(total, 0.01f));
        var cursor = 0.0f;
        var selected = pool[^1];
        foreach (var entry in pool)
        {
            cursor += EffectiveSelectionWeight(entry);
            if (roll <= cursor) { selected = entry; break; }
        }
        return selected;
    }

    private static float EffectiveSelectionWeight(BloodBlessingDefinition definition)
    {
        var multiplier = 1.0f;
        foreach (var effect in definition.Effects)
        {
            if (!effect.Type.StartsWith("BasicAttack", StringComparison.OrdinalIgnoreCase) &&
                !effect.Type.StartsWith("QuickAttack", StringComparison.OrdinalIgnoreCase) &&
                !effect.Type.StartsWith("SpecialAttack", StringComparison.OrdinalIgnoreCase))
                continue;
            multiplier = DirectAttackBlessingWeightMultiplier;
            break;
        }
        return Mathf.Max(definition.Weight, 0) * multiplier;
    }

    private static string SelectFamily(RandomNumberGenerator rng, List<string> families)
    {
        var totalWeight = 0.0f;
        foreach (var family in families)
            totalWeight += family.Equals("Stirps", StringComparison.OrdinalIgnoreCase) ? StirpsFamilySelectionWeight : 1.0f;

        var roll = rng.RandfRange(0.0f, Mathf.Max(totalWeight, 0.01f));
        var cursor = 0.0f;
        foreach (var family in families)
        {
            cursor += family.Equals("Stirps", StringComparison.OrdinalIgnoreCase) ? StirpsFamilySelectionWeight : 1.0f;
            if (roll <= cursor)
                return family;
        }
        return families[^1];
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        if (!FileAccess.FileExists(CatalogPath)) { GD.PushError($"Blood blessing catalog not found: {CatalogPath}"); return; }
        try
        {
            using var document = JsonDocument.Parse(
                FileAccess.GetFileAsString(CatalogPath),
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            ReadRarities(document.RootElement);
            foreach (var element in document.RootElement.GetProperty("blessings").EnumerateArray())
            {
                var effects = new List<BloodBlessingEffectDefinition>();
                foreach (var effect in element.GetProperty("effects").EnumerateArray())
                    effects.Add(ParseEffect(effect));
                var tags = new List<string>();
                if (element.TryGetProperty("tags", out var tagArray)) foreach (var tag in tagArray.EnumerateArray()) tags.Add(tag.GetString() ?? "");
                var parentFamilies = ReadStrings(element, "parentFamilies");
                var requiredAnyEffects = ReadStrings(element, "requiresAnyEffects");
                var requiredAllEffects = ReadStrings(element, "requiresAllEffects");
                var familyName = S(element, "family");
                var iconPath = S(element, "icon");
                if (string.IsNullOrWhiteSpace(iconPath)) iconPath = FamilyIconPath(familyName);
                // Nome, descrição e flavor não ficam no catálogo: vêm de content.json (blessing.<id>.name/.desc/.flavor).
                var definition = new BloodBlessingDefinition(
                    S(element, "id"), S(element, "rarity"), I(element, "weight"), 1, familyName,
                    iconPath, S(element, "exclusiveGroup"), parentFamilies, requiredAnyEffects, requiredAllEffects,
                    tags.ToArray(), effects.ToArray());
                if (string.IsNullOrWhiteSpace(definition.Id) || ById.ContainsKey(definition.Id)) continue;
                Definitions.Add(definition); ById[definition.Id] = definition;
            }
            if (document.RootElement.TryGetProperty("families", out var families))
                foreach (var family in families.EnumerateArray())
                    Families.Add(new BloodBlessingFamily(S(family, "id"), S(family, "name"), S(family, "statusEffect")));
        }
        catch (Exception exception) { GD.PushError($"Failed to load blood blessing catalog: {exception.Message}"); }
    }

    private static string S(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
    private static int I(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : 0;
    private static float F(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.TryGetSingle(out var n) ? n : 0.0f;

    private static string[] ReadStrings(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var result = new List<string>();
        foreach (var value in values.EnumerateArray())
            if (!string.IsNullOrWhiteSpace(value.GetString())) result.Add(value.GetString() ?? "");
        return result.ToArray();
    }

    private static bool IsEligible(BloodBlessingDefinition definition)
    {
        if (RunState.GetBlessingStack(definition.Id) > 0)
            return false;
        if (definition.RequiredAnyEffectTypes.Length > 0)
        {
            var foundAny = false;
            foreach (var effectType in definition.RequiredAnyEffectTypes)
                if (RunState.HasEffectType(effectType)) { foundAny = true; break; }
            if (!foundAny) return false;
        }
        foreach (var effectType in definition.RequiredAllEffectTypes)
            if (!RunState.HasEffectType(effectType)) return false;
        return true;
    }

    private static BloodBlessingEffectDefinition ParseEffect(JsonElement effect)
    {
        var numbers = new Dictionary<string, float>();
        var texts = new Dictionary<string, string>();
        var booleanValue = false;
        var numericValue = 0.0f;
        foreach (var property in effect.EnumerateObject())
        {
            if (property.Name is "type" or "operation") continue;
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetSingle(out var number))
            {
                if (property.Name == "value") numericValue = number;
                else numbers[property.Name] = number;
            }
            else if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                if (property.Name == "value") booleanValue = property.Value.GetBoolean();
                else texts[property.Name] = property.Value.GetBoolean().ToString();
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
                texts[property.Name] = property.Value.GetString() ?? "";
        }
        return new BloodBlessingEffectDefinition(S(effect, "type"), S(effect, "operation"), numericValue, booleanValue, numbers, texts);
    }

    public static string FamilyIconPath(string family) => $"res://images/bloodBoons/{family.ToLowerInvariant()}-icon.png";

    private static void ReadRarities(JsonElement root)
    {
        if (root.TryGetProperty("rarityScaling", out var scaling))
            foreach (var property in scaling.EnumerateObject())
            {
                var displayName = property.Name.Length > 0 ? char.ToUpperInvariant(property.Name[0]) + property.Name[1..].ToLowerInvariant() : property.Name;
                Rarities.Add(new BloodBlessingRarity(property.Name, displayName, F(property.Value, "powerMultiplier"), F(property.Value, "weight")));
            }
        if (Rarities.Count == 0)
        {
            Rarities.Add(new("common", "Common", 1.0f, 50)); Rarities.Add(new("uncommon", "Uncommon", 2.0f, 28));
            Rarities.Add(new("rare", "Rare", 3.0f, 14)); Rarities.Add(new("epic", "Epic", 4.0f, 6)); Rarities.Add(new("legendary", "Legendary", 6.6667f, 2));
        }
    }

    private static BloodBlessingRarity RollRarity(RandomNumberGenerator rng)
    {
        EnsureLoaded();
        var total = 0.0f; foreach (var rarity in Rarities) total += Mathf.Max(rarity.Weight, 0);
        var roll = rng.RandfRange(0, Mathf.Max(total, 0.01f)); var cursor = 0.0f;
        foreach (var rarity in Rarities) { cursor += Mathf.Max(rarity.Weight, 0); if (roll <= cursor) return rarity; }
        return Rarities[^1];
    }
}

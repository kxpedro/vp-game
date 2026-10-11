using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class ArtifactCatalog
{
    private const string CatalogPath = "res://data/artifacts/artifacts.json";
    private static readonly List<ArtifactDefinition> Definitions = new();
    private static readonly Dictionary<string, ArtifactDefinition> DefinitionsById = new();
    private static bool _loaded;
    public static float BossDropPowerMultiplier { get; private set; } = 1.75f;

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

    public static List<ArtifactDefinition> RollChoices(RandomNumberGenerator rng, int count, HashSet<string> excludedIds = null)
    {
        EnsureLoaded();
        var pool = new List<ArtifactDefinition>();
        foreach (var definition in Definitions)
        {
            if (excludedIds != null && excludedIds.Contains(definition.Id))
                continue;

            pool.Add(definition);
        }
        var choices = new List<ArtifactDefinition>();

        while (choices.Count < count && pool.Count > 0)
        {
            var selected = RollOne(rng, pool);
            if (selected == null)
                break;

            choices.Add(selected);
            pool.Remove(selected);
            // Diario de descobertas: artefato oferecido no altar conta como visto.
            CodexProgress.MarkDiscovered(CodexCategory.Artifact, selected.Id);
        }

        return choices;
    }

    public static void Reload()
    {
        _loaded = false;
        BossDropPowerMultiplier = 1.75f;
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
                BossDropPowerMultiplier = Mathf.Max(multiplier, 0.0f);

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

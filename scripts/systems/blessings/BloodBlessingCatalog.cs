using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class BloodBlessingCatalog
{
    private const string CatalogPath = "res://data/blessings/blood_blessings.json";
    private static readonly List<BloodBlessingDefinition> Definitions = new();
    private static readonly Dictionary<string, BloodBlessingDefinition> ById = new();
    private static readonly List<BloodBlessingFamily> Families = new();
    private static readonly List<BloodBlessingRarity> Rarities = new();
    private static bool _loaded;

    public static IReadOnlyList<BloodBlessingDefinition> All { get { EnsureLoaded(); return Definitions; } }
    public static IReadOnlyList<BloodBlessingFamily> AllFamilies { get { EnsureLoaded(); return Families; } }
    public static IReadOnlyList<BloodBlessingRarity> AllRarities { get { EnsureLoaded(); return Rarities; } }

    public static bool TryGet(string id, out BloodBlessingDefinition definition)
    {
        EnsureLoaded();
        return ById.TryGetValue(id, out definition);
    }

    public static List<BloodBlessingDefinition> RollChoices(RandomNumberGenerator rng, int count) => RollChoices(rng, count, out _);

    public static List<BloodBlessingOffer> RollOffers(RandomNumberGenerator rng, int count, out string selectedFamily)
    {
        var definitions = RollChoices(rng, count, out selectedFamily);
        var offers = new List<BloodBlessingOffer>();
        foreach (var definition in definitions)
        {
            var rarity = RollRarity(rng);
            offers.Add(new BloodBlessingOffer(definition, rarity.DisplayName, rarity.PowerMultiplier));
        }
        return offers;
    }

    public static List<BloodBlessingDefinition> RollChoices(RandomNumberGenerator rng, int count, out string selectedFamily)
    {
        EnsureLoaded();
        selectedFamily = "";
        var availableFamilies = new List<string>();
        var fallbackFamilies = new List<string>();
        foreach (var family in Families)
        {
            var available = 0;
            foreach (var definition in Definitions)
                if (definition.Family.Equals(family.DisplayName, StringComparison.OrdinalIgnoreCase) && RunState.GetBlessingStack(definition.Id) == 0) available++;
            if (available > 0) fallbackFamilies.Add(family.DisplayName);
            if (available >= count) availableFamilies.Add(family.DisplayName);
        }
        if (availableFamilies.Count == 0) availableFamilies.AddRange(fallbackFamilies);
        if (availableFamilies.Count == 0) return new List<BloodBlessingDefinition>();
        selectedFamily = availableFamilies[rng.RandiRange(0, availableFamilies.Count - 1)];
        var pool = new List<BloodBlessingDefinition>();
        foreach (var definition in Definitions)
            if (definition.Family.Equals(selectedFamily, StringComparison.OrdinalIgnoreCase) && RunState.GetBlessingStack(definition.Id) == 0)
                pool.Add(definition);

        var choices = new List<BloodBlessingDefinition>();
        while (choices.Count < count && pool.Count > 0)
        {
            var total = 0.0f;
            foreach (var entry in pool) total += Mathf.Max(entry.Weight, 0);
            var roll = rng.RandfRange(0.0f, Mathf.Max(total, 0.01f));
            var cursor = 0.0f;
            var selected = pool[^1];
            foreach (var entry in pool)
            {
                cursor += Mathf.Max(entry.Weight, 0);
                if (roll <= cursor) { selected = entry; break; }
            }
            choices.Add(selected);
            pool.Remove(selected);
        }
        return choices;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        if (!FileAccess.FileExists(CatalogPath)) { GD.PushError($"Blood blessing catalog not found: {CatalogPath}"); return; }
        try
        {
            using var document = JsonDocument.Parse(FileAccess.GetFileAsString(CatalogPath));
            ReadRarities(document.RootElement);
            foreach (var element in document.RootElement.GetProperty("blessings").EnumerateArray())
            {
                var effects = new List<BloodBlessingEffectDefinition>();
                foreach (var effect in element.GetProperty("effects").EnumerateArray())
                    effects.Add(ParseEffect(effect));
                var tags = new List<string>();
                if (element.TryGetProperty("tags", out var tagArray)) foreach (var tag in tagArray.EnumerateArray()) tags.Add(tag.GetString() ?? "");
                var familyName = S(element, "family");
                var definition = new BloodBlessingDefinition(
                    S(element, "id"), S(element, "name"), S(element, "rarity"), I(element, "weight"), 1,
                    NormalizeDescription(S(element, "description")), S(element, "flavorText"), familyName,
                    FamilyIconPath(familyName), S(element, "exclusiveGroup"), tags.ToArray(), effects.ToArray());
                if (string.IsNullOrWhiteSpace(definition.Id) || ById.ContainsKey(definition.Id)) continue;
                Definitions.Add(definition); ById[definition.Id] = definition;
            }
            if (document.RootElement.TryGetProperty("families", out var families))
                foreach (var family in families.EnumerateArray())
                    Families.Add(new BloodBlessingFamily(S(family, "id"), S(family, "name"), S(family, "theme"), S(family, "statusEffect")));
        }
        catch (Exception exception) { GD.PushError($"Failed to load blood blessing catalog: {exception.Message}"); }
    }

    private static string S(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
    private static int I(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : 0;
    private static float F(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.TryGetSingle(out var n) ? n : 0.0f;

    private static string NormalizeDescription(string description)
    {
        // Blessings are unique; their numeric power scales with the rolled quality, not with a nonexistent level.
        return description
            .Replace(" por nível", " (valor base; aumenta com a qualidade)", StringComparison.OrdinalIgnoreCase)
            .Replace(" por nivel", " (valor base; aumenta com a qualidade)", StringComparison.OrdinalIgnoreCase);
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

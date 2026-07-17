using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class SkillCatalog
{
    private const string CatalogPath = "res://data/skills/skills.json";

    // Runtime systems use these stable IDs. The skill data itself lives in skills.json.
    public const string VampireFangsId = "bestialis.vampire_fangs";
    public const string BestialTransformationId = "bestialis.bestial_transformation";
    public const string BloodHuntId = "bestialis.blood_hunt";
    public const string SurvivalInstinctId = "humanitas.survival_instinct";
    public const string PocketWatchId = "humanitas.pocket_watch";
    public const string IronWillId = "humanitas.iron_will";

    private static readonly List<SkillDefinition> Definitions = new();
    private static readonly Dictionary<string, SkillDefinition> DefinitionsById = new();
    private static bool _loaded;

    public static IReadOnlyList<SkillDefinition> All
    {
        get
        {
            EnsureLoaded();
            return Definitions;
        }
    }

    public static bool TryGet(string skillId, out SkillDefinition definition)
    {
        EnsureLoaded();
        return DefinitionsById.TryGetValue(skillId, out definition);
    }

    public static SkillDefinition Get(string skillId)
    {
        EnsureLoaded();
        return DefinitionsById[skillId];
    }

    public static IEnumerable<SkillDefinition> GetTree(SkillTree tree)
    {
        EnsureLoaded();
        foreach (var definition in Definitions)
            if (definition.Tree == tree)
                yield return definition;
    }

    public static bool HasPrerequisites(SkillDefinition definition)
    {
        foreach (var prerequisite in definition.Prerequisites)
            if (!CharacterSheet.HasSkill(prerequisite))
                return false;
        return true;
    }

    public static bool HasExclusiveConflict(SkillDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ExclusiveGroup))
            return false;

        EnsureLoaded();
        foreach (var candidate in Definitions)
        {
            if (candidate.Id != definition.Id &&
                candidate.ExclusiveGroup == definition.ExclusiveGroup &&
                CharacterSheet.HasSkill(candidate.Id))
                return true;
        }
        return false;
    }

    public static void Reload()
    {
        _loaded = false;
        Definitions.Clear();
        DefinitionsById.Clear();
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        Definitions.Clear();
        DefinitionsById.Clear();

        if (!FileAccess.FileExists(CatalogPath))
        {
            GD.PushError($"Skill catalog not found: {CatalogPath}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(FileAccess.GetFileAsString(CatalogPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var schemaVersion) || schemaVersion != 3)
                throw new InvalidOperationException("Skill catalog must use passive-only schemaVersion 3.");
            var design = ReadRequiredObject(root, "design", "Skill catalog");
            if (!design.TryGetProperty("passiveOnly", out var passiveOnly) || passiveOnly.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("Skill catalog must declare design.passiveOnly as true.");
            var trees = ReadRequiredArray(root, "trees", "Skill catalog");
            foreach (var treeElement in trees.EnumerateArray())
                ParseTree(treeElement);
            ValidateDefinitions();
        }
        catch (Exception exception)
        {
            Definitions.Clear();
            DefinitionsById.Clear();
            GD.PushError($"Failed to load skill catalog: {exception.Message}");
        }
    }

    private static void ParseTree(JsonElement treeElement)
    {
        var treeId = ReadRequiredString(treeElement, "id", "Skill tree");
        if (!Enum.TryParse(treeId, true, out SkillTree tree))
            throw new InvalidOperationException($"Skill catalog has invalid tree '{treeId}'.");

        var skills = ReadRequiredArray(treeElement, "skills", $"Skill tree '{treeId}'");
        foreach (var skillElement in skills.EnumerateArray())
            AddDefinition(ParseDefinition(skillElement, tree));
    }

    private static SkillDefinition ParseDefinition(JsonElement element, SkillTree tree)
    {
        var id = ReadRequiredString(element, "id", "Skill entry");
        var expectedPrefix = tree == SkillTree.Bestialis ? "bestialis." : "humanitas.";
        if (!id.StartsWith(expectedPrefix, StringComparison.Ordinal) || !HasValidIdSuffix(id[expectedPrefix.Length..]))
            throw new InvalidOperationException($"Skill '{id}' must use '{expectedPrefix}' followed by snake_case.");

        var presentation = ReadRequiredObject(element, "presentation", $"Skill '{id}'");
        var progression = ReadRequiredObject(element, "progression", $"Skill '{id}'");
        var position = ReadRequiredObject(progression, "position", $"Skill '{id}' progression");
        var runtime = ReadRequiredObject(element, "runtime", $"Skill '{id}'");

        var statusText = ReadRequiredString(runtime, "status", $"Skill '{id}' runtime");
        if (!Enum.TryParse(statusText, true, out SkillImplementationStatus implementationStatus))
            throw new InvalidOperationException($"Skill '{id}' has invalid implementation status '{statusText}'.");

        var maxRank = Math.Max(ReadNonNegativeInt(progression, "maxRank", $"Skill '{id}' progression"), 1);
        var effects = ReadEffects(runtime, id, maxRank);
        if (implementationStatus == SkillImplementationStatus.Implemented && effects.Length == 0)
            throw new InvalidOperationException($"Implemented skill '{id}' must declare at least one runtime effect.");

        return new SkillDefinition(
            id,
            ReadRequiredString(presentation, "name", $"Skill '{id}' presentation"),
            tree,
            ReadNonNegativeInt(progression, "cost", $"Skill '{id}' progression"),
            maxRank,
            ReadRequiredString(presentation, "description", $"Skill '{id}' presentation"),
            ReadRequiredString(presentation, "flavorText", $"Skill '{id}' presentation"),
            ReadRequiredString(presentation, "metalReference", $"Skill '{id}' presentation"),
            ReadRequiredString(presentation, "iconText", $"Skill '{id}' presentation"),
            ReadStringArray(progression, "prerequisites", $"Skill '{id}' progression"),
            Math.Max(ReadNonNegativeInt(progression, "tier", $"Skill '{id}' progression"), 1),
            ReadNonNegativeInt(position, "column", $"Skill '{id}' position"),
            ReadNonNegativeInt(position, "row", $"Skill '{id}' position"),
            ReadOptionalString(progression, "exclusiveGroup"),
            ReadStringArray(presentation, "tags", $"Skill '{id}' presentation"),
            implementationStatus,
            effects
        );
    }

    private static SkillEffectDefinition[] ReadEffects(JsonElement runtime, string skillId, int maxRank)
    {
        var effectsElement = ReadRequiredArray(runtime, "effects", $"Skill '{skillId}' runtime");
        var effects = new List<SkillEffectDefinition>();
        foreach (var effectElement in effectsElement.EnumerateArray())
        {
            var values = ReadFloatArray(effectElement, "values", $"Skill '{skillId}' effect");
            if (values.Length != 1 && values.Length != maxRank)
                throw new InvalidOperationException($"Skill '{skillId}' effect values must contain one value or one value per rank.");

            var numbers = new Dictionary<string, float>();
            var texts = new Dictionary<string, string>();
            var flags = new Dictionary<string, bool>();
            if (effectElement.TryGetProperty("parameters", out var parameters))
            {
                if (parameters.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException($"Skill '{skillId}' effect parameters must be an object.");
                foreach (var property in parameters.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetSingle(out var number)) numbers[property.Name] = number;
                    else if (property.Value.ValueKind == JsonValueKind.String) texts[property.Name] = property.Value.GetString() ?? "";
                    else if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) flags[property.Name] = property.Value.GetBoolean();
                    else throw new InvalidOperationException($"Skill '{skillId}' effect parameter '{property.Name}' has an unsupported value.");
                }
            }

            effects.Add(new SkillEffectDefinition(
                ReadRequiredString(effectElement, "type", $"Skill '{skillId}' effect"),
                ReadRequiredString(effectElement, "operation", $"Skill '{skillId}' effect"),
                values,
                numbers,
                texts,
                flags
            ));
        }
        return effects.ToArray();
    }

    private static void AddDefinition(SkillDefinition definition)
    {
        if (DefinitionsById.ContainsKey(definition.Id))
            throw new InvalidOperationException($"Skill catalog contains duplicate id '{definition.Id}'.");
        Definitions.Add(definition);
        DefinitionsById[definition.Id] = definition;
    }

    private static void ValidateDefinitions()
    {
        var positions = new HashSet<string>();
        foreach (var definition in Definitions)
        {
            var positionKey = $"{definition.Tree}:{definition.GridColumn}:{definition.GridRow}";
            if (!positions.Add(positionKey))
                throw new InvalidOperationException($"Skill tree position '{positionKey}' is used more than once.");

            foreach (var prerequisite in definition.Prerequisites)
            {
                if (!DefinitionsById.TryGetValue(prerequisite, out var prerequisiteDefinition))
                    throw new InvalidOperationException($"Skill '{definition.Id}' references missing prerequisite '{prerequisite}'.");
                if (prerequisiteDefinition.Tree != definition.Tree)
                    throw new InvalidOperationException($"Skill '{definition.Id}' references prerequisite '{prerequisite}' from another tree.");
            }
        }

        var states = new Dictionary<string, int>();
        foreach (var definition in Definitions)
            Visit(definition, states);
    }

    private static void Visit(SkillDefinition definition, Dictionary<string, int> states)
    {
        if (states.TryGetValue(definition.Id, out var state))
        {
            if (state == 1)
                throw new InvalidOperationException($"Skill prerequisite cycle detected at '{definition.Id}'.");
            if (state == 2)
                return;
        }
        states[definition.Id] = 1;
        foreach (var prerequisite in definition.Prerequisites)
            Visit(DefinitionsById[prerequisite], states);
        states[definition.Id] = 2;
    }

    private static bool HasValidIdSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '_' || value[^1] == '_')
            return false;
        foreach (var character in value)
            if (!(character is >= 'a' and <= 'z') && !(character is >= '0' and <= '9') && character != '_')
                return false;
        return true;
    }

    private static JsonElement ReadRequiredObject(JsonElement element, string propertyName, string context)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{context} is missing object '{propertyName}'.");
        return value;
    }

    private static JsonElement ReadRequiredArray(JsonElement element, string propertyName, string context)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{context} is missing array '{propertyName}'.");
        return value;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string context)
    {
        var result = ReadOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException($"{context} is missing string '{propertyName}'.");
        return result;
    }

    private static string ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadNonNegativeInt(JsonElement element, string propertyName, string context)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var result))
            throw new InvalidOperationException($"{context} is missing integer '{propertyName}'.");
        return Math.Max(result, 0);
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName, string context)
    {
        var values = ReadRequiredArray(element, propertyName, context);
        var result = new List<string>();
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidOperationException($"{context} has an invalid value in '{propertyName}'.");
            result.Add(value.GetString() ?? "");
        }
        return result.ToArray();
    }

    private static float[] ReadFloatArray(JsonElement element, string propertyName, string context)
    {
        var values = ReadRequiredArray(element, propertyName, context);
        var result = new List<float>();
        foreach (var value in values.EnumerateArray())
        {
            if (!value.TryGetSingle(out var number))
                throw new InvalidOperationException($"{context} has an invalid number in '{propertyName}'.");
            result.Add(number);
        }
        return result.ToArray();
    }
}

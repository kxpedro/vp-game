using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Um efeito de modificacao de arma. Type = chave de efeito (ex.: "AttackRange.Multiplier"); Field = "value" ou "value2",
/// sufixo da chave de tuning weapon.mod.&lt;id&gt;.&lt;field&gt;; DefaultValue = padrao gravado no JSON.
/// </summary>
public sealed record WeaponModEffect(string Type, string Field, float DefaultValue);

/// <summary>Modificacao de arma da Bigorna (docs/design/bigorna.md). Nome/descricao/flavor ficam em content.json.</summary>
public sealed record WeaponModDefinition(string Id, string WeaponId, WeaponModEffect[] Effects)
{
    public string NameKey => $"weaponmod.{Id}.name";
    public string DescKey => $"weaponmod.{Id}.desc";
    public string FlavorKey => $"weaponmod.{Id}.flavor";
    public string DisplayName => Localization.Text(NameKey);
    public string Flavor => Localization.Text(FlavorKey);

    /// <summary>Valor vigente do efeito (chave de tuning weapon.mod.&lt;id&gt;.&lt;field&gt;, padrao do JSON).</summary>
    public float GetValue(WeaponModEffect effect)
        => CompendiumTuning.GetFloat($"weapon.mod.{Id}.{effect.Field}", effect.DefaultValue);

    /// <summary>Soma dos valores vigentes dos efeitos desta modificacao com o tipo dado (0 se nao houver).</summary>
    public float GetTotal(string effectType)
    {
        var total = 0.0f;
        foreach (var effect in Effects)
            if (effect.Type == effectType)
                total += GetValue(effect);
        return total;
    }

    /// <summary>Descricao no idioma atual, com {0}/{1} = percentuais dos efeitos na ordem do JSON (valor tunado, ex.: "15%").</summary>
    public string Description
    {
        get
        {
            var args = new object[Math.Max(Effects.Length, 2)];
            for (var i = 0; i < args.Length; i++)
                args[i] = i < Effects.Length ? $"{Mathf.RoundToInt(GetValue(Effects[i]) * 100.0f)}%" : "";
            return Localization.TextFormat(DescKey, args);
        }
    }
}

public static class WeaponModCatalog
{
    private const string CatalogPath = "res://data/weapons/weapon_mods.json";
    private static readonly List<WeaponModDefinition> Definitions = new();
    private static readonly Dictionary<string, WeaponModDefinition> DefinitionsById = new();
    private static bool _loaded;

    public static IReadOnlyList<WeaponModDefinition> All
    {
        get
        {
            EnsureLoaded();
            return Definitions;
        }
    }

    /// <summary>Modificacao pelo id, ou null se nao existir.</summary>
    public static WeaponModDefinition Get(string id)
    {
        EnsureLoaded();
        return id != null && DefinitionsById.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>Modificacoes da arma (na ordem do JSON); vazio se a arma nao tem nenhuma.</summary>
    public static List<WeaponModDefinition> ForWeapon(string weaponId)
    {
        EnsureLoaded();
        var result = new List<WeaponModDefinition>();
        foreach (var definition in Definitions)
            if (definition.WeaponId == weaponId)
                result.Add(definition);
        return result;
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

        if (!FileAccess.FileExists(CatalogPath))
        {
            GD.PushError($"Weapon mod catalog not found: {CatalogPath}");
            return;
        }

        try
        {
            var json = FileAccess.GetFileAsString(CatalogPath);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Array)
            {
                GD.PushError("Weapon mod catalog is missing a mods array.");
                return;
            }

            foreach (var element in mods.EnumerateArray())
                AddDefinition(ParseDefinition(element));
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to load weapon mod catalog: {exception.Message}");
        }
    }

    private static WeaponModDefinition ParseDefinition(JsonElement element)
    {
        var effects = new List<WeaponModEffect>();
        if (element.TryGetProperty("effects", out var effectArray) && effectArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var effect in effectArray.EnumerateArray())
            {
                var field = ReadString(effect, "field");
                effects.Add(new WeaponModEffect(
                    ReadString(effect, "type"),
                    string.IsNullOrWhiteSpace(field) ? "value" : field,
                    effect.TryGetProperty("value", out var value) && value.TryGetSingle(out var number) ? number : 0.0f));
            }
        }

        return new WeaponModDefinition(ReadString(element, "id"), ReadString(element, "weaponId"), effects.ToArray());
    }

    private static void AddDefinition(WeaponModDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            GD.PushWarning("Weapon mod catalog ignored an entry without id.");
            return;
        }

        if (DefinitionsById.ContainsKey(definition.Id))
        {
            GD.PushWarning($"Weapon mod catalog ignored duplicate id: {definition.Id}");
            return;
        }

        Definitions.Add(definition);
        DefinitionsById[definition.Id] = definition;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}

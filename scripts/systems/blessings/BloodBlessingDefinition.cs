using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed record BloodBlessingEffectDefinition(
    string Type,
    string Operation,
    float Value,
    bool BooleanValue,
    IReadOnlyDictionary<string, float> NumberParameters,
    IReadOnlyDictionary<string, string> TextParameters
)
{
    public float Number(string key, float fallback = 0.0f) => NumberParameters.TryGetValue(key, out var value) ? value : fallback;
    public string Text(string key, string fallback = "") => TextParameters.TryGetValue(key, out var value) ? value : fallback;
}

// Textos exibidos vêm de data/localization/<idioma>/content.json, com chaves derivadas do ID:
// blessing.<id>.name, blessing.<id>.desc (modelo com {n} = valor do efeito n e marcação [kw=...]) e blessing.<id>.flavor.
// As propriedades resolvem o texto a cada leitura, então a troca de idioma vale sem recarregar o catálogo.
public sealed record BloodBlessingDefinition(
    string Id,
    string Rarity,
    int Weight,
    int MaxStacks,
    string Family,
    string IconPath,
    string ExclusiveGroup,
    string[] ParentFamilies,
    string[] RequiredAnyEffectTypes,
    string[] RequiredAllEffectTypes,
    string[] Tags,
    BloodBlessingEffectDefinition[] Effects
)
{
    public string NameKey => $"blessing.{Id}.name";
    public string DescriptionKey => $"blessing.{Id}.desc";
    public string FlavorKey => $"blessing.{Id}.flavor";

    public string DisplayName => Localization.Text(NameKey);
    // Modelo cru (placeholders e marcação). Para exibir, use BloodBlessingText.FormatDescription.
    public string Description => Localization.Text(DescriptionKey);
    public string FlavorText => Localization.Text(FlavorKey);
}

public static class BloodBlessingProgression
{
    public const int MaximumLevel = 10;
    public const float BonusPerAdditionalLevel = 0.25f;

    public static float GetLevelMultiplier(int level)
        => 1.0f + (System.Math.Clamp(level, 1, MaximumLevel) - 1) * CompendiumTuning.GetFloat("blessing.level_bonus", BonusPerAdditionalLevel);
}

// Name = nome no idioma em que a bênção foi obtida (só fallback). Para exibir, use DisplayName.
public sealed record BloodBlessingRecord(
    string Id,
    string Name,
    int Stack,
    string Rarity = "Common",
    float PowerMultiplier = 1.0f,
    int Level = 1)
{
    public float EffectivePowerMultiplier => PowerMultiplier * BloodBlessingProgression.GetLevelMultiplier(Level);

    public string DisplayName => BloodBlessingCatalog.TryGet(Id, out var definition)
        ? definition.DisplayName
        : string.IsNullOrEmpty(Name) ? Id : Name;
}

public sealed record BloodBlessingOffer(BloodBlessingDefinition Definition, string Rarity, float PowerMultiplier);

// Id/DisplayName da raridade são identificadores ("common"/"Common"); o rótulo exibido vem de UiComponents.RarityLabel.
public sealed record BloodBlessingRarity(string Id, string DisplayName, float PowerMultiplier, float Weight);

// DisplayName = nome da linhagem (Dracul, Bathory, Karnstein, Orlok, Stirps): nome próprio, igual em todos os idiomas
// e usado como identificador no campo "family" das bênçãos.
public sealed record BloodBlessingFamily(string Id, string DisplayName, string StatusEffect)
{
    public string Theme => Localization.Text($"blessing.family.{Id}.theme");
}

// Termo destacado em descrições de bênção (cor/negrito) e explicação mostrada no glossário da oferta.
public sealed record BlessingKeyword(string Id, string Term, string Color, bool Bold, string Hint);

// Montagem das descrições de bênção a partir do modelo localizado:
// - {n} recebe o valor exibido do efeito de índice n (escalado por raridade/nível, em % quando o efeito é fracionário);
// - [kw=<id>]texto[/kw] marca uma palavra-chave (ids em KeywordStyles; termo e explicação em blessing.keyword.<id>.term/.hint).
public static class BloodBlessingText
{
    private static readonly Regex KeywordMarkup = new(@"\[kw=([a-z_]+)\](.*?)\[/kw\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    // Cores mantidas do destaque anterior (guia §9). Ataques em negrito; estados sem negrito.
    private static readonly Dictionary<string, (string Color, bool Bold)> KeywordStyles = new()
    {
        ["basic_attack"] = ("#ffd166", true),
        ["quick_attack"] = ("#72e6ff", true),
        ["special_attack"] = ("#d58cff", true),
        ["fury"] = ("#ff4d5f", true),
        ["bleed"] = ("#ff4055", false),
        ["pestilence"] = ("#78d957", false),
        ["barrier"] = ("#63c7ff", false),
        ["fascination"] = ("#ff6fe8", false),
        ["host"] = ("#b4ff6a", false),
        ["interrupt"] = ("#ffd166", false),
        ["wound"] = ("#ff9f9f", false),
        ["charm"] = ("#dd77ff", false)
    };

    public static IEnumerable<string> KeywordIds => KeywordStyles.Keys;

    // valueColorHex nulo: texto puro (sem marcação, para tooltips). Com cor: valores em negrito colorido (BBCode)
    // e a marcação [kw] preservada para FormatKeywords/CollectKeywords.
    public static string FormatDescription(BloodBlessingDefinition blessing, float effectivePowerMultiplier, string valueColorHex = null)
    {
        if (blessing == null)
            return "";
        var values = new object[blessing.Effects.Length];
        for (var i = 0; i < blessing.Effects.Length; i++)
        {
            var value = FormatEffectValue(blessing, i, effectivePowerMultiplier);
            values[i] = string.IsNullOrEmpty(valueColorHex) || value.Length == 0
                ? value
                : $"[b][color=#{valueColorHex.TrimStart('#')}]{value}[/color][/b]";
        }
        var text = Localization.TextFormat(blessing.DescriptionKey, values);
        return string.IsNullOrEmpty(valueColorHex) ? StripKeywords(text) : text;
    }

    // Valor exibido do efeito index ("12%", "20", "2,5"); vazio para efeitos booleanos/estruturais.
    public static string FormatEffectValue(BloodBlessingDefinition blessing, int index, float effectivePowerMultiplier)
    {
        var effect = blessing.Effects[index];
        if (effect.BooleanValue || effect.Operation is "Set" or "SetBool")
            return "";
        var baseValue = CompendiumTuning.GetFloat($"blessing.{blessing.Id}.effect.{index}.value", effect.Value);
        var value = GetDisplayedValue(effect, baseValue * effectivePowerMultiplier);
        if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat")
            value *= RunState.MaxHealthMultiplier * RunState.SacrificedMaxHealthMultiplier;
        // O sinal fica no texto ("10% menos dano"); o número sai sempre positivo.
        value = Math.Abs(value);
        return UsesPercent(effect)
            ? ArtifactEffectRegistry.FormatNumber(value * 100.0f) + "%"
            : ArtifactEffectRegistry.FormatNumber(value);
    }

    public static bool UsesPercent(BloodBlessingEffectDefinition effect)
        => effect.Operation.Contains("Multiplier", StringComparison.OrdinalIgnoreCase)
            || effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase)
            || ArtifactEffectRegistry.IsFractionType(effect.Type);

    // Valor efetivo como é consumido (chance limitada a 100%, contagens arredondadas).
    public static float GetDisplayedValue(BloodBlessingEffectDefinition effect, float value)
    {
        if (effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase))
            return Math.Clamp(value, 0.0f, 1.0f);
        // Consumidos com arredondamento no BloodBlessingRuntime.
        if (effect.Type is "PestilenceSpreadTargets" or "PestilenceSpreadOnHostHit")
            return MathF.Round(value);
        return value;
    }

    // [kw=id]texto[/kw] -> [color=...][b]texto[/b][/color]. Id desconhecido: só o texto.
    public static string FormatKeywords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";
        return KeywordMarkup.Replace(text, match =>
        {
            var word = match.Groups[2].Value;
            if (!KeywordStyles.TryGetValue(match.Groups[1].Value, out var style))
                return word;
            var body = style.Bold ? $"[b]{word}[/b]" : word;
            return $"[color={style.Color}]{body}[/color]";
        });
    }

    public static string StripKeywords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";
        return KeywordMarkup.Replace(text, match => match.Groups[2].Value);
    }

    // Termos marcados no texto, em ordem de aparição e sem repetir (glossário da oferta).
    public static void CollectKeywords(string text, List<BlessingKeyword> into)
    {
        if (string.IsNullOrEmpty(text) || into == null)
            return;
        foreach (Match match in KeywordMarkup.Matches(text))
        {
            var id = match.Groups[1].Value;
            if (!KeywordStyles.ContainsKey(id) || into.Exists(existing => existing.Id == id))
                continue;
            into.Add(ResolveKeyword(id));
        }
    }

    public static BlessingKeyword ResolveKeyword(string id)
    {
        (string Color, bool Bold) style = KeywordStyles.TryGetValue(id, out var found) ? found : ("#ffffff", false);
        return new BlessingKeyword(id, Localization.Text($"blessing.keyword.{id}.term"), style.Color, style.Bold,
            Localization.Text($"blessing.keyword.{id}.hint"));
    }
}

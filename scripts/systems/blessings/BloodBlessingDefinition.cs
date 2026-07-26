using System.Collections.Generic;

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

public sealed record BloodBlessingDefinition(
    string Id,
    string DisplayName,
    string Rarity,
    int Weight,
    int MaxStacks,
    string Description,
    string FlavorText,
    string Family,
    string IconPath,
    string ExclusiveGroup,
    string[] ParentFamilies,
    string[] RequiredAnyEffectTypes,
    string[] RequiredAllEffectTypes,
    string[] Tags,
    BloodBlessingEffectDefinition[] Effects
);

public static class BloodBlessingProgression
{
    public const int MaximumLevel = 10;
    public const float BonusPerAdditionalLevel = 0.25f;

    public static float GetLevelMultiplier(int level)
        => 1.0f + (System.Math.Clamp(level, 1, MaximumLevel) - 1) * BonusPerAdditionalLevel;
}

public sealed record BloodBlessingRecord(
    string Id,
    string Name,
    int Stack,
    string Rarity = "Common",
    float PowerMultiplier = 1.0f,
    int Level = 1)
{
    public float EffectivePowerMultiplier => PowerMultiplier * BloodBlessingProgression.GetLevelMultiplier(Level);
}

public sealed record BloodBlessingOffer(BloodBlessingDefinition Definition, string Rarity, float PowerMultiplier);

public sealed record BloodBlessingRarity(string Id, string DisplayName, float PowerMultiplier, float Weight);

public sealed record BloodBlessingFamily(string Id, string DisplayName, string Theme, string StatusEffect);

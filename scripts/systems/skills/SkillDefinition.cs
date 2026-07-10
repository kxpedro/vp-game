public enum SkillTree
{
    Bestialis,
    Humanitas
}

public enum SkillActivationMode
{
    Passive,
    Active
}

public sealed record SkillDefinition(
    string Id,
    string DisplayName,
    SkillTree Tree,
    SkillActivationMode ActivationMode,
    int Cost,
    int MaxRank,
    float CooldownSeconds,
    string Description,
    string IconText,
    string[] Prerequisites
);

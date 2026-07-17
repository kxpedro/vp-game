using System.Collections.Generic;

public enum SkillTree
{
    Bestialis,
    Humanitas
}

public enum SkillImplementationStatus
{
    Planned,
    Implemented
}

public sealed record SkillEffectDefinition(
    string Type,
    string Operation,
    float[] Values,
    Dictionary<string, float> NumberParameters,
    Dictionary<string, string> TextParameters,
    Dictionary<string, bool> BoolParameters
);

public sealed record SkillDefinition(
    string Id,
    string DisplayName,
    SkillTree Tree,
    int Cost,
    int MaxRank,
    string Description,
    string FlavorText,
    string MetalReference,
    string IconText,
    string[] Prerequisites,
    int Tier,
    int GridColumn,
    int GridRow,
    string ExclusiveGroup,
    string[] Tags,
    SkillImplementationStatus ImplementationStatus,
    SkillEffectDefinition[] Effects
)
{
    public bool IsImplemented => ImplementationStatus == SkillImplementationStatus.Implemented;
}

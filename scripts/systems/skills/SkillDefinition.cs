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

// Textos exibidos vêm de content.json, com chaves derivadas do ID: skill.<id>.name, .desc, .flavor e .icon
// (monograma do nó na Fonte de Sangue). MetalReference é metadado de autoria e nunca é exibido.
public sealed record SkillDefinition(
    string Id,
    SkillTree Tree,
    int Cost,
    int MaxRank,
    string MetalReference,
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

    public string NameKey => $"skill.{Id}.name";
    public string DescriptionKey => $"skill.{Id}.desc";
    public string FlavorKey => $"skill.{Id}.flavor";
    public string IconTextKey => $"skill.{Id}.icon";

    public string DisplayName => Localization.Text(NameKey);
    public string Description => Localization.Text(DescriptionKey);
    public string FlavorText => Localization.Text(FlavorKey);
    public string IconText => Localization.Text(IconTextKey);
}

using System.Collections.Generic;

public sealed record ArtifactEffectDefinition(
    string Type,
    string Operation,
    float Value
);

// SaveName: identificador legado gravado no save (ArtifactRecord.Name e slots equipados guardam esse texto).
// Não é exibido e não pode mudar. Textos exibidos vêm de content.json: artifact.<id>.name/.desc/.flavor.
public sealed record ArtifactDefinition(
    string Id,
    string SaveName,
    string IconPath,
    string Rarity,
    string Slot,
    int Weight,
    string[] Tags,
    ArtifactEffectDefinition[] Effects
)
{
    public string NameKey => $"artifact.{Id}.name";
    public string DescriptionKey => $"artifact.{Id}.desc";
    public string FlavorKey => $"artifact.{Id}.flavor";

    public string DisplayName => Localization.Text(NameKey);
    public string Description => Localization.Text(DescriptionKey);
    public string FlavorText => Localization.Text(FlavorKey);
}

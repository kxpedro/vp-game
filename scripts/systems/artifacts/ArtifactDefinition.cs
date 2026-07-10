using System.Collections.Generic;

public sealed record ArtifactEffectDefinition(
    string Type,
    string Operation,
    float Value
);

public sealed record ArtifactDefinition(
    string Id,
    string DisplayName,
    string IconPath,
    string Rarity,
    string Slot,
    int Weight,
    string Description,
    string FlavorText,
    string[] Tags,
    ArtifactEffectDefinition[] Effects
);

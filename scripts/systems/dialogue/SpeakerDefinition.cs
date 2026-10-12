using System.Collections.Generic;
using Godot;

public sealed record PortraitSpec(string TexturePath, string Initial, Color Color, Color Accent);

public sealed record SpeakerDefinition(string Id, string NameKey, bool IsPlayer, Color NameColor,
    IReadOnlyDictionary<string, PortraitSpec> Portraits)
{
    public string DisplayName => Localization.Text(NameKey);
}

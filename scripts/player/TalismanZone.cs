using Godot;
using System.Collections.Generic;

// Talismã de Liaozhai: zona fixa no chão, com duração, que sela quem entra. Criado pelo ataque especial
// (bênção Talismã de Contenção). O círculo Bagua e o bônus de movimento consultam a lista estática.
public partial class TalismanZone : Node3D
{
    private static readonly List<TalismanZone> Active = new();
    private static readonly Color Jade = new(0.12f, 0.53f, 0.46f);

    public float Radius { get; private set; } = 2.2f;
    private float _remaining = 6.0f;
    private float _tick;
    private MeshInstance3D _disc;
    private Sprite3D _ringSprite;

    public static IReadOnlyList<TalismanZone> All => Active;

    public static bool IsInside(Vector3 position)
    {
        foreach (var zone in Active)
            if (IsInstanceValid(zone) && zone.GlobalPosition.DistanceTo(position) <= zone.Radius) return true;
        return false;
    }

    public static void Spawn(Node context, Vector3 position, float radius, float duration)
    {
        var scene = context?.GetTree()?.CurrentScene;
        if (scene == null) return;
        var zone = new TalismanZone { Name = "TalismanZone", Radius = radius, _remaining = duration };
        scene.AddChild(zone);
        zone.GlobalPosition = new Vector3(position.X, position.Y, position.Z);
    }

    public override void _Ready()
    {
        Active.Add(this);
        if (GameMode.IsPixelArt)
        {
            // No 2D o talismã é o anel Bagua em pixel art (tools/pixel_art/family_rings).
            var ring = FamilyFx.BuildRing(this, FamilyRing.Bagua, Radius, out _ringSprite);
            ring.Position = new Vector3(0.0f, 0.07f, 0.0f);
            AddChild(ring);
            return;
        }
        _disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Radius, BottomRadius = Radius, Height = 0.03f, RadialSegments = 8 },
            MaterialOverride = FamilyFx.DiscMaterial(Jade, 0.32f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0.0f, 0.07f, 0.0f)
        };
        AddChild(_disc);
        // Aro interno mais claro: lê como um glifo/octógono do Bagua.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Radius * 0.55f, BottomRadius = Radius * 0.55f, Height = 0.03f, RadialSegments = 8 },
            MaterialOverride = FamilyFx.DiscMaterial(new Color(0.36f, 0.69f, 0.55f), 0.30f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0.0f, 0.09f, 0.0f)
        });
    }

    public override void _ExitTree() => Active.Remove(this);

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _remaining -= d;
        if (_remaining <= 0.0f) { QueueFree(); return; }
        if (_remaining < 1.0f)
        {
            if (_ringSprite != null)
                _ringSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.95f * _remaining);
            else if (_disc?.MaterialOverride is StandardMaterial3D material)
                material.AlbedoColor = new Color(Jade, 0.32f * _remaining);
        }
        _tick -= d;
        if (_tick > 0.0f) return;
        _tick = 0.4f;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
            if (node is EnemyBasic enemy && !enemy.IsQueuedForDeletion() && enemy.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
                enemy.RefreshSeal(1.2f);
    }
}

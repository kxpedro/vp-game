using Godot;

// Efeitos visuais simples (disco/pulso achatado, sem luz) das famílias novas. Cores da AAP-64.
// Anel de cada família no 2D (tools/pixel_art/family_rings); Disc = o disco liso de antes (3D e famílias sem anel).
public enum FamilyRing { Disc, Claws, Bagua, Moon, Shadow }

public static class FamilyFx
{
    private static readonly System.Collections.Generic.Dictionary<FamilyRing, Texture2D> RingTextures = new();

    private static Texture2D LoadRing(FamilyRing ring)
    {
        if (!RingTextures.TryGetValue(ring, out var texture))
            RingTextures[ring] = texture = GD.Load<Texture2D>($"res://assets/pixel_art/effects/family_rings/ring_{ring.ToString().ToLowerInvariant()}.png");
        return texture;
    }

    // Anel pixel art deitado no chão, esticado como as poças, já no tamanho final (raio em metros).
    public static Node3D BuildRing(Node context, FamilyRing ring, float radius, out Sprite3D sprite)
    {
        var camera = context.GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        var stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        var full = radius / (96.0f * PlayerSpriteVisual.PixelSize);
        var root = new Node3D { Name = "FamilyRing", Rotation = new Vector3(0.0f, yaw, 0.0f), Scale = new Vector3(full, 1.0f, full * stretch) };
        sprite = new Sprite3D
        {
            Texture = LoadRing(ring),
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Shaded = false,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.95f)
        };
        root.AddChild(sprite);
        return root;
    }

    // Pulso: o anel cresce de 20% até o raio e some.
    private static void PulseRing(Node context, Vector3 position, float radius, float duration, FamilyRing ring)
    {
        var scene = context.GetTree().CurrentScene;
        var root = BuildRing(context, ring, radius, out var sprite);
        var finalScale = root.Scale;
        scene.AddChild(root);
        root.GlobalPosition = new Vector3(position.X, position.Y + 0.08f, position.Z);
        root.Scale = new Vector3(finalScale.X * 0.2f, 1.0f, finalScale.Z * 0.2f);
        var tween = root.CreateTween().SetParallel(true);
        tween.TweenProperty(root, "scale", finalScale, duration);
        tween.TweenProperty(sprite, "modulate:a", 0.0f, duration).SetDelay(duration * 0.35f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFree));
    }

    // Disco que cresce e some: onda de choque, explosão de clone, uivo.
    public static void Pulse(Node context, Vector3 position, float radius, Color color, float duration = 0.45f, FamilyRing ring = FamilyRing.Disc)
    {
        var scene = context?.GetTree()?.CurrentScene;
        if (scene == null) return;
        if (ring != FamilyRing.Disc && GameMode.IsPixelArt)
        {
            PulseRing(context, position, radius, duration, ring);
            return;
        }
        var disc = new MeshInstance3D
        {
            Name = "FamilyPulse",
            Mesh = new CylinderMesh { TopRadius = 1.0f, BottomRadius = 1.0f, Height = 0.04f, RadialSegments = 24 },
            MaterialOverride = DiscMaterial(color, 0.55f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        scene.AddChild(disc);
        disc.GlobalPosition = new Vector3(position.X, position.Y + 0.08f, position.Z);
        disc.Scale = new Vector3(0.2f, 1.0f, 0.2f);
        var tween = disc.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(disc, "scale", new Vector3(radius, 1.0f, radius), duration);
        tween.TweenProperty(disc.MaterialOverride, "albedo_color:a", 0.0f, duration);
        tween.Chain().TweenCallback(Callable.From(disc.QueueFree));
    }

    public static StandardMaterial3D DiscMaterial(Color color, float alpha) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(color, alpha),
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };
}

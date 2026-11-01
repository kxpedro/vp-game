using Godot;

// Efeitos visuais simples (disco/pulso achatado, sem luz) das famílias novas. Cores da AAP-64.
public static class FamilyFx
{
    // Disco que cresce e some: onda de choque, explosão de clone, uivo.
    public static void Pulse(Node context, Vector3 position, float radius, Color color, float duration = 0.45f)
    {
        var scene = context?.GetTree()?.CurrentScene;
        if (scene == null) return;
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

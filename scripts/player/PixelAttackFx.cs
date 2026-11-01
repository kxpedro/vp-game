using Godot;
using System;

// Indicador dos golpes do Virgil no 2D: em vez da malha 3D lisa (arco, círculo, linha), desenha em pixel art, na hora, o
// formato do golpe já na projeção 2:1 da tela (como as poças): preenchimento em dither, borda de 2 px clara e contorno
// escuro, só com cores da AAP-64. Cor: neutra (leve = verde-água, pesado = amarelo) ou a da herança do slot (BloodlineVisuals),
// ajustada à cor mais próxima da AAP-64. Vale só no 2D; o 3D continua com as malhas.
public static class PixelAttackFx
{
    private static readonly string[] Aap64 =
    {
        "060608", "141013", "3b1725", "73172d", "b4202a", "df3e23", "fa6a0a", "f9a31b", "ffd541", "fffc40", "d6f264", "9cdb43", "59c135", "14a02e", "1a7a3e", "24523b",
        "122020", "143464", "285cc4", "249fde", "20d6c7", "a6fcdb", "ffffff", "fef3c0", "fad6b8", "f5a097", "e86a73", "bc4a9b", "793a80", "403353", "242234", "221c1a",
        "322b28", "71413b", "bb7547", "dba463", "f4d29c", "dae0ea", "b3b9d1", "8b93af", "6d758d", "4a5462", "333941", "422433", "5b3138", "8e5252", "ba756a", "e9b5a3",
        "e3e6ff", "b9bffb", "849be4", "588dbe", "477d85", "23674e", "328464", "5daf8d", "92dcba", "cdf7e2", "e4d2aa", "c7b08b", "a08662", "796755", "5a4e44", "423934",
    };

    private static Color[] _palette;
    private const float Padding = 0.25f;
    private const float EdgeMeters = 0.09f;

    private static Color[] Palette => _palette ??= Array.ConvertAll(Aap64, hex => Color.FromHtml(hex));

    // Cor da AAP-64 mais próxima (distância no RGB).
    public static Color Nearest(Color color)
    {
        var best = Palette[0];
        var bestDistance = float.MaxValue;
        foreach (var candidate in Palette)
        {
            var dr = candidate.R - color.R;
            var dg = candidate.G - color.G;
            var db = candidate.B - color.B;
            var distance = dr * dr + dg * dg + db * db;
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    // Cores do golpe: (preenchimento, borda). Sem herança no slot, leve = verde-água e pesado = amarelo da AAP-64.
    public static (Color Fill, Color Edge) Colors(PlayerAttackType attackType, bool heavy)
    {
        if (BloodlineVisuals.TryGetSlotColor(attackType, out var family))
        {
            var fill = Nearest(family);
            return (fill, Nearest(BloodlineVisuals.Lighten(family, 0.55f)));
        }
        return heavy
            ? (Color.FromHtml("ffd541"), Color.FromHtml("fef3c0"))
            : (Color.FromHtml("5daf8d"), Color.FromHtml("cdf7e2"));
    }

    // Golpe em arco (arcDegrees; 360 = círculo), apontando para a frente de `player`, alcance em metros.
    public static void SpawnArc(Node3D player, float range, float arcDegrees, Color fill, Color edge, float duration = 0.16f)
        => Spawn(player, range, range, Mathf.DegToRad(arcDegrees * 0.5f), 0.0f, fill, edge, duration);

    // Golpe em linha (estocada): retângulo de `width` m por `range` m à frente.
    public static void SpawnLine(Node3D player, float range, float width, Color fill, Color edge, float duration = 0.14f)
        => Spawn(player, range, 0.0f, 0.0f, width, fill, edge, duration);

    private static void Spawn(Node3D player, float extent, float arcRadius, float halfArc, float lineWidth, Color fill, Color edge, float duration)
    {
        var scene = player?.GetTree()?.CurrentScene;
        if (scene == null || !GodotObject.IsInstanceValid(player)) return;
        var camera = player.GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        var stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        var pixel = PlayerSpriteVisual.PixelSize;

        // Frente do jogador no referencial do indicador (que gira com a câmera, como as poças).
        var forward = -player.GlobalTransform.Basis.Z;
        forward.Y = 0.0f;
        forward = forward.LengthSquared() > 0.0001f ? forward.Normalized() : Vector3.Forward;
        var local = forward.Rotated(Vector3.Up, -yaw);
        var fx = local.X;
        var fz = local.Z;

        var half = extent + Padding;
        var width = Mathf.Max(8, Mathf.CeilToInt(half * 2.0f / pixel));
        var height = Mathf.Max(8, Mathf.CeilToInt(half * 2.0f / (pixel * stretch)));
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var outline = Color.FromHtml("141013");
        var isLine = lineWidth > 0.0f;
        var fullCircle = halfArc >= Mathf.Pi - 0.01f;

        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var gx = (px + 0.5f - width * 0.5f) * pixel;
                var gz = (py + 0.5f - height * 0.5f) * pixel * stretch;
                float inside; // distância normalizada até a borda: >= 0 dentro, < 0 fora
                if (isLine)
                {
                    var along = gx * fx + gz * fz;
                    var across = Mathf.Abs(-gx * fz + gz * fx);
                    inside = Mathf.Min(Mathf.Min(along, extent - along), lineWidth * 0.5f - across);
                }
                else
                {
                    var distance = Mathf.Sqrt(gx * gx + gz * gz);
                    var radial = arcRadius - distance;
                    var angular = float.MaxValue;
                    if (!fullCircle && distance > 0.001f)
                    {
                        var dot = Mathf.Clamp((gx * fx + gz * fz) / distance, -1.0f, 1.0f);
                        // folga angular até a borda lateral, em metros ao longo do arco (>= 0 dentro)
                        angular = (halfArc - Mathf.Acos(dot)) * distance;
                    }
                    inside = Mathf.Min(radial, angular);
                }
                if (inside < -pixel * 1.6f) continue;
                if (inside < 0.0f)
                    image.SetPixel(px, py, outline);
                else if (inside < EdgeMeters)
                    image.SetPixel(px, py, edge);
                else if (((px + py) & 1) == 0)
                    image.SetPixel(px, py, new Color(fill, 0.55f));
                else
                    image.SetPixel(px, py, new Color(fill, 0.22f));
            }
        }

        var root = new Node3D { Name = "PixelAttackIndicator", Rotation = new Vector3(0.0f, yaw, 0.0f), Scale = new Vector3(1.0f, 1.0f, stretch) };
        var sprite = new Sprite3D
        {
            Texture = ImageTexture.CreateFromImage(image),
            PixelSize = pixel,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Shaded = false,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f)
        };
        root.AddChild(sprite);
        scene.AddChild(root);
        root.GlobalPosition = new Vector3(player.GlobalPosition.X, player.GlobalPosition.Y + 0.1f, player.GlobalPosition.Z);
        var tween = root.CreateTween().SetParallel(true);
        tween.TweenProperty(sprite, "modulate:a", 0.0f, duration).SetDelay(duration * 0.35f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFree));
    }
}

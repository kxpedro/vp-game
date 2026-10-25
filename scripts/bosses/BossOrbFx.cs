using Godot;

/// <summary>
/// Orbe psíquico da Guardiã Invisível: sai da mão dela, faz um arco e cai sobre o aviso no chão exatamente no impacto,
/// deixando um rastro de faíscas. Pixel art desenhada em código (paleta AAP-64, rampa violeta do BossRuneFx).
/// Só visual: o dano continua no BossController.StrikeAt.
/// </summary>
public partial class BossOrbFx : Node3D
{
    private const int OrbSize = 11;
    private const float TrailInterval = 0.035f;

    private static Texture2D _orbTexture;
    private static Texture2D _sparkTexture;

    private Vector3 _from;
    private Vector3 _to;
    private float _seconds;
    private float _elapsed;
    private float _arcHeight;
    private float _trailTimer;
    private Sprite3D _sprite;

    public static void Launch(Node parent, Vector3 from, Vector3 to, float seconds)
    {
        if (parent == null) return;
        var orb = new BossOrbFx
        {
            Name = "BossOrbFx",
            _from = from,
            _to = to + Vector3.Up * 0.25f,
            _seconds = Mathf.Max(seconds, 0.1f),
        };
        orb._arcHeight = 1.0f + from.DistanceTo(to) * 0.18f;
        parent.AddChild(orb);
        orb.GlobalPosition = from;
    }

    public override void _Ready()
    {
        // Orbe em dobro (22 px na tela): é o golpe que o jogador precisa ver, não o aviso no chão.
        _sprite = MakeSprite(OrbTexture, 1.0f);
        _sprite.Scale = Vector3.One * 2.0f;
        AddChild(_sprite);
        var pulse = _sprite.CreateTween().SetLoops();
        pulse.TweenProperty(_sprite, "scale", Vector3.One * 2.4f, 0.09f);
        pulse.TweenProperty(_sprite, "scale", Vector3.One * 2.0f, 0.09f);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        var t = Mathf.Clamp(_elapsed / _seconds, 0.0f, 1.0f);
        GlobalPosition = _from.Lerp(_to, t) + Vector3.Up * (_arcHeight * 4.0f * t * (1.0f - t));
        _trailTimer -= (float)delta;
        if (_trailTimer <= 0.0f)
        {
            _trailTimer = TrailInterval;
            SpawnSpark();
        }
        if (t >= 1.0f) QueueFree();
    }

    private void SpawnSpark()
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null) return;
        var spark = MakeSprite(SparkTexture, 0.9f);
        spark.Scale = Vector3.One * 2.0f;
        scene.AddChild(spark);
        spark.GlobalPosition = GlobalPosition + new Vector3((float)GD.RandRange(-0.08, 0.08), (float)GD.RandRange(-0.08, 0.08), 0.0f);
        var tween = spark.CreateTween();
        tween.TweenProperty(spark, "modulate:a", 0.0f, 0.25f);
        tween.Parallel().TweenProperty(spark, "position:y", spark.Position.Y - 0.15f, 0.25f);
        tween.TweenCallback(Callable.From(spark.QueueFree));
    }

    private static Sprite3D MakeSprite(Texture2D texture, float alpha) => new()
    {
        Texture = texture,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
        Shaded = false,
        DoubleSided = true,
        PixelSize = PlayerSpriteVisual.PixelSize,
        Modulate = new Color(1.0f, 1.0f, 1.0f, alpha),
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    // Orbe 11x11: contorno escuro, corpo magenta, núcleo claro e brilho no alto à esquerda.
    private static Texture2D OrbTexture
    {
        get
        {
            if (_orbTexture != null) return _orbTexture;
            var image = Image.CreateEmpty(OrbSize, OrbSize, false, Image.Format.Rgba8);
            var c = (OrbSize - 1) * 0.5f;
            for (var y = 0; y < OrbSize; y++)
            for (var x = 0; x < OrbSize; x++)
            {
                var d = new Vector2(x - c, y - c).Length();
                Color? color = d > 5.2f ? null
                    : d > 4.3f ? BossRuneFx.Violet[0]
                    : d > 3.3f ? BossRuneFx.Violet[1]
                    : d > 1.6f ? BossRuneFx.Violet[2]
                    : BossRuneFx.Violet[3];
                if (color is { } value) image.SetPixel(x, y, value);
            }
            image.SetPixel(3, 3, BossRuneFx.Violet[3]);
            image.SetPixel(4, 3, BossRuneFx.Violet[3]);
            image.SetPixel(3, 4, BossRuneFx.Violet[3]);
            _orbTexture = ImageTexture.CreateFromImage(image);
            return _orbTexture;
        }
    }

    // Faísca 3x3 em cruz.
    private static Texture2D SparkTexture
    {
        get
        {
            if (_sparkTexture != null) return _sparkTexture;
            var image = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
            image.SetPixel(1, 0, BossRuneFx.Violet[2]);
            image.SetPixel(0, 1, BossRuneFx.Violet[2]);
            image.SetPixel(1, 1, BossRuneFx.Violet[3]);
            image.SetPixel(2, 1, BossRuneFx.Violet[2]);
            image.SetPixel(1, 2, BossRuneFx.Violet[2]);
            _sparkTexture = ImageTexture.CreateFromImage(image);
            return _sparkTexture;
        }
    }
}

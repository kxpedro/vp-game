using Godot;

/// <summary>
/// Gota de Sangue (cura do Tributo Carmesim): gota em pixel art (tools/pixel_art/blood_drop/) em billboard, que flutua
/// e cintila, com uma sombra no chão que acompanha a altura. Ao tocar, cura uma fração da vida máxima.
/// </summary>
public partial class BloodOrbPickup : Area3D
{
    private const string SheetPath = "res://assets/pixel_art/effects/blood_drop/blood_drop_sheet.png";
    private const string ShadowPath = "res://assets/pixel_art/effects/blood_drop/blood_drop_shadow.png";
    private const int Frames = 6;
    private const float ShimmerFps = 4.0f;
    private const float BobAmplitude = 0.08f;
    private const float BobSpeed = 2.6f;
    private const float PixelSize = PlayerSpriteVisual.PixelSize * 0.56f;
    // Pé da gota em relação ao centro do pickup (que fica 0,35 m acima do chão): ela boia pouco acima do chão.
    private const float SpriteHeight = -0.15f;

    private static Texture2D _sheet;
    private static Texture2D _shadowTexture;

    private Sprite3D _sprite;
    private Node3D _shadowRoot;
    private Sprite3D _shadow;
    private float _time;

    public float HealMaxHealthRatio { get; set; }

    public override void _Ready()
    {
        BodyEntered += body =>
        {
            if (!body.IsInGroup("player")) return;
            var health = body.GetNodeOrNull<Health>("Health");
            health?.Heal(health.MaxHealth * HealMaxHealthRatio);
            QueueFree();
        };
        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.65f } });
        _time = (float)GD.RandRange(0.0, Mathf.Tau);
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        if (_sprite == null) return;
        _time += (float)delta;
        var bob = Mathf.Sin(_time * BobSpeed);
        _sprite.Position = new Vector3(0.0f, SpriteHeight + bob * BobAmplitude, 0.0f);
        _sprite.Frame = (int)(_time * ShimmerFps) % Frames;
        if (_shadow != null)
        {
            // A sombra encolhe um pouco quando a gota sobe.
            var s = 1.0f - bob * 0.08f;
            _shadow.Scale = new Vector3(s, s, 1.0f);
        }
    }

    private void BuildVisual()
    {
        _sheet ??= LoadTexture(SheetPath);
        if (_sheet == null)
        {
            GD.PushWarning($"BloodOrbPickup: textura ausente em {SheetPath}");
            return;
        }
        _sprite = new Sprite3D
        {
            Name = "Sprite",
            Texture = _sheet,
            Hframes = Frames,
            PixelSize = PixelSize,
            // Ponto de apoio na base da gota, para a altura valer a do pé.
            Offset = new Vector2(0.0f, 16.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            DoubleSided = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_sprite);

        _shadowTexture ??= LoadTexture(ShadowPath);
        if (_shadowTexture == null) return;
        var camera = GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        var stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        _shadowRoot = new Node3D
        {
            Name = "ShadowRoot",
            TopLevel = true,
            Rotation = new Vector3(0.0f, yaw, 0.0f),
            Scale = new Vector3(1.0f, 1.0f, stretch),
        };
        AddChild(_shadowRoot);
        _shadowRoot.GlobalPosition = new Vector3(GlobalPosition.X, 0.03f, GlobalPosition.Z);
        _shadow = new Sprite3D
        {
            Name = "Shadow",
            Texture = _shadowTexture,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
        };
        _shadowRoot.AddChild(_shadow);
    }

    private static Texture2D LoadTexture(string path)
    {
        var image = PixelRoomComposer.LoadImage(path);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}

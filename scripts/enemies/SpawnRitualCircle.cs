using Godot;

/// <summary>
/// Círculo de ritual que marca no chão onde um monstro vai surgir (pixel art, tools/pixel_art/ritual_circle/; só no 2D).
/// Acende traçando o anel e o pentagrama, pulsa enquanto a wave não chega e estoura quando o monstro aparece (Burst),
/// liberando-se sozinho no fim. Deitado no chão e girado para a câmera como a BloodPool.
/// </summary>
public partial class SpawnRitualCircle : Node3D
{
    private const string SheetPath = "res://assets/pixel_art/effects/ritual_circle/ritual_circle_sheet.png";
    // Rampa violeta da Guardiã Invisível: marca os lacaios dela (BossMinionSummoner).
    public const string VioletSheetPath = "res://assets/pixel_art/effects/ritual_circle/ritual_circle_violet_sheet.png";
    private const int IgniteFrames = 10;
    private const int PulseFrames = 6;
    private const int BurstFrames = 8;
    private const int Columns = IgniteFrames + PulseFrames + BurstFrames;
    private const float MaxIgniteSeconds = 0.8f;
    private const float PulseFps = 9.0f;
    private const float BurstFps = 14.0f;
    // Rente ao chão, abaixo das poças de sangue (BloodPool começa em 0,022 m).
    private const float GroundHeight = 0.018f;

    private enum State { Igniting, Pulsing, Bursting }

    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Sheets = new();

    private Texture2D _sheet;
    private Sprite3D _sprite;
    private State _state = State.Igniting;
    private float _timer;
    private float _igniteSeconds = MaxIgniteSeconds;

    // leadTime: tempo até o monstro surgir; o acender cabe na primeira metade dele.
    public static SpawnRitualCircle Spawn(Node parent, Vector3 position, float leadTime, string sheetPath = SheetPath)
    {
        var sheet = parent == null ? null : LoadSheet(sheetPath);
        if (sheet == null)
            return null;
        var circle = new SpawnRitualCircle
        {
            Name = "SpawnRitualCircle",
            _sheet = sheet,
            Position = new Vector3(position.X, GroundHeight, position.Z),
            _igniteSeconds = Mathf.Clamp(leadTime * 0.5f, 0.1f, MaxIgniteSeconds),
        };
        parent.AddChild(circle);
        return circle;
    }

    public override void _Ready()
    {
        var camera = GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        // A folha já está na projeção 2:1 da tela: estica no chão o que a inclinação da câmera achata.
        var stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        Rotation = new Vector3(0.0f, yaw, 0.0f);
        Scale = new Vector3(1.0f, 1.0f, stretch);
        _sprite = new Sprite3D
        {
            Name = "Sprite",
            Texture = _sheet,
            Hframes = Columns,
            Frame = 0,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
        };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        switch (_state)
        {
            case State.Igniting:
            {
                var frame = (int)(_timer / _igniteSeconds * IgniteFrames);
                if (frame >= IgniteFrames)
                {
                    _state = State.Pulsing;
                    _timer = 0.0f;
                    frame = IgniteFrames;
                }
                SetFrame(frame);
                break;
            }
            case State.Pulsing:
                SetFrame(IgniteFrames + (int)(_timer * PulseFps) % PulseFrames);
                break;
            case State.Bursting:
            {
                var frame = (int)(_timer * BurstFps);
                if (frame >= BurstFrames)
                {
                    QueueFree();
                    return;
                }
                SetFrame(IgniteFrames + PulseFrames + frame);
                break;
            }
        }
    }

    // O monstro surgiu: clarão e o círculo se desfaz.
    public void Burst()
    {
        if (_state == State.Bursting)
            return;
        _state = State.Bursting;
        _timer = 0.0f;
        SetFrame(IgniteFrames + PulseFrames);
    }

    private void SetFrame(int column)
    {
        if (_sprite != null)
            _sprite.Frame = Mathf.Clamp(column, 0, Columns - 1);
    }

    private static Texture2D LoadSheet(string path)
    {
        if (Sheets.TryGetValue(path, out var cached))
            return cached;
        var image = PixelRoomComposer.LoadImage(path);
        var sheet = image == null ? null : ImageTexture.CreateFromImage(image);
        Sheets[path] = sheet;
        return sheet;
    }
}

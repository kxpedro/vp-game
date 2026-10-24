using Godot;

/// <summary>
/// Poça de sangue que escorre do corpo do inimigo morto (pixel art, tools/pixel_art/blood_pool/). Fica deitada no chão,
/// girada para a câmera e esticada em profundidade, de modo que cada pixel da folha vire um pixel da arte na tela.
/// Ao limpar a sala, o HUD a suga (Drain) e um filete de sangue escorre dela até o Virgil (HUD.BlessingOffer, grupo "blood_pools").
/// </summary>
public partial class BloodPool : Node3D
{
    public const string Group = "blood_pools";
    private const string SheetPath = "res://assets/pixel_art/effects/blood_pool/blood_pool_sheet.png";
    private const int Columns = 12;
    private const int Variants = 3;
    private const int SpreadFrames = 8;
    private const int ShimmerFrames = 4;
    private const float SpreadFps = 13.0f;
    private const float ShimmerFps = 3.0f;
    // Rente ao chão, acima do quadrilátero da sala 2D e da sombra do Virgil (0,03 m fica por cima, transparente).
    private const float GroundHeight = 0.022f;
        // Poças que nascem perto demais são empurradas para longe até esta distância entre centros (m).
    private const float MinSeparation = 0.5f;
    // Cada poça nova fica um degrau acima da anterior: a mais nova sempre cobre a de baixo, sem as duas brigarem
    // pelo mesmo plano (z-fighting). O ciclo volta ao chão a cada LayerCount poças.
    private const float LayerStep = 0.0025f;
    private const int LayerCount = 24;
    private static int _nextLayer;

    private enum State { Waiting, Spreading, Idle, Draining, Drained }

    private static Texture2D _sheet;

    private Sprite3D _sprite;
    private State _state = State.Waiting;
    private int _variant;
    private float _timer;
    private float _drainDuration = 1.0f;
    private int _drainFromFrame;

    public bool IsDrained => _state == State.Drained;

    // delay: espera o corpo cair antes de o sangue começar a escorrer.
    public static BloodPool Spawn(Node parent, Vector3 position, float delay)
    {
        if (parent == null || LoadSheet() == null)
            return null;
        var height = GroundHeight + (_nextLayer++ % LayerCount) * LayerStep;
        var planar = SeparateFromOtherPools(parent.GetTree(), new Vector3(position.X, 0.0f, position.Z));
        var pool = new BloodPool
        {
            Name = "BloodPool",
            Position = new Vector3(planar.X, height, planar.Z),
            _timer = -delay,
        };
        parent.AddChild(pool);
        return pool;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        _variant = (int)(GD.Randi() % Variants);
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
            Vframes = Variants,
            Frame = _variant * Columns,
            FlipH = GD.Randf() < 0.5f,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            // No 2D a luz já está na arte; no 3D a poça recebe a luz da sala.
            Shaded = !GameMode.IsPixelArt,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Visible = false,
        };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        switch (_state)
        {
            case State.Waiting:
                if (_timer < 0.0f)
                    return;
                _state = State.Spreading;
                _sprite.Visible = true;
                SetFrame(0);
                break;
            case State.Spreading:
            {
                var frame = (int)(_timer * SpreadFps);
                if (frame >= SpreadFrames)
                {
                    _state = State.Idle;
                    _timer = (float)GD.RandRange(0.0, ShimmerFrames / ShimmerFps);
                    frame = SpreadFrames - 1;
                }
                SetFrame(frame);
                break;
            }
            case State.Idle:
                SetFrame(SpreadFrames + (int)(_timer * ShimmerFps) % ShimmerFrames);
                break;
            case State.Draining:
            {
                var progress = Mathf.Clamp(_timer / _drainDuration, 0.0f, 1.0f);
                var frame = Mathf.RoundToInt(Mathf.Lerp(_drainFromFrame, -1.0f, progress));
                if (frame < 0)
                {
                    _state = State.Drained;
                    _sprite.Visible = false;
                    return;
                }
                SetFrame(frame);
                break;
            }
        }
    }

    // Empurra o centro para longe das poças já no chão, para duas não ocuparem o mesmo lugar.
    private static Vector3 SeparateFromOtherPools(SceneTree tree, Vector3 position)
    {
        if (tree == null)
            return position;
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var node in tree.GetNodesInGroup(Group))
            {
                if (node is not BloodPool other || other.IsQueuedForDeletion())
                    continue;
                var offset = position - new Vector3(other.GlobalPosition.X, 0.0f, other.GlobalPosition.Z);
                var distance = offset.Length();
                if (distance >= MinSeparation)
                    continue;
                var direction = distance > 0.01f
                    ? offset / distance
                    : Vector3.Right.Rotated(Vector3.Up, (float)GD.RandRange(0.0, Mathf.Tau));
                position += direction * (MinSeparation - distance);
            }
        }
        return position;
    }

    // Seca a poça de trás para frente (quadros do espalhar ao contrário). Roda mesmo com a árvore pausada.
    public void Drain(float duration)
    {
        if (_state is State.Draining or State.Drained)
            return;
        ProcessMode = ProcessModeEnum.Always;
        _drainFromFrame = _state switch
        {
            State.Waiting => -1,
            State.Spreading => Mathf.Min((int)(_timer * SpreadFps), SpreadFrames - 1),
            _ => SpreadFrames - 1,
        };
        _drainDuration = Mathf.Max(duration, 0.05f);
        _timer = 0.0f;
        _state = State.Draining;
    }

    private void SetFrame(int column)
    {
        if (_sprite != null)
            _sprite.Frame = _variant * Columns + Mathf.Clamp(column, 0, Columns - 1);
    }

    private static Texture2D LoadSheet()
    {
        _sheet ??= LoadTexture(SheetPath);
        return _sheet;
    }

    private static Texture2D LoadTexture(string path)
    {
        var image = PixelRoomComposer.LoadImage(path);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}

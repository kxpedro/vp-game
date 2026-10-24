using Godot;

// Modo 2D: boneco de treino do hub em pixel art (assets/pixel_art/crypt/sprites/training_dummy.png, gerado por
// tools/pixel_art/crypt_kit/training_dummy.py). Sprite3D em billboard no lugar do cilindro; ao apanhar, pisca em
// vermelho e balança para longe de quem bateu (quadros 1..N-1 da folha, espelhados quando o golpe vem da direita).
public partial class TrainingDummySprite : Node3D
{
    private const string SheetPath = "res://assets/pixel_art/crypt/sprites/training_dummy.png";
    private const int FrameSize = 96;
    // Pés no pixel y = 84 do quadro de 96: 36 px abaixo do centro.
    private const float FeetOffsetPixels = 36.0f;
    private const float SwayFps = 14.0f;

    private static Texture2D _texture;
    private Sprite3D _sprite;
    private int _frames = 1;
    private double _swayTime = -1.0;
    private Tween _flashTween;

    public override void _Ready()
    {
        _texture ??= LoadSheet();
        if (_texture == null)
            return;
        _frames = Mathf.Max(_texture.GetWidth() / FrameSize, 1);
        _sprite = new Sprite3D
        {
            Name = "Sprite",
            Texture = _texture,
            Hframes = _frames,
            Frame = 0,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = true,
            DoubleSided = true,
            PixelSize = PixelRoom.PixelMeters,
            Offset = new Vector2(0.0f, FeetOffsetPixels),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_sprite);
    }

    private static Texture2D LoadSheet()
    {
        var image = PixelRoomComposer.LoadImage(SheetPath);
        if (image == null)
        {
            GD.PushWarning($"TrainingDummySprite: folha nao encontrada: {SheetPath}");
            return null;
        }
        return ImageTexture.CreateFromImage(image);
    }

    // Golpe vindo de "attacker" (posição no mundo): balança para o lado da tela oposto ao dele.
    public void Hit(Vector3 attacker)
    {
        if (_sprite == null)
            return;
        var camera = GetViewport()?.GetCamera3D();
        if (camera != null)
        {
            var side = (GlobalPosition - attacker).Dot(camera.GlobalTransform.Basis.X);
            if (Mathf.Abs(side) > 0.01f)
                _sprite.FlipH = side < 0.0f;
        }
        _swayTime = 0.0;
        _flashTween?.Kill();
        _sprite.Modulate = new Color(1.0f, 0.45f, 0.45f);
        _flashTween = CreateTween();
        _flashTween.TweenProperty(_sprite, "modulate", Colors.White, 0.14f);
    }

    public override void _Process(double delta)
    {
        if (_sprite == null || _swayTime < 0.0)
            return;
        _swayTime += delta;
        var frame = 1 + (int)(_swayTime * SwayFps);
        if (frame >= _frames)
        {
            _swayTime = -1.0;
            frame = 0;
        }
        _sprite.Frame = frame;
    }
}

using Godot;
using System.Collections.Generic;

// TESTE (nao definitivo): Virgil como sprite pixel art 2D (LPC) em billboard no mundo 3D.
// Esconde o CharacterModel e toca as animacoes do sprite a partir das mesmas chamadas do PlayerAnimator.
// Liga/desliga em debug com F3 ou com o comando "sprite" do console. F4 = render pixelado (PixelRenderTest.cs).
public partial class PlayerSpriteVisual : Node
{
    public static bool Enabled { get; set; } = true;

    private const string SheetFolder = "res://assets/characters/lpc_virgil_test/";
    private const int CellSize = 192;
    public const float PixelSize = 0.033f;
    // Pes do personagem ficam 30 px abaixo do centro da celula de 192 px.
    private const float FeetOffsetPixels = 30.0f;
    private const float WalkFps = 12.0f;
    private const float WalkReferenceSpeed = 3.0f;

    private static readonly string[] Directions = { "n", "w", "s", "e" };

    // TESTE de arte (PixelLab): F6 alterna LPC animado -> imagem parada "frente" -> "de cima".
    private const string PreviewFolder = "res://assets/characters/pixellab_virgil_test/";
    // Entrada terminada em "/" = pasta com as 8 direcoes (south.png, south-east.png...), segue o movimento.
    private static readonly string[] PreviewFiles = { "v5/", "front_s.png", "topdown_s.png", "v3/", "v4/" };
    private static readonly string[] PreviewDirections =
        { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
    private const float PreviewHeightMeters = 1.6f;

    // TESTE: escala do sprite ao vivo (PageUp/PageDown, passos de 10%), pes continuam no chao.
    private const float ScaleStep = 1.1f;
    private const float MinScale = 0.5f;
    private const float MaxScale = 3.0f;
    // Modo 2D: 1x deixa o pixel do sprite igual ao pixel do cenário (PixelRoom.PixelMeters). No teste com a câmera 3D
    // em 70% o Pedro tinha escolhido 1,4x.
    private static float _spriteScale = 1.0f;

    private enum Mode { Loop, OneShot, Dead }

    private CharacterBody3D _player;
    private Node3D _model;
    private AnimatedSprite3D _sprite;
    private MeshInstance3D _blobShadow;
    private Health _health;
    private readonly Dictionary<string, int> _frameCounts = new();
    private string _direction = "s";
    private Mode _mode = Mode.Loop;
    private string _oneShotBase = "";
    private float _oneShotTimer;
    private bool _isMoving;
    private bool _isRunning;
    private float _movementSpeed;
    private Sprite3D _previewSprite;
    private int _previewIndex;
    private readonly Dictionary<string, Texture2D> _previewDirTextures = new();
    private float _previewBasePixelSize = PixelSize;
    private Label _scaleLabel;

    public override void _Ready()
    {
        _player = GetParent<CharacterBody3D>();
        _model = _player.GetNodeOrNull<Node3D>("CharacterModel");
        _health = _player.GetNodeOrNull<Health>("Health");
        if (_health != null)
            _health.Died += OnDied;

        var frames = BuildFrames();
        if (frames == null)
        {
            GD.PushWarning("PlayerSpriteVisual: folhas do sprite de teste nao encontradas.");
            return;
        }

        _sprite = new AnimatedSprite3D
        {
            Name = "SpriteVisual",
            SpriteFrames = frames,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            // Recebe a luz da cena (tochas, portas) em vez de brilho fixo.
            Shaded = true,
            DoubleSided = true,
            PixelSize = PixelSize,
            Offset = new Vector2(0.0f, FeetOffsetPixels),
            // Sombra de billboard sai torta; a sombra fica por conta da mancha no chao.
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _player.AddChild(_sprite);
        _previewSprite = new Sprite3D
        {
            Name = "SpritePreview",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = true,
            DoubleSided = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        _player.AddChild(_previewSprite);
        _blobShadow = BuildBlobShadow();
        _player.AddChild(_blobShadow);
        _player.AddChild(new PixelRenderTest { Name = "PixelRenderTest" });
        if (OS.IsDebugBuild())
        {
            var layer = new CanvasLayer { Name = "SpriteScaleLayer", Layer = 50 };
            _scaleLabel = new Label
            {
                Name = "SpriteScaleLabel",
                Position = new Vector2(16.0f, 112.0f),
                Modulate = new Color(1.0f, 1.0f, 1.0f, 0.85f),
            };
            layer.AddChild(_scaleLabel);
            AddChild(layer);
        }
        ApplyScale();
        ApplyEnabled();
        PlayLoop();
    }

    public override void _ExitTree()
    {
        if (_health != null)
            _health.Died -= OnDied;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild())
            return;
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 })
        {
            Enabled = !Enabled;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F6 })
        {
            _previewIndex = (_previewIndex + 1) % (PreviewFiles.Length + 1);
            ApplyPreview();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.Pageup or Key.Pagedown } key)
        {
            var factor = key.Keycode == Key.Pageup ? ScaleStep : 1.0f / ScaleStep;
            _spriteScale = Mathf.Clamp(_spriteScale * factor, MinScale, MaxScale);
            ApplyScale();
            GD.Print("PlayerSpriteVisual: ", ScaleText());
            GetViewport().SetInputAsHandled();
        }
    }

    // Offset fica em pixels da textura, entao mudar o PixelSize mantem os pes na origem.
    private void ApplyScale()
    {
        if (_sprite != null)
            _sprite.PixelSize = PixelSize * _spriteScale;
        if (_previewSprite != null)
            _previewSprite.PixelSize = _previewBasePixelSize * _spriteScale;
        if (_blobShadow != null)
            _blobShadow.Scale = new Vector3(_spriteScale, 1.0f, _spriteScale);
    }

    // Altura aproximada em 1080p para um personagem de ~1,6 m na distancia atual da camera.
    private string ScaleText()
    {
        var camera = GetViewport()?.GetCamera3D();
        var px = 0.0f;
        if (camera != null && _player != null)
        {
            var distance = camera.GlobalPosition.DistanceTo(_player.GlobalPosition + Vector3.Up * 0.8f * _spriteScale);
            var viewHeight = 2.0f * distance * Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
            px = PreviewHeightMeters * _spriteScale / Mathf.Max(viewHeight, 0.001f) * 1080.0f;
        }
        return string.Format("Escala do sprite: {0:0.00}x  (~{1:0} px em 1080p)  PageUp/PageDown", _spriteScale, px);
    }

    // Imagem parada no mesmo billboard, pes na origem do jogador e altura ~1,6 m.
    private void ApplyPreview()
    {
        if (_previewSprite == null || _previewIndex == 0)
            return;

        _previewDirTextures.Clear();
        var path = PreviewFolder + PreviewFiles[_previewIndex - 1];
        if (path.EndsWith("/"))
        {
            ApplyDirectionalPreview(path);
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image == null || image.IsEmpty())
        {
            GD.PushWarning("PlayerSpriteVisual: imagem de teste nao encontrada: " + path);
            _previewIndex = 0;
            return;
        }

        var used = image.GetUsedRect();
        _previewSprite.Texture = ImageTexture.CreateFromImage(image);
        _previewBasePixelSize = PreviewHeightMeters / Mathf.Max(used.Size.Y, 1);
        ApplyScale();
        _previewSprite.Offset = new Vector2(0.0f, used.End.Y - image.GetHeight() * 0.5f);
        GD.Print("PlayerSpriteVisual: previa ", PreviewFiles[_previewIndex - 1]);
    }

    // 8 direcoes: escala pela altura do quadro sul e pes na base comum de todos os quadros.
    private void ApplyDirectionalPreview(string folder)
    {
        var bottom = 0;
        var southHeight = 0;
        var canvasHeight = 0;
        foreach (var dir in PreviewDirections)
        {
            var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(folder + dir + ".png"));
            if (image == null || image.IsEmpty())
                continue;
            var used = image.GetUsedRect();
            bottom = Mathf.Max(bottom, used.End.Y);
            canvasHeight = image.GetHeight();
            if (dir == "south")
                southHeight = used.Size.Y;
            _previewDirTextures[dir] = ImageTexture.CreateFromImage(image);
        }

        if (_previewDirTextures.Count == 0 || southHeight == 0)
        {
            GD.PushWarning("PlayerSpriteVisual: direcoes de teste nao encontradas em " + folder);
            _previewDirTextures.Clear();
            _previewIndex = 0;
            return;
        }

        _previewBasePixelSize = PreviewHeightMeters / southHeight;
        ApplyScale();
        _previewSprite.Offset = new Vector2(0.0f, bottom - canvasHeight * 0.5f);
        UpdatePreviewDirection();
        GD.Print("PlayerSpriteVisual: previa 8 direcoes ", folder);
    }

    private void UpdatePreviewDirection()
    {
        if (_previewDirTextures.Count == 0)
            return;
        var camera = GetViewport()?.GetCamera3D();
        if (camera == null || _player == null)
            return;

        var forward = -_player.GlobalBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.0001f)
            return;

        var camRight = camera.GlobalBasis.X;
        var camForward = -camera.GlobalBasis.Z;
        camRight.Y = 0.0f;
        camForward.Y = 0.0f;
        var x = forward.Dot(camRight.Normalized());
        var y = forward.Dot(camForward.Normalized());
        // 0 = de costas para a camera (north), sentido horario: north-east, east...
        var angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(x, y)), 360.0f);
        var index = Mathf.RoundToInt(angle / 45.0f) % 8;
        if (_previewDirTextures.TryGetValue(PreviewDirections[index], out var texture) && _previewSprite.Texture != texture)
            _previewSprite.Texture = texture;
    }

    public override void _Process(double delta)
    {
        if (_sprite == null)
            return;

        ApplyEnabled();
        UpdateDirection();
        UpdatePreviewDirection();
        if (_scaleLabel != null)
            _scaleLabel.Text = Enabled ? ScaleText() : "";

        if (_mode == Mode.OneShot)
        {
            _oneShotTimer -= (float)delta;
            if (_oneShotTimer <= 0.0f)
                _mode = Mode.Loop;
            else
                SwapDirection(_oneShotBase);
        }

        if (_mode == Mode.Loop)
            PlayLoop();
    }

    public void SetMovementState(bool isMoving, bool isRunning, float movementSpeed)
    {
        _isMoving = isMoving;
        _isRunning = isRunning;
        _movementSpeed = Mathf.Max(movementSpeed, 0.0f);
    }

    public void PlayLightAttack(int comboIndex, float duration, WeaponAttackShape shape)
    {
        var animation = shape == WeaponAttackShape.Line ? "thrust" : comboIndex % 2 == 0 ? "slash" : "slash_reverse";
        PlayOneShot(animation, duration);
    }

    public void PlayHeavyAttack(float duration) => PlayOneShot("slash", duration);

    public void PlayDodge(float duration) => PlayOneShot("dodge", duration);

    public void PlayHitReaction(float duration) => PlayOneShot("hit", Mathf.Max(duration, 0.15f));

    public void CancelOneShot()
    {
        if (_mode != Mode.OneShot)
            return;
        _mode = Mode.Loop;
        _oneShotTimer = 0.0f;
    }

    private void ApplyEnabled()
    {
        var on = Enabled && _sprite != null;
        var animatedOn = on && _previewIndex == 0;
        if (_sprite != null && _sprite.Visible != animatedOn)
            _sprite.Visible = animatedOn;
        var previewOn = on && _previewIndex > 0;
        if (_previewSprite != null && _previewSprite.Visible != previewOn)
            _previewSprite.Visible = previewOn;
        if (_blobShadow != null && _blobShadow.Visible != on)
            _blobShadow.Visible = on;
        if (_model != null && _model.Visible == on)
            _model.Visible = !on;
    }

    private void OnDied()
    {
        if (_sprite == null)
            return;
        _mode = Mode.Dead;
        _sprite.SpeedScale = 1.0f;
        _sprite.Play("death");
    }

    private void PlayLoop()
    {
        if (!_isMoving)
        {
            Play("idle_" + _direction, 1.0f);
            return;
        }

        var reference = _isRunning ? WalkReferenceSpeed * 1.3f : WalkReferenceSpeed;
        Play("walk_" + _direction, Mathf.Clamp(_movementSpeed / reference, 0.6f, 2.2f));
    }

    private void PlayOneShot(string baseName, float duration)
    {
        if (_sprite == null || _mode == Mode.Dead)
            return;

        UpdateDirection();
        var name = baseName + "_" + (baseName == "hit" ? "s" : _direction);
        if (!_sprite.SpriteFrames.HasAnimation(name))
            return;

        duration = Mathf.Max(duration, 0.05f);
        var frameCount = _sprite.SpriteFrames.GetFrameCount(name);
        var naturalLength = frameCount / (float)_sprite.SpriteFrames.GetAnimationSpeed(name);
        _mode = Mode.OneShot;
        _oneShotBase = baseName;
        _oneShotTimer = duration;
        _sprite.Play(name);
        _sprite.Frame = 0;
        _sprite.SpeedScale = naturalLength / duration;
    }

    // Golpe virou de direcao no meio (mira): troca a linha mantendo o quadro.
    private void SwapDirection(string baseName)
    {
        if (baseName == "hit")
            return;
        var name = baseName + "_" + _direction;
        if (_sprite.Animation == name || !_sprite.SpriteFrames.HasAnimation(name))
            return;
        var frame = _sprite.Frame;
        var progress = _sprite.FrameProgress;
        _sprite.Play(name);
        _sprite.SetFrameAndProgress(frame, progress);
    }

    private void Play(string name, float speed)
    {
        if (_sprite.Animation != name || !_sprite.IsPlaying())
            _sprite.Play(name);
        _sprite.SpeedScale = speed;
    }

    private void UpdateDirection()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera == null || _player == null)
            return;

        var forward = -_player.GlobalBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.0001f)
            return;

        var camRight = camera.GlobalBasis.X;
        var camForward = -camera.GlobalBasis.Z;
        camForward.Y = 0.0f;
        camRight.Y = 0.0f;
        var x = forward.Dot(camRight.Normalized());
        var y = forward.Dot(camForward.Normalized());
        // Folga pequena evita trocar de linha a cada quadro na diagonal exata.
        if (Mathf.Abs(x) > Mathf.Abs(y) * 1.05f)
            _direction = x > 0.0f ? "e" : "w";
        else if (Mathf.Abs(y) > Mathf.Abs(x) * 1.05f)
            _direction = y > 0.0f ? "n" : "s";
    }

    private static MeshInstance3D BuildBlobShadow()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.0f, 0.0f, 0.0f, 0.55f));
        gradient.SetColor(1, new Color(0.0f, 0.0f, 0.0f, 0.0f));
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0.0f),
            Width = 32,
            Height = 32,
        };
        return new MeshInstance3D
        {
            Name = "SpriteBlobShadow",
            Mesh = new PlaneMesh { Size = new Vector2(0.95f, 0.6f) },
            Position = new Vector3(0.0f, 0.03f, 0.0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoTexture = texture,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            },
        };
    }

    private SpriteFrames BuildFrames()
    {
        var walk = LoadSheet("virgil_walk.png");
        var slash = LoadSheet("virgil_slash.png");
        var slashReverse = LoadSheet("virgil_slash_reverse.png");
        var thrust = LoadSheet("virgil_thrust.png");
        var hurt = LoadSheet("virgil_hurt.png");
        if (walk == null)
            return null;

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        for (var row = 0; row < Directions.Length; row++)
        {
            var dir = Directions[row];
            AddAnimation(frames, "idle_" + dir, walk, row, 0, 1, 1.0f, true);
            AddAnimation(frames, "walk_" + dir, walk, row, 1, 8, WalkFps, true);
            AddAnimation(frames, "dodge_" + dir, walk, row, 1, 8, WalkFps * 2.0f, false);
            AddAnimation(frames, "slash_" + dir, slash, row, 0, 6, 12.0f, false);
            AddAnimation(frames, "slash_reverse_" + dir, slashReverse, row, 0, 6, 12.0f, false);
            AddAnimation(frames, "thrust_" + dir, thrust, row, 0, 8, 14.0f, false);
        }
        AddAnimation(frames, "hit_s", hurt, 0, 0, 2, 10.0f, false);
        AddAnimation(frames, "death", hurt, 0, 0, 6, 8.0f, false);
        return frames;
    }

    private static void AddAnimation(SpriteFrames frames, string name, Texture2D sheet, int row, int first, int count, float fps, bool loop)
    {
        if (sheet == null)
            return;
        frames.AddAnimation(name);
        frames.SetAnimationSpeed(name, fps);
        frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
        for (var i = 0; i < count; i++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2((first + i) * CellSize, row * CellSize, CellSize, CellSize)
            };
            frames.AddFrame(name, atlas);
        }
    }

    private static Texture2D LoadSheet(string file)
    {
        var path = SheetFolder + file;
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        // Sem .import (projeto ainda nao aberto no editor): carrega o PNG direto.
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        return image == null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }
}

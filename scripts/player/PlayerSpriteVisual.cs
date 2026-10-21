using Godot;
using System.Collections.Generic;

// TESTE (nao definitivo): Virgil como sprite pixel art 2D (LPC) em billboard no mundo 3D.
// Esconde o CharacterModel e toca as animacoes do sprite a partir das mesmas chamadas do PlayerAnimator.
// Junto com o sprite liga um filtro de tela que pixela o mundo 3D em blocos do tamanho do pixel do sprite.
// Liga/desliga em debug com F3 ou com o comando "sprite" do console ("pixel <n>" ajusta o tamanho do bloco).
public partial class PlayerSpriteVisual : Node
{
    public static bool Enabled { get; set; } = true;
    // Bloco do filtro de tela, em pixels do sprite (1 = mesmo tamanho; 0 = filtro desligado).
    public static float PixelBlockScale { get; set; } = 1.0f;

    private const string SheetFolder = "res://assets/characters/lpc_virgil_test/";
    private const int CellSize = 192;
    private const float PixelSize = 0.033f;
    // Pes do personagem ficam 30 px abaixo do centro da celula de 192 px.
    private const float FeetOffsetPixels = 30.0f;
    private const float WalkFps = 12.0f;
    private const float WalkReferenceSpeed = 3.0f;

    private const string PixelateShaderCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
// Tamanho do bloco como fracao da altura da tela.
uniform float block_ratio = 0.0015;
uniform float color_levels = 24.0;
void fragment() {
    float block = max(block_ratio / SCREEN_PIXEL_SIZE.y, 1.0);
    vec2 cell = (floor(FRAGCOORD.xy / block) + 0.5) * block;
    vec3 c = texture(screen_tex, cell * SCREEN_PIXEL_SIZE).rgb;
    c = floor(c * color_levels + 0.5) / color_levels;
    COLOR = vec4(c, 1.0);
}";

    private static readonly string[] Directions = { "n", "w", "s", "e" };

    private enum Mode { Loop, OneShot, Dead }

    private CharacterBody3D _player;
    private Node3D _model;
    private AnimatedSprite3D _sprite;
    private MeshInstance3D _blobShadow;
    private CanvasLayer _pixelLayer;
    private ShaderMaterial _pixelMaterial;
    private Health _health;
    private readonly Dictionary<string, int> _frameCounts = new();
    private string _direction = "s";
    private Mode _mode = Mode.Loop;
    private string _oneShotBase = "";
    private float _oneShotTimer;
    private bool _isMoving;
    private bool _isRunning;
    private float _movementSpeed;

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
            Shaded = true,
            DoubleSided = true,
            PixelSize = PixelSize,
            Offset = new Vector2(0.0f, FeetOffsetPixels),
            // Sombra de billboard gira com a camera; a sombra fica por conta do disco nos pes.
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _player.AddChild(_sprite);
        _blobShadow = CreateBlobShadow();
        _player.AddChild(_blobShadow);
        CreatePixelFilter();
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
    }

    public override void _Process(double delta)
    {
        if (_sprite == null)
            return;

        ApplyEnabled();
        UpdatePixelFilter();
        UpdateDirection();

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
        if (_sprite != null && _sprite.Visible != on)
            _sprite.Visible = on;
        if (_blobShadow != null && _blobShadow.Visible != on)
            _blobShadow.Visible = on;
        if (_model != null && _model.Visible == on)
            _model.Visible = !on;
    }

    private MeshInstance3D CreateBlobShadow()
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
            Width = 64,
            Height = 64,
        };
        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
        return new MeshInstance3D
        {
            Name = "SpriteBlobShadow",
            Mesh = new PlaneMesh { Size = new Vector2(0.9f, 0.6f) },
            MaterialOverride = material,
            Position = new Vector3(0.0f, 0.03f, 0.0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    private void CreatePixelFilter()
    {
        _pixelMaterial = new ShaderMaterial { Shader = new Shader { Code = PixelateShaderCode } };
        var rect = new ColorRect { Name = "PixelFilter", Material = _pixelMaterial, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Abaixo do HUD (layer 1): pixela so o mundo, a interface continua nitida.
        _pixelLayer = new CanvasLayer { Name = "SpritePixelFilter", Layer = -50 };
        _pixelLayer.AddChild(rect);
        _player.AddChild(_pixelLayer);
    }

    // Bloco = tamanho na tela de um pixel do sprite, medido na distancia da camera ao jogador.
    private void UpdatePixelFilter()
    {
        if (_pixelLayer == null)
            return;

        var on = Enabled && PixelBlockScale > 0.0f;
        if (_pixelLayer.Visible != on)
            _pixelLayer.Visible = on;
        var camera = GetViewport()?.GetCamera3D();
        if (!on || camera == null)
            return;

        float visibleHeight;
        if (camera.Projection == Camera3D.ProjectionType.Orthogonal)
            visibleHeight = camera.Size;
        else
        {
            var distance = Mathf.Max(camera.GlobalPosition.DistanceTo(_player.GlobalPosition), 0.1f);
            visibleHeight = 2.0f * distance * Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        }
        _pixelMaterial.SetShaderParameter("block_ratio", PixelSize * PixelBlockScale / Mathf.Max(visibleHeight, 0.01f));
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
        frames.SetAnimationLoop(name, loop);
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

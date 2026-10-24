using Godot;
using System.Collections.Generic;

// Modo 2D: Virgil como sprite pixel art em billboard no mundo 3D (folha gerada por tools/pixel_art/virgil/).
// Esconde o CharacterModel e toca as animacoes do sprite a partir das mesmas chamadas do PlayerAnimator.
// Liga/desliga em debug com F3 ou com o comando "sprite" do console.
public partial class PlayerSpriteVisual : Node
{
    public static bool Enabled { get; set; } = true;

    private const string SheetFolder = "res://assets/pixel_art/characters/virgil/";
    private const string SheetFile = "virgil_sheet.png";
    // Folha: uma linha por direcao (ordem de Directions); colunas 0-7 = idle, 8-15 = corrida.
    private const int CellWidth = 80;
    private const int CellHeight = 112;
    private const int IdleFrames = 8;
    private const int RunFrames = 8;
    // Folha de combate (tools/pixel_art/virgil/combat.py): celula maior, porque a espada estendida passa dos 80 px.
    // Mesma ordem de linhas; colunas = golpes em sequencia (CombatAnims). Pes tambem 40 px abaixo do centro.
    private const string CombatSheetFile = "virgil_combat.png";
    private const int CombatCellWidth = 160;
    private const int CombatCellHeight = 128;
    // Nome, quadros e quadro do impacto (o que cai no instante do dano; -1 = sem golpe, toca linear).
    private static readonly (string Name, int Frames, int Impact)[] CombatAnims =
    {
        ("slash", 6, 3), ("slash_reverse", 6, 3), ("thrust", 6, 3), ("spin", 8, 2), ("dash", 5, -1),
    };
    // Rastro do dash e do giro (estilo Hades): copias do quadro atual deixadas para tras, sumindo.
    private const float GhostInterval = 0.035f;
    private const float GhostLifetime = 0.22f;
    private static readonly Color GhostColor = new(0.75f, 0.12f, 0.2f, 0.55f);
    public const float PixelSize = 0.033f;
    // A folha do Virgil e renderizada a 0,0185 m por pixel (render.py, px): com esse PixelSize ele fica com a altura
    // real (~1,8 m) sem perder resolucao; com o pixel do cenario (0,033) ficava com ~3,2 m.
    private const float VirgilPixelSize = 0.0185f;
    // Pes do personagem ficam 40 px abaixo do centro da celula de 112 px.
    private const float FeetOffsetPixels = 40.0f;
    private const float IdleFps = 6.0f;
    private const float WalkFps = 12.0f;
    private const float WalkReferenceSpeed = 3.0f;

    // 8 direcoes em sentido horario, a partir de "de costas para a camera".
    private static readonly string[] Directions = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

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
    // Modo 2D: 1x = Virgil na altura real (VirgilPixelSize), com pixel menor que o do cenario (PixelRoom.PixelMeters).
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
    private float _oneShotElapsed;
    private float _oneShotHitDelay;
    private float _oneShotRecovery;
    private int _oneShotImpact;
    private float _ghostTimer;
    private float _ghostTimeLeft;
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
            PixelSize = VirgilPixelSize,
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
            _sprite.PixelSize = VirgilPixelSize * _spriteScale;
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
            _oneShotElapsed += (float)delta;
            if (_oneShotTimer <= 0.0f)
                _mode = Mode.Loop;
            else
                StepOneShot();
        }
        TickGhosts((float)delta);

        if (_mode == Mode.Loop)
            PlayLoop();
    }

    public void SetMovementState(bool isMoving, bool isRunning, float movementSpeed)
    {
        _isMoving = isMoving;
        _isRunning = isRunning;
        _movementSpeed = Mathf.Max(movementSpeed, 0.0f);
    }

    // Combo da espada: corte, corte de volta e estocada (o terceiro golpe e em linha).
    public void PlayLightAttack(int comboIndex, float duration, WeaponAttackShape shape) => PlayLightAttack(comboIndex, 0.0f, duration, shape);

    public void PlayLightAttack(int comboIndex, float hitDelay, float recovery, WeaponAttackShape shape)
    {
        if (IsRangedWeapon())
            return;
        var animation = shape == WeaponAttackShape.Line ? "thrust" : comboIndex % 2 == 0 ? "slash" : "slash_reverse";
        PlayOneShot(animation, hitDelay, recovery);
    }

    // Especial (Q): ainda sem animacao propria, usa o corte.
    public void PlayHeavyAttack(float duration) => PlayHeavyAttack(0.0f, duration);

    public void PlayHeavyAttack(float hitDelay, float recovery)
    {
        if (!IsRangedWeapon())
            PlayOneShot("slash", hitDelay, recovery);
    }

    // Ataque rapido (saindo da esquiva): giro com a espada estendida, dano em area.
    public void PlayDashAttack(float hitDelay, float recovery)
    {
        if (IsRangedWeapon())
            return;
        PlayOneShot("spin", hitDelay, recovery);
        // O giro deixa o mesmo vulto vermelho do dash a cada quadro da volta.
        StartGhosts(hitDelay + recovery);
    }

    public void PlayDodge(float duration)
    {
        PlayOneShot("dash", 0.0f, duration);
        StartGhosts(duration);
    }

    private void StartGhosts(float duration)
    {
        _ghostTimeLeft = Mathf.Max(duration, 0.05f);
        _ghostTimer = 0.0f;
    }

    public void PlayHitReaction(float duration) => PlayOneShot("hit", 0.0f, Mathf.Max(duration, 0.15f));

    // Os golpes da folha sao de espada; com arma de fogo o sprite segue no idle/corrida.
    private bool IsRangedWeapon() => WeaponLoadout.EquippedWeapon?.IsRanged == true;

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
        if (_sprite == null || !_sprite.SpriteFrames.HasAnimation("death"))
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

    // Quadros conduzidos a mao: ate o quadro do impacto em hitDelay (instante do dano), o resto em recovery.
    private void PlayOneShot(string baseName, float hitDelay, float recovery)
    {
        if (_sprite == null || _mode == Mode.Dead)
            return;

        UpdateDirection();
        if (!_sprite.SpriteFrames.HasAnimation(OneShotName(baseName)))
            return;

        _mode = Mode.OneShot;
        _oneShotBase = baseName;
        _oneShotImpact = -1;
        foreach (var anim in CombatAnims)
        {
            if (anim.Name == baseName)
                _oneShotImpact = anim.Impact;
        }
        if (hitDelay <= 0.0f)
            _oneShotImpact = -1;
        _oneShotHitDelay = Mathf.Max(hitDelay, 0.0f);
        _oneShotRecovery = Mathf.Max(recovery, 0.05f);
        _oneShotTimer = _oneShotHitDelay + _oneShotRecovery;
        _oneShotElapsed = 0.0f;
        StepOneShot();
    }

    private string OneShotName(string baseName) => baseName + "_" + (baseName == "hit" ? "s" : _direction);

    // Mira mudou no meio do golpe: troca de linha mantendo o quadro.
    private void StepOneShot()
    {
        var name = OneShotName(_oneShotBase);
        if (!_sprite.SpriteFrames.HasAnimation(name))
            return;
        var count = _sprite.SpriteFrames.GetFrameCount(name);
        int frame;
        if (_oneShotImpact > 0 && _oneShotImpact < count)
        {
            frame = _oneShotElapsed < _oneShotHitDelay
                ? Mathf.FloorToInt(_oneShotElapsed / Mathf.Max(_oneShotHitDelay, 0.001f) * _oneShotImpact)
                : _oneShotImpact + Mathf.FloorToInt((_oneShotElapsed - _oneShotHitDelay) / _oneShotRecovery * (count - _oneShotImpact));
        }
        else
        {
            frame = Mathf.FloorToInt(_oneShotElapsed / Mathf.Max(_oneShotHitDelay + _oneShotRecovery, 0.001f) * count);
        }
        frame = Mathf.Clamp(frame, 0, count - 1);
        if (_sprite.Animation != name)
            _sprite.Animation = name;
        if (_sprite.IsPlaying())
            _sprite.Pause();
        _sprite.SpeedScale = 1.0f;
        if (_sprite.Frame != frame)
            _sprite.Frame = frame;
    }

    private void TickGhosts(float delta)
    {
        if (_ghostTimeLeft <= 0.0f)
            return;
        _ghostTimeLeft -= delta;
        _ghostTimer -= delta;
        if (_ghostTimer > 0.0f || !_sprite.Visible)
            return;
        _ghostTimer = GhostInterval;
        SpawnGhost();
    }

    private void SpawnGhost()
    {
        var texture = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (texture == null || !IsInstanceValid(_player) || !_player.IsInsideTree())
            return;
        var ghost = new Sprite3D
        {
            Name = "DashGhost",
            Texture = texture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Shaded = false,
            DoubleSided = true,
            PixelSize = _sprite.PixelSize,
            Offset = _sprite.Offset,
            Modulate = GhostColor,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };
        _player.GetParent().AddChild(ghost);
        ghost.GlobalPosition = _sprite.GlobalPosition;
        var tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0.0f, GhostLifetime);
        tween.TweenCallback(Callable.From(ghost.QueueFree));
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
        // 0 = de costas para a camera (n), sentido horario: ne, e, se...
        var angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(x, y)), 360.0f);
        var current = System.Array.IndexOf(Directions, _direction);
        // Folga pequena evita trocar de linha a cada quadro na divisa entre duas direcoes.
        if (current >= 0 && Mathf.Abs(Mathf.AngleDifference(Mathf.DegToRad(angle), Mathf.DegToRad(current * 45.0f))) < Mathf.DegToRad(26.0f))
            return;
        _direction = Directions[Mathf.RoundToInt(angle / 45.0f) % Directions.Length];
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
        var sheet = LoadSheet(SheetFile);
        if (sheet == null)
            return null;

        // Dano e morte ainda nao tem quadros: sem a animacao, PlayOneShot mantem o idle/corrida.
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        for (var row = 0; row < Directions.Length; row++)
        {
            var dir = Directions[row];
            AddAnimation(frames, "idle_" + dir, sheet, row, 0, IdleFrames, IdleFps, true);
            AddAnimation(frames, "walk_" + dir, sheet, row, IdleFrames, RunFrames, WalkFps, true);
        }

        // Sem a folha de combate os golpes nao tocam (PlayOneShot mantem o idle/corrida).
        var combat = LoadSheet(CombatSheetFile);
        if (combat == null)
            GD.PushWarning("PlayerSpriteVisual: folha de combate nao encontrada: " + CombatSheetFile);
        for (var row = 0; combat != null && row < Directions.Length; row++)
        {
            var column = 0;
            foreach (var anim in CombatAnims)
            {
                AddAnimation(frames, anim.Name + "_" + Directions[row], combat, row, column, anim.Frames, 12.0f, false, CombatCellWidth, CombatCellHeight);
                column += anim.Frames;
            }
        }
        return frames;
    }

    private static void AddAnimation(SpriteFrames frames, string name, Texture2D sheet, int row, int first, int count, float fps, bool loop,
        int cellWidth = CellWidth, int cellHeight = CellHeight)
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
                Region = new Rect2((first + i) * cellWidth, row * cellHeight, cellWidth, cellHeight)
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

using Godot;

/// <summary>
/// Fruta do Diabo: fruto de sangue coagulado. Coletada por interação (tecla "interact") com o jogador na área, com o
/// prompt "Pegar: Fruta" (InteractPrompt). Soma vida maxima so na run (RunState.AddDevilsFruit) e cura o jogador.
/// Visual: sprite pixel art em billboard. Reutilizavel via DevilsFruit.Spawn (boss, portas de recompensa, loja).
/// </summary>
public partial class DevilsFruit : Area3D
{
    // Emitido ao ser coletada. As recompensas de sala (RunManager) esperam por ele quando a sala paga Fruta.
    [Signal]
    public delegate void CollectedEventHandler();

    private const string IconPath = "res://assets/pixel_art/rewards/devils_fruit.png";
    private const float PickupRadius = 0.9f;
    private const float HoverHeight = 0.9f;
    private const float PromptHeight = 1.9f;

    private Node3D _visual;
    private Label3D _promptLabel;
    private Node3D _playerBody;
    private float _time;
    private bool _collected;

    /// <summary>Textos localizados para ofertas de recompensa, loja e HUD.</summary>
    public static string DisplayName => Localization.Text("reward.devils_fruit.name");
    public static string Description => Localization.TextFormat("reward.devils_fruit.desc", Mathf.RoundToInt(RunState.DevilsFruitMaxHealthAmount));

    public static DevilsFruit Spawn(Node parent, Vector3 position)
    {
        var fruit = new DevilsFruit { Name = "DevilsFruit" };
        parent.AddChild(fruit);
        if (fruit.IsInsideTree())
            fruit.GlobalPosition = position;
        else
            fruit.Position = position;
        return fruit;
    }

    public override void _Ready()
    {
        AddToGroup("devils_fruit");
        Monitoring = true;
        Monitorable = false;

        AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new SphereShape3D { Radius = PickupRadius },
            Position = new Vector3(0.0f, HoverHeight, 0.0f)
        });

        BuildVisual();
        _time = GD.Randf() * Mathf.Tau;
        _promptLabel = InteractPrompt.CreatePrompt(this, PromptText(), PromptHeight);
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    public override void _Process(double delta)
    {
        if (_visual == null)
            return;

        _time += (float)delta;
        _visual.Position = new Vector3(0.0f, HoverHeight + Mathf.Sin(_time * 2.2f) * 0.1f, 0.0f);

        if (!_collected && _playerBody != null && IsInstanceValid(_playerBody) && InteractPrompt.ConsumeInteract(this))
            TryCollect();
    }

    private string PromptText() => Localization.TextFormat("reward.pickup.prompt", Localization.Text("reward.door.fruit.short"));

    private void OnLanguageChanged(string language)
    {
        if (_promptLabel != null)
            InteractPrompt.SetText(_promptLabel, PromptText());
    }

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "Visual", Position = new Vector3(0.0f, HoverHeight, 0.0f) };
        AddChild(_visual);

        // Pixel art 32x32 (AAP-64), de frente para a câmera; mesmo emblema da porta de Fruta.
        var texture = ResourceLoader.Exists(IconPath) ? ResourceLoader.Load<Texture2D>(IconPath) : null;
        if (texture == null)
            GD.PushWarning($"DevilsFruit: ícone ausente ({IconPath}).");
        else
            _visual.AddChild(new Sprite3D
            {
                Name = "Body",
                Texture = texture,
                PixelSize = 0.8f / 32.0f,
                Shaded = false,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
            });

        _visual.AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = new Color(0.9f, 0.08f, 0.1f),
            LightEnergy = 0.8f,
            OmniRange = 3.2f
        });
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_collected || !body.IsInGroup("player"))
            return;

        _playerBody = body;
        UpdatePromptVisibility();
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        if (ReferenceEquals(body, _playerBody))
            _playerBody = null;
        UpdatePromptVisibility();
    }

    private void UpdatePromptVisibility()
    {
        if (_promptLabel != null)
            _promptLabel.Visible = !_collected && _playerBody != null;
    }

    private void TryCollect()
    {
        var health = _playerBody.GetNodeOrNull<Health>("Health");
        if (health != null && health.IsDead)
            return;

        _collected = true;
        UpdatePromptVisibility();
        Collect(_playerBody as PlayerController, health);
    }

    private void Collect(PlayerController player, Health health)
    {
        // Soma o bonus na run e reaplica a vida maxima: o jogador ganha o aumento como vida atual
        // (SetMaxHealthPreservingMissingHealth mantem a vida faltante), o que ja equivale a curar o valor da fruta.
        var maxHealthGain = RunState.AddDevilsFruit();
        player?.ApplyRuntimeTuning();

        // Se o ajuste de cura for maior que o de vida maxima (CompendiumTuning), cura so a diferenca.
        var extraHeal = RunState.DevilsFruitHealAmount - maxHealthGain;
        if (extraHeal > 0.0f)
            health?.Heal(extraHeal);

        GetTree().CallGroup("audio_manager", "PlayEffect", "treasure");
        GetTree().CallGroup("hud", "NotifySkillMessage",
            Localization.TextFormat("reward.devils_fruit.toast", Mathf.RoundToInt(maxHealthGain)));
        EmitSignal(SignalName.Collected);
        QueueFree();
    }
}

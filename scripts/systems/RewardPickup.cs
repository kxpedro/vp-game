using Godot;

// Recompensa de sala no chão (estilo Hades; docs/design/portas-recompensas.md): Bênção de Sangue, pilha de Moedas de Sangue,
// Bigorna ou Sangue Amaldiçoado.
// Ao se aproximar, aparece o prompt "Pegar: <tipo>" (InteractPrompt); ao interagir, emite Claimed uma única vez e some.
// Quem recebe o sinal (RunManager) paga a recompensa e libera as portas. A Fruta do Diabo não passa por aqui:
// é o DevilsFruit, que também coleta por interação.
// Visual provisório: medalhão de DoorRewardIcon (o mesmo da porta), de frente para a câmera, sem giro; só sobe e desce. Rótulo ao lado.
public partial class RewardPickup : Area3D
{
    [Signal]
    public delegate void ClaimedEventHandler();

    private const float InteractRadius = 1.1f;
    private const float HoverHeight = 0.9f;
    private const float PromptHeight = 1.9f;

    /// <summary>Recompensa representada pelo item (a quantidade de Moedas ou de Sangue Amaldiçoado vem de CoinAmount).</summary>
    public RoomRewardDoor Door { get; private set; }

    private Node3D _visual;
    private Label3D _promptLabel;
    private bool _playerInside;
    private bool _claimed;
    private float _time;

    /// <summary>Cria o item no chão, como filho de <paramref name="parent"/>. Não aceita Fruta (use DevilsFruit.Spawn).</summary>
    public static RewardPickup Spawn(Node parent, Vector3 position, RoomRewardDoor door)
    {
        if (parent == null || door == null || door.Type == RoomRewardType.DevilsFruit)
        {
            GD.PushError("RewardPickup.Spawn: recebe Bênção, Moedas, Bigorna ou Sangue Amaldiçoado; a Fruta do Diabo usa DevilsFruit.Spawn.");
            return null;
        }

        var pickup = new RewardPickup { Name = "RewardPickup", Door = door };
        parent.AddChild(pickup);
        if (pickup.IsInsideTree())
            pickup.GlobalPosition = position;
        else
            pickup.Position = position;
        return pickup;
    }

    public override void _Ready()
    {
        AddToGroup("room_reward_pickups");
        Monitoring = true;
        Monitorable = false;

        AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new SphereShape3D { Radius = InteractRadius },
            Position = Vector3.Up * HoverHeight
        });

        _visual = new Node3D { Name = "Visual", Position = Vector3.Up * HoverHeight };
        AddChild(_visual);
        DoorRewardIcon.PopulatePickup(_visual, Door);

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
        if (_claimed || _visual == null)
            return;

        _time += (float)delta;
        _visual.Position = new Vector3(0.0f, HoverHeight + Mathf.Sin(_time * 2.2f) * 0.08f, 0.0f);

        if (_playerInside && InteractPrompt.ConsumeInteract(this))
            Claim();
    }

    private void Claim()
    {
        _claimed = true;
        if (Door.Type is RoomRewardType.Coins or RoomRewardType.CursedBlood)
            GetTree().CallGroup("audio_manager", "PlayEffect", "treasure");
        EmitSignal(SignalName.Claimed);
        QueueFree();
    }

    private string PromptText() => Localization.TextFormat("reward.pickup.prompt", DoorRewardIcon.ShortLabel(Door));

    private void OnLanguageChanged(string language)
    {
        if (_promptLabel != null)
            InteractPrompt.SetText(_promptLabel, PromptText());
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;
        _playerInside = true;
        UpdatePromptVisibility();
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;
        _playerInside = false;
        UpdatePromptVisibility();
    }

    private void UpdatePromptVisibility()
    {
        if (_promptLabel != null)
            _promptLabel.Visible = _playerInside && !_claimed;
    }
}

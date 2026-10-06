using Godot;

public partial class RoomArtifact : Area3D
{
    public enum ArtifactEffect
    {
        Damage,
        MaxHealth
    }

    [Export] public ArtifactEffect Effect { get; set; } = ArtifactEffect.Damage;
    [Export] public float Amount { get; set; } = 0.15f;
    [Export] public string ArtifactName { get; set; } = "Tesouro de Artefatos";

    private bool _isPlayerInside;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        CreatePromptLabel();
    }

    public override void _Process(double delta)
    {
        if (!_isPlayerInside || !InteractPrompt.ConsumeInteract(this))
            return;

        OpenArtifactOffer();
    }

    public void Consume()
    {
        GetTree().CallGroup("audio_manager", "PlayEffect", "treasure");
        QueueFree();
    }

    private void OpenArtifactOffer()
    {
        GetTree().CallGroup("hud", "OpenArtifactOffer", this);
    }

    private void CreatePromptLabel()
    {
        _promptLabel = InteractPrompt.CreatePrompt(this, "Invocar recompensa", 1.85f);
        // O título (AltarTitle) é adicionado pelo RunManager depois do AddChild: estiliza no fim do frame.
        Callable.From(StyleTitle).CallDeferred();
    }

    private void StyleTitle()
    {
        if (IsInstanceValid(this) && IsInsideTree())
            InteractPrompt.StyleTitlesAndAnchor(this, _promptLabel);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = true;
        if (_promptLabel != null)
            _promptLabel.Visible = true;
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = false;
        if (_promptLabel != null)
            _promptLabel.Visible = false;
    }
}

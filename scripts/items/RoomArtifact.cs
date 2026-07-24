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
        if (!_isPlayerInside || !Input.IsActionJustPressed("interact"))
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
        _promptLabel = new Label3D
        {
            Name = "PromptLabel",
            Text = $"E / X (A) - invocar recompensa no {ArtifactName}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34,
            Modulate = new Color(1.0f, 0.08f, 0.04f),
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.4f,
            Visible = false
        };
        AddChild(_promptLabel);
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

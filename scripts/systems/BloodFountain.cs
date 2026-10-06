using Godot;

public partial class BloodFountain : Area3D
{
    [Export] public string PromptText { get; set; } = "Abrir Fonte de Sangue";

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

        GetTree().CallGroup("hud", "OpenBloodFountain");
    }

    private void CreatePromptLabel()
    {
        _promptLabel = InteractPrompt.CreatePrompt(this, PromptText, 1.85f);
        InteractPrompt.StyleSiblingTitlesAndAnchor(this, _promptLabel);
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

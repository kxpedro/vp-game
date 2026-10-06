using Godot;

public partial class ArtifactChest : Area3D
{
    [Export] public string PromptText { get; set; } = "Abrir Baú de Artefatos";

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

        GetTree().CallGroup("hud", "OpenArtifactChest");
    }

    private void CreatePromptLabel()
    {
        _promptLabel = InteractPrompt.CreatePrompt(this, PromptText, 1.55f);
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

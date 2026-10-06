using Godot;

public partial class WeaponBench : Area3D
{
    [Export] public string PromptText { get; set; } = "Escolher arma";
    private bool _isPlayerInside;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        BodyEntered += body => SetInside(body, true);
        BodyExited += body => SetInside(body, false);
        _promptLabel = InteractPrompt.CreatePrompt(this, PromptText, 1.55f);
        InteractPrompt.StyleSiblingTitlesAndAnchor(this, _promptLabel);
    }

    public override void _Process(double delta)
    {
        if (_isPlayerInside && InteractPrompt.ConsumeInteract(this))
            GetTree().CallGroup("hud", "OpenWeaponBench");
    }

    private void SetInside(Node3D body, bool inside)
    {
        if (!body.IsInGroup("player"))
            return;
        _isPlayerInside = inside;
        _promptLabel.Visible = inside;
    }
}

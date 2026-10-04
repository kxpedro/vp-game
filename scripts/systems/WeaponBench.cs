using Godot;

public partial class WeaponBench : Area3D
{
    [Export] public string PromptText { get; set; } = "E - escolher arma";
    private bool _isPlayerInside;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        BodyEntered += body => SetInside(body, true);
        BodyExited += body => SetInside(body, false);
        _promptLabel = new Label3D
        {
            PixelSize = 0.0085f,
            Text = PromptText,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34,
            Modulate = new Color(1, 0.08f, 0.04f),
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.55f,
            Visible = false
        };
        AddChild(_promptLabel);
    }

    public override void _Process(double delta)
    {
        if (_isPlayerInside && Input.IsActionJustPressed("interact"))
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

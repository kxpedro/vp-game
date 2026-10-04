using Godot;

public partial class BloodFountain : Area3D
{
    [Export] public string PromptText { get; set; } = "E - abrir Fonte de Sangue";

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

        GetTree().CallGroup("hud", "OpenBloodFountain");
    }

    private void CreatePromptLabel()
    {
        _promptLabel = new Label3D
        {
            PixelSize = 0.0085f,
            Name = "PromptLabel",
            Text = PromptText,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34,
            Modulate = new Color(1.0f, 0.08f, 0.04f),
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.85f,
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

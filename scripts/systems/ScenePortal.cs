using Godot;

public partial class ScenePortal : Area3D
{
    public enum RunPortalAction
    {
        None,
        StartRun,
        AdvanceRoom,
        CompleteBoss
    }

    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath { get; set; } = "";
    [Export] public string PromptText { get; set; } = "Pressione E";
    [Export] public bool StartsEnabled { get; set; } = true;
    [Export] public RunPortalAction PortalAction { get; set; } = RunPortalAction.None;

    private bool _isPlayerInside;
    private bool _isEnabled;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        _isEnabled = StartsEnabled;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        CreatePromptLabel();
        UpdatePromptVisibility();
    }

    public override void _Process(double delta)
    {
        if (!_isEnabled || !_isPlayerInside)
            return;

        if (Input.IsActionJustPressed("interact") && !string.IsNullOrWhiteSpace(TargetScenePath))
        {
            ApplyPortalAction();
            GetTree().ChangeSceneToFile(TargetScenePath);
        }
    }

    public void ConfigureDestination(string targetScenePath, string promptText, RunPortalAction portalAction)
    {
        TargetScenePath = targetScenePath;
        PromptText = promptText;
        PortalAction = portalAction;

        if (_promptLabel != null)
            _promptLabel.Text = PromptText;
    }

    public void SetPortalEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        UpdatePromptVisibility();
    }

    private void CreatePromptLabel()
    {
        _promptLabel = new Label3D
        {
            Name = "PromptLabel",
            Text = PromptText,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 36,
            Modulate = new Color(1.0f, 0.08f, 0.04f),
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.65f,
            Visible = false
        };
        AddChild(_promptLabel);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = true;
        UpdatePromptVisibility();
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = false;
        UpdatePromptVisibility();
    }

    private void UpdatePromptVisibility()
    {
        if (_promptLabel != null)
            _promptLabel.Visible = _isEnabled && _isPlayerInside;
    }

    private void ApplyPortalAction()
    {
        if (PortalAction == RunPortalAction.StartRun)
            RunState.StartRun();

        if (PortalAction == RunPortalAction.AdvanceRoom)
            RunState.AdvanceRoom();

        if (PortalAction == RunPortalAction.CompleteBoss)
            RunState.CompleteBoss();
    }
}

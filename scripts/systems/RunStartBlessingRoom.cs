using Godot;

public partial class RunStartBlessingRoom : Node3D
{
    private Area3D _fountainArea;
    private Label3D _fountainPrompt;
    private Node3D _exitPortalRoot;
    private ScenePortal _exitPortal;
    private bool _playerAtFountain;
    private bool _offerOpened;
    private bool _blessingClaimed;

    public override void _Ready()
    {
        AddToGroup("run_manager");
        ControllerUiSupport.EnsureMappings();
        _fountainArea = GetNode<Area3D>("BloodFountain/InteractionArea");
        _fountainPrompt = GetNode<Label3D>("BloodFountain/Prompt");
        _exitPortalRoot = GetNode<Node3D>("ExitPortal");
        _exitPortal = GetNode<ScenePortal>("ExitPortal/PortalArea");
        _fountainArea.BodyEntered += OnFountainBodyEntered;
        _fountainArea.BodyExited += OnFountainBodyExited;
        _exitPortal.SetPortalEnabled(false);

        // Reloading this scene must never grant a second starting blessing.
        if (RunState.RunBlessings.Count > 0)
            UnlockExitAfterReward();
    }

    public override void _Process(double delta)
    {
        if (!_playerAtFountain || _offerOpened || !InteractPrompt.ConsumeInteract(this))
            return;

        _offerOpened = true;
        _fountainPrompt.Visible = false;
        if (GetTree().GetFirstNodeInGroup("hud") is HUD hud)
            hud.OpenMandatoryBloodBlessingOffer();
    }

    public void UnlockExitAfterReward()
    {
        if (_blessingClaimed)
            return;
        _blessingClaimed = true;
        _offerOpened = true;
        _fountainPrompt.Visible = false;
        _exitPortal.SetPortalEnabled(true);
        var tween = _exitPortalRoot.CreateTween();
        _exitPortalRoot.Scale = Vector3.One * 0.78f;
        tween.TweenProperty(_exitPortalRoot, "scale", Vector3.One * 1.12f, 0.18f);
        tween.TweenProperty(_exitPortalRoot, "scale", Vector3.One, 0.12f);
    }

    private void OnFountainBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerAtFountain = true;
        _fountainPrompt.Visible = !_offerOpened;
    }

    private void OnFountainBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerAtFountain = false;
        _fountainPrompt.Visible = false;
    }
}

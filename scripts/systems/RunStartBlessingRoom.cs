using Godot;

public partial class RunStartBlessingRoom : Node3D
{
    private const string RoomScenePath = "res://scenes/game/TestArena.tscn";

    private Area3D _fountainArea;
    private Label3D _fountainPrompt;
    private Node3D _exitPortalRoot;
    private ScenePortal _exitPortal;
    private bool _playerAtFountain;
    private bool _offerOpened;
    private bool _blessingClaimed;
    private System.Action<InputDevice> _deviceChangedHandler;

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
        PlaceExitOnNorthWall();
        // Prompt da fonte com o mesmo visual e glifo dos demais interativos (InteractPrompt), no idioma atual.
        InteractPrompt.StylePrompt(_fountainPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("BloodFountain"), _fountainPrompt);
        RefreshFountainPrompt();
        _deviceChangedHandler = _ => RefreshFountainPrompt();
        UiInput.DeviceChanged += _deviceChangedHandler;
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;

        // Reloading this scene must never grant a second starting blessing.
        if (RunState.RunBlessings.Count > 0)
            UnlockExitAfterReward();
    }

    public override void _ExitTree()
    {
        if (_deviceChangedHandler != null)
            UiInput.DeviceChanged -= _deviceChangedHandler;
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language) => RefreshFountainPrompt();

    private void RefreshFountainPrompt()
    {
        if (_fountainPrompt != null && IsInstanceValid(_fountainPrompt))
            InteractPrompt.SetText(_fountainPrompt, Localization.Text("scene.start_room.fountain_prompt"));
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

    // A saída fica na parede norte (NorthWall) virada para dentro (+Z) desde o início, travada até a bênção; as portas
    // de recompensa nascem dela no mesmo lugar. No 2D o PixelSceneSetup já moveu a NorthWall para a borda da sala do
    // kit (o arco do layout fica atrás da porta).
    private void PlaceExitOnNorthWall()
    {
        var wall = GetNode<Node3D>("NorthWall");
        var thickness = (wall.GetNode<CollisionShape3D>("Collision").Shape as BoxShape3D)?.Size.Z ?? 0.45f;
        ScenePortal.PlaceOnWall(_exitPortalRoot, new Vector3(0.0f, 0.18f, wall.Position.Z), new Vector3(0.0f, 0.0f, 1.0f), thickness);
    }

    public void UnlockExitAfterReward()
    {
        if (_blessingClaimed)
            return;
        _blessingClaimed = true;
        _offerOpened = true;
        _fountainPrompt.Visible = false;

        // Entrada do mapa 1 (origem 0): sorteia o conjunto de portas (docs/design/portas-recompensas.md, 7.3).
        // Reabrir a cena não rola de novo: o conjunto fica guardado na run. Aqui é sempre uma porta só.
        var set = RunState.GetOrRollRoomRewardSet(Mathf.Clamp(RunState.CurrentMapIndex, 0, 3), 0, singleDoor: true);
        var doors = set == null
            ? null
            : ScenePortal.BuildRewardDoors(_exitPortalRoot, set, RoomScenePath, ScenePortal.RunPortalAction.None, true);
        if (doors is { Count: > 0 })
        {
            foreach (var door in doors)
                door.PlayOpenPulse();
            return;
        }

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

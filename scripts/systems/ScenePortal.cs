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
    // Texto do prompt ou chave de data/localization/<idioma>/*.json (resolvida na exibição e na troca de idioma).
    // Vazio = "portal.default".
    [Export] public string PromptText { get; set; } = "";
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
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language)
    {
        if (_promptLabel != null)
            InteractPrompt.SetText(_promptLabel, ResolvePromptText());
    }

    private string ResolvePromptText()
    {
        if (string.IsNullOrEmpty(PromptText))
            return Localization.Text("portal.default");
        return Localization.Has(PromptText) ? Localization.Text(PromptText) : PromptText;
    }

    public override void _Process(double delta)
    {
        if (!_isEnabled || !_isPlayerInside)
            return;

        if (InteractPrompt.ConsumeInteract(this) && !string.IsNullOrWhiteSpace(TargetScenePath))
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
            InteractPrompt.SetText(_promptLabel, ResolvePromptText());
    }

    public void SetPortalEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        UpdatePromptVisibility();
    }

    private void CreatePromptLabel()
    {
        // Sem título no portal (salas da run): prompt acima do lintel (~2,9 m). No hub, ancora no título irmão.
        _promptLabel = InteractPrompt.CreatePrompt(this, ResolvePromptText(), 3.2f);
        InteractPrompt.StyleSiblingTitlesAndAnchor(this, _promptLabel);
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

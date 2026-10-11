using Godot;

public partial class BloodFountain : Area3D
{
    // Vazio = texto padrão localizado; também aceita uma chave de data/localization/<idioma>/*.json.
    [Export] public string PromptText { get; set; } = "";

    private bool _isPlayerInside;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        CreatePromptLabel();
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language) => InteractPrompt.SetText(_promptLabel, ResolvePromptText());

    private string ResolvePromptText()
    {
        if (string.IsNullOrEmpty(PromptText))
            return Localization.Text("prompt.blood_fountain");
        return Localization.Has(PromptText) ? Localization.Text(PromptText) : PromptText;
    }

    public override void _Process(double delta)
    {
        if (!_isPlayerInside || !InteractPrompt.ConsumeInteract(this))
            return;

        GetTree().CallGroup("hud", "OpenBloodFountain");
    }

    private void CreatePromptLabel()
    {
        _promptLabel = InteractPrompt.CreatePrompt(this, ResolvePromptText(), 1.85f);
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

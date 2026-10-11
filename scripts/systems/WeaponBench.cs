using Godot;

public partial class WeaponBench : Area3D
{
    // Vazio = texto padrão localizado; também aceita uma chave de data/localization/<idioma>/*.json.
    [Export] public string PromptText { get; set; } = "";
    private bool _isPlayerInside;
    private Label3D _promptLabel;

    public override void _Ready()
    {
        BodyEntered += body => SetInside(body, true);
        BodyExited += body => SetInside(body, false);
        _promptLabel = InteractPrompt.CreatePrompt(this, ResolvePromptText(), 1.55f);
        InteractPrompt.StyleSiblingTitlesAndAnchor(this, _promptLabel);
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
            return Localization.Text("prompt.weapon_bench");
        return Localization.Has(PromptText) ? Localization.Text(PromptText) : PromptText;
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

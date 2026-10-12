using Godot;

// NPC interativo do hub (geometria provisoria). Interage por "interact" ou, nas conversas essenciais
// marcadas com auto_start, por proximidade. Uma conversa por visita ao hub (volta ao recarregar a cena).
public partial class DialogueNpc : Node3D
{
    private const double MarkerProbeSeconds = 0.5;

    [Export] public string SpeakerId { get; set; } = "odalric";
    // Chave de data/localization/<idioma>/dialogue.json; recebe o nome do falante como {0}. Vazio = "dlg.npc.prompt".
    [Export] public string PromptText { get; set; } = "";

    private Label3D _title;
    private Label3D _prompt;
    private Label3D _marker;
    private bool _isPlayerInside;
    private bool _talkedThisVisit;
    private double _probeTimer;

    public override void _Ready()
    {
        BuildVisual();

        _title = new Label3D { Name = "Title", Position = new Vector3(0, 2.6f, 0), Text = SpeakerName() };
        AddChild(_title);
        InteractPrompt.StyleTitle(_title);

        _prompt = InteractPrompt.CreatePrompt(this, ResolvePromptText(), 2.2f);
        InteractPrompt.StyleTitlesAndAnchor(this, _prompt);

        // Marcador "!" criado depois do estilo de titulo (StyleTitlesAndAnchor trata todo Label3D como titulo).
        _marker = new Label3D { Name = "NewStoryMarker", Position = new Vector3(0, 2.15f, 0), Text = "!", Visible = false };
        AddChild(_marker);
        _marker.PixelSize = 0.0025f;
        _marker.FixedSize = true;
        _marker.FontSize = 64;
        _marker.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _marker.Modulate = UiPalette.Gold;
        _marker.OutlineSize = 12;
        _marker.OutlineModulate = UiPalette.TextOutline;
        _marker.NoDepthTest = true;

        var area = new Area3D { Name = "ProximityArea" };
        area.AddChild(new CollisionShape3D { Position = new Vector3(0, 1.0f, 0), Shape = new BoxShape3D { Size = new Vector3(3.2f, 2.0f, 3.2f) } });
        AddChild(area);
        area.BodyEntered += OnBodyEntered;
        area.BodyExited += OnBodyExited;

        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
        RefreshMarker();
    }

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    public override void _Process(double delta)
    {
        _probeTimer -= delta;
        if (_probeTimer <= 0.0)
        {
            _probeTimer = MarkerProbeSeconds;
            RefreshMarker();
        }

        if (!_isPlayerInside || _talkedThisVisit || !InteractPrompt.ConsumeInteract(this))
            return;
        TryTalk();
    }

    private void TryTalk()
    {
        if (_talkedThisVisit || DialogueManager.Instance == null || !DialogueManager.Instance.TryTalkTo(SpeakerId))
            return;
        _talkedThisVisit = true;
        if (IsInstanceValid(_prompt)) _prompt.Visible = false;
        if (IsInstanceValid(_title)) _title.Modulate = UiPalette.Gold.Darkened(0.5f);
        RefreshMarker();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;
        _isPlayerInside = true;
        if (_talkedThisVisit)
            return;
        if (IsInstanceValid(_prompt)) _prompt.Visible = true;
        // Adiado: nao abre modal dentro do callback de fisica.
        if (DialogueManager.Instance?.PeekNpcConversation(SpeakerId)?.AutoStart == true)
            CallDeferred(nameof(TryTalk));
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;
        _isPlayerInside = false;
        if (IsInstanceValid(_prompt)) _prompt.Visible = false;
    }

    private void RefreshMarker()
    {
        if (!IsInstanceValid(_marker)) return;
        _marker.Visible = !_talkedThisVisit && DialogueManager.Instance != null && DialogueManager.Instance.HasPriorityConversation(SpeakerId);
    }

    private void OnLanguageChanged(string language)
    {
        if (IsInstanceValid(_title)) _title.Text = SpeakerName();
        InteractPrompt.SetText(_prompt, ResolvePromptText());
    }

    private string SpeakerName()
        => DialogueCatalog.TryGetSpeaker(SpeakerId, out var speaker) ? speaker.DisplayName : SpeakerId;

    private string ResolvePromptText()
    {
        var key = string.IsNullOrEmpty(PromptText) ? "dlg.npc.prompt" : PromptText;
        return Localization.TextFormat(key, SpeakerName());
    }

    // Geometria provisoria (sem assets): corpo, cabeca, capuz e um corvo no ombro.
    private void BuildVisual()
    {
        var robe = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.15f, 0.3f), Roughness = 0.95f };
        var skin = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.66f, 0.62f), Roughness = 0.9f };
        var hood = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.09f, 0.16f), Roughness = 0.95f };
        var raven = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.07f), Roughness = 0.6f };

        var body = new StaticBody3D { Name = "Body" };
        AddChild(body);
        body.AddChild(new CollisionShape3D { Position = new Vector3(0, 0.85f, 0), Shape = new CapsuleShape3D { Radius = 0.5f, Height = 1.7f } });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 0.75f, 0), Mesh = new CylinderMesh { BottomRadius = 0.45f, TopRadius = 0.25f, Height = 1.5f }, MaterialOverride = robe });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.7f, 0), Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f }, MaterialOverride = skin });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.82f, -0.02f), Mesh = new CylinderMesh { BottomRadius = 0.3f, TopRadius = 0.02f, Height = 0.5f }, MaterialOverride = hood });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0.3f, 1.45f, 0.0f), Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.16f, 0.26f) }, MaterialOverride = raven });
    }
}

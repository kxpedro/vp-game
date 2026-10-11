using Godot;

// Ponto reservado no hub para um futuro personagem desbloqueável (boss derrotado). Só marcador visual:
// sarcófago em geometria provisória, título "???/Bloqueado" e aviso "Em breve" ao chegar perto. Sem interação.
public partial class CharacterPlaceholder : Node3D
{
    private Label3D _title;
    private Label3D _prompt;

    public override void _Ready()
    {
        var stone = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.18f, 0.22f), Roughness = 0.95f };
        var lidMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.27f, 0.24f, 0.3f), Roughness = 0.9f };
        var sealMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.08f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(0.5f, 0.06f, 0.08f),
            EmissionEnergyMultiplier = 0.5f
        };

        var body = new StaticBody3D { Name = "Body" };
        AddChild(body);
        body.AddChild(new CollisionShape3D { Position = new Vector3(0, 0.5f, 0), Shape = new BoxShape3D { Size = new Vector3(1.3f, 1.0f, 2.4f) } });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 0.35f, 0), Mesh = new BoxMesh { Size = new Vector3(1.2f, 0.7f, 2.3f) }, MaterialOverride = stone });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 0.78f, 0), Mesh = new BoxMesh { Size = new Vector3(1.35f, 0.16f, 2.45f) }, MaterialOverride = lidMaterial });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 0.88f, 0), Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.04f, 0.9f) }, MaterialOverride = sealMaterial });
        body.AddChild(new MeshInstance3D { Position = new Vector3(0, 0.88f, 0), Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.04f, 0.18f) }, MaterialOverride = sealMaterial });

        _title = new Label3D { Name = "Title", Position = new Vector3(0, 2.2f, 0), Text = Localization.Text("scene.crows_nest.character_locked") };
        AddChild(_title);
        InteractPrompt.StyleTitle(_title);

        _prompt = InteractPrompt.CreatePrompt(this, Localization.Text("prompt.character_soon"), 2.2f);
        InteractPrompt.StyleTitlesAndAnchor(this, _prompt);

        var area = new Area3D { Name = "ProximityArea" };
        area.AddChild(new CollisionShape3D { Position = new Vector3(0, 0.8f, 0), Shape = new BoxShape3D { Size = new Vector3(3.0f, 1.8f, 3.0f) } });
        AddChild(area);
        area.BodyEntered += body2 => SetNear(body2, true);
        area.BodyExited += body2 => SetNear(body2, false);

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
        if (IsInstanceValid(_title))
            _title.Text = Localization.Text("scene.crows_nest.character_locked");
        InteractPrompt.SetText(_prompt, Localization.Text("prompt.character_soon"));
    }

    private void SetNear(Node3D body, bool near)
    {
        if (body.IsInGroup("player") && IsInstanceValid(_prompt))
            _prompt.Visible = near;
    }
}

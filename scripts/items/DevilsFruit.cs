using Godot;

/// <summary>
/// Fruta do Diabo: fruto de sangue coagulado. Coletada ao encostar (grupo "player"), soma vida maxima so na run
/// (RunState.AddDevilsFruit) e cura o jogador. Visual provisorio em geometria. Reutilizavel via DevilsFruit.Spawn
/// (boss, portas de recompensa, loja).
/// </summary>
public partial class DevilsFruit : Area3D
{
    private const float PickupRadius = 0.9f;
    private const float HoverHeight = 0.9f;

    private Node3D _visual;
    private float _time;
    private bool _collected;

    /// <summary>Textos localizados para ofertas de recompensa, loja e HUD.</summary>
    public static string DisplayName => Localization.Text("reward.devils_fruit.name");
    public static string Description => Localization.TextFormat("reward.devils_fruit.desc", Mathf.RoundToInt(RunState.DevilsFruitMaxHealthAmount));

    public static DevilsFruit Spawn(Node parent, Vector3 position)
    {
        var fruit = new DevilsFruit { Name = "DevilsFruit" };
        parent.AddChild(fruit);
        if (fruit.IsInsideTree())
            fruit.GlobalPosition = position;
        else
            fruit.Position = position;
        return fruit;
    }

    public override void _Ready()
    {
        AddToGroup("devils_fruit");
        Monitoring = true;
        Monitorable = false;

        AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new SphereShape3D { Radius = PickupRadius },
            Position = new Vector3(0.0f, HoverHeight, 0.0f)
        });

        BuildVisual();
        _time = GD.Randf() * Mathf.Tau;
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        if (_visual == null)
            return;

        _time += (float)delta;
        _visual.Position = new Vector3(0.0f, HoverHeight + Mathf.Sin(_time * 2.2f) * 0.1f, 0.0f);
        _visual.RotateY((float)delta * 1.1f);
    }

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "Visual", Position = new Vector3(0.0f, HoverHeight, 0.0f) };
        AddChild(_visual);

        var fleshMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.32f, 0.02f, 0.05f),
            Roughness = 0.25f,
            Metallic = 0.15f,
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.04f, 0.08f),
            EmissionEnergyMultiplier = 1.4f
        };
        _visual.AddChild(new MeshInstance3D
        {
            Name = "Body",
            Mesh = new SphereMesh { Radius = 0.26f, Height = 0.52f, RadialSegments = 24, Rings = 12 },
            MaterialOverride = fleshMaterial,
            Scale = new Vector3(1.0f, 1.18f, 1.0f)
        });

        var stemMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.05f, 0.04f),
            Roughness = 0.8f
        };
        _visual.AddChild(new MeshInstance3D
        {
            Name = "Stem",
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.04f, Height = 0.16f, RadialSegments = 8 },
            MaterialOverride = stemMaterial,
            Position = new Vector3(0.0f, 0.34f, 0.0f),
            RotationDegrees = new Vector3(0.0f, 0.0f, 12.0f)
        });

        _visual.AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = new Color(0.9f, 0.08f, 0.1f),
            LightEnergy = 0.8f,
            OmniRange = 3.2f
        });
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_collected || !body.IsInGroup("player"))
            return;

        var health = body.GetNodeOrNull<Health>("Health");
        if (health != null && health.IsDead)
            return;

        _collected = true;
        Collect(body as PlayerController, health);
    }

    private void Collect(PlayerController player, Health health)
    {
        // Soma o bonus na run e reaplica a vida maxima: o jogador ganha o aumento como vida atual
        // (SetMaxHealthPreservingMissingHealth mantem a vida faltante), o que ja equivale a curar o valor da fruta.
        var maxHealthGain = RunState.AddDevilsFruit();
        player?.ApplyRuntimeTuning();

        // Se o ajuste de cura for maior que o de vida maxima (CompendiumTuning), cura so a diferenca.
        var extraHeal = RunState.DevilsFruitHealAmount - maxHealthGain;
        if (extraHeal > 0.0f)
            health?.Heal(extraHeal);

        GetTree().CallGroup("audio_manager", "PlayEffect", "treasure");
        GetTree().CallGroup("hud", "NotifySkillMessage",
            Localization.TextFormat("reward.devils_fruit.toast", Mathf.RoundToInt(maxHealthGain)));
        QueueFree();
    }
}

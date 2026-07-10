using Godot;

public partial class RunManager : Node3D
{
    [Signal]
    public delegate void BloodCoinsChangedEventHandler(int amount);

    [Signal]
    public delegate void BloodCoinsRewardedEventHandler(int reward, int totalAmount);

    [Signal]
    public delegate void RoomClearedEventHandler();

    [Signal]
    public delegate void EnemyProgressChangedEventHandler(int defeatedEnemies, int totalEnemies);

    [Export] public NodePath ExitPortalPath { get; set; } = new("ExitPortal");
    [Export] public NodePath RoomGeneratorPath { get; set; } = new("RoomGenerator");
    [Export] public float ArtifactSpawnChance { get; set; } = 0.45f;

    public int BloodCoins { get; private set; }
    public int LastBloodCoinsReward { get; private set; }
    public int DefeatedEnemies { get; private set; }
    public int TotalEnemies { get; private set; }
    public bool IsRoomCleared { get; private set; }

    private Node3D _exitPortal;
    private ScenePortal _scenePortal;
    private RoomGenerator _roomGenerator;
    private StandardMaterial3D _lockedPortalMaterial;
    private StandardMaterial3D _openPortalMaterial;
    private float _roomCheckTimer;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        AddToGroup("run_manager");
        _rng.Randomize();
        _exitPortal = GetNodeOrNull<Node3D>(ExitPortalPath);
        _scenePortal = _exitPortal?.GetNodeOrNull<ScenePortal>("PortalArea");
        _roomGenerator = GetNodeOrNull<RoomGenerator>(RoomGeneratorPath);
        BuildPortalMaterials();
        SetPortalOpen(false);
        ConfigureRoom();
        BloodCoins = CharacterSheet.BloodCoins;

        EmitSignal(SignalName.BloodCoinsChanged, BloodCoins);
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "SyncBloodCoins", BloodCoins);
        GetTree().CallGroup("hud", "SyncEnemyProgress", DefeatedEnemies, TotalEnemies);
    }

    public override void _Process(double delta)
    {
        if (IsRoomCleared)
            return;

        _roomCheckTimer -= (float)delta;
        if (_roomCheckTimer > 0.0f)
            return;

        _roomCheckTimer = 0.2f;

        if (GetTree().GetNodeCountInGroup("enemies") == 0)
            CompleteRoom();
    }

    public void AddBloodCoins(int amount)
    {
        LastBloodCoinsReward = amount;
        CharacterSheet.AddBloodCoins(amount);
        BloodCoins = CharacterSheet.BloodCoins;
        EmitSignal(SignalName.BloodCoinsChanged, BloodCoins);
        EmitSignal(SignalName.BloodCoinsRewarded, amount, BloodCoins);
        GetTree().CallGroup("hud", "NotifyBloodCoinsRewarded", amount, BloodCoins);
    }

    public void NotifyEnemyDefeated()
    {
        if (IsRoomCleared)
            return;

        DefeatedEnemies = Mathf.Min(DefeatedEnemies + 1, TotalEnemies);
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "NotifyEnemyDefeated", DefeatedEnemies, TotalEnemies, LastBloodCoinsReward);

        if (TotalEnemies > 0 && DefeatedEnemies >= TotalEnemies)
            CompleteRoom();
    }

    private void CompleteRoom()
    {
        IsRoomCleared = true;

        if (_exitPortal != null)
        {
            ConfigureExitPortal();
            SetPortalOpen(true);
            var tween = _exitPortal.CreateTween();
            _exitPortal.Scale = Vector3.One * 0.78f;
            tween.TweenProperty(_exitPortal, "scale", Vector3.One * 1.12f, 0.18f);
            tween.TweenProperty(_exitPortal, "scale", Vector3.One, 0.12f);
        }

        EmitSignal(SignalName.RoomCleared);
        GetTree().CallGroup("hud", "NotifyRoomCleared");
    }

    private void ConfigureRoom()
    {
        if (RunState.IsBossRoom)
            ConfigureBossRoom();
        else
            ConfigureCommonRoom();
    }

    private void ConfigureCommonRoom()
    {
        var difficultyBonus = Mathf.Max(RunState.CurrentRoom - 1, 0);
        var enemies = GetTree().GetNodesInGroup("enemies");
        TotalEnemies = enemies.Count;
        DefeatedEnemies = 0;

        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] is not EnemyBasic enemy)
                continue;

            enemy.MoveSpeed = CompendiumTuning.GetFloat("enemy.basic.move_speed", enemy.MoveSpeed) + difficultyBonus * 0.2f;
            enemy.AttackDamage = CompendiumTuning.GetFloat("enemy.basic.damage", enemy.AttackDamage) + difficultyBonus * 2.0f;
            enemy.MinBloodCoinsReward = CompendiumTuning.GetInt("enemy.basic.min_blood_coins", enemy.MinBloodCoinsReward);
            enemy.MaxBloodCoinsReward = CompendiumTuning.GetInt("enemy.basic.max_blood_coins", enemy.MaxBloodCoinsReward);
            enemy.FuryOnDeath = CompendiumTuning.GetFloat("enemy.basic.fury_on_death", enemy.FuryOnDeath);

            var health = enemy.GetNodeOrNull<Health>("Health");
            health?.SetMaxHealth(CompendiumTuning.GetFloat("enemy.basic.health", 80.0f) + difficultyBonus * 18.0f, true);
        }

        SpawnArtifact();

        CreateRoomLabel($"Sala {RunState.CurrentRoom}/{RunState.RoomsBeforeBoss - 1}");
    }

    private void ConfigureBossRoom()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        TotalEnemies = enemies.Count > 0 ? 1 : 0;
        DefeatedEnemies = 0;

        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] is not EnemyBasic enemy)
                continue;

            if (i == 0)
            {
                enemy.Name = "BloodMageBoss";
                enemy.Scale = Vector3.One * 1.55f;
                enemy.MoveSpeed = CompendiumTuning.GetFloat("enemy.blood_mage.move_speed", 2.7f);
                enemy.AttackDamage = CompendiumTuning.GetFloat("enemy.blood_mage.damage", 30.0f);
                enemy.AttackRange = CompendiumTuning.GetFloat("enemy.blood_mage.attack_range", 2.25f);
                enemy.MinBloodCoinsReward = CompendiumTuning.GetInt("enemy.blood_mage.min_blood_coins", 8);
                enemy.MaxBloodCoinsReward = CompendiumTuning.GetInt("enemy.blood_mage.max_blood_coins", 10);

                var health = enemy.GetNodeOrNull<Health>("Health");
                health?.SetMaxHealth(CompendiumTuning.GetFloat("enemy.blood_mage.health", 260.0f), true);
            }
            else
            {
                enemy.QueueFree();
            }
        }

        CreateRoomLabel("Sala do Chefe - Mago de Sangue");
    }

    private void ConfigureExitPortal()
    {
        if (_scenePortal == null)
            return;

        if (RunState.IsBossRoom)
        {
            _scenePortal.ConfigureDestination(
                "res://scenes/game/CrowsNest.tscn",
                "E - retornar ao Ninho",
                ScenePortal.RunPortalAction.CompleteBoss
            );
            return;
        }

        _scenePortal.ConfigureDestination(
            "res://scenes/game/TestArena.tscn",
            "E - avancar para proxima sala",
            ScenePortal.RunPortalAction.AdvanceRoom
        );
    }

    private void SpawnArtifact()
    {
        var artifact = new RoomArtifact
        {
            Name = "RoomArtifact",
            Position = _roomGenerator?.GetArtifactSpawnPosition() ?? new Vector3(_rng.RandfRange(-5.0f, 5.0f), 0.35f, _rng.RandfRange(-5.0f, 5.0f)),
            ArtifactName = "Tesouro de Artefatos"
        };
        AddChild(artifact);

        var visual = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.85f, 0.02f, 0.04f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.0f, 0.0f),
                EmissionEnergyMultiplier = 1.8f
            }
        };
        artifact.AddChild(visual);

        var collision = new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 1.1f },
            Position = Vector3.Up * 0.3f
        };
        artifact.AddChild(collision);
    }

    private void CreateRoomLabel(string text)
    {
        var label = new Label3D
        {
            Name = "RoomTitle",
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            Modulate = new Color(1.0f, 0.08f, 0.04f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = new Vector3(0.0f, 2.2f, -7.7f)
        };
        AddChild(label);
    }

    private void BuildPortalMaterials()
    {
        _lockedPortalMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.18f, 0.2f, 0.42f),
            EmissionEnabled = true,
            Emission = new Color(0.12f, 0.12f, 0.14f),
            EmissionEnergyMultiplier = 0.35f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _openPortalMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.02f, 0.03f, 0.78f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 2.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    private void SetPortalOpen(bool isOpen)
    {
        if (_exitPortal == null)
            return;

        _exitPortal.Visible = true;
        _scenePortal?.SetPortalEnabled(isOpen);
        var material = isOpen ? _openPortalMaterial : _lockedPortalMaterial;

        foreach (var child in _exitPortal.GetChildren())
        {
            if (child is MeshInstance3D mesh)
                mesh.MaterialOverride = material;
        }
    }
}

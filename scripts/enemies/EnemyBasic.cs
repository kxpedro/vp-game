using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class EnemyBasic : CharacterBody3D
{
    private static readonly Dictionary<string, Texture2D> StatusTextures = new();
    public EnemyRarity Rarity { get; private set; }
    public EnemyArchetype Archetype { get; private set; }
    [Export] public float MoveSpeed { get; set; } = 3.2f;
    [Export] public float DetectionRange { get; set; } = 22.0f;
    [Export] public bool RequireLineOfSight { get; set; } = true;
    [Export] public float LineOfSightEyeHeight { get; set; } = 1.05f;
    [Export] public float PatrolSpeed { get; set; } = 1.65f;
    [Export] public float PatrolRadius { get; set; } = 12.0f;
    [Export] public float PatrolRetargetMinTime { get; set; } = 1.2f;
    [Export] public float PatrolRetargetMaxTime { get; set; } = 3.0f;
    [Export] public float AttackRange { get; set; } = 1.65f;
    [Export] public float AttackDamage { get; set; } = 22.0f;
    [Export] public float AttackWindup { get; set; } = 0.85f;
    [Export] public float AttackCooldown { get; set; } = 1.55f;
    [Export] public float SpawnAttackDelay { get; set; } = 2.0f;
    [Export] public float RangedPreferredDistance { get; set; } = 6.25f;
    [Export] public float RangedRetreatDistance { get; set; } = 4.6f;
    [Export] public float TacticalMoveSpeedMultiplier { get; set; } = 0.82f;
    [Export] public float AttackWidth { get; set; } = 1.25f;
    [Export] public float AttackDepthBonus { get; set; } = 0.75f;
    [Export] public float ContactDamage { get; set; } = 0.05f;
    [Export] public float ContactDamageRange { get; set; } = 0.95f;
    [Export] public float ContactDamageCooldown { get; set; } = 0.8f;
    [Export] public float KnockbackResistance { get; set; } = 0.45f;
    [Export] public float FuryOnDeath { get; set; } = 12.0f;
    [Export] public int MaxPestilenceStacks { get; set; } = 5;
    [Export] public float DamageTakenPerStatusStack { get; set; } = 0.05f;
    [Export] public NodePath HealthPath { get; set; } = new("Health");
    [Export] public NodePath VisualPath { get; set; } = new("Visual");

    private Node3D _player;
    private Health _health;
    private MeshInstance3D _visual;
    private MeshInstance3D _archetypeGlow;
    private bool _isAttacking;
    private bool _hasDetectedPlayer;
    private Vector3 _lockedAttackDirection = Vector3.Forward;
    private Vector3 _knockbackVelocity;
    private Vector3 _spawnPosition;
    private Vector3 _patrolTarget;
    private float _patrolRetargetTimer;
    private StandardMaterial3D _baseMaterial;
    private StandardMaterial3D _hitMaterial;
    // Telegraph de ataque (melee/tank/ranged): contorno fixo no tamanho final + preenchimento que cresce da base.
    private StandardMaterial3D _telegraphFillMaterial;
    private StandardMaterial3D _telegraphBorderMaterial;
    private StandardMaterial3D _windupFlashMaterial;
    private Node3D _telegraphFillPivot;
    private Tween _telegraphTween;
    private Tween _windupBodyTween;
    private float _aimTrackRemaining;
    private Vector3 _windupBodyBaseScale = Vector3.One;
    private float _windupBodyBasePositionY;
    private static float AimLockSeconds => CompendiumTuning.GetFloat("enemy.ranged.aim_lock_seconds", 0.25f);
    private const float WindupFlashSeconds = 0.1f;
    private static float ProjectileRangeMultiplier => CompendiumTuning.GetFloat("enemy.projectile.range_multiplier", 1.5f);
    // Atalho para leituras do painel de dev (CompendiumTuning); sem override devolve o padrao.
    private static float G(string key, float defaultValue) => CompendiumTuning.GetFloat(key, defaultValue);
    private readonly List<(int Id, Vector3 Center, Vector3 End, float Radius, bool IsSegment)> _bossThreats = new();
    private int _bossThreatCounter;
    private MeshInstance3D _healthBarFill;
    private MeshInstance3D _rarityIcon;
    private float _healthBarWidth = 1.55f;
    private float _overheadHeight = 2.12f;
    private bool _overheadHealthEnabled = true;
    private bool _overheadHealthRevealed;
    private Sprite3D _bleedingStatusIcon;
    private Sprite3D _pestilenceStatusIcon;
    private Sprite3D _charmedStatusIcon;
    private Label3D _bleedingStackLabel;
    private Label3D _pestilenceStackLabel;
    private Label3D _attackWarningLabel;
    private Node3D _attackTelegraph;
    private float _contactDamageTimer;
    private float _alternatePathTimer;
    private int _avoidanceSide = 1;
    private float _slowTimer;
    private float _slowMultiplier = 1.0f;
    private float _fearTimer;
    private float _damageTakenBonus;
    private float _damageTakenTimer;
    public float DamageTakenMultiplier => 1.0f + _damageTakenBonus
        + (_pestilenceStacks + _bleedingStacks) * DamageTakenPerStatusStack;
    public float HitTargetRadius { get; private set; } = 0.35f;
    private Node3D _importedVisual;
    private Vector3 _importedVisualBaseScale = Vector3.One;
    private readonly Dictionary<MeshInstance3D, Material> _importedBaseMaterials = new();
    private Tween _importedHitTween;
    private AnimationPlayer _importedAnimationPlayer;
    private string _importedIdleAnimation = "";
    private string _importedWalkAnimation = "";
    private string _importedAttackAnimation = "";
    private string _importedHitAnimation = "";
    private int _hitFeedbackVersion;
    private float _hitStunTimer;
    private float _pendingWallImpactDamage;
    private int _attackSequenceVersion;
    private float _attackCooldownRemaining;
    private float _spawnAttackDelayRemaining;
    private float _tacticalMoveTimer;
    private float _rangedFleeTimer;
    private int _tacticalSide = 1;
    private bool _bossActionLocked;
    private bool _bossMovesEnabled;
    private int _pestilenceStacks;
    private float _pestilenceTimer;
    private float _pestilenceTickTimer;
    private int _bleedingStacks;
    private float _bleedingTimer;
    private float _bleedingTickTimer;
    private float _bleedingDamagePerSecond;
    private float _charmTimer;
    private float _charmAttackTimer;
    private bool _permanentCharm;
    private bool _fascinated;
    private EnemyBasic _charmLinkedEnemy;
    private MeshInstance3D _charmLinkVisual;
    private bool _pestilenceCloudTriggered;
    // Fontes de dano internas (comparadas por igualdade); o texto exibido vem de world.json.
    public const string BackstabDamageSource = "backstab";
    public const string WallImpactDamageSource = "wall_impact";
    private static readonly string[] LegendaryNameIds = { "vharos", "morcant", "seraph", "nerezza", "vorigan" };
    // Guarda o id (nao o texto) para o nome acompanhar a troca de idioma.
    private string _legendaryNameId = "";
    private float _bossActionAnimationTimer;
    private float _attackAnimationTimer;
    public bool IsPestilent => _pestilenceStacks > 0;
    public bool IsBleeding => _bleedingStacks > 0;
    public int PestilenceStacks => _pestilenceStacks;
    public int BleedingStacks => _bleedingStacks;
    public bool IsFascinated => _fascinated;
    public int NegativeStatusCount => (IsPestilent ? 1 : 0) + (IsBleeding ? 1 : 0)
        + (IsCharmed || IsFascinated ? 1 : 0) + (_fearTimer > 0.0f ? 1 : 0);
    public bool IsPestilenceHost => _pestilenceStacks >= Mathf.RoundToInt(RunState.GetEffectParameter("DamageVsPlagueHost", "requiredStacks", 5));
    public bool IsCharmed => _permanentCharm || _charmTimer > 0.0f;
    public bool HasStaggerArmor => Name.ToString().Contains("Boss")
        || (_health?.ShieldHits ?? 0) > 0
        || (Rarity is EnemyRarity.Rare or EnemyRarity.Legendary && Archetype == EnemyArchetype.Tank);
    public bool CanAttackAfterSpawn => _spawnAttackDelayRemaining <= 0.0f;

    // Valores base por instancia (enemy.base.*): aplicados uma vez no _Ready; sem override mantem o valor atual (cena/Export).
    private void ApplyBaseDevTuning()
    {
        DetectionRange = G("enemy.base.detection_range", DetectionRange);
        PatrolSpeed = G("enemy.base.patrol_speed", PatrolSpeed);
        PatrolRadius = G("enemy.base.patrol_radius", PatrolRadius);
        AttackRange = G("enemy.base.attack_range", AttackRange);
        AttackWindup = G("enemy.base.attack_windup", AttackWindup);
        AttackCooldown = G("enemy.base.attack_cooldown", AttackCooldown);
        SpawnAttackDelay = G("enemy.base.spawn_attack_delay", SpawnAttackDelay);
        RangedPreferredDistance = G("enemy.base.ranged_preferred_distance", RangedPreferredDistance);
        RangedRetreatDistance = G("enemy.base.ranged_retreat_distance", RangedRetreatDistance);
        TacticalMoveSpeedMultiplier = G("enemy.base.tactical_speed_multiplier", TacticalMoveSpeedMultiplier);
        AttackWidth = G("enemy.base.attack_width", AttackWidth);
        AttackDepthBonus = G("enemy.base.attack_depth_bonus", AttackDepthBonus);
        ContactDamage = G("enemy.base.contact_damage", ContactDamage);
        ContactDamageRange = G("enemy.base.contact_range", ContactDamageRange);
        ContactDamageCooldown = G("enemy.base.contact_cooldown", ContactDamageCooldown);
        KnockbackResistance = G("enemy.base.knockback_resistance", KnockbackResistance);
        MaxPestilenceStacks = CompendiumTuning.GetInt("enemy.base.max_pestilence_stacks", MaxPestilenceStacks);
        DamageTakenPerStatusStack = G("enemy.base.damage_per_status_stack", DamageTakenPerStatusStack);
    }

    public override void _Ready()
    {
        // O TrainingDummy zera estes campos antes de chamar base._Ready: nao sobrescrever.
        if (this is not TrainingDummy)
            ApplyBaseDevTuning();
        AddToGroup("enemies");
        _spawnPosition = GlobalPosition;
        _spawnAttackDelayRemaining = SpawnAttackDelay;
        _health = GetNode<Health>(HealthPath);
        _visual = GetNodeOrNull<MeshInstance3D>(VisualPath);
        _baseMaterial = _visual?.MaterialOverride as StandardMaterial3D;
        _hitMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.02f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.8f
        };
        _windupFlashMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.95f, 0.8f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.85f, 0.55f),
            EmissionEnergyMultiplier = 2.6f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _health.Damaged += OnDamaged;
        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnDied;
        _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        CreateHealthBar();
        CreateStatusIcons();
        CreateAttackTelegraph();
        PickNewPatrolTarget();
        UpdateHealthBar(_health.CurrentHealth, _health.MaxHealth);
        SetHealthBarMeshesVisible(false);
    }

    public void ConfigureVariant(EnemyRarity rarity, EnemyArchetype archetype)
    {
        Rarity = rarity;
        Archetype = archetype;
        var tuning = EnemyTuningCatalog.Get(rarity);
        MoveSpeed *= tuning.Speed;
        AttackDamage *= tuning.Damage;
        _health?.SetMaxHealth(_health.MaxHealth * tuning.Health, true);
        _health.ShieldHits = tuning.FirstHitShield ? 1 : 0;
        if (rarity == EnemyRarity.Legendary && !Name.ToString().Contains("Boss"))
        {
            _legendaryNameId = LegendaryNameIds[Mathf.Abs(Name.ToString().GetHashCode()) % LegendaryNameIds.Length];
        }

        if (archetype == EnemyArchetype.Ranged)
        {
            AttackRange = G("enemy.ranged.attack_range", 11.0f); AttackDamage *= G("enemy.ranged.damage_multiplier", 0.8f); MoveSpeed *= G("enemy.ranged.speed_multiplier", 0.9f);
        }
        else if (archetype == EnemyArchetype.Tank)
        {
            _health?.SetMaxHealth(_health.MaxHealth * G("enemy.tank.health_multiplier", 1.8f), true);
            MoveSpeed *= G("enemy.tank.speed_multiplier", 0.62f);
            AttackDamage *= G("enemy.tank.damage_multiplier", 1.52f);
            AttackWindup *= G("enemy.tank.windup_multiplier", 1.65f);
            AttackRange *= G("enemy.tank.range_multiplier", 1.18f);
            AttackWidth *= G("enemy.tank.width_multiplier", 1.65f);
            AttackDepthBonus += G("enemy.tank.depth_bonus_add", 0.85f);
            AttackCooldown *= G("enemy.tank.cooldown_multiplier", 1.18f);
            KnockbackResistance *= G("enemy.tank.knockback_multiplier", 0.3f);
        }

        ConfigureGeometricVisual(archetype);
        UpdateAttackTelegraphGeometry();

        UpdateRarityVisual(tuning);
        UpdateHealthBar(_health.CurrentHealth, _health.MaxHealth);
        if (_visual != null)
        {
            var archetypeColor = archetype switch
            {
                EnemyArchetype.Ranged => new Color(1.0f, 0.02f, 0.48f),
                EnemyArchetype.Tank => new Color(1.0f, 0.78f, 0.04f),
                _ => new Color(0.0f, 0.86f, 0.96f)
            };
            _baseMaterial = new StandardMaterial3D
            {
                AlbedoColor = archetypeColor.Darkened(0.12f),
                EmissionEnabled = true,
                Emission = archetypeColor,
                EmissionEnergyMultiplier = 0.55f,
                Roughness = 0.72f
            };
            _visual.MaterialOverride = _baseMaterial;
            ConfigureArchetypeGlow(archetypeColor);
        }
    }

    private void ConfigureArchetypeGlow(Color color)
    {
        if (Name.ToString().Contains("Boss") || Name.ToString().Contains("TrainingDummy"))
            return;

        _archetypeGlow?.QueueFree();
        _archetypeGlow = new MeshInstance3D
        {
            Name = "ArchetypeGlow",
            Position = new Vector3(0.0f, 0.04f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = Archetype == EnemyArchetype.Tank ? 0.72f : 0.58f,
                OuterRadius = Archetype == EnemyArchetype.Tank ? 0.91f : 0.75f,
                Rings = 32,
                RingSegments = 12
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color.R, color.G, color.B, 0.72f),
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = 2.2f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
        AddChild(_archetypeGlow);

        var pulse = CreateTween().SetLoops();
        pulse.TweenProperty(_archetypeGlow, "scale", Vector3.One * 1.06f, 0.65f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(_archetypeGlow, "scale", Vector3.One * 0.96f, 0.65f).SetTrans(Tween.TransitionType.Sine);
    }

    private void ConfigureGeometricVisual(EnemyArchetype archetype)
    {
        if (_visual == null) return;
        _visual.Position = archetype == EnemyArchetype.Tank ? new Vector3(0.0f, 0.72f, 0.0f) : new Vector3(0.0f, 0.68f, 0.0f);
        _visual.Scale = Vector3.One;
        _visual.Mesh = archetype switch
        {
            EnemyArchetype.Ranged => new PrismMesh { Size = new Vector3(0.95f, 1.35f, 0.9f) },
            EnemyArchetype.Tank => new BoxMesh { Size = new Vector3(1.15f, 1.45f, 1.15f) },
            _ => new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 1.35f, RadialSegments = 24 }
        };
        HitTargetRadius = archetype == EnemyArchetype.Tank ? G("enemy.tank.hit_radius", 0.68f) : archetype == EnemyArchetype.Ranged ? G("enemy.ranged.hit_radius", 0.48f) : G("enemy.melee.hit_radius", 0.48f);
        if (GetNodeOrNull<Node3D>("Eye") is { } eye) eye.Visible = false;
    }

    private void UpdateAttackTelegraphGeometry()
    {
        if (_attackWarningLabel != null)
            _attackWarningLabel.Text = Archetype == EnemyArchetype.Tank ? "!!" : "!";
        if (_attackTelegraph == null) return;
        foreach (var child in _attackTelegraph.GetChildren())
            child.QueueFree();

        var ranged = Archetype == EnemyArchetype.Ranged;
        var tank = Archetype == EnemyArchetype.Tank;
        // Alinhado ao teste de acerto (IsPlayerInsideLockedAttack): da origem do inimigo ate AttackRange + AttackDepthBonus.
        var depth = ranged ? AttackRange * ProjectileRangeMultiplier : AttackRange + AttackDepthBonus;
        var width = ranged ? 0.38f : AttackWidth;
        var fillColor = ranged ? new Color(0.62f, 0.12f, 1.0f) : tank ? new Color(1.0f, 0.22f, 0.02f) : new Color(1.0f, 0.18f, 0.02f);
        var borderColor = ranged ? new Color(0.82f, 0.62f, 1.0f) : tank ? new Color(1.0f, 0.84f, 0.12f) : new Color(1.0f, 0.55f, 0.12f);
        _telegraphFillMaterial = CreateTelegraphMaterial(fillColor, 0.55f, 2.4f, 6);
        _telegraphBorderMaterial = CreateTelegraphMaterial(borderColor, 0.95f, 3.4f, 7);
        var frameMaterial = CreateTelegraphMaterial(fillColor, 0.17f, 1.2f, 5);

        _attackTelegraph.AddChild(new MeshInstance3D
        {
            Name = "TelegraphFrame",
            Mesh = new BoxMesh { Size = new Vector3(width, 0.02f, depth) },
            MaterialOverride = frameMaterial,
            Position = new Vector3(0.0f, 0.04f, -depth * 0.5f)
        });

        _telegraphFillPivot = new Node3D { Name = "TelegraphFillPivot", Scale = new Vector3(1.0f, 1.0f, 0.001f) };
        _attackTelegraph.AddChild(_telegraphFillPivot);
        _telegraphFillPivot.AddChild(new MeshInstance3D
        {
            Name = "TelegraphFill",
            Mesh = new BoxMesh { Size = new Vector3(width, 0.04f, 1.0f) },
            MaterialOverride = _telegraphFillMaterial,
            Position = new Vector3(0.0f, 0.06f, -0.5f)
        });

        if (ranged)
        {
            // Marcador de fim da linha de tiro.
            _attackTelegraph.AddChild(new MeshInstance3D
            {
                Name = "TelegraphEndMarker",
                Mesh = new TorusMesh { InnerRadius = 0.3f, OuterRadius = 0.44f, Rings = 24, RingSegments = 8 },
                MaterialOverride = _telegraphBorderMaterial,
                Position = new Vector3(0.0f, 0.07f, -depth)
            });
            return;
        }

        var border = tank ? 0.12f : 0.07f;
        void AddStrip(string stripName, Vector3 size, Vector3 position) => _attackTelegraph.AddChild(new MeshInstance3D
        {
            Name = stripName,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _telegraphBorderMaterial,
            Position = position
        });
        AddStrip("BorderLeft", new Vector3(border, 0.05f, depth), new Vector3(-width * 0.5f + border * 0.5f, 0.08f, -depth * 0.5f));
        AddStrip("BorderRight", new Vector3(border, 0.05f, depth), new Vector3(width * 0.5f - border * 0.5f, 0.08f, -depth * 0.5f));
        AddStrip("BorderFar", new Vector3(width, 0.05f, border), new Vector3(0.0f, 0.08f, -depth + border * 0.5f));
        AddStrip("BorderNear", new Vector3(width, 0.05f, border), new Vector3(0.0f, 0.08f, -border * 0.5f));
    }

    private static StandardMaterial3D CreateTelegraphMaterial(Color color, float alpha, float energy, int renderPriority) => new()
    {
        AlbedoColor = new Color(color.R, color.G, color.B, alpha),
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = energy,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = renderPriority
    };

    public void ApplyPlaceholderVisual(EnemyVisualDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.ScenePath)) return;
        var scene = EnemyVisualCatalog.LoadScene(definition);
        if (scene == null)
        {
            GD.PushWarning($"Placeholder 3D ainda nao importado: {definition.ScenePath}");
            return;
        }

        _importedVisual?.QueueFree();
        _importedVisual = scene.Instantiate<Node3D>();
        _importedVisual.Name = "ImportedVisual";
        _importedVisual.Scale = Vector3.One * definition.Scale;
        _importedVisual.RotationDegrees = new Vector3(0.0f, definition.FacingYawDegrees, 0.0f);
        AddChild(_importedVisual);
        if (definition.MatchPlayerHeight)
            MatchImportedVisualToPlayerHeight(definition.PlayerHeightMultiplier);
        HitTargetRadius = definition.CombatRadius;
        if (_visual != null) _visual.Visible = false;
        if (GetNodeOrNull<Node3D>("Eye") is { } eye) eye.Visible = false;
        _importedIdleAnimation = definition.IdleAnimation;
        _importedWalkAnimation = definition.WalkAnimation;
        _importedAttackAnimation = definition.AttackAnimation;
        _importedHitAnimation = definition.HitAnimation;
        _importedAnimationPlayer = FindImportedAnimationPlayer(_importedVisual);
        if (_importedAnimationPlayer != null)
        {
            StripImportedRootMotionTracks(_importedAnimationPlayer);
            _importedAnimationPlayer.AnimationFinished += OnImportedAnimationFinished;
            PlayImportedAnimation(_importedVisual, _importedIdleAnimation);
        }
        CaptureImportedVisualBaseline();
        UpdateOverheadHeight();
    }

    private void CaptureImportedVisualBaseline()
    {
        _importedBaseMaterials.Clear();
        if (_importedVisual == null) return;
        _importedVisualBaseScale = _importedVisual.Scale;
        foreach (var node in _importedVisual.FindChildren("*", "MeshInstance3D", true, false))
            if (node is MeshInstance3D mesh)
                _importedBaseMaterials[mesh] = mesh.MaterialOverride;
    }

    public void PlayBossMoveAnimation(string moveId, int phase)
    {
        if (_importedAnimationPlayer == null || !Name.ToString().Contains("Boss")) return;
        if (!_importedAnimationPlayer.HasAnimation(_importedAttackAnimation)) return;
        _importedAnimationPlayer.Play(_importedAttackAnimation, 0.08, 1.25f + (phase - 1) * 0.12f);
        _bossActionAnimationTimer = 0.7f;
    }

    private void OnImportedAnimationFinished(StringName animation)
    {
        if (_health?.IsDead == true || _importedAnimationPlayer == null || string.IsNullOrWhiteSpace(_importedIdleAnimation)) return;
        if (_importedAnimationPlayer.HasAnimation(_importedIdleAnimation)) _importedAnimationPlayer.Play(_importedIdleAnimation, 0.12, 1.12f);
    }

    private static AnimationPlayer FindImportedAnimationPlayer(Node root)
    {
        if (root is AnimationPlayer animationPlayer) return animationPlayer;
        foreach (var child in root.GetChildren())
            if (FindImportedAnimationPlayer(child) is { } found) return found;
        return null;
    }

    private static void StripImportedRootMotionTracks(AnimationPlayer player)
    {
        foreach (var animationName in player.GetAnimationList())
        {
            if (!player.HasAnimation(animationName))
                continue;

            var animation = player.GetAnimation(animationName);
            for (var trackIndex = animation.GetTrackCount() - 1; trackIndex >= 0; trackIndex--)
            {
                if (animation.TrackGetType(trackIndex) != Animation.TrackType.Position3D)
                    continue;

                var path = animation.TrackGetPath(trackIndex).ToString();
                var separator = path.LastIndexOf(':');
                var trackName = separator >= 0 ? path[(separator + 1)..] : path;
                if (trackName.Equals("root", System.StringComparison.OrdinalIgnoreCase)
                    || trackName.Equals("rootnode", System.StringComparison.OrdinalIgnoreCase)
                    || trackName.Equals("bone", System.StringComparison.OrdinalIgnoreCase))
                    animation.RemoveTrack(trackIndex);
            }
        }
    }

    private void MatchImportedVisualToPlayerHeight(float heightMultiplier)
    {
        if (_importedVisual == null)
            return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var playerVisual = player?.GetNodeOrNull<Node3D>("CharacterModel");
        if (!TryGetVisualBounds(_importedVisual, out var bossMin, out var bossMax))
            return;

        var targetHeight = 1.75f;
        if (playerVisual != null && TryGetVisualBounds(playerVisual, out var playerMin, out var playerMax))
            targetHeight = Mathf.Max(playerMax.Y - playerMin.Y, 0.1f);

        var currentHeight = bossMax.Y - bossMin.Y;
        if (currentHeight <= 0.001f)
            return;

        var factor = targetHeight * Mathf.Max(heightMultiplier, 0.1f) / currentHeight;
        _importedVisual.Scale *= factor;

        if (TryGetVisualBounds(_importedVisual, out var normalizedMin, out _))
            _importedVisual.GlobalPosition += Vector3.Up * (GlobalPosition.Y - normalizedMin.Y);
    }

    private static bool TryGetVisualBounds(Node root, out Vector3 minimum, out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var foundMesh = false;

        foreach (var candidate in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (candidate is not MeshInstance3D mesh || mesh.Mesh == null)
                continue;

            var bounds = mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var localPoint = bounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0.0f : bounds.Size.X,
                    (corner & 2) == 0 ? 0.0f : bounds.Size.Y,
                    (corner & 4) == 0 ? 0.0f : bounds.Size.Z);
                var worldPoint = mesh.GlobalTransform * localPoint;
                minimum = new Vector3(Mathf.Min(minimum.X, worldPoint.X), Mathf.Min(minimum.Y, worldPoint.Y), Mathf.Min(minimum.Z, worldPoint.Z));
                maximum = new Vector3(Mathf.Max(maximum.X, worldPoint.X), Mathf.Max(maximum.Y, worldPoint.Y), Mathf.Max(maximum.Z, worldPoint.Z));
                foundMesh = true;
            }
        }

        return foundMesh;
    }

    public void SetSpawnActive(bool active)
    {
        SetPhysicsProcess(active);
        ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    private static void PlayImportedAnimation(Node node, string preferredAnimation)
    {
        if (node is AnimationPlayer player)
        {
            if (player.HasAnimation(preferredAnimation)) player.Play(preferredAnimation);
            else
            {
                foreach (var animation in player.GetAnimationList())
                {
                    if (animation == "RESET") continue;
                    player.Play(animation);
                    break;
                }
            }
        }
        foreach (var child in node.GetChildren()) PlayImportedAnimation(child, preferredAnimation);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health.IsDead || _player == null)
            return;

        _slowTimer = Mathf.Max(_slowTimer - (float)delta, 0); if (_slowTimer <= 0) _slowMultiplier = 1;
        _fearTimer = Mathf.Max(_fearTimer - (float)delta, 0);
        _damageTakenTimer = Mathf.Max(_damageTakenTimer - (float)delta, 0); if (_damageTakenTimer <= 0) _damageTakenBonus = 0;
        _hitStunTimer = Mathf.Max(_hitStunTimer - (float)delta, 0.0f);
        _attackCooldownRemaining = Mathf.Max(_attackCooldownRemaining - (float)delta, 0.0f);
        _spawnAttackDelayRemaining = Mathf.Max(_spawnAttackDelayRemaining - (float)delta, 0.0f);
        _tacticalMoveTimer = Mathf.Max(_tacticalMoveTimer - (float)delta, 0.0f);
        _rangedFleeTimer = Mathf.Max(_rangedFleeTimer - (float)delta, 0.0f);
        TickBlessingStatuses((float)delta);
        UpdateCharmLinkVisual();
        _bossActionAnimationTimer = Mathf.Max(_bossActionAnimationTimer - (float)delta, 0.0f);
        _attackAnimationTimer = Mathf.Max(_attackAnimationTimer - (float)delta, 0.0f);
        if (_knockbackVelocity.LengthSquared() > 0.01f)
        {
            Velocity = _knockbackVelocity;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, G("enemy.knockback_decay", 18.0f) * (float)delta);
            MoveAndSlide();
            ApplyPendingWallImpact();
            return;
        }
        if (_hitStunTimer > 0.0f && !_isAttacking)
        {
            Velocity = Vector3.Zero;
            return;
        }
        if (IsCharmed)
        {
            RunCharmedBehavior((float)delta);
            return;
        }
        if (_bossActionLocked)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (CanAttackAfterSpawn)
            TickContactDamage((float)delta, distance);

        if (_isAttacking)
        {
            if (_aimTrackRemaining > 0.0f)
            {
                _aimTrackRemaining -= (float)delta;
                LockAttackDirection();
            }
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        _pendingWallImpactDamage = 0.0f;

        if (!_hasDetectedPlayer && distance <= DetectionRange && CanSeePlayer())
            DetectPlayer();

        if (!_hasDetectedPlayer)
        {
            Patrol((float)delta);
            return;
        }

        LookAt(GlobalPosition + toPlayer.Normalized(), Vector3.Up);

        var insideTacticalRange = distance <= AttackRange + (Archetype == EnemyArchetype.Ranged ? G("enemy.ranged.tactical_range_pad", 2.0f) : G("enemy.melee.tactical_range_pad", 0.9f));
        if ((_tacticalMoveTimer > 0.0f || _attackCooldownRemaining > 0.0f || _rangedFleeTimer > 0.0f) && insideTacticalRange)
        {
            MoveTactically(toPlayer, distance, (float)delta);
            return;
        }

        if (Archetype == EnemyArchetype.Ranged && distance < RangedRetreatDistance)
        {
            BeginTacticalMovement(0.55f, 0.85f);
            MoveTactically(toPlayer, distance, (float)delta);
            return;
        }

        if (distance <= AttackRange && _attackCooldownRemaining <= 0.0f && CanAttackAfterSpawn && !_bossMovesEnabled)
        {
            if (Archetype == EnemyArchetype.Ranged && !CanSeePlayer())
            {
                _hasDetectedPlayer = false;
                PickNewPatrolTarget();
                return;
            }
            Velocity = Vector3.Zero;
            MoveAndSlide();
            BeginAttack();
            return;
        }

        if (_bossMovesEnabled && distance <= AttackRange)
        {
            BeginTacticalMovement(0.35f, 0.65f);
            MoveTactically(toPlayer, distance, (float)delta);
            return;
        }

        var chaseDirection = (_fearTimer > 0 ? -toPlayer : toPlayer).Normalized();
        if (_alternatePathTimer > 0)
        {
            _alternatePathTimer -= (float)delta;
            chaseDirection = new Vector3(-chaseDirection.Z * _avoidanceSide, 0, chaseDirection.X * _avoidanceSide).Normalized();
        }
        Velocity = chaseDirection * MoveSpeed * _slowMultiplier;
        PlayImportedLocomotionAnimation(true);
        MoveAndSlide();
        if (GetSlideCollisionCount() > 0 && _alternatePathTimer <= 0)
        {
            _avoidanceSide *= -1;
            _alternatePathTimer = 0.65f;
        }
    }

    private void BeginTacticalMovement(float minimumSeconds, float maximumSeconds)
    {
        _tacticalMoveTimer = Mathf.Max(_tacticalMoveTimer, (float)GD.RandRange(minimumSeconds, maximumSeconds));
        if (GD.Randf() < 0.45f) _tacticalSide *= -1;
        if (Archetype == EnemyArchetype.Ranged && GD.Randf() < G("enemy.ranged.flee_chance", 0.34f))
            _rangedFleeTimer = Mathf.Max(_rangedFleeTimer, (float)GD.RandRange(G("enemy.ranged.flee_min_seconds", 1.15f), G("enemy.ranged.flee_max_seconds", 2.25f)));
    }

    private void MoveTactically(Vector3 toPlayer, float distance, float delta)
    {
        if (toPlayer.LengthSquared() <= 0.001f) return;
        var radial = toPlayer.Normalized();
        var tangent = new Vector3(-radial.Z * _tacticalSide, 0.0f, radial.X * _tacticalSide);
        Vector3 direction;

        if (Archetype == EnemyArchetype.Ranged)
        {
            if (_rangedFleeTimer > 0.0f)
                direction = (-radial + tangent * 0.22f).Normalized();
            else if (distance < RangedPreferredDistance - 0.65f)
                direction = (-radial + tangent * 0.38f).Normalized();
            else if (distance > RangedPreferredDistance + 0.9f)
                direction = (radial + tangent * 0.28f).Normalized();
            else
                direction = tangent;
        }
        else
        {
            var retreatWeight = Archetype == EnemyArchetype.Tank ? G("enemy.tank.retreat_weight", 0.16f) : G("enemy.melee.retreat_weight", 0.32f);
            direction = (tangent - radial * retreatWeight).Normalized();
        }

        Velocity = direction * MoveSpeed * TacticalMoveSpeedMultiplier * _slowMultiplier;
        PlayImportedLocomotionAnimation(true);
        MoveAndSlide();
        if (GetSlideCollisionCount() > 0)
        {
            _tacticalSide *= -1;
            _tacticalMoveTimer = Mathf.Max(_tacticalMoveTimer, 0.35f);
        }
    }

    public void RefreshPlayerHitStatuses()
    {
        if (IsPestilent) _pestilenceTimer = Mathf.Max(_pestilenceTimer, G("enemy.status.pestilence_refresh_seconds", 3.0f));
        if (IsBleeding) _bleedingTimer = Mathf.Max(_bleedingTimer, G("enemy.status.bleeding_refresh_seconds", 2.0f));
    }
    public void ApplyPestilence(float seconds, int maxStacks = -1)
    {
        var stackLimit = maxStacks > 0 ? maxStacks : MaxPestilenceStacks;
        _pestilenceStacks = Mathf.Min(_pestilenceStacks + 1, Mathf.Max(stackLimit, 1));
        _pestilenceTimer = Mathf.Max(_pestilenceTimer, seconds);
        if (RunState.GetEffectTotal("PestilenceAlsoBleeds.Flat") > 0)
            ApplyBleeding(
                RunState.GetEffectParameter("PestilenceAlsoBleeds", "duration", 4),
                RunState.GetEffectParameter("PestilenceAlsoBleeds", "damagePerSecond", 5),
                Mathf.RoundToInt(RunState.GetEffectParameter("PestilenceAlsoBleeds", "maxStacks", 3)));
        UpdateStatusIcons();
    }

    public void ApplyBleeding(float seconds, float damagePerSecond, int maxStacks)
    {
        _bleedingStacks = Mathf.Min(_bleedingStacks + 1, Mathf.Max(maxStacks, 1));
        _bleedingTimer = Mathf.Max(_bleedingTimer, seconds);
        _bleedingDamagePerSecond = Mathf.Max(_bleedingDamagePerSecond, damagePerSecond);
        if (!IsPestilent && RunState.GetEffectTotal("BleedingAlsoPestilence.Flat") > 0)
        {
            _pestilenceStacks = 1;
            _pestilenceTimer = RunState.GetEffectParameter("BleedingAlsoPestilence", "duration", 3);
        }
        UpdateStatusIcons();
    }

    public void ApplyCharm(float seconds, bool permanent)
    {
        var charmImmune = Name.ToString().Contains("Boss") || Rarity == EnemyRarity.Legendary;
        if (charmImmune)
        {
            if (permanent)
                _fascinated = true;
            UpdateStatusIcons();
            return;
        }

        _permanentCharm |= permanent;
        _charmTimer = Mathf.Max(_charmTimer, seconds);
        AddToGroup("charmed_enemies");
        EnsureCharmLink();
        UpdateStatusIcons();
    }

    public bool TryTriggerPestilenceCloud()
    {
        if (_pestilenceCloudTriggered)
            return false;
        _pestilenceCloudTriggered = true;
        return true;
    }

    private void TickBlessingStatuses(float delta)
    {
        if (!_permanentCharm) _charmTimer = Mathf.Max(_charmTimer - delta, 0.0f);
        if (!IsCharmed && IsInGroup("charmed_enemies")) RemoveFromGroup("charmed_enemies");
        if (!IsCharmed) ClearCharmLink();
        if (_pestilenceStacks > 0)
        {
            _pestilenceTimer -= delta;
            _pestilenceTickTimer -= delta;
            if (_pestilenceTickTimer <= 0)
            {
                _pestilenceTickTimer = G("enemy.status.pestilence_tick_seconds", 1.0f);
                if (RunState.GetEffectTotal("InfinitePestilenceSpread.Flat") > 0)
                    foreach (var node in GetTree().GetNodesInGroup("enemies"))
                        if (node is EnemyBasic enemy && enemy != this && !enemy.IsPestilent && enemy.GlobalPosition.DistanceTo(GlobalPosition) <= G("enemy.status.pestilence_spread_radius", 6.0f)) { enemy.ApplyPestilence(Mathf.Max(_pestilenceTimer, G("enemy.status.pestilence_refresh_seconds", 3.0f))); break; }
            }
            if (_pestilenceTimer <= 0) _pestilenceStacks = 0;
        }

        if (_bleedingStacks > 0)
        {
            _bleedingTimer -= delta;
            _bleedingTickTimer -= delta;
            if (_bleedingTickTimer <= 0)
            {
                _bleedingTickTimer = G("enemy.status.bleeding_tick_seconds", 1.0f);
                _health.ApplyDamage(_bleedingDamagePerSecond * _bleedingStacks);
            }
            if (_bleedingTimer <= 0)
            {
                _bleedingStacks = 0;
                _bleedingDamagePerSecond = 0.0f;
            }
        }

        UpdateStatusIcons();
    }

    private void RunCharmedBehavior(float delta)
    {
        _charmAttackTimer -= delta;
        EnemyBasic target = null;
        var closest = float.PositiveInfinity;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic enemy || enemy == this || enemy.IsCharmed) continue;
            var distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (enemy.Rarity != EnemyRarity.Normal && RunState.GetEffectTotal("CharmedDamageVsElite.Multiplier") > 0) distance *= 0.55f;
            if (distance < closest) { closest = distance; target = enemy; }
        }
        if (target == null) { Patrol(delta); return; }
        var direction = target.GlobalPosition - GlobalPosition; direction.Y = 0;
        LookAt(GlobalPosition + direction.Normalized(), Vector3.Up);
        if (closest > AttackRange) { Velocity = direction.Normalized() * MoveSpeed; MoveAndSlide(); return; }
        Velocity = Vector3.Zero;
        if (_charmAttackTimer > 0) return;
        _charmAttackTimer = Mathf.Max(AttackCooldown, 0.5f);
        var damage = AttackDamage * (1.0f + RunState.GetEffectTotal("CharmedEnemyDamage.Multiplier"));
        if (target.Rarity != EnemyRarity.Normal) damage *= 1.0f + RunState.GetEffectTotal("CharmedDamageVsElite.Multiplier");
        target.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
        var linkDamage = damage * RunState.GetEffectTotal("CharmDamageLink.Multiplier");
        if (linkDamage > 0)
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic linked && linked != this && linked != target && linked.GlobalPosition.DistanceTo(target.GlobalPosition) <= 4) { linked.GetNodeOrNull<Health>("Health")?.ApplyDamage(linkDamage); break; }
        _player?.GetNodeOrNull<Health>("Health")?.Heal(damage * RunState.GetEffectTotal("HealFromCharmedDamage.Multiplier"));
    }

    private void EnsureCharmLink()
    {
        if (!IsCharmed || RunState.GetEffectTotal("CharmDamageLink.Multiplier") <= 0.0f)
        {
            ClearCharmLink();
            return;
        }

        if (IsInstanceValid(_charmLinkedEnemy) &&
            !_charmLinkedEnemy.IsQueuedForDeletion() &&
            _charmLinkedEnemy.GetNodeOrNull<Health>("Health")?.IsDead != true)
            return;

        _charmLinkedEnemy = null;
        var closestDistance = float.PositiveInfinity;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic candidate || candidate == this || candidate.IsCharmed)
                continue;
            var health = candidate.GetNodeOrNull<Health>("Health");
            if (health == null || health.IsDead)
                continue;
            var distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            _charmLinkedEnemy = candidate;
        }

        if (_charmLinkedEnemy == null)
        {
            if (_charmLinkVisual != null)
                _charmLinkVisual.Visible = false;
            return;
        }
        if (_charmLinkVisual != null)
            return;

        _charmLinkVisual = new MeshInstance3D
        {
            Name = "CharmDamageLink",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.028f,
                BottomRadius = 0.028f,
                Height = 1.0f,
                RadialSegments = 12
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.03f, 0.58f, 0.88f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.0f, 0.52f),
                EmissionEnergyMultiplier = 4.5f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true
            }
        };
        GetTree().CurrentScene?.AddChild(_charmLinkVisual);
    }

    private void UpdateCharmLinkVisual()
    {
        if (!IsCharmed || RunState.GetEffectTotal("CharmDamageLink.Multiplier") <= 0.0f)
        {
            ClearCharmLink();
            return;
        }

        EnsureCharmLink();
        if (_charmLinkVisual == null || !IsInstanceValid(_charmLinkedEnemy))
            return;

        var from = GlobalPosition + Vector3.Up * 0.9f;
        var to = _charmLinkedEnemy.GlobalPosition + Vector3.Up * 0.9f;
        var delta = to - from;
        var distance = delta.Length();
        if (distance <= 0.01f)
        {
            _charmLinkVisual.Visible = false;
            return;
        }

        _charmLinkVisual.Visible = true;
        var rotation = new Quaternion(Vector3.Up, delta / distance);
        _charmLinkVisual.GlobalTransform = new Transform3D(new Basis(rotation), (from + to) * 0.5f);
        _charmLinkVisual.Scale = new Vector3(1.0f, distance, 1.0f);
    }

    private void ClearCharmLink()
    {
        _charmLinkedEnemy = null;
        if (_charmLinkVisual != null && IsInstanceValid(_charmLinkVisual))
            _charmLinkVisual.QueueFree();
        _charmLinkVisual = null;
    }

    private void Patrol(float delta)
    {
        _patrolRetargetTimer -= delta;
        var toTarget = _patrolTarget - GlobalPosition;
        toTarget.Y = 0.0f;

        if (_patrolRetargetTimer <= 0.0f || toTarget.LengthSquared() <= 0.35f)
            PickNewPatrolTarget();

        toTarget = _patrolTarget - GlobalPosition;
        toTarget.Y = 0.0f;

        if (toTarget.LengthSquared() <= 0.001f)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        var direction = toTarget.Normalized();
        LookAt(GlobalPosition + direction, Vector3.Up);
        Velocity = direction * PatrolSpeed;
        PlayImportedLocomotionAnimation(true);
        MoveAndSlide();
    }

    private void TickContactDamage(float delta, float distanceToPlayer)
    {
        _contactDamageTimer = Mathf.Max(_contactDamageTimer - delta, 0.0f);

        if (_contactDamageTimer > 0.0f || distanceToPlayer > ContactDamageRange)
            return;

        _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(
            Mathf.Max(AttackDamage * ContactDamage, 0.0f),
            false,
            Localization.TextFormat("enemy.damage.contact", GetDisplayName()));
        _contactDamageTimer = ContactDamageCooldown;
    }

    private void PickNewPatrolTarget()
    {
        var angle = GD.Randf() * Mathf.Tau;
        var distance = GD.Randf() * PatrolRadius;
        _patrolTarget = _spawnPosition + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
        _patrolRetargetTimer = (float)GD.RandRange(PatrolRetargetMinTime, PatrolRetargetMaxTime);
    }

    private void DetectPlayer()
    {
        _hasDetectedPlayer = true;

        SpawnAlertMarker();
    }

    private bool CanSeePlayer()
    {
        if (!RequireLineOfSight || _player == null)
            return true;

        var from = GlobalPosition + Vector3.Up * LineOfSightEyeHeight;
        var to = _player.GlobalPosition + Vector3.Up * LineOfSightEyeHeight;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
            return true;

        return result.TryGetValue("collider", out var colliderVariant)
            && colliderVariant.AsGodotObject() == _player;
    }

    public bool IsAttackedFromBehind(Vector3 attackerPosition)
    {
        var toAttacker = attackerPosition - GlobalPosition;
        toAttacker.Y = 0.0f;
        if (toAttacker.LengthSquared() <= 0.001f) return false;
        var forward = -GlobalTransform.Basis.Z;
        forward.Y = 0.0f;
        return forward.LengthSquared() > 0.001f && forward.Normalized().Dot(toAttacker.Normalized()) < G("enemy.backstab_dot", -0.45f);
    }

    public void ApplyKnockback(Vector3 direction, float force, float wallImpactDamage = 0.0f)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.001f)
            return;

        var bossResistance = Name.ToString().Contains("Boss") ? G("enemy.boss.knockback_factor", 0.05f) : 1.0f;
        _knockbackVelocity = direction.Normalized() * force * KnockbackResistance * bossResistance;
        _pendingWallImpactDamage = Mathf.Max(wallImpactDamage * bossResistance, 0.0f);
    }

    public void ApplyStagger(float duration)
    {
        if (duration <= 0.0f || HasStaggerArmor)
            return;

        var rarityMultiplier = Rarity == EnemyRarity.Normal ? 1.0f : G("enemy.stagger.rarity_multiplier", 0.72f);
        _hitStunTimer = Mathf.Max(_hitStunTimer, duration * rarityMultiplier);
        if (!_isAttacking)
            return;

        _attackSequenceVersion++;
        _isAttacking = false;
        _attackAnimationTimer = 0.0f;
        HideAttackTelegraph();
    }

    public bool IsThreateningDodge(Vector3 position)
    {
        var delta = position - GlobalPosition;
        delta.Y = 0.0f;
        if (_bossThreats.Count > 0)
        {
            // Boss em golpe: ameaca = qualquer area de dano ativa (circulo/linha) perto do jogador.
            foreach (var threat in _bossThreats)
            {
                var offset = position - threat.Center;
                offset.Y = 0.0f;
                if (threat.IsSegment)
                {
                    // Linha (carga): distancia ponto-segmento, margem de 0,5 u.
                    var segment = threat.End - threat.Center;
                    segment.Y = 0.0f;
                    var lengthSquared = segment.LengthSquared();
                    var progress = lengthSquared > 0.0001f ? Mathf.Clamp(offset.Dot(segment) / lengthSquared, 0.0f, 1.0f) : 0.0f;
                    if ((offset - segment * progress).Length() <= threat.Radius + 0.5f) return true;
                }
                else if (offset.Length() <= threat.Radius + 1.0f) return true;
            }
        }
        return (_isAttacking || _attackWarningLabel?.Visible == true) && delta.Length() <= AttackRange + AttackDepthBonus + 1.0f;
    }

    // Registro de areas de dano do boss (usado por IsThreateningDodge para a esquiva perfeita).
    public int AddBossThreat(Vector3 center, float radius)
    {
        var id = ++_bossThreatCounter;
        _bossThreats.Add((id, center, center, radius, false));
        return id;
    }

    // Ameaca em linha (carga): segmento inicio-fim com meia-largura.
    public int AddBossLineThreat(Vector3 start, Vector3 end, float halfWidth)
    {
        var id = ++_bossThreatCounter;
        _bossThreats.Add((id, start, end, halfWidth, true));
        return id;
    }

    public void ClearBossThreats() => _bossThreats.Clear();

    public void RemoveBossThreat(int id) => _bossThreats.RemoveAll(threat => threat.Id == id);

    // Flash curto (~0,1 s) no corpo: marca o inicio do wind-up. Usa material proprio e nao
    // concorre com o flash de dano (FlashImportedVisual / _hitMaterial).
    public async void FlashWindup()
    {
        if (_health == null || _health.IsDead) return;
        if (_importedVisual != null)
        {
            foreach (var pair in _importedBaseMaterials)
                if (IsInstanceValid(pair.Key) && pair.Key.MaterialOverride != _hitMaterial)
                    pair.Key.MaterialOverride = _windupFlashMaterial;
        }
        else if (_visual != null && _visual.MaterialOverride != _hitMaterial)
        {
            _visual.MaterialOverride = _windupFlashMaterial;
        }

        await Wait(WindupFlashSeconds);
        if (!IsInstanceValid(this) || _health.IsDead) return;
        foreach (var pair in _importedBaseMaterials)
            if (IsInstanceValid(pair.Key) && pair.Key.MaterialOverride == _windupFlashMaterial)
                pair.Key.MaterialOverride = pair.Value;
        if (_visual != null && _visual.MaterialOverride == _windupFlashMaterial)
            _visual.MaterialOverride = _baseMaterial;
    }

    private async void PlayAnticipatedAttackAnimation(int attackSequence)
    {
        if (_importedAnimationPlayer == null || string.IsNullOrWhiteSpace(_importedAttackAnimation)
            || !_importedAnimationPlayer.HasAnimation(_importedAttackAnimation))
            return;
        const float speed = 1.16f;
        var duration = (float)_importedAnimationPlayer.GetAnimation(_importedAttackAnimation).Length / speed;
        // O golpe fica por volta da metade da animacao: comeca para que ele caia no fim do wind-up.
        var delay = Mathf.Max(AttackWindup - duration * 0.5f, 0.0f);
        if (delay > 0.02f)
        {
            // Durante o atraso fica em idle (nao em walk parado).
            if (!string.IsNullOrWhiteSpace(_importedIdleAnimation) && _importedAnimationPlayer.HasAnimation(_importedIdleAnimation))
                _importedAnimationPlayer.Play(_importedIdleAnimation, 0.12, 1.0f);
            await Wait(delay);
        }
        if (!IsInstanceValid(this) || attackSequence != _attackSequenceVersion || _health.IsDead) return;
        PlayImportedActionAnimation(_importedAttackAnimation, speed);
        // Trava a locomocao ate a animacao de ataque terminar.
        _attackAnimationTimer = duration;
    }

    private void ApplyPendingWallImpact()
    {
        if (_pendingWallImpactDamage <= 0.0f || GetSlideCollisionCount() == 0) return;
        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is not PhysicsBody3D surface || surface is CharacterBody3D) continue;
            var normal = collision.GetNormal();
            if (Mathf.Abs(normal.Y) > 0.4f) continue;
            var knockbackDirection = _knockbackVelocity.Normalized();
            if (knockbackDirection.LengthSquared() <= 0.001f || knockbackDirection.Dot(normal) > -0.35f) continue;
            var impactDamage = _pendingWallImpactDamage;
            _pendingWallImpactDamage = 0.0f;
            _knockbackVelocity = Vector3.Zero;
            _health.ApplyDamage(impactDamage, false, WallImpactDamageSource);
            CombatFeedback.Instance?.PlayPlayerHit(1, false, true);
            return;
        }
    }

    private async void BeginAttack()
    {
        if (_isAttacking)
            return;

        _isAttacking = true;
        var attackSequence = ++_attackSequenceVersion;
        LockAttackDirection();
        // Ranged acompanha o alvo e trava a mira nos ultimos AimLockSeconds do wind-up.
        _aimTrackRemaining = Archetype == EnemyArchetype.Ranged ? Mathf.Max(AttackWindup - AimLockSeconds, 0.0f) : 0.0f;
        ShowAttackTelegraph();
        FlashWindup();
        GetTree().CallGroup("audio_manager", "PlayEffect", Archetype switch
        {
            EnemyArchetype.Ranged => "enemy_ranged_charge",
            EnemyArchetype.Tank => "enemy_windup_heavy",
            _ => "enemy_windup"
        });
        PlayAnticipatedAttackAnimation(attackSequence);
        await Wait(AttackWindup);
        if (attackSequence != _attackSequenceVersion) return;
        HideAttackTelegraph();
        if (!_health.IsDead && Archetype != EnemyArchetype.Ranged)
        {
            GetTree().CallGroup("audio_manager", "PlayEffect", "enemy_attack");
            SpawnAttackImpact();
        }

        if (!_health.IsDead && _player != null && Archetype == EnemyArchetype.Ranged && CanSeePlayer())
        {
            SpawnProjectile();
        }
        else if (!_health.IsDead && _player != null && IsPlayerInsideLockedAttack())
        {
            var normalPenalty = Rarity == EnemyRarity.Normal ? 1.0f + RunState.GetEffectTotal("DamageTakenFromNormal.Multiplier") : 1.0f;
            var infectedPenalty = IsPestilent ? 1.0f + RunState.GetEffectTotal("InfectedEnemyDamage.Multiplier") : 1.0f;
            _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage * normalPenalty * Mathf.Max(infectedPenalty, 0.1f), false, GetDisplayName());
            if (Rarity == EnemyRarity.Legendary)
            {
                await Wait(G("enemy.legendary.double_hit_delay", 0.16f));
                if (attackSequence != _attackSequenceVersion) return;
                if (!_health.IsDead && IsPlayerInsideLockedAttack())
                    _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage * G("enemy.legendary.double_hit_damage", 0.55f), false, GetDisplayName());
            }
        }

        _attackCooldownRemaining = Mathf.Max(AttackCooldown, 0.25f);
        BeginTacticalMovement(
            Archetype == EnemyArchetype.Ranged ? G("enemy.ranged.post_attack_move_min", 0.9f) : Archetype == EnemyArchetype.Tank ? G("enemy.tank.post_attack_move_min", 0.45f) : G("enemy.melee.post_attack_move_min", 0.28f),
            Archetype == EnemyArchetype.Ranged ? G("enemy.ranged.post_attack_move_max", 1.35f) : Archetype == EnemyArchetype.Tank ? G("enemy.tank.post_attack_move_max", 0.8f) : G("enemy.melee.post_attack_move_max", 0.58f));
        _isAttacking = false;
    }

    public string GetDisplayName()
    {
        if (Name.ToString().Contains("Boss")) return BossCatalog.Get(RunState.CurrentMapIndex).DisplayName;
        if (!string.IsNullOrWhiteSpace(_legendaryNameId)) return Localization.Text($"enemy.legendary.{_legendaryNameId}.name");
        var archetypeName = EnemyTuningCatalog.ArchetypeName(Archetype);
        if (Rarity == EnemyRarity.Normal) return archetypeName;
        return Localization.TextFormat("enemy.display_name.with_rarity", archetypeName, EnemyTuningCatalog.Get(Rarity).DisplayName);
    }

    public void ApplyBossMoveTuning(BossMoveDefinition move, int phase)
    {
        AttackCooldown = Mathf.Max(move.TunedCooldown / (1.0f + (phase - 1) * G("boss.cooldown_phase_reduction", 0.12f)), G("boss.attack_cooldown_min", 0.35f));
        // Base 30 / 2.25 vem de enemy.blood_mage.damage / .attack_range (nome legado): o valor tunado passa a valer a cada golpe.
        var bossBaseDamage = G("enemy.blood_mage.damage", 30.0f) * (1.0f + RunState.CurrentMapIndex * G("boss.damage_map_scale", 0.22f));
        AttackDamage = Mathf.Max(bossBaseDamage * move.TunedDamageMultiplier * (1.0f + (phase - 1) * G("boss.damage_phase_scale", 0.18f)) * G("boss.damage_final_multiplier", 0.9f), 1.0f);
        AttackRange = Mathf.Max(G("enemy.blood_mage.attack_range", 2.25f) * move.TunedRangeMultiplier, 1.0f);
        AttackWidth = Mathf.Max(G("boss.attack_width_base", 1.5f) * move.TunedRangeMultiplier, 1.0f);
    }

    public void EnableBossMoves()
    {
        _bossMovesEnabled = true;
        _attackCooldownRemaining = 0.0f;
    }

    public void SetBossActionLocked(bool locked)
    {
        _bossActionLocked = locked;
        if (!locked) return;
        _attackSequenceVersion++;
        _isAttacking = false;
        Velocity = Vector3.Zero;
        HideAttackTelegraph();
    }

    public Node3D GetCombatTarget() => _player;

    public void FaceCombatTarget()
    {
        if (_player == null) return;
        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.001f) LookAt(GlobalPosition + direction.Normalized(), Vector3.Up);
    }

    public void MoveBoss(Vector3 displacement)
    {
        displacement.Y = 0.0f;
        if (displacement.LengthSquared() > 0.001f) MoveAndCollide(displacement);
    }

    public void TeleportBoss(Vector3 destination)
    {
        destination.Y = GlobalPosition.Y;
        GlobalPosition = destination;
    }

    private void SpawnProjectile()
    {
        if (_player == null || !CanSeePlayer()) return;
        var origin = GlobalPosition + Vector3.Up * 1.1f;
        // Sai pela direcao travada no telegraph (nao recalcula para a posicao atual do jogador).
        var direction = _lockedAttackDirection.LengthSquared() > 0.001f
            ? _lockedAttackDirection.Normalized()
            : (_player.GlobalPosition + Vector3.Up * 0.8f - origin).Normalized();
        GetTree().CallGroup("audio_manager", "PlayEffect", "enemy_ranged_fire");
        var projectile = new EnemyProjectile
        {
            Name = "EnemyProjectile",
            SpawnPosition = origin,
            Direction = direction,
            MaxDistance = AttackRange * ProjectileRangeMultiplier,
            Speed = G("enemy.projectile.speed", 5.2f),
            Damage = AttackDamage,
            SourceName = GetDisplayName(),
            OwnerRid = GetRid()
        };
        GetTree().CurrentScene.AddChild(projectile);
    }

    private void OnDied()
    {
        ClearCharmLink();
        if (!RunState.IsBossRoom) SpawnBloodEssence();
        GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime")?.OnEnemyKilled(this);
        RollDefeatRewards(out var bloodCoins, out var cursedBlood);
        GetTree().CallGroup("run_manager", "QueueEnemyDefeat", bloodCoins, cursedBlood, GlobalPosition);
        GetTree().CallGroup("player_skills", "OnEnemyDefeated");
        RunState.NotifyEnemyKilled();

        var combat = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        combat?.AddFury(FuryOnDeath);

        SetPhysicsProcess(false);
        QueueFree();
    }

    private void SpawnBloodEssence()
    {
        var essence = new Node3D
        {
            Name = "BloodEssence",
            Position = GlobalPosition + Vector3.Up * 0.68f
        };
        var visual = new Node3D { Name = "EssenceVisual" };
        essence.AddChild(visual);
        visual.AddChild(new MeshInstance3D
        {
            Name = "BloodDrop",
            Mesh = new SphereMesh { Radius = 0.105f, Height = 0.25f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.58f, 0.008f, 0.025f, 0.92f),
                EmissionEnabled = true,
                Emission = new Color(0.72f, 0.0f, 0.035f),
                EmissionEnergyMultiplier = 1.35f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.2f
            },
            Scale = new Vector3(0.72f, 1.3f, 0.72f)
        });
        visual.AddChild(new MeshInstance3D
        {
            Name = "BloodGlint",
            Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.24f, 0.28f, 0.72f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.04f, 0.08f),
                EmissionEnergyMultiplier = 2.0f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            Position = new Vector3(-0.035f, 0.055f, -0.055f)
        });
        GetTree().CurrentScene.AddChild(essence);
        essence.AddToGroup("blood_essences");
        var tween = visual.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(visual, "position:y", 0.16f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(visual, "position:y", 0.0f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    /// <summary>
    /// Recompensas da morte (economia v2). Moedas de Sangue vao para o saldo da run; Sangue Amaldicoado e permanente.
    /// Boss: valores fixos por mapa (nao soma o bonus de Lendario nem o multiplicador de moedas da skill).
    /// Lendario comum: Sangue Amaldicoado conforme o mapa. Os valores vem de CompendiumTuning (economy.*).
    /// </summary>
    private void RollDefeatRewards(out int bloodCoins, out int cursedBlood)
    {
        var mapIndex = Mathf.Clamp(RunState.CurrentMapIndex, 0, 3);
        if (GetNodeOrNull<BossController>("BossController") != null)
        {
            bloodCoins = CompendiumTuning.GetInt($"economy.coins.boss.{mapIndex}", EconomyDefaults.BossCoins[mapIndex]);
            cursedBlood = CompendiumTuning.GetInt($"economy.cursed_blood.boss.{mapIndex}", EconomyDefaults.BossCursedBlood[mapIndex]);
            return;
        }

        bloodCoins = RollBloodCoinsReward(mapIndex);
        cursedBlood = 0;
        if (Rarity == EnemyRarity.Legendary)
        {
            var chance = CompendiumTuning.GetFloat("economy.cursed_blood.legendary_drop_chance", 1.0f);
            if (chance >= 1.0f || GD.Randf() < chance)
                cursedBlood = CompendiumTuning.GetInt($"economy.cursed_blood.legendary.{mapIndex}", EconomyDefaults.LegendaryCursedBlood[mapIndex]);
        }
    }

    private int RollBloodCoinsReward(int mapIndex)
    {
        var minReward = CompendiumTuning.GetInt("economy.coins.enemy.base_min", EconomyDefaults.EnemyCoinsMin);
        var maxReward = Mathf.Max(CompendiumTuning.GetInt("economy.coins.enemy.base_max", EconomyDefaults.EnemyCoinsMax), minReward);
        // GD.RandRange(int, int) e inclusivo nos dois lados: sorteia exatamente [min, max].
        var roll = GD.RandRange(minReward, maxReward);

        var archetypeMultiplier = Archetype switch
        {
            EnemyArchetype.Tank => CompendiumTuning.GetFloat("economy.coins.archetype.tank", 1.5f),
            EnemyArchetype.Ranged => CompendiumTuning.GetFloat("economy.coins.archetype.ranged", 1.0f),
            _ => CompendiumTuning.GetFloat("economy.coins.archetype.melee", 1.0f)
        };
        var rarityMultiplier = Rarity switch
        {
            EnemyRarity.Magic => CompendiumTuning.GetFloat("economy.coins.rarity.magic", 2.0f),
            EnemyRarity.Rare => CompendiumTuning.GetFloat("economy.coins.rarity.rare", 4.0f),
            EnemyRarity.Legendary => CompendiumTuning.GetFloat("economy.coins.rarity.legendary", 8.0f),
            _ => CompendiumTuning.GetFloat("economy.coins.rarity.normal", 1.0f)
        };
        var mapFactor = CompendiumTuning.GetFloat($"economy.coins.map_factor.{mapIndex}", EconomyDefaults.CoinMapFactor[mapIndex]);
        var skillController = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
        var skillMultiplier = skillController?.BloodCoinMultiplier() ?? 1.0f;

        // Arredonda so no final (nunca multiplicando um inteiro ja arredondado); minimo 1.
        return Mathf.Max(1, Mathf.RoundToInt(roll * archetypeMultiplier * rarityMultiplier * mapFactor * skillMultiplier));
    }

    private async void OnDamaged(float amount)
    {
        if (amount <= 0)
        {
            SpawnCombatMarker(Localization.Text("enemy.marker.blocked"), new Color(0.72f, 0.86f, 1.0f));
            SpawnHitImpact(new Color(0.62f, 0.78f, 1.0f), 1.25f);
            GetTree().CallGroup("audio_manager", "PlayEffect", "armor_block");
            return;
        }
        if (_overheadHealthEnabled)
        {
            _overheadHealthRevealed = true;
            SetHealthBarMeshesVisible(true);
        }
        SpawnDamageNumber(amount);
        if (_health.LastDamageSource == BackstabDamageSource)
        {
            SpawnHitImpact(new Color(0.95f, 0.08f, 0.32f), 1.45f);
            GetTree().CallGroup("audio_manager", "PlayEffect", "hit_backstab");
        }
        else if (_health.LastDamageSource == WallImpactDamageSource)
        {
            SpawnHitImpact(new Color(1.0f, 0.42f, 0.08f), 1.65f);
            GetTree().CallGroup("audio_manager", "PlayEffect", "wall_impact");
        }
        else
        {
            SpawnHitImpact();
        }
        FlashImportedVisual();
        if (!_isAttacking && !_bossActionLocked)
            PlayImportedActionAnimation(_importedHitAnimation, 1.3f);
        if (Archetype == EnemyArchetype.Ranged && GD.Randf() < 0.42f)
        {
            _rangedFleeTimer = Mathf.Max(_rangedFleeTimer, (float)GD.RandRange(1.25f, 2.35f));
            _tacticalMoveTimer = Mathf.Max(_tacticalMoveTimer, _rangedFleeTimer);
        }

        if (_visual != null)
            _visual.MaterialOverride = _hitMaterial;

        await Wait(0.08f);

        if (_visual != null && !_health.IsDead)
        {
            _visual.MaterialOverride = _baseMaterial;
        }
    }

    private async void FlashImportedVisual()
    {
        if (_importedVisual == null) return;
        var version = ++_hitFeedbackVersion;

        _importedHitTween?.Kill();
        foreach (var pair in _importedBaseMaterials)
            if (IsInstanceValid(pair.Key)) pair.Key.MaterialOverride = _hitMaterial;

        _importedVisual.Scale = new Vector3(
            _importedVisualBaseScale.X * 1.08f,
            _importedVisualBaseScale.Y * 0.92f,
            _importedVisualBaseScale.Z * 1.08f);
        _importedHitTween = _importedVisual.CreateTween();
        _importedHitTween.TweenProperty(_importedVisual, "scale", _importedVisualBaseScale, 0.11f);
        await Wait(0.09f);
        if (version != _hitFeedbackVersion || _health.IsDead) return;
        foreach (var pair in _importedBaseMaterials)
            if (IsInstanceValid(pair.Key)) pair.Key.MaterialOverride = pair.Value;
    }

    private async void SpawnHitImpact(Color? contextColor = null, float sizeMultiplier = 1.0f)
    {
        var color = contextColor ?? new Color(1, 0.3f, 0.04f);
        var impactMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, 0.84f),
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 3.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var impact = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.22f * sizeMultiplier, Height = 0.44f * sizeMultiplier },
            MaterialOverride = impactMaterial,
            Position = Vector3.Up * 1.0f
        };
        AddChild(impact);
        for (var index = 0; index < 7; index++)
        {
            var angle = Mathf.Tau * index / 7.0f + (float)GD.RandRange(-0.18f, 0.18f);
            var spark = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = impactMaterial,
                Position = Vector3.Up * 1.0f
            };
            AddChild(spark);
            var destination = spark.Position + new Vector3(Mathf.Cos(angle), (float)GD.RandRange(-0.2f, 0.45f), Mathf.Sin(angle)) * 0.75f * sizeMultiplier;
            var sparkTween = spark.CreateTween();
            sparkTween.TweenProperty(spark, "position", destination, 0.16f);
            sparkTween.Parallel().TweenProperty(spark, "transparency", 1.0f, 0.16f);
            sparkTween.TweenCallback(Callable.From(spark.QueueFree));
        }
        var tween = impact.CreateTween();
        tween.TweenProperty(impact, "scale", Vector3.One * 1.8f, 0.08f);
        tween.Parallel().TweenProperty(impact, "transparency", 1.0f, 0.14f);
        await ToSignal(tween, Tween.SignalName.Finished);
        impact.QueueFree();
    }

    private void SpawnCombatMarker(string text, Color color)
    {
        var label = new Label3D
        {
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            Modulate = color,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.05f
        };
        AddChild(label);
        AnimateDamageNumber(label);
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealthBar(currentHealth, maxHealth);
    }

    private void CreateHealthBar()
    {
        _healthBarFill = new MeshInstance3D
        {
            Name = "EnemyHealthBarFill",
            Mesh = new QuadMesh { Size = new Vector2(_healthBarWidth, 0.13f) },
            MaterialOverride = CreateBillboardMaterial(new Color(0.82f, 0.025f, 0.045f), 2),
            Position = new Vector3(0.0f, _overheadHeight, 0.0f)
        };
        AddChild(_healthBarFill);
        _rarityIcon = new MeshInstance3D
        {
            Name = "EnemyRarityIcon",
            Mesh = new QuadMesh { Size = new Vector2(0.18f, 0.18f) },
            MaterialOverride = CreateBillboardMaterial(Colors.White, 3),
            Position = new Vector3(-_healthBarWidth * 0.5f - 0.18f, _overheadHeight, 0.0f),
            Rotation = new Vector3(0.0f, 0.0f, Mathf.Pi * 0.25f)
        };
        AddChild(_rarityIcon);
    }

    private static StandardMaterial3D CreateBillboardMaterial(Color color, int renderPriority = 0) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = false,
        RenderPriority = renderPriority
    };

    private void CreateStatusIcons()
    {
        _bleedingStatusIcon = CreateStatusIcon("BleedingStatusIcon", "res://images/effects/bleeding.png", -0.22f);
        _pestilenceStatusIcon = CreateStatusIcon("PestilenceStatusIcon", "res://images/effects/pestilence.png", 0.0f);
        _charmedStatusIcon = CreateStatusIcon("CharmedStatusIcon", "res://images/effects/enchanted.png", 0.22f);
        _bleedingStackLabel = CreateStatusStackLabel("BleedingStackLabel", -0.17f, new Color(1.0f, 0.28f, 0.34f));
        _pestilenceStackLabel = CreateStatusStackLabel("PestilenceStackLabel", 0.05f, new Color(0.48f, 0.92f, 0.34f));
        UpdateStatusIcons();
    }

    private Sprite3D CreateStatusIcon(string name, string texturePath, float xPosition)
    {
        if (!StatusTextures.TryGetValue(texturePath, out var texture))
        {
            texture = ResourceLoader.Exists(texturePath) ? GD.Load<Texture2D>(texturePath) : null;
            StatusTextures[texturePath] = texture;
        }
        var icon = new Sprite3D
        {
            Name = name,
            Texture = texture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.006f,
            Position = new Vector3(xPosition * 1.45f, _overheadHeight + 0.28f, 0.0f),
            Visible = false
        };
        AddChild(icon);
        return icon;
    }

    private Label3D CreateStatusStackLabel(string name, float xPosition, Color color)
    {
        var label = new Label3D
        {
            Name = name,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            PixelSize = 0.006f,
            Modulate = color,
            OutlineSize = 12,
            OutlineModulate = Colors.Black,
            NoDepthTest = true,
            RenderPriority = 4,
            Position = new Vector3(xPosition * 1.45f, _overheadHeight + 0.23f, 0.0f),
            Visible = false
        };
        AddChild(label);
        return label;
    }

    private void UpdateStatusIcons()
    {
        if (_bleedingStatusIcon != null) _bleedingStatusIcon.Visible = IsBleeding;
        if (_pestilenceStatusIcon != null) _pestilenceStatusIcon.Visible = IsPestilent;
        if (_charmedStatusIcon != null)
        {
            _charmedStatusIcon.Visible = IsCharmed || IsFascinated;
            _charmedStatusIcon.Modulate = IsFascinated ? new Color(1.0f, 0.35f, 0.95f) : Colors.White;
        }
        if (_bleedingStackLabel != null)
        {
            _bleedingStackLabel.Visible = IsBleeding;
            _bleedingStackLabel.Text = _bleedingStacks.ToString();
        }
        if (_pestilenceStackLabel != null)
        {
            _pestilenceStackLabel.Visible = IsPestilent;
            _pestilenceStackLabel.Text = _pestilenceStacks.ToString();
        }
    }

    private void CreateAttackTelegraph()
    {
        _attackTelegraph = new Node3D
        {
            Name = "AttackTelegraph",
            Position = new Vector3(0.0f, 0.03f, 0.0f),
            Visible = false
        };
        AddChild(_attackTelegraph);

        _attackWarningLabel = new Label3D
        {
            Name = "AttackWarningLabel",
            Text = "!",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 78,
            Modulate = new Color(1.0f, 0.02f, 0.0f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.75f,
            Visible = false
        };
        AddChild(_attackWarningLabel);
        UpdateAttackTelegraphGeometry();
    }

    private void UpdateRarityVisual(EnemyRarityDefinition tuning)
    {
        _healthBarWidth = Rarity == EnemyRarity.Legendary ? 2.15f : 1.55f;
        if (_healthBarFill?.Mesh is QuadMesh fill) fill.Size = new Vector2(_healthBarWidth, Rarity == EnemyRarity.Legendary ? 0.17f : 0.13f);
        if (_rarityIcon != null)
        {
            _rarityIcon.Position = new Vector3(-_healthBarWidth * 0.5f - 0.18f, _overheadHeight, 0.0f);
            _rarityIcon.MaterialOverride = CreateBillboardMaterial(tuning.Color, 3);
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (_healthBarFill == null) return;
        var ratio = maxHealth > 0.0f ? Mathf.Clamp(currentHealth / maxHealth, 0.0f, 1.0f) : 0.0f;
        if (_healthBarFill.Mesh is QuadMesh fill)
            fill.Size = new Vector2(Mathf.Max(_healthBarWidth * ratio, 0.001f), Rarity == EnemyRarity.Legendary ? 0.17f : 0.13f);
        _healthBarFill.Scale = Vector3.One;
        _healthBarFill.Position = new Vector3(-_healthBarWidth * (1.0f - ratio) * 0.5f, _overheadHeight, 0.0f);
    }

    public void SetOverheadHealthVisible(bool visible)
    {
        _overheadHealthEnabled = visible;
        _overheadHealthRevealed = visible;
        SetHealthBarMeshesVisible(visible);
    }

    private void SetHealthBarMeshesVisible(bool visible)
    {
        if (_healthBarFill != null) _healthBarFill.Visible = visible;
        if (_rarityIcon != null) _rarityIcon.Visible = visible;
    }

    private void UpdateOverheadHeight()
    {
        if (_importedVisual != null && TryGetVisualBounds(_importedVisual, out _, out var maximum))
            _overheadHeight = Mathf.Max(maximum.Y - GlobalPosition.Y + 0.28f, 1.7f);
        UpdateOverheadLayout();
    }

    private void UpdateOverheadLayout()
    {
        if (_healthBarFill != null) _healthBarFill.Position = new Vector3(_healthBarFill.Position.X, _overheadHeight, 0.0f);
        if (_rarityIcon != null) _rarityIcon.Position = new Vector3(-_healthBarWidth * 0.5f - 0.18f, _overheadHeight, 0.0f);
        if (_bleedingStatusIcon != null) _bleedingStatusIcon.Position = new Vector3(-0.319f, _overheadHeight + 0.28f, 0.0f);
        if (_pestilenceStatusIcon != null) _pestilenceStatusIcon.Position = new Vector3(0.0f, _overheadHeight + 0.28f, 0.0f);
        if (_charmedStatusIcon != null) _charmedStatusIcon.Position = new Vector3(0.319f, _overheadHeight + 0.28f, 0.0f);
        if (_bleedingStackLabel != null) _bleedingStackLabel.Position = new Vector3(-0.20f, _overheadHeight + 0.23f, 0.01f);
        if (_pestilenceStackLabel != null) _pestilenceStackLabel.Position = new Vector3(0.12f, _overheadHeight + 0.23f, 0.01f);
        SetHealthBarMeshesVisible(_overheadHealthEnabled && _overheadHealthRevealed);
    }

    private void PlayImportedLocomotionAnimation(bool moving)
    {
        if (_importedAnimationPlayer == null || _isAttacking || _bossActionAnimationTimer > 0.0f || _attackAnimationTimer > 0.0f) return;
        var animation = moving && !string.IsNullOrWhiteSpace(_importedWalkAnimation)
            ? _importedWalkAnimation
            : _importedIdleAnimation;
        if (_importedAnimationPlayer.HasAnimation(animation) && _importedAnimationPlayer.CurrentAnimation != animation)
            _importedAnimationPlayer.Play(animation, 0.12, moving ? 1.2f : 1.0f);
    }

    private void PlayImportedActionAnimation(string animation, float speed)
    {
        if (_importedAnimationPlayer == null || string.IsNullOrWhiteSpace(animation) || !_importedAnimationPlayer.HasAnimation(animation))
            return;
        _importedAnimationPlayer.Play(animation, 0.06, speed);
    }

    private void SpawnDamageNumber(float amount)
    {
        var isCritical = _health?.LastDamageWasCritical == true;
        var reason = _health?.LastDamageSource switch
        {
            BackstabDamageSource => "\n" + Localization.TextFormat("enemy.marker.backstab", Mathf.RoundToInt((WeaponLoadout.EquippedWeapon.BackstabDamageMultiplier - 1.0f) * 100.0f)),
            WallImpactDamageSource => "\n" + Localization.TextFormat("enemy.marker.wall_impact", 30),
            _ => ""
        };
        var label = new Label3D
        {
            Text = $"{Mathf.RoundToInt(amount)}{reason}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = isCritical ? 228 : 76,
            Modulate = isCritical ? new Color(1.0f, 0.843f, 0.0f) : Colors.White,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.8f
        };

        AddChild(label);
        AnimateDamageNumber(label);
    }

    private void SpawnAlertMarker()
    {
        var label = new Label3D
        {
            Text = "!",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 72,
            Modulate = new Color(1.0f, 0.02f, 0.0f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.65f
        };
        AddChild(label);
        AnimateAlertMarker(label);
    }

    private void LockAttackDirection()
    {
        if (_player == null)
            return;

        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.001f)
            return;

        _lockedAttackDirection = direction.Normalized();
        LookAt(GlobalPosition + _lockedAttackDirection, Vector3.Up);
    }

    private bool IsPlayerInsideLockedAttack()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;

        var forwardDistance = toPlayer.Dot(_lockedAttackDirection);
        if (forwardDistance < 0.0f || forwardDistance > AttackRange + AttackDepthBonus)
            return false;

        var lateralDirection = new Vector3(-_lockedAttackDirection.Z, 0.0f, _lockedAttackDirection.X);
        var lateralDistance = Mathf.Abs(toPlayer.Dot(lateralDirection));
        return lateralDistance <= AttackWidth * 0.5f;
    }

    private void ShowAttackTelegraph()
    {
        if (_attackWarningLabel == null)
            return;

        var ranged = Archetype == EnemyArchetype.Ranged;
        var windup = Mathf.Max(AttackWindup, 0.25f);
        var depth = ranged ? AttackRange * ProjectileRangeMultiplier : AttackRange + AttackDepthBonus;
        _telegraphTween?.Kill();
        _windupBodyTween?.Kill();

        _attackWarningLabel.Visible = true;
        _attackWarningLabel.Modulate = ranged ? new Color(0.72f, 0.38f, 1.0f)
            : Archetype == EnemyArchetype.Tank ? new Color(1.0f, 0.8f, 0.1f) : new Color(1.0f, 0.12f, 0.0f);
        _attackWarningLabel.Scale = Vector3.One * 0.5f;

        var tween = CreateTween();
        _telegraphTween = tween;
        // Ping do "!": estoura no inicio e assenta.
        tween.TweenProperty(_attackWarningLabel, "scale", Vector3.One * 1.7f, 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_attackWarningLabel, "scale", Vector3.One * 1.15f, 0.18f).SetDelay(0.1f);

        if (_attackTelegraph != null && _telegraphFillPivot != null && _telegraphFillMaterial != null)
        {
            _attackTelegraph.Visible = true;
            _telegraphFillPivot.Scale = new Vector3(1.0f, 1.0f, 0.001f);
            var fillColor = _telegraphFillMaterial.Emission;
            _telegraphFillMaterial.AlbedoColor = new Color(fillColor.R, fillColor.G, fillColor.B, 0.55f);
            _telegraphFillMaterial.EmissionEnergyMultiplier = 2.4f;
            _telegraphBorderMaterial.EmissionEnergyMultiplier = 3.4f;
            // Preenchimento cresce da base (origem do inimigo) para a ponta; o contorno ja esta no tamanho final.
            tween.Parallel().TweenProperty(_telegraphFillPivot, "scale", new Vector3(1.0f, 1.0f, depth), windup);
            // Pulso de brilho nos ultimos ~0,1 s.
            var pulseDelay = Mathf.Max(windup - WindupFlashSeconds, 0.0f);
            tween.Parallel().TweenProperty(_telegraphFillMaterial, "albedo_color:a", 0.95f, WindupFlashSeconds).SetDelay(pulseDelay);
            tween.Parallel().TweenProperty(_telegraphFillMaterial, "emission_energy_multiplier", 6.0f, WindupFlashSeconds).SetDelay(pulseDelay);
            tween.Parallel().TweenProperty(_telegraphBorderMaterial, "emission_energy_multiplier", 8.0f, WindupFlashSeconds).SetDelay(pulseDelay);
            if (ranged)
            {
                // Aviso de travamento da mira.
                var lockDelay = Mathf.Max(windup - AimLockSeconds, 0.0f);
                tween.Parallel().TweenProperty(_telegraphBorderMaterial, "emission_energy_multiplier", 9.0f, 0.06f).SetDelay(lockDelay);
                tween.Parallel().TweenProperty(_telegraphBorderMaterial, "emission_energy_multiplier", 3.4f, 0.09f).SetDelay(lockDelay + 0.06f);
            }
        }

        // Anticipation do corpo geometrico: agacha/comprime ate o golpe.
        if (_visual != null && _importedVisual == null)
        {
            _windupBodyBaseScale = _visual.Scale;
            _windupBodyBasePositionY = _visual.Position.Y;
            _windupBodyTween = CreateTween();
            _windupBodyTween.TweenProperty(_visual, "scale", _windupBodyBaseScale * new Vector3(1.14f, 0.82f, 1.14f), windup * 0.9f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            // Mantem a base no piso enquanto comprime (centro do mesh desce junto com a altura).
            _windupBodyTween.Parallel().TweenProperty(_visual, "position:y", _windupBodyBasePositionY * 0.82f, windup * 0.9f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
    }

    private void HideAttackTelegraph()
    {
        _telegraphTween?.Kill();
        _telegraphTween = null;
        _aimTrackRemaining = 0.0f;
        if (_windupBodyTween != null)
        {
            _windupBodyTween.Kill();
            _windupBodyTween = null;
            if (_visual != null)
            {
                _visual.Scale = _windupBodyBaseScale;
                _visual.Position = new Vector3(_visual.Position.X, _windupBodyBasePositionY, _visual.Position.Z);
            }
        }

        if (_attackTelegraph != null)
            _attackTelegraph.Visible = false;

        if (_attackWarningLabel != null)
        {
            _attackWarningLabel.Visible = false;
            _attackWarningLabel.Scale = Vector3.One;
        }

        if (_visual != null && !_health.IsDead)
        {
            _visual.MaterialOverride = _baseMaterial;
        }
    }

    private async void SpawnAttackImpact()
    {
        var attackDepth = AttackRange + AttackDepthBonus;
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.22f, 0.03f, 0.82f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.04f, 0.0f),
            EmissionEnergyMultiplier = 3.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        if (Archetype == EnemyArchetype.Tank)
        {
            var shockwave = new MeshInstance3D
            {
                Name = "TankAttackShockwave",
                Mesh = new CylinderMesh { TopRadius = AttackWidth * 0.32f, BottomRadius = AttackWidth * 0.42f, Height = 0.07f, RadialSegments = 32 },
                MaterialOverride = material,
                Position = new Vector3(0.0f, 0.08f, -attackDepth * 0.45f)
            };
            AddChild(shockwave);
            var shockwaveTween = shockwave.CreateTween();
            shockwaveTween.TweenProperty(shockwave, "scale", new Vector3(2.2f, 1.0f, 2.2f), 0.16f);
            shockwaveTween.Parallel().TweenProperty(shockwave, "transparency", 1.0f, 0.18f);
            await ToSignal(shockwaveTween, Tween.SignalName.Finished);
            shockwave.QueueFree();
            return;
        }

        var slashRoot = new Node3D { Name = "MeleeAttackSlash" };
        AddChild(slashRoot);
        for (var index = 0; index < 5; index++)
        {
            var normalized = index / 4.0f;
            var streak = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.055f, 0.075f, attackDepth * (0.62f + normalized * 0.16f)) },
                MaterialOverride = material,
                Position = new Vector3(0.0f, 0.22f + normalized * 0.035f, -attackDepth * 0.42f),
                RotationDegrees = new Vector3(0.0f, Mathf.Lerp(-28.0f, 28.0f, normalized), Mathf.Lerp(-8.0f, 8.0f, normalized)),
                Scale = new Vector3(0.2f, 1.0f, 0.25f)
            };
            slashRoot.AddChild(streak);
            var streakTween = streak.CreateTween();
            streakTween.TweenProperty(streak, "scale", Vector3.One, 0.075f).SetDelay(index * 0.012f);
            streakTween.TweenProperty(streak, "transparency", 1.0f, 0.10f);
        }
        await Wait(0.2f);
        if (IsInstanceValid(slashRoot)) slashRoot.QueueFree();
    }

    private async void AnimateDamageNumber(Label3D label)
    {
        var start = label.Position;
        var tween = CreateTween();
        tween.TweenProperty(label, "position", start + Vector3.Up * 0.8f, 0.45f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.45f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }

    private async void AnimateAlertMarker(Label3D label)
    {
        var tween = CreateTween();
        tween.TweenProperty(label, "position", label.Position + Vector3.Up * 0.55f, 0.45f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.45f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}

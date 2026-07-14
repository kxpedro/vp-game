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
    [Export] public float AttackWidth { get; set; } = 1.25f;
    [Export] public float AttackDepthBonus { get; set; } = 0.75f;
    [Export] public float ContactDamage { get; set; } = 7.0f;
    [Export] public float ContactDamageRange { get; set; } = 0.95f;
    [Export] public float ContactDamageCooldown { get; set; } = 0.8f;
    [Export] public float KnockbackResistance { get; set; } = 0.45f;
    [Export] public int MinBloodCoinsReward { get; set; } = 1;
    [Export] public int MaxBloodCoinsReward { get; set; } = 10;
    [Export] public float FuryOnDeath { get; set; } = 12.0f;
    [Export] public NodePath HealthPath { get; set; } = new("Health");
    [Export] public NodePath VisualPath { get; set; } = new("Visual");

    private Node3D _player;
    private Health _health;
    private MeshInstance3D _visual;
    private bool _isAttacking;
    private bool _hasDetectedPlayer;
    private Vector3 _lockedAttackDirection = Vector3.Forward;
    private Vector3 _knockbackVelocity;
    private Vector3 _spawnPosition;
    private Vector3 _patrolTarget;
    private float _patrolRetargetTimer;
    private StandardMaterial3D _baseMaterial;
    private StandardMaterial3D _hitMaterial;
    private StandardMaterial3D _alertMaterial;
    private StandardMaterial3D _warningMaterial;
    private MeshInstance3D _healthBarBackground;
    private MeshInstance3D _healthBarFill;
    private MeshInstance3D _rarityIcon;
    private float _healthBarWidth = 1.55f;
    private Sprite3D _bleedingStatusIcon;
    private Sprite3D _pestilenceStatusIcon;
    private Sprite3D _charmedStatusIcon;
    private Label3D _attackWarningLabel;
    private MeshInstance3D _attackTelegraph;
    private float _contactDamageTimer;
    private float _alternatePathTimer;
    private int _avoidanceSide = 1;
    private float _slowTimer;
    private float _slowMultiplier = 1.0f;
    private float _fearTimer;
    private float _damageTakenBonus;
    private float _damageTakenTimer;
    public float DamageTakenMultiplier => 1.0f + _damageTakenBonus;
    public float HitTargetRadius { get; private set; } = 0.35f;
    private Node3D _importedVisual;
    private AnimationPlayer _importedAnimationPlayer;
    private string _importedIdleAnimation = "";
    private readonly List<Node> _ownedProjectiles = new();
    private int _hitFeedbackVersion;
    private float _hitStunTimer;
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
    private string _legendaryName = "";
    private float _bossActionAnimationTimer;
    public bool IsPestilent => _pestilenceStacks > 0;
    public bool IsBleeding => _bleedingStacks > 0;
    public bool IsPestilenceHost => _pestilenceStacks >= Mathf.RoundToInt(RunState.GetEffectParameter("DamageVsPlagueHost", "requiredStacks", 5));
    public bool IsCharmed => _permanentCharm || _charmTimer > 0.0f;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _spawnPosition = GlobalPosition;
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
        _alertMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.48f, 0.02f, 0.03f),
            EmissionEnabled = true,
            Emission = new Color(0.7f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.55f
        };
        _warningMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.02f, 0.0f, 0.48f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 1.9f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
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
            string[] titles = { "Vharos, o Faminto", "Morcant, o Profano", "Seraph, o Rubro", "Nerezza, a Imortal", "Vorigan, o Carrasco" };
            _legendaryName = titles[Mathf.Abs(Name.ToString().GetHashCode()) % titles.Length];
        }

        if (archetype == EnemyArchetype.Ranged)
        {
            AttackRange = 8.0f; AttackDamage *= 0.8f; MoveSpeed *= 0.9f;
            _visual.Scale = new Vector3(0.82f, 1.12f, 0.82f);
        }
        else if (archetype == EnemyArchetype.Tank)
        {
            _health?.SetMaxHealth(_health.MaxHealth * 1.8f, true);
            MoveSpeed *= 0.62f; AttackDamage *= 1.25f; KnockbackResistance *= 0.3f;
            _visual.Scale = Vector3.One * 1.35f;
        }

        UpdateRarityVisual(tuning);
        UpdateHealthBar(_health.CurrentHealth, _health.MaxHealth);
        if (_visual != null && archetype != EnemyArchetype.Melee)
        {
            _baseMaterial = new StandardMaterial3D
            {
                AlbedoColor = archetype == EnemyArchetype.Ranged ? new Color(0.18f, 0.22f, 0.58f) : new Color(0.34f, 0.25f, 0.16f),
                Roughness = 0.82f
            };
            _visual.MaterialOverride = _baseMaterial;
        }
    }

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
        PlayImportedAnimation(_importedVisual, definition.IdleAnimation);
        _importedIdleAnimation = definition.IdleAnimation;
        _importedAnimationPlayer = FindImportedAnimationPlayer(_importedVisual);
        if (_importedAnimationPlayer != null)
            _importedAnimationPlayer.AnimationFinished += OnImportedAnimationFinished;
    }

    public void PlayBossMoveAnimation(string moveId, int phase)
    {
        if (_importedAnimationPlayer == null || !Name.ToString().Contains("Boss")) return;
        var secondBoss = RunState.CurrentMapIndex == 1;
        var animation = secondBoss
            ? moveId switch
            {
                "psionic" => phase >= 2 ? "105031_PsionicStrike_02" : "105031_PsionicStrike_01",
                "shield" => "105041_Shield",
                "outbreak" => "105051_Outbreak",
                _ => "105011_Primary_01"
            }
            : moveId switch
            {
                "claw" => "Mon_BlackDragon31_Btl_Atk01",
                "tail_sweep" => "Mon_BlackDragon31_Btl_Skl03",
                "dragon_burst" => phase >= 3 ? "Mon_BlackDragon31_Btl_Skl08" : "Mon_BlackDragon31_Btl_Skl07",
                _ => "Mon_BlackDragon31_Btl_Atk02"
            };
        if (!_importedAnimationPlayer.HasAnimation(animation)) return;
        _importedAnimationPlayer.Play(animation, 0.08, 1.25f + (phase - 1) * 0.12f);
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
        TickBlessingStatuses((float)delta);
        _bossActionAnimationTimer = Mathf.Max(_bossActionAnimationTimer - (float)delta, 0.0f);
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
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        TickContactDamage((float)delta, distance);

        if (_isAttacking)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        if (_knockbackVelocity.LengthSquared() > 0.01f)
        {
            Velocity = _knockbackVelocity;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, 18.0f * (float)delta);
            MoveAndSlide();
            return;
        }

        if (!_hasDetectedPlayer && distance <= DetectionRange && CanSeePlayer())
            DetectPlayer();

        if (!_hasDetectedPlayer)
        {
            Patrol((float)delta);
            return;
        }

        LookAt(GlobalPosition + toPlayer.Normalized(), Vector3.Up);

        if (distance <= AttackRange)
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

        var chaseDirection = (_fearTimer > 0 ? -toPlayer : toPlayer).Normalized();
        if (_alternatePathTimer > 0)
        {
            _alternatePathTimer -= (float)delta;
            chaseDirection = new Vector3(-chaseDirection.Z * _avoidanceSide, 0, chaseDirection.X * _avoidanceSide).Normalized();
        }
        Velocity = chaseDirection * MoveSpeed * _slowMultiplier;
        PlayBossLocomotionAnimation(true);
        MoveAndSlide();
        if (GetSlideCollisionCount() > 0 && _alternatePathTimer <= 0)
        {
            _avoidanceSide *= -1;
            _alternatePathTimer = 0.65f;
        }
    }

    public void ApplyTimedSlow(float multiplier, float seconds) { _slowMultiplier = Mathf.Clamp(multiplier, 0.05f, 1); _slowTimer = Mathf.Max(_slowTimer, seconds); }
    public void ApplyFear(float seconds) { _fearTimer = Mathf.Max(_fearTimer, seconds); }
    public void ApplyDamageTakenDebuff(float bonus, float seconds) { _damageTakenBonus = Mathf.Max(_damageTakenBonus, bonus); _damageTakenTimer = Mathf.Max(_damageTakenTimer, seconds); }
    public void RefreshPlayerHitStatuses()
    {
        if (IsPestilent) _pestilenceTimer = 3.0f;
        if (IsBleeding) _bleedingTimer = 2.0f;
    }
    public void ApplyPestilence(float seconds)
    {
        _pestilenceStacks = Mathf.Min(_pestilenceStacks + 1, 99);
        _pestilenceTimer = 3.0f;
        UpdateStatusIcons();
    }

    public void ApplyBleeding(float seconds, float damagePerSecond, int maxStacks)
    {
        _bleedingStacks = Mathf.Min(_bleedingStacks + 1, Mathf.Max(maxStacks, 1));
        _bleedingTimer = 2.0f;
        _bleedingDamagePerSecond = Mathf.Max(_bleedingDamagePerSecond, damagePerSecond);
        UpdateStatusIcons();
    }

    public void ApplyCharm(float seconds, bool permanent)
    {
        _permanentCharm |= permanent;
        _charmTimer = Mathf.Max(_charmTimer, seconds);
        AddToGroup("charmed_enemies");
        UpdateStatusIcons();
    }

    private void TickBlessingStatuses(float delta)
    {
        if (!_permanentCharm) _charmTimer = Mathf.Max(_charmTimer - delta, 0.0f);
        if (!IsCharmed && IsInGroup("charmed_enemies")) RemoveFromGroup("charmed_enemies");
        if (_pestilenceStacks > 0)
        {
            _pestilenceTimer -= delta;
            _pestilenceTickTimer -= delta;
            if (_pestilenceTickTimer <= 0)
            {
                _pestilenceTickTimer = 1.0f;
                if (RunState.GetEffectTotal("InfinitePestilenceSpread.Flat") > 0)
                    foreach (var node in GetTree().GetNodesInGroup("enemies"))
                        if (node is EnemyBasic enemy && enemy != this && !enemy.IsPestilent && enemy.GlobalPosition.DistanceTo(GlobalPosition) <= 6) { enemy.ApplyPestilence(Mathf.Max(_pestilenceTimer, 3)); break; }
            }
            if (_pestilenceTimer <= 0) _pestilenceStacks = 0;
        }

        if (_bleedingStacks > 0)
        {
            _bleedingTimer -= delta;
            _bleedingTickTimer -= delta;
            if (_bleedingTickTimer <= 0)
            {
                _bleedingTickTimer = 1.0f;
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
        MoveAndSlide();
    }

    private void TickContactDamage(float delta, float distanceToPlayer)
    {
        _contactDamageTimer = Mathf.Max(_contactDamageTimer - delta, 0.0f);

        if (_contactDamageTimer > 0.0f || distanceToPlayer > ContactDamageRange)
            return;

        _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(ContactDamage, false, GetDisplayName());
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

        if (_visual != null)
            _visual.MaterialOverride = _alertMaterial;

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

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.001f)
            return;

        _knockbackVelocity = direction.Normalized() * force * KnockbackResistance;
    }

    private async void BeginAttack()
    {
        if (_isAttacking)
            return;

        _isAttacking = true;
        LockAttackDirection();
        ShowAttackTelegraph();
        if (Archetype == EnemyArchetype.Ranged)
            SpawnProjectileVisual();
        await Wait(AttackWindup);
        HideAttackTelegraph();

        if (!_health.IsDead && _player != null && IsPlayerInsideLockedAttack() && (Archetype != EnemyArchetype.Ranged || CanSeePlayer()))
        {
            SpawnAttackImpact();
            var normalPenalty = Rarity == EnemyRarity.Normal ? 1.0f + RunState.GetEffectTotal("DamageTakenFromNormal.Multiplier") : 1.0f;
            var infectedPenalty = IsPestilent ? 1.0f + RunState.GetEffectTotal("InfectedEnemyDamage.Multiplier") : 1.0f;
            _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage * normalPenalty * Mathf.Max(infectedPenalty, 0.1f), false, GetDisplayName());
            if (Rarity == EnemyRarity.Legendary)
            {
                await Wait(0.16f);
                if (!_health.IsDead && IsPlayerInsideLockedAttack())
                    _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage * 0.55f, false, GetDisplayName());
            }
        }

        await Wait(AttackCooldown);
        _isAttacking = false;
    }

    public string GetDisplayName()
    {
        if (Name.ToString().Contains("Boss")) return $"Chefe de {RunState.CurrentMap.Name}";
        if (!string.IsNullOrWhiteSpace(_legendaryName)) return _legendaryName;
        return $"{EnemyTuningCatalog.Get(Rarity).DisplayName} {Archetype}";
    }

    public void ApplyBossMoveTuning(BossMoveDefinition move, int phase)
    {
        AttackCooldown = Mathf.Max(move.Cooldown / (1.0f + (phase - 1) * 0.12f), 0.35f);
        var bossBaseDamage = RunState.CurrentMapIndex == 1 ? 40.5f : 30.0f;
        AttackDamage = Mathf.Max(bossBaseDamage * move.DamageMultiplier * (1.0f + (phase - 1) * 0.18f), 1.0f);
        AttackRange = Mathf.Max(2.25f * move.RangeMultiplier, 1.0f);
        AttackWidth = Mathf.Max(1.5f * move.RangeMultiplier, 1.0f);
    }

    private async void SpawnProjectileVisual()
    {
        if (_player == null || !CanSeePlayer()) return;
        var origin = GlobalPosition + Vector3.Up * 1.1f;
        var destination = _player.GlobalPosition + Vector3.Up * 0.8f;
        var query = PhysicsRayQueryParameters3D.Create(origin, destination);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (collision.Count > 0 && collision.TryGetValue("position", out var hitPosition))
            destination = hitPosition.AsVector3();

        var projectile = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.15f, 0.42f, 1), EmissionEnabled = true,
                Emission = new Color(0.08f, 0.25f, 1), EmissionEnergyMultiplier = 2.2f
            }
        };
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = origin;
        projectile.AddToGroup("enemy_projectiles");
        _ownedProjectiles.Add(projectile);
        CleanupProjectileAfter(projectile, 2.0f);
        var tween = projectile.CreateTween();
        tween.TweenProperty(projectile, "global_position", destination, Mathf.Max(AttackWindup, 0.1f));
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(projectile)) projectile.QueueFree();
    }

    private async void CleanupProjectileAfter(Node projectile, float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(projectile)) projectile.QueueFree();
    }

    private void OnDied()
    {
        foreach (var projectile in _ownedProjectiles)
            if (IsInstanceValid(projectile)) projectile.QueueFree();
        _ownedProjectiles.Clear();
        if (!RunState.IsBossRoom) SpawnBloodEssence();
        GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime")?.OnEnemyKilled(this);
        GetTree().CallGroup("run_manager", "QueueEnemyDefeat", RollBloodCoinsReward());
        GetTree().CallGroup("player_skills", "OnEnemyDefeated");
        RunState.NotifyEnemyKilled();

        var combat = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        combat?.AddFury(FuryOnDeath);

        SetPhysicsProcess(false);
        QueueFree();
    }

    private void SpawnBloodEssence()
    {
        var essence = new MeshInstance3D
        {
            Name = "BloodEssence",
            Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.01f, 0.04f), EmissionEnabled = true,
                Emission = new Color(1, 0, 0), EmissionEnergyMultiplier = 2.8f
            },
            Position = GlobalPosition + Vector3.Up * 0.65f
        };
        GetTree().CurrentScene.AddChild(essence);
        essence.AddToGroup("blood_essences");
        var tween = essence.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(essence, "position:y", essence.Position.Y + 0.22f, 0.45f);
        tween.TweenProperty(essence, "position:y", essence.Position.Y, 0.45f);
    }

    private int RollBloodCoinsReward()
    {
        var minReward = Mathf.Min(MinBloodCoinsReward, MaxBloodCoinsReward);
        var maxReward = Mathf.Max(MinBloodCoinsReward, MaxBloodCoinsReward);
        var rarityMultiplier = Rarity switch
        {
            EnemyRarity.Magic => 2.0f,
            EnemyRarity.Rare => 4.0f,
            EnemyRarity.Legendary => 8.0f,
            _ => 1.0f
        };
        return Mathf.RoundToInt((int)GD.RandRange(minReward, maxReward + 1) * rarityMultiplier);
    }

    private async void OnDamaged(float amount)
    {
        if (amount <= 0) { SpawnAlertMarker(); return; }
        if (!_isAttacking)
        {
            var stun = Archetype switch
            {
                EnemyArchetype.Tank => 0.035f,
                EnemyArchetype.Ranged => 0.095f,
                _ => 0.075f
            };
            if (Name.ToString().Contains("Boss")) stun = 0.025f;
            if (Rarity == EnemyRarity.Legendary) stun *= 0.55f;
            _hitStunTimer = Mathf.Max(_hitStunTimer, stun);
        }
        SpawnDamageNumber(amount);
        SpawnHitImpact();
        FlashImportedVisual();

        if (_visual != null)
            _visual.MaterialOverride = _hitMaterial;

        await Wait(0.08f);

        if (_visual != null && !_health.IsDead)
        {
            if (_hasDetectedPlayer)
                _visual.MaterialOverride = _alertMaterial;
            else
                _visual.MaterialOverride = _baseMaterial;
        }
    }

    private async void FlashImportedVisual()
    {
        if (_importedVisual == null) return;
        var version = ++_hitFeedbackVersion;
        var originals = new Dictionary<MeshInstance3D, Material>();
        foreach (var node in _importedVisual.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh) continue;
            originals[mesh] = mesh.MaterialOverride;
            mesh.MaterialOverride = _hitMaterial;
        }
        var originalScale = _importedVisual.Scale;
        _importedVisual.Scale = new Vector3(originalScale.X * 1.08f, originalScale.Y * 0.92f, originalScale.Z * 1.08f);
        var tween = _importedVisual.CreateTween();
        tween.TweenProperty(_importedVisual, "scale", originalScale, 0.11f);
        await Wait(0.09f);
        if (version != _hitFeedbackVersion || _health.IsDead) return;
        foreach (var pair in originals)
            if (IsInstanceValid(pair.Key)) pair.Key.MaterialOverride = pair.Value;
    }

    private async void SpawnHitImpact()
    {
        var impact = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 0.82f, 0.3f, 0.82f), EmissionEnabled = true,
                Emission = new Color(1, 0.3f, 0.04f), EmissionEnergyMultiplier = 3.0f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            },
            Position = Vector3.Up * 1.0f
        };
        AddChild(impact);
        var tween = impact.CreateTween();
        tween.TweenProperty(impact, "scale", Vector3.One * 1.8f, 0.08f);
        tween.Parallel().TweenProperty(impact, "transparency", 1.0f, 0.14f);
        await ToSignal(tween, Tween.SignalName.Finished);
        impact.QueueFree();
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealthBar(currentHealth, maxHealth);
    }

    private void CreateHealthBar()
    {
        _healthBarBackground = new MeshInstance3D
        {
            Name = "EnemyHealthBarBackground",
            Mesh = new QuadMesh { Size = new Vector2(_healthBarWidth + 0.08f, 0.2f) },
            MaterialOverride = CreateBillboardMaterial(new Color(0.025f, 0.01f, 0.012f, 0.96f), 1),
            Position = new Vector3(0.0f, 2.12f, 0.0f)
        };
        AddChild(_healthBarBackground);
        _healthBarFill = new MeshInstance3D
        {
            Name = "EnemyHealthBarFill",
            Mesh = new QuadMesh { Size = new Vector2(_healthBarWidth, 0.13f) },
            MaterialOverride = CreateBillboardMaterial(new Color(0.82f, 0.025f, 0.045f), 2),
            Position = new Vector3(0.0f, 2.12f, 0.012f)
        };
        AddChild(_healthBarFill);
        _rarityIcon = new MeshInstance3D
        {
            Name = "EnemyRarityIcon",
            Mesh = new QuadMesh { Size = new Vector2(0.18f, 0.18f) },
            MaterialOverride = CreateBillboardMaterial(Colors.White, 3),
            Position = new Vector3(-_healthBarWidth * 0.5f - 0.18f, 2.12f, 0.018f),
            Rotation = new Vector3(0.0f, 0.0f, Mathf.Pi * 0.25f)
        };
        AddChild(_rarityIcon);
    }

    private static StandardMaterial3D CreateBillboardMaterial(Color color, int renderPriority = 0) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        Transparency = color.A < 1.0f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        NoDepthTest = true,
        RenderPriority = renderPriority
    };

    private void CreateStatusIcons()
    {
        _bleedingStatusIcon = CreateStatusIcon("BleedingStatusIcon", "res://images/effects/bleeding.png", -0.22f);
        _pestilenceStatusIcon = CreateStatusIcon("PestilenceStatusIcon", "res://images/effects/pestilence.png", 0.0f);
        _charmedStatusIcon = CreateStatusIcon("CharmedStatusIcon", "res://images/effects/enchanted.png", 0.22f);
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
            Position = new Vector3(xPosition * 1.45f, 2.43f, 0.0f),
            Visible = false
        };
        AddChild(icon);
        return icon;
    }

    private void UpdateStatusIcons()
    {
        if (_bleedingStatusIcon != null) _bleedingStatusIcon.Visible = IsBleeding;
        if (_pestilenceStatusIcon != null) _pestilenceStatusIcon.Visible = IsPestilent;
        if (_charmedStatusIcon != null) _charmedStatusIcon.Visible = IsCharmed;
    }

    private void CreateAttackTelegraph()
    {
        var attackDepth = AttackRange + AttackDepthBonus;
        _attackTelegraph = new MeshInstance3D
        {
            Name = "AttackTelegraph",
            Mesh = new BoxMesh
            {
                Size = new Vector3(AttackWidth, 0.035f, attackDepth)
            },
            MaterialOverride = _warningMaterial,
            Position = new Vector3(0.0f, 0.035f, -0.35f - attackDepth * 0.5f),
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
    }

    private void UpdateRarityVisual(EnemyRarityDefinition tuning)
    {
        _healthBarWidth = Rarity == EnemyRarity.Legendary ? 2.15f : 1.55f;
        if (_healthBarBackground?.Mesh is QuadMesh background) background.Size = new Vector2(_healthBarWidth + 0.08f, Rarity == EnemyRarity.Legendary ? 0.25f : 0.2f);
        if (_healthBarFill?.Mesh is QuadMesh fill) fill.Size = new Vector2(_healthBarWidth, Rarity == EnemyRarity.Legendary ? 0.17f : 0.13f);
        if (_rarityIcon != null)
        {
            _rarityIcon.Position = new Vector3(-_healthBarWidth * 0.5f - 0.18f, 2.12f, 0.018f);
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
        _healthBarFill.Position = new Vector3(-_healthBarWidth * (1.0f - ratio) * 0.5f, 2.12f, 0.012f);
    }

    public void SetOverheadHealthVisible(bool visible)
    {
        if (_healthBarBackground != null) _healthBarBackground.Visible = visible;
        if (_healthBarFill != null) _healthBarFill.Visible = visible;
        if (_rarityIcon != null) _rarityIcon.Visible = visible;
    }

    private void PlayBossLocomotionAnimation(bool moving)
    {
        if (_importedAnimationPlayer == null || !Name.ToString().Contains("Boss") || _bossActionAnimationTimer > 0.0f) return;
        var animation = RunState.CurrentMapIndex == 1
            ? (moving ? "Run_Fwd_C" : "Idle_C")
            : (moving ? "Mon_BlackDragon31_Btl_Run01" : "Mon_BlackDragon31_Btl_Std01");
        if (_importedAnimationPlayer.HasAnimation(animation) && _importedAnimationPlayer.CurrentAnimation != animation)
            _importedAnimationPlayer.Play(animation, 0.12, RunState.CurrentMapIndex == 1 ? 1.35f : 1.05f);
    }

    private void SpawnDamageNumber(float amount)
    {
        var isCritical = _health?.LastDamageWasCritical == true;
        var label = new Label3D
        {
            Text = Mathf.RoundToInt(amount).ToString(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = isCritical ? 108 : 76,
            Modulate = isCritical ? new Color(1.0f, 0.82f, 0.08f) : Colors.White,
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
        if (_attackTelegraph == null || _attackWarningLabel == null)
            return;

        _attackTelegraph.Visible = true;
        _attackTelegraph.Scale = new Vector3(0.45f, 1.0f, 0.35f);
        _attackWarningLabel.Visible = true;

        if (_visual != null)
            _visual.MaterialOverride = _warningMaterial;

        var tween = CreateTween();
        tween.TweenProperty(_attackTelegraph, "scale", Vector3.One, AttackWindup);
        tween.Parallel().TweenProperty(_attackWarningLabel, "scale", Vector3.One * 1.25f, AttackWindup * 0.5f);
        tween.TweenProperty(_attackWarningLabel, "scale", Vector3.One, AttackWindup * 0.5f);
    }

    private void HideAttackTelegraph()
    {
        if (_attackTelegraph != null)
            _attackTelegraph.Visible = false;

        if (_attackWarningLabel != null)
        {
            _attackWarningLabel.Visible = false;
            _attackWarningLabel.Scale = Vector3.One;
        }

        if (_visual != null && !_health.IsDead)
        {
            if (_hasDetectedPlayer)
                _visual.MaterialOverride = _alertMaterial;
            else
                _visual.MaterialOverride = _baseMaterial;
        }
    }

    private async void SpawnAttackImpact()
    {
        var attackDepth = AttackRange + AttackDepthBonus;
        var impact = new MeshInstance3D
        {
            Name = "AttackImpact",
            Mesh = new BoxMesh
            {
                Size = new Vector3(AttackWidth + 0.2f, 0.06f, attackDepth + 0.1f)
            },
            MaterialOverride = _warningMaterial,
            Position = new Vector3(0.0f, 0.08f, -0.35f - attackDepth * 0.5f)
        };
        AddChild(impact);

        var tween = impact.CreateTween();
        tween.TweenProperty(impact, "scale", new Vector3(1.18f, 1.0f, 1.12f), 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
        impact.QueueFree();
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

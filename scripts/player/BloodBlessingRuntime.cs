using Godot;
using System.Collections.Generic;

public partial class BloodBlessingRuntime : Node
{
    [Signal]
    public delegate void BarrierChangedEventHandler(float current, float maximum);

    [Export] public float BarrierDecayPerSecond { get; set; } = 0.25f;
    [Export] public float BarrierDecayDelaySeconds { get; set; } = 8.0f;

    private Health _health;
    private float _barrier;
    private float _barrierDecayDelayTimer;
    private float _damageAfterHealTimer;
    private float _damageReductionAfterHealTimer;
    private float _attackSpeedAfterHealTimer;
    private float _criticalAfterDashTimer;
    private float _poolHealTimer;
    private float _bloodPoolBleedTimer;
    private int _recoverableDamageVersion;
    private bool _fatalCharmSacrificeUsed;
    private int _trackedMapIndex;
    private int _trackedRoom;

    public bool HasBarrier => _barrier > 0.01f;
    public float CurrentBarrier => _barrier;
    public float MaximumBarrier => _health == null || RunState.GetEffectTotal("OverhealToBarrier.Multiplier") <= 0.0f
        ? 0.0f
        : _health.MaxHealth * RunState.GetEffectParameter("OverhealToBarrier", "maxHealthCap", 0.15f);
    public bool IsOnBlood { get; private set; }

    public override void _Ready()
    {
        _health = GetParent()?.GetNodeOrNull<Health>("Health");
        _trackedMapIndex = RunState.CurrentMapIndex;
        _trackedRoom = RunState.CurrentRoom;
        ResetBarrier();
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _damageAfterHealTimer = Mathf.Max(_damageAfterHealTimer - d, 0);
        _damageReductionAfterHealTimer = Mathf.Max(_damageReductionAfterHealTimer - d, 0);
        _attackSpeedAfterHealTimer = Mathf.Max(_attackSpeedAfterHealTimer - d, 0);
        _criticalAfterDashTimer = Mathf.Max(_criticalAfterDashTimer - d, 0);
        if (_trackedMapIndex != RunState.CurrentMapIndex || _trackedRoom != RunState.CurrentRoom)
        {
            _trackedMapIndex = RunState.CurrentMapIndex;
            _trackedRoom = RunState.CurrentRoom;
            ResetBarrier();
        }
        TickBarrierDecay(d);
        UpdateBloodPoolState(d);
        UpdateBloodPoolBleeding(d);
    }

    public void ResetBarrier()
    {
        _barrier = 0.0f;
        _barrierDecayDelayTimer = 0.0f;
        EmitSignal(SignalName.BarrierChanged, _barrier, MaximumBarrier);
    }

    private void TickBarrierDecay(float delta)
    {
        if (_barrier <= 0.0f)
            return;

        _barrierDecayDelayTimer = Mathf.Max(_barrierDecayDelayTimer - delta, 0.0f);
        if (_barrierDecayDelayTimer > 0.0f)
            return;

        var decayPerSecond = RunState.GetEffectParameter(
            "OverhealToBarrier",
            "decayPerSecond",
            BarrierDecayPerSecond);
        if (decayPerSecond <= 0.0f)
            return;

        var previous = _barrier;
        _barrier = Mathf.Max(_barrier - decayPerSecond * delta, 0.0f);
        if (!Mathf.IsEqualApprox(previous, _barrier))
            EmitSignal(SignalName.BarrierChanged, _barrier, MaximumBarrier);
    }

    private void RefreshBarrierDecayDelay()
    {
        _barrierDecayDelayTimer = RunState.GetEffectParameter(
            "OverhealToBarrier",
            "decayDelay",
            BarrierDecayDelaySeconds);
    }

    public float ModifyOutgoingDamage(float damage, EnemyBasic enemy, PlayerAttackType attackType)
    {
        var bonus = RunState.BlessingPermanentDamage + RunState.BlessingRoomDamage;
        if (attackType == PlayerAttackType.Basic) bonus += RunState.GetEffectTotal("BasicAttackDamage.Multiplier");
        if (attackType == PlayerAttackType.Quick) bonus += RunState.GetEffectTotal("QuickAttackDamage.Multiplier");
        if (attackType == PlayerAttackType.Special) bonus += RunState.GetEffectTotal("SpecialAttackDamage.Multiplier");
        var healthRatio = _health?.MaxHealth > 0 ? _health.CurrentHealth / _health.MaxHealth : 0;
        if (healthRatio >= RunState.GetEffectParameter("DamageAboveHealth", "threshold", 0.8f)) bonus += RunState.GetEffectTotal("DamageAboveHealth.Multiplier");
        if (_damageAfterHealTimer > 0) bonus += RunState.GetEffectTotal("DamageAfterHealing.Multiplier");
        if (HasBarrier) bonus += RunState.GetEffectTotal("DamageWhileBarrier.Multiplier");
        if (IsOnBlood) bonus += RunState.GetEffectTotal("DamageOnBlood.Multiplier");
        if (enemy?.IsPestilenceHost == true) bonus += RunState.GetEffectTotal("DamageVsPlagueHost.Multiplier");
        if (enemy?.IsFascinated == true) bonus += RunState.GetEffectTotal("FascinationDamageTaken.Multiplier");
        var charmedCount = Mathf.Min(GetTree().GetNodeCountInGroup("charmed_enemies"), Mathf.RoundToInt(RunState.GetEffectParameter("DamagePerCharmedEnemy", "maxTargets", 5)));
        bonus += charmedCount * RunState.GetEffectTotal("DamagePerCharmedEnemy.Multiplier");
        return damage * Mathf.Max(1.0f + bonus, 0.0f);
    }

    public void OnSpecialAttack(Vector3 position, float range, WeaponAttackShape shape)
    {
        if (RunState.GetEffectTotal("SpecialAttackBloodPool.Flat") <= 0.0f)
            return;
        var radius = shape == WeaponAttackShape.Radial ? range : Mathf.Max(range * 0.55f, 1.2f);
        var duration = RunState.GetEffectParameter("SpecialAttackBloodPool", "duration", 5.0f);
        SpawnBloodPool(position, radius, duration);
    }

    public float BonusCriticalChance()
    {
        var bonus = 0.0f;
        if (_health != null && Mathf.IsEqualApprox(_health.CurrentHealth, _health.MaxHealth)) bonus += RunState.GetEffectTotal("CriticalChanceAtFullHealth.Flat");
        if (_criticalAfterDashTimer > 0) bonus += RunState.GetEffectTotal("CriticalChanceAfterDash.Flat");
        return bonus;
    }

    public float BonusAttackSpeed() => _attackSpeedAfterHealTimer > 0 ? RunState.GetEffectTotal("AttackSpeedAfterHighHealthHeal.Multiplier") : 0.0f;

    public float MovementMultiplier()
    {
        var bonus = IsOnBlood ? RunState.GetEffectTotal("MoveSpeedOnBlood.Multiplier") : 0.0f;
        var maxTargets = Mathf.RoundToInt(RunState.GetEffectParameter("MoveSpeedPerCharmedEnemy", "maxTargets", 5));
        bonus += Mathf.Min(GetTree().GetNodeCountInGroup("charmed_enemies"), maxTargets) * RunState.GetEffectTotal("MoveSpeedPerCharmedEnemy.Multiplier");
        return 1.0f + bonus;
    }

    public float AbsorbBarrierAndReduceDamage(float amount)
    {
        var reduction = 0.0f;
        if (_damageReductionAfterHealTimer > 0) reduction += RunState.GetEffectTotal("DamageReductionAfterHealing.Multiplier");
        if (IsOnBlood) reduction += RunState.GetEffectTotal("DamageReductionOnBlood.Multiplier");
        if (HasNearbyEnemy()) reduction += Mathf.Max(-RunState.GetEffectTotal("NearbyEnemyDamage.Multiplier"), 0.0f);
        amount *= Mathf.Clamp(1.0f - reduction, 0.1f, 1.0f);
        if (_barrier <= 0) return amount;
        var absorbed = Mathf.Min(_barrier, amount);
        _barrier -= absorbed;
        if (_barrier > 0.0f)
            RefreshBarrierDecayDelay();
        EmitSignal(SignalName.BarrierChanged, _barrier, MaximumBarrier);
        return amount - absorbed;
    }

    public bool ProtectMinimumHealth(float incomingDamage)
    {
        return IsOnBlood && RunState.GetEffectTotal("MinimumHealthOnBlood.Flat") > 0 && _health != null && incomingDamage >= _health.CurrentHealth;
    }

    public void OnHealed(float requested, float actual, float overheal)
    {
        if (actual > 0)
        {
            _damageAfterHealTimer = Mathf.Max(_damageAfterHealTimer, RunState.GetEffectParameter("DamageAfterHealing", "duration", 5) * TemporaryEffectDurationMultiplier());
            _damageReductionAfterHealTimer = Mathf.Max(_damageReductionAfterHealTimer, RunState.GetEffectParameter("DamageReductionAfterHealing", "duration", 4) * TemporaryEffectDurationMultiplier());
            var threshold = RunState.GetEffectParameter("AttackSpeedAfterHighHealthHeal", "healthThreshold", 0.9f);
            if (_health != null && _health.CurrentHealth / _health.MaxHealth >= threshold)
                _attackSpeedAfterHealTimer = Mathf.Max(_attackSpeedAfterHealTimer, RunState.GetEffectParameter("AttackSpeedAfterHighHealthHeal", "duration", 5) * TemporaryEffectDurationMultiplier());
        }
        if (overheal <= 0 || _health == null) return;
        var barrierRatio = RunState.GetEffectTotal("OverhealToBarrier.Multiplier");
        var barrierCap = _health.MaxHealth * RunState.GetEffectParameter("OverhealToBarrier", "maxHealthCap", 0.15f);
        var previousBarrier = _barrier;
        _barrier = Mathf.Min(_barrier + overheal * barrierRatio, barrierCap);
        if (_barrier > previousBarrier)
            RefreshBarrierDecayDelay();
        EmitSignal(SignalName.BarrierChanged, _barrier, barrierCap);
        var permanentRate = RunState.GetEffectTotal("PermanentDamageFromOverheal.Multiplier");
        var unit = RunState.GetEffectParameter("PermanentDamageFromOverheal", "perOverhealMaxHealthPercent", 0.01f);
        if (permanentRate > 0 && unit > 0) RunState.AddPermanentBlessingDamage(permanentRate * (overheal / _health.MaxHealth) / unit);
    }

    public void OnDodged()
    {
        _criticalAfterDashTimer = Mathf.Max(_criticalAfterDashTimer, RunState.GetEffectParameter("CriticalChanceAfterDash", "duration", 2) * TemporaryEffectDurationMultiplier());
    }

    public async void OnPlayerDamaged(float amount)
    {
        var recoverable = amount * RunState.GetEffectTotal("DamageToRecoverableHealth.Multiplier");
        if (recoverable <= 0 || _health == null) return;
        var version = ++_recoverableDamageVersion;
        var delay = RunState.GetEffectParameter("DamageToRecoverableHealth", "recoveryDelay", 2);
        await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        if (version != _recoverableDamageVersion || _health.IsDead) return;
        var duration = Mathf.Max(RunState.GetEffectParameter("DamageToRecoverableHealth", "duration", 5), 0.1f);
        const float recoveryStep = 0.25f;
        var steps = Mathf.Max(Mathf.CeilToInt(duration / recoveryStep), 1);
        var healingPerStep = recoverable / steps;
        for (var step = 0; step < steps; step++)
        {
            await ToSignal(GetTree().CreateTimer(recoveryStep), SceneTreeTimer.SignalName.Timeout);
            if (version != _recoverableDamageVersion || _health.IsDead) return;
            _health.Heal(healingPerStep);
        }
    }

    public bool TrySacrificeCharmedOnFatal(out float restoredHealth)
    {
        restoredHealth = 0.0f;
        if (_fatalCharmSacrificeUsed || RunState.GetEffectTotal("SacrificeCharmedOnFatalDamage.Flat") <= 0 || _health == null) return false;
        EnemyBasic sacrifice = null;
        var highestHealth = 0.0f;
        foreach (var node in GetTree().GetNodesInGroup("charmed_enemies"))
        {
            if (node is not EnemyBasic enemy) continue;
            var health = enemy.GetNodeOrNull<Health>("Health");
            if (health != null && health.CurrentHealth > highestHealth) { highestHealth = health.CurrentHealth; sacrifice = enemy; }
        }
        if (sacrifice == null) return false;
        _fatalCharmSacrificeUsed = true;
        sacrifice.GetNodeOrNull<Health>("Health")?.ApplyDamage(999999);
        restoredHealth = _health.MaxHealth * Mathf.Clamp(RunState.GetEffectTotal("SacrificeCharmedOnFatalDamage.Flat"), 0.01f, 1.0f);
        return true;
    }

    public void OnPlayerHit(EnemyBasic enemy, float damage, bool critical, PlayerAttackType attackType)
    {
        if (enemy == null || _health == null) return;
        var isBasicAttack = attackType == PlayerAttackType.Basic;
        var isQuickAttack = attackType == PlayerAttackType.Quick;
        var isSpecialAttack = attackType == PlayerAttackType.Special;
        enemy.RefreshPlayerHitStatuses();
        var lifeSteal = RunState.GetEffectTotal("LifeSteal.Multiplier");
        if (isBasicAttack) lifeSteal += RunState.GetEffectTotal("BasicAttackLifeSteal.Multiplier");
        if (isQuickAttack) lifeSteal += RunState.GetEffectTotal("QuickAttackLifeSteal.Multiplier");
        if (isSpecialAttack) lifeSteal += RunState.GetEffectTotal("SpecialAttackLifeSteal.Multiplier");
        if (enemy.IsBleeding) lifeSteal += RunState.GetEffectTotal("LifeStealVsBleeding.Multiplier");
        var enemyHealth = enemy.GetNodeOrNull<Health>("Health");
        var threshold = RunState.GetEffectParameter("LifeStealVsLowHealthEnemy", "threshold", 0.25f);
        if (enemyHealth != null && enemyHealth.CurrentHealth / enemyHealth.MaxHealth <= threshold) lifeSteal += RunState.GetEffectTotal("LifeStealVsLowHealthEnemy.Multiplier");
        if (lifeSteal > 0 && _health.CurrentHealth < _health.MaxHealth)
        {
            _health.Heal(damage * lifeSteal);
            SpawnLifeStealTrail(enemy.GlobalPosition, GetParent<Node3D>().GlobalPosition);
        }

        if (critical && GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("CharmOnCriticalChance.Flat"), 0, 1))
            enemy.ApplyCharm(GetCharmDuration("CharmOnCriticalChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("PestilenceChance.Flat"), 0, 1))
            enemy.ApplyPestilence(RunState.GetEffectParameter("PestilenceChance", "duration", 5));

        if (isBasicAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("BasicAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("BasicAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
            if (RunState.GetEffectTotal("BasicAttackPestilence.Flat") > 0)
                enemy.ApplyPestilence(RunState.GetEffectParameter("BasicAttackPestilence", "duration", 5));
            if (RunState.GetEffectTotal("BasicAttackBleed.Flat") > 0)
                enemy.ApplyBleeding(
                    RunState.GetEffectParameter("BasicAttackBleed", "duration", 5),
                    RunState.GetEffectParameter("BasicAttackBleed", "damagePerSecond", 6),
                    Mathf.RoundToInt(RunState.GetEffectParameter("BasicAttackBleed", "maxStacks", 5)));
        }
        else if (isQuickAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("QuickAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackBleedChance.Flat"), 0, 1))
                enemy.ApplyBleeding(
                    RunState.GetEffectParameter("QuickAttackBleedChance", "duration", 4),
                    RunState.GetEffectParameter("QuickAttackBleedChance", "damagePerSecond", 6),
                    Mathf.RoundToInt(RunState.GetEffectParameter("QuickAttackBleedChance", "maxStacks", 3)));
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackPestilenceChance.Flat"), 0, 1))
                enemy.ApplyPestilence(RunState.GetEffectParameter("QuickAttackPestilenceChance", "duration", 5));
        }
        else if (isSpecialAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("SpecialAttackPestilenceChance.Flat"), 0, 1))
                enemy.ApplyPestilence(RunState.GetEffectParameter("SpecialAttackPestilenceChance", "duration", 6));
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("SpecialAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("SpecialAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        }

        if (enemy.IsPestilenceHost && RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat") > 0)
            SpreadPestilence(
                enemy,
                Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat")),
                RunState.GetEffectParameter("PestilenceSpreadOnHostHit", "radius", 4.5f),
                allowExistingStacks: true);
        TrySpawnPestilenceCloud(enemy);

        if (enemy.IsCharmed && RunState.GetEffectTotal("CharmedTargetBleed.Flat") > 0)
            enemy.ApplyBleeding(
                RunState.GetEffectParameter("CharmedTargetBleed", "duration", 4),
                RunState.GetEffectParameter("CharmedTargetBleed", "damagePerSecond", 5),
                Mathf.RoundToInt(RunState.GetEffectParameter("CharmedTargetBleed", "maxStacks", 3)));
    }

    public void OnEnemyKilled(EnemyBasic enemy)
    {
        if (enemy == null) return;
        if (enemy.Rarity != EnemyRarity.Normal) RunState.AddRoomBlessingDamage(RunState.GetEffectTotal("DamageOnEliteKill.Multiplier"));
        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("BloodOrbDropChance.Flat"), 0, 1)) SpawnBloodOrb(enemy.GlobalPosition);
        if (RunState.GetEffectTotal("BloodPoolOnKill.Flat") > 0) SpawnBloodPool(enemy.GlobalPosition);
        if (IsPositionOnBlood(enemy.GlobalPosition) && GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("BloodPoolRefreshOnKillChance.Flat"), 0, 1))
        {
            _health?.Heal((_health?.MaxHealth ?? 0) * RunState.GetEffectTotal("HealOnBloodPoolKill.Flat"));
            SpawnBloodPool(enemy.GlobalPosition);
        }
        if (enemy.IsPestilent)
        {
            if (RunState.GetEffectTotal("RatSummonOnPestilentKill.Flat") > 0)
                SpawnSummonedRats(enemy.GlobalPosition);
            _health?.Heal((_health?.MaxHealth ?? 0.0f) * RunState.GetEffectTotal("HealOnPestilentKill.Flat"));
            var spreadRadius = RunState.GetEffectParameter("BasicAttackPestilence", "deathRadius", 5.0f)
                + RunState.GetEffectTotal("PestilenceDeathSpreadRadius.Flat");
            SpawnPestilenceDeathBurst(enemy.GlobalPosition, spreadRadius);
            var spreadTargets = 3 + Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadTargets.Flat"));
            SpreadPestilence(enemy, Mathf.Max(spreadTargets, 3), spreadRadius);
            if (RunState.GetEffectTotal("PestilenceExplosionBloodPool.Flat") > 0)
                SpawnBloodPool(enemy.GlobalPosition, spreadRadius,
                    RunState.GetEffectParameter("PestilenceExplosionBloodPool", "duration", 5.0f), true);
            if (RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier") > 0) DamageEnemiesNear(enemy.GlobalPosition, 2.5f, 32 * RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier"), enemy);
        }
        if (enemy.IsCharmed && RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier") > 0)
        {
            DamageEnemiesNear(enemy.GlobalPosition, 2.8f, 32 * RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier"), enemy);
            SpawnCharmedDeathExplosion(enemy.GlobalPosition, 2.8f);
        }
    }

    private void SpawnSummonedRats(Vector3 position)
    {
        var capacity = 1;
        foreach (var blessing in RunState.RunBlessings)
        {
            if (blessing.Id != "orlok_rats_blessing") continue;
            capacity = BloodBlessingCatalog.GetSummonedRatCapacity(blessing);
            break;
        }

        var activeRats = GetTree().GetNodeCountInGroup("summoned_rats");
        if (activeRats >= capacity)
            return;

        var player = GetParent() as PlayerController;
        var combat = player?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        var scene = GetTree().CurrentScene;
        if (player == null || combat == null || scene == null)
            return;

        var lifetime = RunState.GetEffectParameter("RatSummonOnPestilentKill", "duration", 8.0f);
        var movementSpeed = player.SprintSpeed;
        var attackInterval = combat.GetCurrentBasicAttackInterval();
        var attackDamage = combat.GetCurrentBasicAttackDamage() * 0.5f;
        for (var slot = activeRats; slot < capacity; slot++)
        {
            var angle = Mathf.Tau * slot / Mathf.Max(capacity, 1);
            var spawnOffset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * 0.32f;
            var rat = new SummonedRat
            {
                OwnerPlayer = player,
                LifetimeSeconds = lifetime,
                MovementSpeed = movementSpeed,
                AttackInterval = attackInterval,
                AttackDamage = attackDamage
            };
            scene.AddChild(rat);
            rat.GlobalPosition = position + spawnOffset + Vector3.Up * 0.08f;
        }
    }

    private void UpdateBloodPoolState(float delta)
    {
        IsOnBlood = false;
        var player = GetParent<Node3D>();
        foreach (var node in GetTree().GetNodesInGroup("blood_pools"))
            if (node is Node3D pool && pool.GlobalPosition.DistanceTo(player.GlobalPosition) <= (float)pool.GetMeta("radius", 1.8f)) { IsOnBlood = true; break; }
        if (!IsOnBlood || _health == null) return;
        _poolHealTimer -= delta;
        if (_poolHealTimer <= 0) { _poolHealTimer = 0.25f; _health.Heal(_health.MaxHealth * RunState.GetEffectTotal("BloodPoolHealPerSecond.Flat") * 0.25f); }
    }

    private void UpdateBloodPoolBleeding(float delta)
    {
        _bloodPoolBleedTimer -= delta;
        if (_bloodPoolBleedTimer > 0.0f) return;
        _bloodPoolBleedTimer = 1.0f;
        foreach (var poolNode in GetTree().GetNodesInGroup("blood_pools"))
        {
            if (poolNode is not Node3D pool || !(bool)pool.GetMeta("causes_bleeding", false)) continue;
            var radius = (float)pool.GetMeta("radius", 1.8f);
            foreach (var enemyNode in GetTree().GetNodesInGroup("enemies"))
            {
                if (enemyNode is not EnemyBasic enemy || enemy.GlobalPosition.DistanceTo(pool.GlobalPosition) > radius) continue;
                enemy.ApplyBleeding(
                    RunState.GetEffectParameter("PestilenceExplosionBloodPool", "bleedDuration", 3.0f),
                    RunState.GetEffectParameter("PestilenceExplosionBloodPool", "bleedDamagePerSecond", 6.0f),
                    Mathf.RoundToInt(RunState.GetEffectParameter("PestilenceExplosionBloodPool", "bleedMaxStacks", 3.0f)));
            }
        }
    }

    private async void SpawnBloodPool(Vector3 position, float radiusOverride = 0.0f, float durationOverride = 0.0f, bool causesBleeding = false)
    {
        var baseRadius = radiusOverride > 0.0f ? radiusOverride : 1.8f;
        var radius = baseRadius * (1.0f + RunState.GetEffectTotal("BloodPoolArea.Multiplier"));
        var baseDuration = durationOverride > 0.0f ? durationOverride : RunState.GetEffectParameter("BloodPoolOnKill", "duration", 4);
        var duration = (baseDuration + RunState.GetEffectTotal("BloodPoolDuration.Flat")) * TemporaryEffectDurationMultiplier();
        var pool = new Node3D { Name = "BloodPool" };
        GetTree().CurrentScene.AddChild(pool);
        pool.GlobalPosition = position + Vector3.Up * 0.035f;
        pool.AddToGroup("blood_pools");
        pool.SetMeta("radius", radius);
        pool.SetMeta("causes_bleeding", causesBleeding);
        var bloodMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.62f, 0.0f, 0.025f, 0.76f),
            EmissionEnabled = true,
            Emission = new Color(0.72f, 0.0f, 0.025f),
            EmissionEnergyMultiplier = 1.75f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var surface = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.045f, RadialSegments = 64 },
            MaterialOverride = bloodMaterial
        };
        pool.AddChild(surface);
        var rim = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius * 0.94f, OuterRadius = radius, Rings = 64 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.015f, 0.04f, 0.86f), EmissionEnabled = true,
                Emission = new Color(0.95f, 0.0f, 0.03f), EmissionEnergyMultiplier = 2.4f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            Position = Vector3.Up * 0.035f
        };
        pool.AddChild(rim);
        var pulse = rim.CreateTween().SetLoops();
        pulse.TweenProperty(rim, "scale", Vector3.One * 1.035f, 0.55f);
        pulse.TweenProperty(rim, "scale", Vector3.One * 0.985f, 0.55f);
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(pool)) pool.QueueFree();
    }

    private float TemporaryEffectDurationMultiplier()
    {
        return GetParent()?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")?.TemporaryEffectDurationMultiplier() ?? 1.0f;
    }

    private float GetCharmDuration(string effectType)
    {
        return Mathf.Max(
            RunState.GetEffectParameter(effectType, "duration", 5.0f) * TemporaryEffectDurationMultiplier(),
            0.1f);
    }

    private void SpawnBloodOrb(Vector3 position)
    {
        var orb = new BloodOrbPickup { HealMaxHealthRatio = RunState.GetEffectTotal("BloodOrbHealMaxHealth.Flat"), Position = position + Vector3.Up * 0.35f };
        GetTree().CurrentScene.AddChild(orb);
        orb.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.03f, 0.08f), EmissionEnabled = true, Emission = Colors.Red, EmissionEnergyMultiplier = 2 } });
        orb.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.65f } });
    }

    private void SpreadPestilence(EnemyBasic source, int targets, float radius = 7.0f, bool allowExistingStacks = false)
    {
        if (targets <= 0) return;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (targets <= 0) break;
            if (node is not EnemyBasic enemy || enemy == source || (!allowExistingStacks && enemy.IsPestilent) || enemy.GlobalPosition.DistanceTo(source.GlobalPosition) > radius) continue;
            var duration = RunState.GetEffectParameter(
                "BasicAttackPestilence",
                "duration",
                RunState.GetEffectParameter("PestilenceChance", "duration", 5));
            enemy.ApplyPestilence(duration); targets--;
        }
    }

    private void TrySpawnPestilenceCloud(EnemyBasic host)
    {
        var multiplier = RunState.GetEffectTotal("PestilenceCloudAtStacks.Multiplier");
        var requiredStacks = Mathf.RoundToInt(RunState.GetEffectParameter("PestilenceCloudAtStacks", "requiredStacks", 3));
        if (multiplier <= 0.0f || host.PestilenceStacks < requiredStacks || !host.TryTriggerPestilenceCloud())
            return;

        var combat = GetParent()?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        var damagePerSecond = (combat?.GetCurrentBasicAttackDamage() ?? 32.0f) * multiplier;
        SpawnPestilenceCloud(
            host.GlobalPosition,
            damagePerSecond,
            RunState.GetEffectParameter("PestilenceCloudAtStacks", "duration", 3.0f),
            RunState.GetEffectParameter("PestilenceCloudAtStacks", "radius", 3.2f));
    }

    private async void SpawnPestilenceCloud(Vector3 position, float damagePerSecond, float duration, float radius)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var cloud = new MeshInstance3D
        {
            Name = "PestilenceCloud",
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.05f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.28f, 0.78f, 0.12f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(0.25f, 0.85f, 0.08f),
                EmissionEnergyMultiplier = 1.8f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        scene.AddChild(cloud);
        cloud.GlobalPosition = position + Vector3.Up * 0.04f;
        var elapsed = 0.0f;
        while (elapsed < duration && IsInsideTree() && GodotObject.IsInstanceValid(cloud))
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (node is not EnemyBasic enemy || enemy.GlobalPosition.DistanceTo(position) > radius) continue;
                enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damagePerSecond, false, "Nuvem de Pestilência");
            }
            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
            elapsed += 1.0f;
        }
        if (GodotObject.IsInstanceValid(cloud))
            cloud.QueueFree();
    }

    private void DamageEnemiesNear(Vector3 position, float radius, float damage, EnemyBasic excluded)
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
            if (node is EnemyBasic enemy && enemy != excluded && enemy.GlobalPosition.DistanceTo(position) <= radius) enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
    }

    private async void SpawnCharmedDeathExplosion(Vector3 position, float radius)
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null)
            return;

        var burst = new MeshInstance3D
        {
            Name = "CharmedDeathExplosion",
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2.0f, RadialSegments = 32, Rings = 16 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.04f, 0.58f, 0.34f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.0f, 0.48f),
                EmissionEnergyMultiplier = 4.0f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            Scale = Vector3.One * 0.08f
        };
        scene.AddChild(burst);
        burst.GlobalPosition = position + Vector3.Up * 0.55f;
        var tween = burst.CreateTween();
        tween.TweenProperty(burst, "scale", Vector3.One, 0.16f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(burst, "transparency", 1.0f, 0.28f);
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(burst))
            burst.QueueFree();
    }

    private async void SpawnPestilenceDeathBurst(Vector3 position, float radius)
    {
        var burst = new MeshInstance3D
        {
            Name = "PestilenceDeathBurst",
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.035f,
                RadialSegments = 64
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.8f, 0.18f, 0.28f),
                EmissionEnabled = true,
                Emission = new Color(0.12f, 0.72f, 0.08f),
                EmissionEnergyMultiplier = 1.8f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            },
            Position = position + Vector3.Up * 0.04f,
            Scale = Vector3.One * 0.1f
        };
        GetTree().CurrentScene.AddChild(burst);
        var tween = burst.CreateTween();
        tween.TweenProperty(burst, "scale", Vector3.One, 0.18f);
        tween.TweenProperty(burst, "transparency", 1.0f, 0.22f);
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(burst)) burst.QueueFree();
    }

    private async void SpawnLifeStealTrail(Vector3 source, Vector3 destination)
    {
        var orb = new MeshInstance3D
        {
            Name = "LifeStealTrail",
            Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.02f, 0.12f, 0.9f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.0f, 0.08f),
                EmissionEnergyMultiplier = 4.0f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
        var scene = GetTree()?.CurrentScene;
        if (scene == null) return;
        scene.AddChild(orb);
        if (!IsInstanceValid(orb) || !orb.IsInsideTree()) return;
        orb.GlobalPosition = source + Vector3.Up * 1.0f;
        var tween = orb.CreateTween();
        tween.TweenProperty(orb, "global_position", destination + Vector3.Up * 1.0f, 0.28f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(orb, "scale", Vector3.One * 0.25f, 0.28f);
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(orb)) orb.QueueFree();
    }

    private bool HasNearbyEnemy()
    {
        var player = GetParent<Node3D>();
        foreach (var node in GetTree().GetNodesInGroup("enemies")) if (node is Node3D enemy && enemy.GlobalPosition.DistanceTo(player.GlobalPosition) <= 4) return true;
        return false;
    }

    private bool IsPositionOnBlood(Vector3 position)
    {
        foreach (var node in GetTree().GetNodesInGroup("blood_pools"))
            if (node is Node3D pool && pool.GlobalPosition.DistanceTo(position) <= (float)pool.GetMeta("radius", 1.8f)) return true;
        return false;
    }
}

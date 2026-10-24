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
    private readonly System.Collections.Generic.List<(string EffectType, float Part)> _damageBonusParts = new();
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
        // Cada parte guarda o tipo de efeito de origem para as estatisticas do game over (RunStats.SetHitBonus).
        _damageBonusParts.Clear();
        var bonus = Bonus("PermanentDamageFromOverheal", RunState.BlessingPermanentDamage) + Bonus("DamageOnEliteKill", RunState.BlessingRoomDamage);
        if (attackType == PlayerAttackType.Basic) bonus += Bonus("BasicAttackDamage", RunState.GetEffectTotal("BasicAttackDamage.Multiplier"));
        if (attackType == PlayerAttackType.Quick) bonus += Bonus("QuickAttackDamage", RunState.GetEffectTotal("QuickAttackDamage.Multiplier"));
        if (attackType == PlayerAttackType.Special) bonus += Bonus("SpecialAttackDamage", RunState.GetEffectTotal("SpecialAttackDamage.Multiplier"));
        var healthRatio = _health?.MaxHealth > 0 ? _health.CurrentHealth / _health.MaxHealth : 0;
        if (healthRatio >= RunState.GetEffectParameter("DamageAboveHealth", "threshold", 0.8f)) bonus += Bonus("DamageAboveHealth", RunState.GetEffectTotal("DamageAboveHealth.Multiplier"));
        if (_damageAfterHealTimer > 0) bonus += Bonus("DamageAfterHealing", RunState.GetEffectTotal("DamageAfterHealing.Multiplier"));
        if (HasBarrier) bonus += Bonus("DamageWhileBarrier", RunState.GetEffectTotal("DamageWhileBarrier.Multiplier"));
        if (IsOnBlood) bonus += Bonus("DamageOnBlood", RunState.GetEffectTotal("DamageOnBlood.Multiplier"));
        if (enemy?.IsPestilenceHost == true) bonus += Bonus("DamageVsPlagueHost", RunState.GetEffectTotal("DamageVsPlagueHost.Multiplier"));
        if (enemy?.IsFascinated == true) bonus += Bonus("FascinationDamageTaken", RunState.GetEffectTotal("FascinationDamageTaken.Multiplier"));
        var charmedCount = Mathf.Min(GetTree().GetNodeCountInGroup("charmed_enemies"), Mathf.RoundToInt(RunState.GetEffectParameter("DamagePerCharmedEnemy", "maxTargets", 5)));
        bonus += Bonus("DamagePerCharmedEnemy", charmedCount * RunState.GetEffectTotal("DamagePerCharmedEnemy.Multiplier"));
        var multiplier = Mathf.Max(1.0f + bonus, 0.0f);
        RunStats.SetHitBonus(_damageBonusParts, multiplier);
        return damage * multiplier;
    }

    private float Bonus(string effectType, float value)
    {
        if (value > 0.0f) _damageBonusParts.Add((effectType, value));
        return value;
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
        var recoveryStep = Mathf.Max(CompendiumTuning.GetFloat("blessing.recoverable.step", 0.25f), 0.02f);
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
            enemy.ApplyCharm(GetCharmDuration("CharmOnCriticalChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0, "CharmOnCriticalChance");
        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("PestilenceChance.Flat"), 0, 1))
            enemy.ApplyPestilence(RunState.GetEffectParameter("PestilenceChance", "duration", 5));

        if (isBasicAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("BasicAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("BasicAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0, "BasicAttackCharmChance");
            if (RunState.GetEffectTotal("BasicAttackPestilence.Flat") > 0)
                enemy.ApplyPestilence(RunState.GetEffectParameter("BasicAttackPestilence", "duration", 5));
            if (RunState.GetEffectTotal("BasicAttackBleed.Flat") > 0)
                enemy.ApplyBleeding(
                    RunState.GetEffectParameter("BasicAttackBleed", "duration", 5),
                    RunState.GetEffectParameter("BasicAttackBleed", "damagePerSecond", 6),
                    Mathf.RoundToInt(RunState.GetEffectParameter("BasicAttackBleed", "maxStacks", 5)), "BasicAttackBleed");
        }
        else if (isQuickAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("QuickAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0, "QuickAttackCharmChance");
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackBleedChance.Flat"), 0, 1))
                enemy.ApplyBleeding(
                    RunState.GetEffectParameter("QuickAttackBleedChance", "duration", 4),
                    RunState.GetEffectParameter("QuickAttackBleedChance", "damagePerSecond", 6),
                    Mathf.RoundToInt(RunState.GetEffectParameter("QuickAttackBleedChance", "maxStacks", 3)), "QuickAttackBleedChance");
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackPestilenceChance.Flat"), 0, 1))
                enemy.ApplyPestilence(RunState.GetEffectParameter("QuickAttackPestilenceChance", "duration", 5));
        }
        else if (isSpecialAttack)
        {
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("SpecialAttackPestilenceChance.Flat"), 0, 1))
                enemy.ApplyPestilence(RunState.GetEffectParameter("SpecialAttackPestilenceChance", "duration", 6));
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("SpecialAttackCharmChance.Flat"), 0, 1))
                enemy.ApplyCharm(GetCharmDuration("SpecialAttackCharmChance"), RunState.GetEffectTotal("PermanentCharm.Flat") > 0, "SpecialAttackCharmChance");
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
                Mathf.RoundToInt(RunState.GetEffectParameter("CharmedTargetBleed", "maxStacks", 3)), "CharmedTargetBleed");
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
            var baseSpreadTargets = CompendiumTuning.GetInt("blessing.pestilence.base_spread_targets", 3);
            var spreadTargets = baseSpreadTargets + Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadTargets.Flat"));
            SpreadPestilence(enemy, Mathf.Max(spreadTargets, baseSpreadTargets), spreadRadius);
            if (RunState.GetEffectTotal("PestilenceExplosionBloodPool.Flat") > 0)
                SpawnBloodPool(enemy.GlobalPosition, spreadRadius,
                    RunState.GetEffectParameter("PestilenceExplosionBloodPool", "duration", 5.0f), true);
            if (RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier") > 0) DamageEnemiesNear(enemy.GlobalPosition, CompendiumTuning.GetFloat("blessing.corpse_explosion.radius", 2.5f), CompendiumTuning.GetFloat("blessing.corpse_explosion.base_damage", 32.0f) * RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier"), enemy, "InfectedCorpseExplosion");
        }
        if (enemy.IsCharmed && RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier") > 0)
        {
            var explosionRadius = CompendiumTuning.GetFloat("blessing.charmed_explosion.radius", 2.8f);
            DamageEnemiesNear(enemy.GlobalPosition, explosionRadius, CompendiumTuning.GetFloat("blessing.charmed_explosion.base_damage", 32.0f) * RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier"), enemy, "CharmedDeathExplosion");
            SpawnCharmedDeathExplosion(enemy.GlobalPosition, explosionRadius);
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
        var attackDamage = combat.GetCurrentBasicAttackDamage() * CompendiumTuning.GetFloat("blessing.rat.damage_ratio", 0.5f);
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
        if (_poolHealTimer <= 0)
        {
            var tick = Mathf.Max(CompendiumTuning.GetFloat("blessing.pool.heal_tick", 0.25f), 0.02f);
            _poolHealTimer = tick;
            _health.Heal(_health.MaxHealth * RunState.GetEffectTotal("BloodPoolHealPerSecond.Flat") * tick);
        }
    }

    private void UpdateBloodPoolBleeding(float delta)
    {
        _bloodPoolBleedTimer -= delta;
        if (_bloodPoolBleedTimer > 0.0f) return;
        _bloodPoolBleedTimer = Mathf.Max(CompendiumTuning.GetFloat("blessing.pool.bleed_tick", 1.0f), 0.05f);
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
                    Mathf.RoundToInt(RunState.GetEffectParameter("PestilenceExplosionBloodPool", "bleedMaxStacks", 3.0f)), "PestilenceExplosionBloodPool");
            }
        }
    }

    private async void SpawnBloodPool(Vector3 position, float radiusOverride = 0.0f, float durationOverride = 0.0f, bool causesBleeding = false)
    {
        var baseRadius = radiusOverride > 0.0f ? radiusOverride : CompendiumTuning.GetFloat("blessing.pool.base_radius", 1.8f);
        var radius = baseRadius * (1.0f + RunState.GetEffectTotal("BloodPoolArea.Multiplier"));
        var baseDuration = durationOverride > 0.0f ? durationOverride : RunState.GetEffectParameter("BloodPoolOnKill", "duration", 4);
        var duration = (baseDuration + RunState.GetEffectTotal("BloodPoolDuration.Flat")) * TemporaryEffectDurationMultiplier();
        var expiresAt = Time.GetTicksMsec() / 1000.0 + duration;
        if (TryMergeBloodPool(position, radius, expiresAt, causesBleeding))
            return;

        var pool = new Node3D { Name = "BloodPool" };
        GetTree().CurrentScene.AddChild(pool);
        pool.GlobalPosition = position + Vector3.Up * 0.025f;
        pool.AddToGroup("blood_pools");
        pool.SetMeta("radius", radius);
        pool.SetMeta("causes_bleeding", causesBleeding);
        pool.SetMeta("expires_at", expiresAt);
        var random = new RandomNumberGenerator
        {
            Seed = (ulong)Mathf.Abs(Mathf.RoundToInt(position.X * 73856093.0f)
                ^ Mathf.RoundToInt(position.Z * 19349663.0f)
                ^ (int)(Time.GetTicksMsec() & 0x7fffffff))
        };
        var bloodMaterial = new StandardMaterial3D
        {
            AlbedoColor = causesBleeding
                ? new Color(0.24f, 0.015f, 0.025f, 0.72f)
                : new Color(0.31f, 0.008f, 0.018f, 0.68f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var surface = new MeshInstance3D
        {
            Name = "OrganicSurface",
            Mesh = CreateOrganicPoolMesh(radius, random, 24, 0.22f),
            MaterialOverride = bloodMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        pool.AddChild(surface);
        var sheen = new MeshInstance3D
        {
            Name = "SubtleSheen",
            Mesh = CreateOrganicPoolMesh(radius * 0.56f, random, 18, 0.24f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.58f, 0.025f, 0.035f, 0.18f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            Position = new Vector3(radius * 0.16f, 0.008f, -radius * 0.12f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        pool.AddChild(sheen);

        pool.Scale = new Vector3(0.86f, 1.0f, 0.86f);
        pool.CreateTween().TweenProperty(pool, "scale", Vector3.One, 0.22f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);

        while (IsInstanceValid(pool))
        {
            var remaining = (double)pool.GetMeta("expires_at", expiresAt) - Time.GetTicksMsec() / 1000.0;
            if (remaining <= 0.0)
                break;
            await ToSignal(GetTree().CreateTimer(Mathf.Min((float)remaining, 0.25f)), SceneTreeTimer.SignalName.Timeout);
        }
        if (!IsInstanceValid(pool))
            return;

        var fade = pool.CreateTween().SetParallel();
        fade.TweenProperty(surface, "transparency", 1.0f, 0.28f);
        fade.TweenProperty(sheen, "transparency", 1.0f, 0.28f);
        fade.TweenProperty(pool, "scale", new Vector3(0.94f, 1.0f, 0.94f), 0.28f);
        await ToSignal(fade, Tween.SignalName.Finished);
        if (IsInstanceValid(pool))
            pool.QueueFree();
    }

    private bool TryMergeBloodPool(Vector3 position, float radius, double expiresAt, bool causesBleeding)
    {
        foreach (var node in GetTree().GetNodesInGroup("blood_pools"))
        {
            if (node is not Node3D existing)
                continue;
            var existingRadius = (float)existing.GetMeta("radius", 1.8f);
            if (radius > existingRadius * 1.15f
                || existing.GlobalPosition.DistanceTo(position) > Mathf.Min(radius, existingRadius) * 0.72f)
                continue;

            var currentExpiry = (double)existing.GetMeta("expires_at", expiresAt);
            existing.SetMeta("expires_at", Mathf.Max(currentExpiry, expiresAt));
            if (causesBleeding)
                existing.SetMeta("causes_bleeding", true);
            return true;
        }
        return false;
    }

    private static ArrayMesh CreateOrganicPoolMesh(
        float radius,
        RandomNumberGenerator random,
        int segments,
        float irregularity)
    {
        var points = new Vector3[segments];
        var phase = random.RandfRange(0.0f, Mathf.Tau);
        for (var i = 0; i < segments; i++)
        {
            var angle = Mathf.Tau * i / segments;
            var broadWave = Mathf.Sin(angle * 3.0f + phase) * irregularity * 0.45f;
            var jitter = random.RandfRange(-irregularity, irregularity);
            var pointRadius = radius * (1.0f + broadWave + jitter);
            points[i] = new Vector3(Mathf.Cos(angle) * pointRadius, 0.0f, Mathf.Sin(angle) * pointRadius);
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < segments; i++)
        {
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(Vector3.Zero);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(points[(i + 1) % segments]);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(points[i]);
        }
        return surface.Commit();
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
                RunStats.Attribute("PestilenceCloudAtStacks");
                enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damagePerSecond, false, Localization.Text("blessing.damage_source.pestilence_cloud"));
            }
            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
            elapsed += 1.0f;
        }
        if (GodotObject.IsInstanceValid(cloud))
            cloud.QueueFree();
    }

    private void DamageEnemiesNear(Vector3 position, float radius, float damage, EnemyBasic excluded, string sourceEffect)
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic enemy || enemy == excluded || enemy.GlobalPosition.DistanceTo(position) > radius) continue;
            RunStats.Attribute(sourceEffect);
            enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
        }
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
        var scene = GetTree()?.CurrentScene;
        if (scene == null)
            return;

        var random = new RandomNumberGenerator
        {
            Seed = (ulong)Mathf.Abs(Mathf.RoundToInt(position.X * 92837111.0f)
                ^ Mathf.RoundToInt(position.Z * 689287499.0f)
                ^ (int)(Time.GetTicksMsec() & 0x7fffffff))
        };
        var burst = new Node3D
        {
            Name = "PestilenceDeathBurst",
            Position = position + Vector3.Up * 0.035f
        };
        scene.AddChild(burst);

        var groundMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.28f, 0.055f, 0.38f),
            EmissionEnabled = true,
            Emission = new Color(0.18f, 0.38f, 0.045f),
            EmissionEnergyMultiplier = 0.65f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var stain = new MeshInstance3D
        {
            Name = "OrganicContagionStain",
            Mesh = CreateOrganicPoolMesh(radius, random, 24, 0.24f),
            MaterialOverride = groundMaterial,
            Scale = new Vector3(0.08f, 1.0f, 0.08f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        burst.AddChild(stain);
        var veins = new MeshInstance3D
        {
            Name = "PestilenceVeins",
            Mesh = CreatePestilenceVeinMesh(radius, random),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.42f, 0.62f, 0.12f, 0.56f),
                EmissionEnabled = true,
                Emission = new Color(0.34f, 0.6f, 0.08f),
                EmissionEnergyMultiplier = 1.1f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            Position = Vector3.Up * 0.009f,
            Scale = new Vector3(0.08f, 1.0f, 0.08f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        burst.AddChild(veins);

        var sporeMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.68f, 0.14f, 0.78f),
            EmissionEnabled = true,
            Emission = new Color(0.34f, 0.66f, 0.09f),
            EmissionEnergyMultiplier = 1.25f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var sporeMesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 10, Rings = 5 };
        var sporeCount = Mathf.Clamp(Mathf.RoundToInt(radius * 2.2f), 8, 14);
        for (var i = 0; i < sporeCount; i++)
        {
            var angle = random.RandfRange(0.0f, Mathf.Tau);
            var outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            var spore = new MeshInstance3D
            {
                Name = $"PestilenceSpore{i + 1}",
                Mesh = sporeMesh,
                MaterialOverride = sporeMaterial,
                Position = Vector3.Up * random.RandfRange(0.08f, 0.3f),
                Scale = Vector3.One * random.RandfRange(0.45f, 1.05f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            burst.AddChild(spore);
            var destination = outward * random.RandfRange(radius * 0.28f, radius * 0.88f)
                + Vector3.Up * random.RandfRange(0.45f, 1.45f);
            var sporeTween = spore.CreateTween().SetParallel();
            sporeTween.TweenProperty(spore, "position", destination, random.RandfRange(0.32f, 0.48f))
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            sporeTween.TweenProperty(spore, "transparency", 1.0f, 0.46f).SetDelay(0.08f);
            sporeTween.TweenProperty(spore, "scale", Vector3.One * 0.08f, 0.46f);
        }

        var expansion = burst.CreateTween().SetParallel();
        expansion.TweenProperty(stain, "scale", Vector3.One, 0.2f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        expansion.TweenProperty(veins, "scale", Vector3.One, 0.16f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        expansion.TweenProperty(stain, "transparency", 1.0f, 0.34f).SetDelay(0.16f);
        expansion.TweenProperty(veins, "transparency", 1.0f, 0.3f).SetDelay(0.18f);
        await ToSignal(GetTree().CreateTimer(0.58f), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(burst))
            burst.QueueFree();
    }

    private static ArrayMesh CreatePestilenceVeinMesh(float radius, RandomNumberGenerator random)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var veinCount = Mathf.Clamp(Mathf.RoundToInt(radius * 1.7f), 6, 11);
        for (var i = 0; i < veinCount; i++)
        {
            var angle = Mathf.Tau * i / veinCount + random.RandfRange(-0.22f, 0.22f);
            var direction = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            var perpendicular = new Vector3(-direction.Z, 0.0f, direction.X);
            var length = radius * random.RandfRange(0.48f, 0.96f);
            var middle = direction * length * 0.48f + perpendicular * random.RandfRange(-radius * 0.12f, radius * 0.12f);
            var end = direction * length + perpendicular * random.RandfRange(-radius * 0.08f, radius * 0.08f);
            var startWidth = radius * random.RandfRange(0.018f, 0.036f);
            AddFlatQuad(surface, perpendicular * startWidth, -perpendicular * startWidth,
                middle - perpendicular * startWidth * 0.58f, middle + perpendicular * startWidth * 0.58f);
            AddFlatQuad(surface, middle + perpendicular * startWidth * 0.58f, middle - perpendicular * startWidth * 0.58f,
                end - perpendicular * startWidth * 0.12f, end + perpendicular * startWidth * 0.12f);
        }
        return surface.Commit();
    }

    private static void AddFlatQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(a);
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(b);
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(c);
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(a);
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(c);
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(d);
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
        foreach (var node in GetTree().GetNodesInGroup("enemies")) if (node is Node3D enemy && enemy.GlobalPosition.DistanceTo(player.GlobalPosition) <= CompendiumTuning.GetFloat("blessing.nearby_enemy_radius", 4.0f)) return true;
        return false;
    }

    private bool IsPositionOnBlood(Vector3 position)
    {
        foreach (var node in GetTree().GetNodesInGroup("blood_pools"))
            if (node is Node3D pool && pool.GlobalPosition.DistanceTo(position) <= (float)pool.GetMeta("radius", 1.8f)) return true;
        return false;
    }
}

using Godot;
using System.Collections.Generic;

public partial class BloodBlessingRuntime : Node
{
    private Health _health;
    private float _barrier;
    private float _damageAfterHealTimer;
    private float _damageReductionAfterHealTimer;
    private float _attackSpeedAfterHealTimer;
    private float _criticalAfterDashTimer;
    private float _poolHealTimer;
    private int _recoverableDamageVersion;
    private bool _fatalCharmSacrificeUsed;

    public bool HasBarrier => _barrier > 0.01f;
    public bool IsOnBlood { get; private set; }

    public override void _Ready()
    {
        _health = GetParent()?.GetNodeOrNull<Health>("Health");
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _damageAfterHealTimer = Mathf.Max(_damageAfterHealTimer - d, 0);
        _damageReductionAfterHealTimer = Mathf.Max(_damageReductionAfterHealTimer - d, 0);
        _attackSpeedAfterHealTimer = Mathf.Max(_attackSpeedAfterHealTimer - d, 0);
        _criticalAfterDashTimer = Mathf.Max(_criticalAfterDashTimer - d, 0);
        UpdateBloodPoolState(d);
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
        if (enemy != null && enemy.Name.ToString().Contains("Boss") && RunState.GetEffectTotal("PermanentCharm.Flat") > 0) bonus += RunState.GetEffectTotal("FascinationDamageTaken.Multiplier");
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
        _barrier = Mathf.Min(_barrier + overheal * barrierRatio, barrierCap);
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
        _health.Heal(recoverable);
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

    public void OnPlayerHit(EnemyBasic enemy, float damage, bool critical, bool isBasicAttack, bool isQuickAttack = false)
    {
        if (enemy == null || _health == null) return;
        enemy.RefreshPlayerHitStatuses();
        var lifeSteal = RunState.GetEffectTotal("LifeSteal.Multiplier");
        if (isBasicAttack) lifeSteal += RunState.GetEffectTotal("BasicAttackLifeSteal.Multiplier");
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
            enemy.ApplyCharm(RunState.GetEffectParameter("CharmOnCriticalChance", "duration", 3), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("PestilenceChance.Flat"), 0, 1))
            enemy.ApplyPestilence(RunState.GetEffectParameter("PestilenceChance", "duration", 5));
        if (enemy.IsPestilenceHost && RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat") > 0)
            SpreadPestilence(enemy, Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat")));

        if (isQuickAttack && GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("QuickAttackCharmChance.Flat"), 0, 1))
            enemy.ApplyCharm(RunState.GetEffectParameter("QuickAttackCharmChance", "duration", 3), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        if (enemy.IsCharmed && RunState.GetEffectTotal("CharmedTargetBleed.Flat") > 0)
            enemy.ApplyBleeding(
                RunState.GetEffectParameter("CharmedTargetBleed", "duration", 4),
                RunState.GetEffectParameter("CharmedTargetBleed", "damagePerSecond", 5),
                Mathf.RoundToInt(RunState.GetEffectParameter("CharmedTargetBleed", "maxStacks", 3)));

        if (!isBasicAttack) return;

        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("BasicAttackCharmChance.Flat"), 0, 1))
            enemy.ApplyCharm(RunState.GetEffectParameter("BasicAttackCharmChance", "duration", 3), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        if (RunState.GetEffectTotal("BasicAttackPestilence.Flat") > 0)
            enemy.ApplyPestilence(RunState.GetEffectParameter("BasicAttackPestilence", "duration", 5));
        if (RunState.GetEffectTotal("BasicAttackBleed.Flat") > 0)
            enemy.ApplyBleeding(
                RunState.GetEffectParameter("BasicAttackBleed", "duration", 5),
                RunState.GetEffectParameter("BasicAttackBleed", "damagePerSecond", 6),
                Mathf.RoundToInt(RunState.GetEffectParameter("BasicAttackBleed", "maxStacks", 5)));
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
            _health?.Heal((_health?.MaxHealth ?? 0.0f) * RunState.GetEffectTotal("HealOnPestilentKill.Flat"));
            var spreadRadius = RunState.GetEffectParameter("BasicAttackPestilence", "deathRadius", 5.0f)
                + RunState.GetEffectTotal("PestilenceDeathSpreadRadius.Flat");
            SpawnPestilenceDeathBurst(enemy.GlobalPosition, spreadRadius);
            var spreadTargets = 3 + Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadTargets.Flat"));
            SpreadPestilence(enemy, Mathf.Max(spreadTargets, 3), spreadRadius);
            if (RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier") > 0) DamageEnemiesNear(enemy.GlobalPosition, 2.5f, 32 * RunState.GetEffectTotal("InfectedCorpseExplosion.Multiplier"), enemy);
            if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("RatSummonOnInfectedKillChance.Flat"), 0, 1)) DamageEnemiesNear(enemy.GlobalPosition, 2.0f, 24, enemy);
        }
        if (enemy.IsCharmed && RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier") > 0)
            DamageEnemiesNear(enemy.GlobalPosition, 2.8f, 32 * RunState.GetEffectTotal("CharmedDeathExplosion.Multiplier"), enemy);
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

    private async void SpawnBloodPool(Vector3 position, float radiusOverride = 0.0f, float durationOverride = 0.0f)
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

    private void SpawnBloodOrb(Vector3 position)
    {
        var orb = new BloodOrbPickup { HealMaxHealthRatio = RunState.GetEffectTotal("BloodOrbHealMaxHealth.Flat"), Position = position + Vector3.Up * 0.35f };
        GetTree().CurrentScene.AddChild(orb);
        orb.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.03f, 0.08f), EmissionEnabled = true, Emission = Colors.Red, EmissionEnergyMultiplier = 2 } });
        orb.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.65f } });
    }

    private void SpreadPestilence(EnemyBasic source, int targets, float radius = 7.0f)
    {
        if (targets <= 0) return;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (targets <= 0) break;
            if (node is not EnemyBasic enemy || enemy == source || enemy.IsPestilent || enemy.GlobalPosition.DistanceTo(source.GlobalPosition) > radius) continue;
            var duration = RunState.GetEffectParameter(
                "BasicAttackPestilence",
                "duration",
                RunState.GetEffectParameter("PestilenceChance", "duration", 5));
            enemy.ApplyPestilence(duration); targets--;
        }
    }

    private void DamageEnemiesNear(Vector3 position, float radius, float damage, EnemyBasic excluded)
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
            if (node is EnemyBasic enemy && enemy != excluded && enemy.GlobalPosition.DistanceTo(position) <= radius) enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
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
            },
            GlobalPosition = source + Vector3.Up * 1.0f
        };
        GetTree().CurrentScene.AddChild(orb);
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

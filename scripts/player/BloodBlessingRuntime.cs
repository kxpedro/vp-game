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

    public float ModifyOutgoingDamage(float damage, EnemyBasic enemy)
    {
        var bonus = RunState.BlessingPermanentDamage + RunState.BlessingRoomDamage;
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
            _damageAfterHealTimer = Mathf.Max(_damageAfterHealTimer, RunState.GetEffectParameter("DamageAfterHealing", "duration", 5));
            _damageReductionAfterHealTimer = Mathf.Max(_damageReductionAfterHealTimer, RunState.GetEffectParameter("DamageReductionAfterHealing", "duration", 4));
            var threshold = RunState.GetEffectParameter("AttackSpeedAfterHighHealthHeal", "healthThreshold", 0.9f);
            if (_health != null && _health.CurrentHealth / _health.MaxHealth >= threshold)
                _attackSpeedAfterHealTimer = Mathf.Max(_attackSpeedAfterHealTimer, RunState.GetEffectParameter("AttackSpeedAfterHighHealthHeal", "duration", 5));
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
        _criticalAfterDashTimer = Mathf.Max(_criticalAfterDashTimer, RunState.GetEffectParameter("CriticalChanceAfterDash", "duration", 2));
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

    public void OnPlayerHit(EnemyBasic enemy, float damage, bool critical)
    {
        if (enemy == null || _health == null) return;
        var lifeSteal = RunState.GetEffectTotal("LifeSteal.Multiplier");
        var enemyHealth = enemy.GetNodeOrNull<Health>("Health");
        var threshold = RunState.GetEffectParameter("LifeStealVsLowHealthEnemy", "threshold", 0.25f);
        if (enemyHealth != null && enemyHealth.CurrentHealth / enemyHealth.MaxHealth <= threshold) lifeSteal += RunState.GetEffectTotal("LifeStealVsLowHealthEnemy.Multiplier");
        if (lifeSteal > 0) _health.Heal(damage * lifeSteal);

        if (critical && GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("CharmOnCriticalChance.Flat"), 0, 1))
            enemy.ApplyCharm(RunState.GetEffectParameter("CharmOnCriticalChance", "duration", 3), RunState.GetEffectTotal("PermanentCharm.Flat") > 0);
        if (GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal("PestilenceChance.Flat"), 0, 1))
            enemy.ApplyPestilence(RunState.GetEffectParameter("PestilenceChance", "duration", 5));
        if (enemy.IsPestilenceHost && RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat") > 0)
            SpreadPestilence(enemy, Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadOnHostHit.Flat")));
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
            SpreadPestilence(enemy, Mathf.RoundToInt(RunState.GetEffectTotal("PestilenceSpreadTargetsOnDeath.Flat")));
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

    private async void SpawnBloodPool(Vector3 position)
    {
        var radius = 1.8f * (1.0f + RunState.GetEffectTotal("BloodPoolArea.Multiplier"));
        var duration = RunState.GetEffectParameter("BloodPoolOnKill", "duration", 4) + RunState.GetEffectTotal("BloodPoolDuration.Flat");
        var pool = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.025f, RadialSegments = 48 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.0f, 0.02f, 0.62f), EmissionEnabled = true, Emission = new Color(0.35f, 0, 0), Transparency = BaseMaterial3D.TransparencyEnum.Alpha }, Position = position + Vector3.Up * 0.025f };
        GetTree().CurrentScene.AddChild(pool); pool.AddToGroup("blood_pools"); pool.SetMeta("radius", radius);
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(pool)) pool.QueueFree();
    }

    private void SpawnBloodOrb(Vector3 position)
    {
        var orb = new BloodOrbPickup { HealMaxHealthRatio = RunState.GetEffectTotal("BloodOrbHealMaxHealth.Flat"), Position = position + Vector3.Up * 0.35f };
        GetTree().CurrentScene.AddChild(orb);
        orb.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.03f, 0.08f), EmissionEnabled = true, Emission = Colors.Red, EmissionEnergyMultiplier = 2 } });
        orb.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.65f } });
    }

    private void SpreadPestilence(EnemyBasic source, int targets)
    {
        if (targets <= 0) return;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (targets <= 0) break;
            if (node is not EnemyBasic enemy || enemy == source || enemy.IsPestilent || enemy.GlobalPosition.DistanceTo(source.GlobalPosition) > 7) continue;
            enemy.ApplyPestilence(RunState.GetEffectParameter("PestilenceChance", "duration", 5)); targets--;
        }
    }

    private void DamageEnemiesNear(Vector3 position, float radius, float damage, EnemyBasic excluded)
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
            if (node is EnemyBasic enemy && enemy != excluded && enemy.GlobalPosition.DistanceTo(position) <= radius) enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
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

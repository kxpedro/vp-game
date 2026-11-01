using Godot;
using System.Collections.Generic;

// Efeitos das quatro famílias novas de herança (Strigoi, Vârcolac, Liaozhai, Nachtalb).
// Os tipos de efeito vêm de data/blessings/blood_blessings.json e são lidos por RunState.GetEffectTotal/Parameter.
public partial class BloodBlessingRuntime
{
    public const string Strigoi = "Strigoi";
    public const string Varcolac = "Vârcolac";
    public const string Liaozhai = "Liaozhai";
    public const string Nachtalb = "Nachtalb";

    private static readonly Color StrigoiColor = new(0.56f, 0.64f, 0.72f);
    private static readonly Color VarcolacColor = new(0.73f, 0.46f, 0.28f);
    private static readonly Color LiaozhaiColor = new(0.12f, 0.53f, 0.46f);
    private static readonly Color NachtalbColor = new(0.16f, 0.36f, 0.77f);

    private bool _hasStrigoi;
    private bool _hasVarcolac;
    private bool _hasLiaozhai;
    private bool _hasNachtalb;
    private float _familyCacheTimer;

    // Forma de Lobo (Vârcolac).
    public float Instinct { get; private set; }
    public bool IsWolfForm => _wolfTimer > 0.0f;
    public float WolfFormRemaining => _wolfTimer;
    private float _wolfTimer;
    private float _instinctIdle;
    private float _eclipseDelay;
    private float _entryInvulnerability;

    // Strigoi.
    private float _mistTimer;
    private bool _mistUsedThisRoom;
    private float _shadowPackTimer;
    private readonly HashSet<EnemyBasic> _dashHit = new();
    private int _familyMapIndex = -1;
    private int _familyRoom = -1;

    // Liaozhai e Nachtalb.
    private float _baguaTimer;
    private float _auraTimer;
    private float _witchingCooldown;
    private float _witchingScanTimer;

    public bool HasFamily(string family) => family switch
    {
        Strigoi => _hasStrigoi,
        Varcolac => _hasVarcolac,
        Liaozhai => _hasLiaozhai,
        Nachtalb => _hasNachtalb,
        _ => false
    };

    private void RefreshFamilyCache()
    {
        _hasStrigoi = _hasVarcolac = _hasLiaozhai = _hasNachtalb = false;
        foreach (var record in RunState.RunBlessings)
        {
            if (!BloodBlessingCatalog.TryGet(record.Id, out var definition)) continue;
            switch (definition.Family)
            {
                case Strigoi: _hasStrigoi = true; break;
                case Varcolac: _hasVarcolac = true; break;
                case Liaozhai: _hasLiaozhai = true; break;
                case Nachtalb: _hasNachtalb = true; break;
            }
        }
    }

    private PlayerCombat Combat => GetParent()?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
    private Node3D PlayerBody => GetParent<Node3D>();

    private float BaseHitDamage() => Combat?.GetCurrentBasicAttackDamage() ?? 10.0f;

    private void FamilyProcess(float d)
    {
        _familyCacheTimer -= d;
        if (_familyCacheTimer <= 0.0f)
        {
            _familyCacheTimer = 0.4f;
            RefreshFamilyCache();
        }
        if (_familyMapIndex != RunState.CurrentMapIndex || _familyRoom != RunState.CurrentRoom)
        {
            _familyMapIndex = RunState.CurrentMapIndex;
            _familyRoom = RunState.CurrentRoom;
            _mistUsedThisRoom = false;
            _mistTimer = 0.0f;
            _shadowPackTimer = 0.0f;
            _wolfTimer = 0.0f;
            Instinct = 0.0f;
        }
        _mistTimer = Mathf.Max(_mistTimer - d, 0.0f);
        _shadowPackTimer = Mathf.Max(_shadowPackTimer - d, 0.0f);
        _entryInvulnerability = Mathf.Max(_entryInvulnerability - d, 0.0f);
        _witchingCooldown = Mathf.Max(_witchingCooldown - d, 0.0f);
        if (_hasVarcolac) TickWolf(d);
        if (_hasLiaozhai) TickLiaozhai(d);
        if (_hasNachtalb) TickNachtalb(d);
    }

    // ---------- Vârcolac ----------

    private void TickWolf(float d)
    {
        if (_wolfTimer > 0.0f)
        {
            _wolfTimer -= d;
            if (_wolfTimer <= 0.0f)
            {
                _wolfTimer = 0.0f;
                var ratio = _health != null && _health.MaxHealth > 0.0f ? _health.CurrentHealth / _health.MaxHealth : 0.0f;
                if (RunState.GetEffectTotal("WolfEclipse.Flat") > 0.0f && ratio > 0.5f)
                    _eclipseDelay = 1.5f;
            }
            return;
        }
        if (_eclipseDelay > 0.0f)
        {
            _eclipseDelay -= d;
            if (_eclipseDelay <= 0.0f) EnterWolfForm();
            return;
        }
        _instinctIdle += d;
        if (_instinctIdle > 4.0f && Instinct > 0.0f) Instinct = Mathf.Max(Instinct - 6.0f * d, 0.0f);
    }

    private void AddInstinct(float amount)
    {
        if (!_hasVarcolac || IsWolfForm) return;
        _instinctIdle = 0.0f;
        Instinct = Mathf.Min(Instinct + amount * (1.0f + RunState.GetEffectTotal("InstinctGain.Multiplier")), 100.0f);
        if (Instinct >= 100.0f) EnterWolfForm();
    }

    private void EnterWolfForm()
    {
        Instinct = 0.0f;
        _wolfTimer = CompendiumTuning.GetFloat("family.varcolac.wolf_seconds", 6.0f) + RunState.GetEffectTotal("WolfFormDuration.Flat");
        var origin = PlayerBody.GlobalPosition;
        FamilyFx.Pulse(this, origin, 3.5f, VarcolacColor, 0.5f);
        GetTree().CallGroup("hud", "AppendGameLog", "Virgil assume a Forma de Lobo.");
        if (RunState.GetEffectTotal("WolfEntryBurst.Multiplier") > 0.0f)
        {
            _entryInvulnerability = 2.0f;
            DamageEnemiesNear(origin, 4.0f, BaseHitDamage() * RunState.GetEffectTotal("WolfEntryBurst.Multiplier"), null, "WolfEntryBurst");
            FamilyFx.Pulse(this, origin, 4.0f, new Color(0.86f, 0.64f, 0.39f), 0.55f);
        }
        if (RunState.GetEffectTotal("WolfHowl.Flat") > 0.0f)
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (node is not EnemyBasic enemy || enemy.GlobalPosition.DistanceTo(origin) > 5.5f) continue;
                enemy.ApplyKnockback(enemy.GlobalPosition - origin, 7.0f, 0.0f);
                enemy.ApplyStatusLock(RunState.GetEffectParameter("WolfHowl", "stun", 1.2f));
            }
            FamilyFx.Pulse(this, origin, 5.5f, new Color(0.96f, 0.82f, 0.61f), 0.6f);
        }
    }

    // ---------- Liaozhai ----------

    private void TickLiaozhai(float d)
    {
        if (RunState.GetEffectTotal("BaguaPulse.Multiplier") <= 0.0f) return;
        _baguaTimer -= d;
        if (_baguaTimer > 0.0f) return;
        _baguaTimer = 1.5f;
        var zones = new List<TalismanZone>();
        foreach (var zone in TalismanZone.All)
            if (IsInstanceValid(zone) && zone.GlobalPosition.DistanceTo(PlayerBody.GlobalPosition) <= 14.0f) zones.Add(zone);
        if (zones.Count < 3) return;
        var center = Vector3.Zero;
        foreach (var zone in zones) center += zone.GlobalPosition;
        center /= zones.Count;
        var radius = 0.0f;
        foreach (var zone in zones) radius = Mathf.Max(radius, zone.GlobalPosition.DistanceTo(center));
        radius += 1.2f;
        FamilyFx.Pulse(this, center, radius, LiaozhaiColor, 0.5f);
        DamageEnemiesNear(center, radius, BaseHitDamage() * RunState.GetEffectTotal("BaguaPulse.Multiplier"), null, "BaguaPulse");
    }

    // ---------- Nachtalb ----------

    private float SleepSeconds() => CompendiumTuning.GetFloat("family.nachtalb.sleep_seconds", 3.0f) + RunState.GetEffectTotal("SleepDuration.Flat");

    private void TickNachtalb(float d)
    {
        var aura = RunState.GetEffectTotal("DrowsyAura.Flat");
        if (aura > 0.0f)
        {
            _auraTimer -= d;
            if (_auraTimer <= 0.0f)
            {
                _auraTimer = 0.5f;
                foreach (var node in GetTree().GetNodesInGroup("enemies"))
                    if (node is EnemyBasic enemy && enemy.GlobalPosition.DistanceTo(PlayerBody.GlobalPosition) <= 5.0f)
                        enemy.AddDrowsiness(aura * 0.5f, SleepSeconds());
            }
        }
        if (RunState.GetEffectTotal("WitchingHour.Flat") > 0.0f && _witchingCooldown <= 0.0f)
        {
            _witchingScanTimer -= d;
            if (_witchingScanTimer > 0.0f) return;
            _witchingScanTimer = 0.4f;
            var panicked = 0;
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy && enemy.IsPanicked) panicked++;
            if (panicked < 3) return;
            _witchingCooldown = RunState.GetEffectParameter("WitchingHour", "cooldown", 30.0f);
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy) enemy.FallAsleep(RunState.GetEffectParameter("WitchingHour", "sleep", 3.0f));
            FamilyFx.Pulse(this, PlayerBody.GlobalPosition, 14.0f, NachtalbColor, 0.9f);
            GetTree().CallGroup("hud", "AppendGameLog", "Hora das Bruxas: a sala inteira adormece.");
        }
    }

    // ---------- Ganchos de combate ----------

    // Chamado em ModifyOutgoingDamage: devolve o bônus aditivo de dano das famílias novas contra este alvo.
    private float FamilyDamageBonus(EnemyBasic enemy)
    {
        var bonus = 0.0f;
        if (enemy == null) return bonus;
        if (_hasStrigoi)
        {
            if (enemy.IsAttackedFromBehind(PlayerBody.GlobalPosition)) bonus += Bonus("BackstabDamage", RunState.GetEffectTotal("BackstabDamage.Multiplier"));
            if (enemy.IsMarked) bonus += Bonus("DamageVsMarked", RunState.GetEffectTotal("DamageVsMarked.Multiplier"));
        }
        if (IsWolfForm) bonus += Bonus("WolfFormDamage", CompendiumTuning.GetFloat("family.varcolac.wolf_damage", 0.25f) + RunState.GetEffectTotal("WolfFormDamage.Multiplier"));
        if (enemy.IsSealed)
        {
            bonus += Bonus("DamageVsSealed", RunState.GetEffectTotal("DamageVsSealed.Multiplier"));
            if (enemy.Rarity != EnemyRarity.Normal || enemy.Name.ToString().Contains("Boss"))
                bonus += Bonus("SealedEliteBossDamage", RunState.GetEffectTotal("SealedEliteBossDamage.Multiplier"));
        }
        if (enemy.IsSleeping)
        {
            bonus += Bonus("DamageVsSleeping", RunState.GetEffectTotal("DamageVsSleeping.Multiplier"));
            if (RunState.GetEffectTotal("SleepParalysis.Flat") > 0.0f) bonus += Bonus("SleepParalysis", 0.5f);
        }
        return bonus;
    }

    private float FamilyAttackSpeedBonus() => IsWolfForm ? CompendiumTuning.GetFloat("family.varcolac.wolf_attack_speed", 0.2f) : 0.0f;

    private float FamilyMovementBonus()
    {
        var bonus = 0.0f;
        if (IsWolfForm) bonus += CompendiumTuning.GetFloat("family.varcolac.wolf_move_speed", 0.25f);
        if (_hasVarcolac)
        {
            var maxTargets = Mathf.RoundToInt(RunState.GetEffectParameter("MoveSpeedPerMauledEnemy", "maxTargets", 5));
            var mauled = 0;
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy && enemy.IsMauled && ++mauled >= maxTargets) break;
            bonus += Mathf.Min(mauled, maxTargets) * RunState.GetEffectTotal("MoveSpeedPerMauledEnemy.Multiplier");
        }
        if (_hasLiaozhai && TalismanZone.IsInside(PlayerBody.GlobalPosition))
            bonus += RunState.GetEffectTotal("MoveSpeedInTalisman.Multiplier");
        return bonus;
    }

    // Retorna o multiplicador (>0) do dano que o jogador vai receber, vindo das famílias novas. 0 = imune.
    private float FamilyIncomingMultiplier()
    {
        if (_mistTimer > 0.0f || _entryInvulnerability > 0.0f) return 0.0f;
        var multiplier = 1.0f;
        if (IsWolfForm) multiplier *= 1.0f + CompendiumTuning.GetFloat("family.varcolac.wolf_damage_taken", 0.30f);
        var reduction = 0.0f;
        if (_hasLiaozhai && RunState.GetEffectTotal("DamageReductionFromSealed.Multiplier") > 0.0f && AnyEnemyNear(6.0f, e => e.IsSealed))
            reduction += RunState.GetEffectTotal("DamageReductionFromSealed.Multiplier");
        if (_hasNachtalb && RunState.GetEffectTotal("DamageReductionFromDrowsy.Multiplier") > 0.0f && AnyEnemyNear(6.0f, e => e.IsDrowsy || e.IsSleeping))
            reduction += RunState.GetEffectTotal("DamageReductionFromDrowsy.Multiplier");
        return multiplier * Mathf.Clamp(1.0f - reduction, 0.1f, 1.0f);
    }

    private bool AnyEnemyNear(float radius, System.Func<EnemyBasic, bool> predicate)
    {
        var origin = PlayerBody.GlobalPosition;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
            if (node is EnemyBasic enemy && enemy.GlobalPosition.DistanceTo(origin) <= radius && predicate(enemy)) return true;
        return false;
    }

    private void FamilyOnPlayerHit(EnemyBasic enemy, float damage, bool critical, PlayerAttackType attackType)
    {
        var quick = attackType == PlayerAttackType.Quick;
        var special = attackType == PlayerAttackType.Special;
        if (_hasStrigoi)
        {
            if (quick && Roll("QuickAttackShadowMarkChance")) enemy.ApplyShadowMark(RunState.GetEffectParameter("QuickAttackShadowMarkChance", "duration", 5.0f));
            if (special && Roll("SpecialAttackShadowMarkChance")) enemy.ApplyShadowMark(RunState.GetEffectParameter("SpecialAttackShadowMarkChance", "duration", 6.0f));
            if (_shadowPackTimer > 0.0f)
            {
                var echo = damage * RunState.GetEffectParameter("ShadowPackOnMarkedKill", "echo", 0.35f) * 2.0f;
                RunStats.Attribute("ShadowPackOnMarkedKill");
                enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(echo, false, "ShadowPackOnMarkedKill");
            }
        }
        if (_hasVarcolac)
        {
            AddInstinct(CompendiumTuning.GetFloat("family.varcolac.instinct_per_hit", 7.0f));
            if (quick && Roll("QuickAttackMaulChance")) enemy.ApplyMaul(1, RunState.GetEffectParameter("QuickAttackMaulChance", "duration", 5.0f));
        }
        if (_hasLiaozhai)
        {
            if (quick && Roll("QuickAttackSealChance")) enemy.ApplySeal(RunState.GetEffectParameter("QuickAttackSealChance", "duration", 3.0f));
        }
        if (_hasNachtalb)
        {
            enemy.AddDrowsiness(CompendiumTuning.GetFloat("family.nachtalb.drowsiness_per_hit", 16.0f) * (1.0f + RunState.GetEffectTotal("DrowsinessGain.Multiplier")), SleepSeconds());
            if (quick && Roll("QuickAttackDrowsyChance")) enemy.AddDrowsiness(RunState.GetEffectParameter("QuickAttackDrowsyChance", "amount", 45.0f), SleepSeconds());
        }
    }

    private static bool Roll(string effectType) => GD.Randf() < Mathf.Clamp(RunState.GetEffectTotal(effectType + ".Flat"), 0.0f, 1.0f);

    private void FamilyOnEnemyKilled(EnemyBasic enemy)
    {
        var maxHealth = _health?.MaxHealth ?? 0.0f;
        if (_hasStrigoi && enemy.IsMarked)
        {
            _health?.Heal(maxHealth * RunState.GetEffectTotal("HealOnMarkedKill.Flat"));
            var refund = RunState.GetEffectParameter("HealOnMarkedKill", "dashRefund", 0.0f);
            if (refund > 0.0f) (GetParent() as PlayerController)?.RefundDashCooldown(refund);
            if (RunState.GetEffectTotal("ShadowPackOnMarkedKill.Multiplier") > 0.0f || RunState.GetEffectTotal("ShadowPackOnMarkedKill.Flat") > 0.0f)
                _shadowPackTimer = RunState.GetEffectParameter("ShadowPackOnMarkedKill", "duration", 8.0f);
        }
        if (_hasVarcolac && IsWolfForm)
        {
            _health?.Heal(maxHealth * RunState.GetEffectTotal("HealOnWolfKill.Flat"));
            _wolfTimer += RunState.GetEffectTotal("WolfKillExtend.Flat");
        }
        if (_hasLiaozhai && enemy.IsSealed)
        {
            var targets = Mathf.RoundToInt(RunState.GetEffectTotal("SealSpreadOnKill.Flat"));
            var spread = 0;
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (spread >= targets) break;
                if (node is EnemyBasic other && other != enemy && !other.IsSealed && other.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= 5.0f)
                {
                    other.ApplySeal(3.0f);
                    spread++;
                }
            }
        }
        if (_hasNachtalb && enemy.IsSleeping)
        {
            _health?.Heal(maxHealth * RunState.GetEffectTotal("HealOnSleepingKill.Flat"));
            var coins = Mathf.RoundToInt(RunState.GetEffectParameter("HealOnSleepingKill", "coins", 0.0f));
            if (coins > 0) RunState.AddRunBloodCoins(coins);
            if (RunState.GetEffectTotal("NightmareSovereign.Flat") > 0.0f)
                foreach (var node in GetTree().GetNodesInGroup("enemies"))
                    if (node is EnemyBasic other && other != enemy) other.ExtendSleep(RunState.GetEffectParameter("NightmareSovereign", "extend", 2.0f));
        }
    }

    // ---------- Dash ----------

    public void OnDashStarted(Vector3 direction)
    {
        _dashHit.Clear();
        var body = PlayerBody;
        var origin = body.GlobalPosition;
        if (_hasStrigoi)
        {
            if (RunState.GetEffectTotal("DashShadowClone.Flat") > 0.0f)
            {
                var delay = RunState.GetEffectParameter("DashShadowClone", "delay", 1.0f);
                var radius = RunState.GetEffectParameter("DashShadowClone", "radius", 2.6f);
                var damage = BaseHitDamage() * RunState.GetEffectParameter("DashShadowClone", "damage", 1.0f) * RunState.GetEffectTotal("DashShadowClone.Flat");
                FamilyFx.Pulse(this, origin, 0.9f, StrigoiColor, delay);
                GetTree().CreateTimer(delay).Timeout += () =>
                {
                    if (!IsInstanceValid(this)) return;
                    FamilyFx.Pulse(this, origin, radius, StrigoiColor, 0.4f);
                    DamageEnemiesNear(origin, radius, damage, null, "DashShadowClone");
                };
            }
            if (RunState.GetEffectTotal("DashTeleportBehindMarked.Flat") > 0.0f)
            {
                EnemyBasic target = null;
                var best = RunState.GetEffectParameter("DashTeleportBehindMarked", "range", 12.0f);
                foreach (var node in GetTree().GetNodesInGroup("enemies"))
                    if (node is EnemyBasic enemy && enemy.IsMarked && enemy.GlobalPosition.DistanceTo(origin) < best)
                    {
                        best = enemy.GlobalPosition.DistanceTo(origin);
                        target = enemy;
                    }
                if (target != null)
                {
                    var behind = target.GlobalTransform.Basis.Z;
                    behind.Y = 0.0f;
                    behind = behind.LengthSquared() > 0.001f ? behind.Normalized() : Vector3.Back;
                    var destination = target.GlobalPosition + behind * 1.3f;
                    FamilyFx.Pulse(this, origin, 1.2f, StrigoiColor, 0.35f);
                    body.GlobalPosition = new Vector3(destination.X, origin.Y, destination.Z);
                    FamilyFx.Pulse(this, body.GlobalPosition, 1.2f, StrigoiColor, 0.35f);
                }
            }
        }
        if (IsWolfForm && RunState.GetEffectTotal("WolfDashShockwave.Multiplier") > 0.0f)
        {
            FamilyFx.Pulse(this, origin, 3.4f, VarcolacColor, 0.4f);
            DamageEnemiesNear(origin, 3.4f, BaseHitDamage() * RunState.GetEffectTotal("WolfDashShockwave.Multiplier"), null, "WolfDashShockwave");
        }
    }

    // A cada quadro do dash: atravessar inimigos (Névoa Rasteira, Peso no Peito).
    public void OnDashTick(Vector3 position)
    {
        var strigoiPass = _hasStrigoi && RunState.GetEffectTotal("DashPassDamage.Multiplier") > 0.0f;
        var sleepPass = _hasNachtalb && RunState.GetEffectTotal("DashDamageSleeping.Multiplier") > 0.0f;
        if (!strigoiPass && !sleepPass) return;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic enemy || _dashHit.Contains(enemy) || enemy.GlobalPosition.DistanceTo(position) > 1.5f) continue;
            _dashHit.Add(enemy);
            if (strigoiPass)
            {
                RunStats.Attribute("DashPassDamage");
                enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(BaseHitDamage() * RunState.GetEffectTotal("DashPassDamage.Multiplier"), false, "DashPassDamage");
                enemy.ApplyShadowMark(RunState.GetEffectParameter("DashPassDamage", "duration", 5.0f));
            }
            if (sleepPass && enemy.IsSleeping)
            {
                RunStats.Attribute("DashDamageSleeping");
                enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(BaseHitDamage() * RunState.GetEffectTotal("DashDamageSleeping.Multiplier"), false, "DashDamageSleeping");
            }
        }
    }

    // Esquiva na janela de um golpe inimigo (PlayerController.OnPerfectDodge).
    public void OnPerfectDodgeFamily()
    {
        if (!_hasStrigoi) return;
        if (RunState.GetEffectTotal("PerfectDodgeGuaranteedCrit.Flat") > 0.0f) RunState.GrantGuaranteedCritical();
        if (RunState.GetEffectTotal("MistFormAfterPerfectDodge.Flat") > 0.0f && !_mistUsedThisRoom)
        {
            _mistUsedThisRoom = true;
            _mistTimer = RunState.GetEffectParameter("MistFormAfterPerfectDodge", "duration", 2.0f);
            FamilyFx.Pulse(this, PlayerBody.GlobalPosition, 2.0f, StrigoiColor, 0.5f);
        }
    }

    private void SpawnSpecialTalisman(Vector3 position, float range, WeaponAttackShape shape)
    {
        if (!_hasLiaozhai || RunState.GetEffectTotal("SpecialAttackTalisman.Flat") <= 0.0f) return;
        var radius = Mathf.Clamp(shape == WeaponAttackShape.Radial ? range * 0.8f : range * 0.5f, 1.8f, 3.2f);
        TalismanZone.Spawn(this, position, radius, RunState.GetEffectParameter("SpecialAttackTalisman", "duration", 6.0f));
    }
}

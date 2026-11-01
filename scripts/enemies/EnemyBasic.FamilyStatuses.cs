using Godot;

// Estados das quatro famílias novas de herança de sangue no inimigo:
// Marca da Sombra (Strigoi), Dilaceração (Vârcolac), Selo (Liaozhai) e Pesadelo (Nachtalb: Sonolência, Sono e Pânico).
// Os efeitos que alimentam esses estados ficam em BloodBlessingRuntime.Families.cs; aqui só estado, tempo e leitura.
public partial class EnemyBasic
{
    public const int MaxMauledStacks = 5;
    public const int MaxSealStacks = 5;
    public const float DrowsinessLimit = 100.0f;

    private float _markTimer;
    private int _mauledStacks;
    private float _mauledTimer;
    private int _sealStacks;
    private float _sealTimer;
    private bool _sealPrisonUsed;
    private float _drowsiness;
    private float _drowsyDecayDelay;
    private float _sleepTimer;
    private float _panicTimer;
    private bool _panicAttacksAllies;
    private float _statusLockTimer;
    private bool _wasMarked;
    private bool _wasMauled;
    private bool _wasSealed;
    private bool _wasNightmare;

    public bool IsMarked => _markTimer > 0.0f;
    public int MauledStacks => _mauledStacks;
    public bool IsMauled => _mauledStacks > 0;
    public bool IsSealed => _sealStacks > 0;
    public int SealStacks => _sealStacks;
    public bool IsDrowsy => _drowsiness > 0.0f && !IsSleeping;
    public bool IsSleeping => _sleepTimer > 0.0f;
    public bool IsPanicked => _panicTimer > 0.0f;
    public float PanicRemaining => _panicTimer;
    public bool IsStatusLocked => _statusLockTimer > 0.0f;
    private bool IsBossLike => Name.ToString().Contains("Boss") || Rarity == EnemyRarity.Legendary;

    // Dano extra recebido por causa dos estados das famílias novas (somado em DamageTakenMultiplier).
    private float FamilyDamageTakenBonus()
    {
        var bonus = 0.0f;
        if (IsMarked) bonus += CompendiumTuning.GetFloat("family.strigoi.mark_damage_taken", 0.10f);
        if (IsMarked && RunState.GetEffectTotal("EternalNight.Flat") > 0.0f) bonus += RunState.GetEffectParameter("EternalNight", "damageTaken", 0.20f);
        bonus += _mauledStacks * CompendiumTuning.GetFloat("family.varcolac.maul_damage_taken", 0.04f);
        bonus += _sealStacks * CompendiumTuning.GetFloat("family.liaozhai.seal_damage_taken", 0.06f);
        if ((IsSleeping || IsPanicked) && RunState.GetEffectTotal("NightmareSovereign.Flat") > 0.0f)
            bonus += RunState.GetEffectParameter("NightmareSovereign", "damageTaken", 0.20f);
        return bonus;
    }

    public void ApplyShadowMark(float seconds)
    {
        var duration = seconds;
        if (RunState.GetEffectTotal("EternalNight.Flat") > 0.0f) duration = 9999.0f;
        _markTimer = Mathf.Max(_markTimer, duration);
        UpdateStatusIcons();
    }

    public void ApplyMaul(int stacks, float seconds)
    {
        var before = _mauledStacks;
        _mauledStacks = Mathf.Min(_mauledStacks + Mathf.Max(stacks, 1), MaxMauledStacks);
        _mauledTimer = Mathf.Max(_mauledTimer, seconds);
        if (_mauledStacks >= MaxMauledStacks && before < MaxMauledStacks)
            ApplyStatusLock(CompendiumTuning.GetFloat("family.varcolac.maul_stun_seconds", 1.0f));
        UpdateStatusIcons();
    }

    public void ApplySeal(float seconds)
    {
        var bonus = RunState.GetEffectTotal("SealDuration.Flat");
        _sealTimer = Mathf.Max(_sealTimer, seconds + bonus);
        if (IsBossLike) _sealTimer = Mathf.Min(_sealTimer, seconds * 0.5f + bonus);
        _sealStacks = Mathf.Min(_sealStacks + 1, MaxSealStacks);
        var bellStacks = Mathf.RoundToInt(RunState.GetEffectParameter("SealBellStun", "stacks", 3));
        if (RunState.GetEffectTotal("SealBellStun.Flat") > 0.0f && _sealStacks == bellStacks)
            ApplyStatusLock(RunState.GetEffectParameter("SealBellStun", "duration", 1.5f));
        if (RunState.GetEffectTotal("SealPrison.Flat") > 0.0f && _sealStacks >= MaxSealStacks && !_sealPrisonUsed)
        {
            _sealPrisonUsed = true;
            ApplyStatusLock(RunState.GetEffectParameter("SealPrison", "duration", 3.0f));
        }
        UpdateStatusIcons();
    }

    // Talismã: mantém o selo vivo (e o cria com 1 acúmulo) sem empilhar até a prisão.
    public void RefreshSeal(float seconds)
    {
        if (!IsSealed) { ApplySeal(seconds); return; }
        _sealTimer = Mathf.Max(_sealTimer, seconds);
    }

    public void AddDrowsiness(float amount, float sleepSeconds)
    {
        if (IsBossLike || IsSleeping) return;
        _drowsiness += amount;
        _drowsyDecayDelay = 3.0f;
        if (_drowsiness >= DrowsinessLimit)
        {
            _drowsiness = 0.0f;
            FallAsleep(sleepSeconds);
        }
        UpdateStatusIcons();
    }

    public void FallAsleep(float seconds)
    {
        if (IsBossLike || seconds <= 0.0f) return;
        _sleepTimer = Mathf.Max(_sleepTimer, seconds);
        _drowsiness = 0.0f;
        _knockbackVelocity = Vector3.Zero;
        UpdateStatusIcons();
    }

    // Prolonga o sono atual (Soberano do Pesadelo): não acorda ninguém.
    public void ExtendSleep(float seconds)
    {
        if (IsSleeping) _sleepTimer += seconds;
    }

    public void ApplyPanic(float seconds, bool attackAllies)
    {
        if (IsBossLike) return;
        _panicTimer = Mathf.Max(_panicTimer, seconds);
        _panicAttacksAllies |= attackAllies;
        UpdateStatusIcons();
    }

    // Atordoamento/prisão que vem de um estado (sino, prisão, dilaceração máxima). Chefes e lendários resistem.
    public void ApplyStatusLock(float seconds)
    {
        if (IsBossLike) seconds *= 0.25f;
        _statusLockTimer = Mathf.Max(_statusLockTimer, seconds);
    }

    private void TickFamilyStatuses(float delta)
    {
        if (_markTimer > 0.0f) _markTimer -= delta;
        if (_mauledStacks > 0)
        {
            _mauledTimer -= delta;
            if (_mauledTimer <= 0.0f) _mauledStacks = 0;
        }
        if (_sealStacks > 0)
        {
            _sealTimer -= delta;
            if (_sealTimer <= 0.0f) { _sealStacks = 0; _sealPrisonUsed = false; }
            // Selado fica rígido: lento. Só reduz, nunca acelera.
            _slowTimer = Mathf.Max(_slowTimer, 0.15f);
            _slowMultiplier = Mathf.Min(_slowMultiplier, CompendiumTuning.GetFloat("family.liaozhai.seal_slow", 0.5f));
            if (RunState.GetEffectTotal("SealedNoHeal.Flat") > 0.0f && _health != null && !_health.IsDead)
                _health.ApplyDamage(_health.MaxHealth * RunState.GetEffectTotal("SealedNoHeal.Flat") * delta, false, "SealedNoHeal");
            if (Archetype == EnemyArchetype.Ranged && RunState.GetEffectTotal("SealedNoRanged.Flat") > 0.0f)
                _attackCooldownRemaining = Mathf.Max(_attackCooldownRemaining, 0.25f);
        }
        if (_drowsiness > 0.0f)
        {
            _drowsyDecayDelay -= delta;
            if (_drowsyDecayDelay <= 0.0f) _drowsiness = Mathf.Max(_drowsiness - 10.0f * delta, 0.0f);
        }
        if (_sleepTimer > 0.0f)
        {
            _sleepTimer -= delta;
            if (_sleepTimer <= 0.0f) WakeUpInPanic();
        }
        if (_panicTimer > 0.0f)
        {
            _panicTimer -= delta;
            if (_panicTimer <= 0.0f) _panicAttacksAllies = false;
        }
        if (_statusLockTimer > 0.0f) _statusLockTimer -= delta;

        if (IsMarked != _wasMarked || IsMauled != _wasMauled || IsSealed != _wasSealed || (IsSleeping || IsDrowsy || IsPanicked) != _wasNightmare)
            UpdateStatusIcons();
    }

    // O dormente acorda em Pânico (Nachtalb). Sem a família equipada o pânico é só o susto de 2 s.
    private void WakeUpInPanic()
    {
        var panic = CompendiumTuning.GetFloat("family.nachtalb.panic_seconds", 3.0f);
        var chain = Mathf.RoundToInt(RunState.GetEffectTotal("PanicChain.Flat"));
        var attackAllies = RunState.GetEffectTotal("PanicAttacksAllies.Flat") > 0.0f;
        panic += RunState.GetEffectTotal("PanicAttacksAllies.Flat");
        ApplyPanic(panic, attackAllies);
        var wakeDamage = RunState.GetEffectTotal("PanicWakeDamage.Multiplier");
        if (wakeDamage > 0.0f)
        {
            RunStats.Attribute("PanicWakeDamage");
            _health?.ApplyDamage(_health.MaxHealth * wakeDamage, false, "PanicWakeDamage");
        }
        if (chain > 0)
        {
            var spread = 0;
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (spread >= chain) break;
                if (node is EnemyBasic other && other != this && !other.IsPanicked && !other.IsSleeping
                    && other.GlobalPosition.DistanceTo(GlobalPosition) <= CompendiumTuning.GetFloat("family.nachtalb.panic_chain_radius", 5.0f))
                {
                    other.ApplyPanic(panic, attackAllies);
                    spread++;
                }
            }
        }
    }

    // Verdadeiro quando o inimigo não age neste quadro (sono, prisão/atordoamento de estado).
    private bool FamilyStatusBlocksActing()
    {
        if (!IsSleeping && !IsStatusLocked) return false;
        Velocity = Vector3.Zero;
        MoveAndSlide();
        return true;
    }

    private void RunPanicBehavior(float delta)
    {
        if (_panicAttacksAllies)
        {
            RunCharmedBehavior(delta);
            return;
        }
        var away = GlobalPosition - _player.GlobalPosition;
        away.Y = 0.0f;
        if (away.LengthSquared() < 0.01f) away = Vector3.Right;
        away = away.Normalized();
        LookAt(GlobalPosition + away, Vector3.Up);
        Velocity = away * MoveSpeed * 1.1f * _slowMultiplier;
        MoveAndSlide();
    }
}

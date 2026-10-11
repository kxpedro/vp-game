using Godot;
using System;

public partial class PlayerSkillController : Node
{
    [Export] public NodePath CombatPath { get; set; } = new("../PlayerCombat");
    [Export] public NodePath HealthPath { get; set; } = new("../Health");

    private PlayerCombat _combat;
    private Health _health;
    private float _perfectDodgeDamageTimer;
    private float _perfectDodgeMoveTimer;

    public override void _Ready()
    {
        AddToGroup("player_skills");
        _combat = GetNodeOrNull<PlayerCombat>(CombatPath);
        _health = GetNodeOrNull<Health>(HealthPath);
    }

    public override void _Process(double delta)
    {
        _perfectDodgeDamageTimer = Mathf.Max(_perfectDodgeDamageTimer - (float)delta, 0.0f);
        _perfectDodgeMoveTimer = Mathf.Max(_perfectDodgeMoveTimer - (float)delta, 0.0f);
    }

    public void OnEnemyDefeated()
    {
        var healFraction = EffectValue("HealOnKill");
        if (healFraction <= 0.0f || _health == null)
            return;

        var healAmount = _health.MaxHealth * healFraction;
        _health.Heal(healAmount);
        SpawnLifeStealNumber(healAmount);
    }

    public void OnRoomCleared()
    {
        var heal = EffectValue("HealOnRoomClear");
        if (heal > 0.0f)
            _health?.Heal(heal);
    }

    public void OnPerfectDodge()
    {
        var damageDuration = EffectParameter("PerfectDodgeDamage", "durationSeconds", 3.0f);
        var moveDuration = EffectParameter("PerfectDodgeMoveSpeed", "durationSeconds", 3.0f);
        if (EffectValue("PerfectDodgeDamage") > 0.0f)
            _perfectDodgeDamageTimer = Mathf.Max(_perfectDodgeDamageTimer, damageDuration * TemporaryEffectDurationMultiplier());
        if (EffectValue("PerfectDodgeMoveSpeed") > 0.0f)
            _perfectDodgeMoveTimer = Mathf.Max(_perfectDodgeMoveTimer, moveDuration * TemporaryEffectDurationMultiplier());
    }

    public float ModifyOutgoingDamage(float damage, EnemyBasic enemy, bool isBackstab, PlayerAttackType attackType)
    {
        var bonus = EffectValue("Damage");
        if (attackType == PlayerAttackType.Basic) bonus += EffectValue("BasicAttackDamage");
        if (attackType == PlayerAttackType.Quick) bonus += EffectValue("QuickAttackDamage");
        if (attackType == PlayerAttackType.Special) bonus += EffectValue("SpecialAttackDamage");
        var enemyHealth = enemy?.GetNodeOrNull<Health>("Health");
        if (enemyHealth != null && Mathf.IsEqualApprox(enemyHealth.CurrentHealth, enemyHealth.MaxHealth))
            bonus += EffectValue("DamageVsFullHealth");
        if (isBackstab)
            bonus += EffectValue("BackstabDamage");
        if (enemy != null && enemy.NegativeStatusCount >= Mathf.RoundToInt(EffectParameter("DamageVsStatus", "minimumStatusCount", 1.0f)))
            bonus += EffectValue("DamageVsStatus");
        if (HealthRatio() <= EffectParameter("LowHealthDamage", "healthThreshold", 0.3f))
            bonus += EffectValue("LowHealthDamage");
        if (_perfectDodgeDamageTimer > 0.0f)
            bonus += EffectValue("PerfectDodgeDamage");
        return damage * Mathf.Max(1.0f + bonus, 0.0f);
    }

    public float ModifyIncomingDamage(float damage)
    {
        var modifier = EffectValue("DamageTaken");
        var threshold = EffectParameter("LowHealthDamageTaken", "healthThreshold", 0.3f);
        if (HealthRatio() <= threshold)
            modifier += EffectValue("LowHealthDamageTaken");
        return damage * Mathf.Max(1.0f + modifier, 0.05f);
    }

    public float MovementMultiplier()
    {
        var bonus = EffectValue("MoveSpeed");
        if (HealthRatio() <= EffectParameter("LowHealthMoveSpeed", "healthThreshold", 0.1f))
            bonus += EffectValue("LowHealthMoveSpeed");
        if (_perfectDodgeMoveTimer > 0.0f)
            bonus += EffectValue("PerfectDodgeMoveSpeed");
        return 1.0f + bonus;
    }

    public float MaxHealthBonus() => EffectValue("MaxHealth");

    /// <summary>
    /// Desconto nos precos de artefatos da loja da Camara (humanitas.collector_eye: 0,05 por rank = 5/10/15%).
    /// Estatico para a loja consultar pela cena da Camara sem depender do no do jogador (le o rank em CharacterSheet).
    /// </summary>
    public static float ShopPriceDiscount() => Mathf.Max(EffectValue("ShopPriceDiscount"), 0.0f);

    public float TemporaryEffectDurationMultiplier() => 1.0f + EffectValue("TemporaryEffectDuration");
    public float FuryDamageBonus() => EffectValue("FuryDamage");

    /// <summary>Multiplicador das Moedas de Sangue da run (RunState.RunBloodCoins) durante a Furia; nao afeta o Sangue Amaldicoado permanente.</summary>
    public float BloodCoinMultiplier()
    {
        return _combat?.IsFuryActive == true ? 1.0f + EffectValue("BloodCoinsDuringFury") : 1.0f;
    }

    public bool TryPreventFatalDamage(out float restoredHealth)
    {
        restoredHealth = 0.0f;
        if (EffectValue("PreventFatalDamage") <= 0.0f || _health == null || !RunState.TryConsumeSkillFatalProtection())
            return false;
        restoredHealth = _health.MaxHealth * EffectParameter("PreventFatalDamage", "restoreMaxHealthFraction", 0.3f);
        return true;
    }

    public bool CanRerollReward()
    {
        return EffectValue("RewardOfferReroll") > 0.0f && RunState.CanConsumeSkillRewardReroll();
    }

    public bool TryConsumeRewardReroll()
    {
        return CanRerollReward() && RunState.TryConsumeSkillRewardReroll();
    }

    private float HealthRatio() => _health != null && _health.MaxHealth > 0.0f ? _health.CurrentHealth / _health.MaxHealth : 1.0f;

    private static float EffectValue(string effectType)
    {
        var total = 0.0f;
        foreach (var skillId in CharacterSheet.AcquiredSkills)
        {
            if (!SkillCatalog.TryGet(skillId, out var skill) || !skill.IsImplemented) continue;
            foreach (var effect in skill.Effects)
                if (effect.Type.Equals(effectType, StringComparison.OrdinalIgnoreCase))
                {
                    var rank = CharacterSheet.GetSkillRank(skillId);
                    if (effect.Values.Length == 1)
                        total += effect.Values[0] * rank;
                    else if (rank > 0)
                        total += effect.Values[Mathf.Min(rank - 1, effect.Values.Length - 1)];
                }
        }
        return total;
    }

    private static float EffectParameter(string effectType, string parameter, float fallback)
    {
        foreach (var skillId in CharacterSheet.AcquiredSkills)
        {
            if (!SkillCatalog.TryGet(skillId, out var skill) || !skill.IsImplemented) continue;
            foreach (var effect in skill.Effects)
                if (effect.Type.Equals(effectType, StringComparison.OrdinalIgnoreCase) && effect.NumberParameters.TryGetValue(parameter, out var value))
                    return value;
        }
        return fallback;
    }

    private async void SpawnLifeStealNumber(float amount)
    {
        var label = new Label3D
        {
            Text = $"+{Mathf.RoundToInt(amount)}", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 70, Modulate = new Color(0.25f, 1.0f, 0.38f), OutlineSize = 10,
            OutlineModulate = Colors.Black, Position = Vector3.Up * 2.35f
        };
        GetParent()?.AddChild(label);
        var tween = label.CreateTween();
        tween.TweenProperty(label, "position", label.Position + Vector3.Up * 0.9f, 0.5f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.5f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }
}

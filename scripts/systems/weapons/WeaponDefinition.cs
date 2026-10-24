using Godot;

public enum PlayerAttackType
{
    Basic,
    Quick,
    Special
}

public enum WeaponAttackShape
{
    Arc,
    Line,
    Radial
}

public enum PlayerAttackPhase
{
    None,
    Startup,
    Recovery
}

public enum AttackCancelAction
{
    Dodge,
    Special
}

public sealed record AttackControlProfile(
    float InputBufferSeconds = 0.20f,
    bool DodgeCancelsStartup = true,
    float DodgeCancelRecoveryStart = 0.0f,
    bool SpecialCancelsStartup = false,
    float SpecialCancelRecoveryStart = 0.45f,
    float StartupMovementMultiplier = 0.35f,
    float RecoveryMovementMultiplier = 0.65f,
    float AimAssistRadius = 4.25f,
    float AimAssistConeDegrees = 30.0f,
    float AimAssistStrength = 0.65f,
    float RetargetAtImpactStrength = 0.18f
)
{
    public static AttackControlProfile Default { get; } = new();
}

// NameKey: chave em content.json (weapon.<arma>.<golpe>.name). Name resolve no idioma atual; se receber um texto
// que não é chave (ex.: golpe recriado com o Name já traduzido), devolve o próprio texto.
public sealed record WeaponAttackDefinition(
    string NameKey,
    float Damage,
    float HitDelay,
    float Recovery,
    float Knockback,
    float Range,
    float Width,
    float SlashTilt,
    WeaponAttackShape Shape = WeaponAttackShape.Arc,
    float StaggerDuration = 0.24f,
    AttackControlProfile Control = null,
    // 0 = acerta todos os alvos na área; N = só os N mais próximos na direção do golpe (tiro do revólver).
    int MaxTargets = 0
)
{
    public string Name => Localization.Has(NameKey) ? Localization.Text(NameKey) : NameKey ?? "";
}

// Nome e descrição vêm de content.json: weapon.<id>.name e weapon.<id>.desc.
public sealed record WeaponDefinition(
    string Id,
    WeaponAttackDefinition[] BasicCombo,
    WeaponAttackDefinition QuickAttack,
    WeaponAttackDefinition SpecialAttack,
    float BasicAttackSpeedMultiplier = 1.0f,
    float AttackArcDegrees = 118.0f,
    float HitboxEdgeForgiveness = 1.15f,
    float QuickAttackHitboxScale = 0.5f,
    float QuickAttackDamageMultiplier = 1.35f,
    float BackstabDamageMultiplier = 1.5f,
    float WallImpactDamageMultiplier = 0.3f,
    float BasicAttackKnockbackMultiplier = 3.0f,
    float QuickAttackKnockbackMultiplier = 2.7f,
    float SpecialAttackKnockbackMultiplier = 2.5f,
    float LineAttackLungeDistance = 0.8f,
    float SpecialAttackCooldown = 0.26f,
    // Multiplicadores gerais da arma (antes eram os ajustes rápidos globais game.damage/attack_delay/attack_range).
    float DamageMultiplier = 1.0f,
    float AttackDelayMultiplier = 1.0f,
    float RangeMultiplier = 1.3f,
    // false = arma placeholder (WeaponCatalog.Placeholder): sem moveset próprio, só aparece como "Em breve".
    bool IsImplemented = true,
    // Arma de tiro: golpes em Linha viram disparo instantâneo (sem avanço, rastro de bala, param na parede).
    bool IsRanged = false,
    // Munição: 0 = sem munição. Com tambor vazio a recarga é automática; o especial pode recarregar o tambor.
    int MagazineSize = 0,
    float ReloadSeconds = 0.0f,
    bool SpecialReloadsMagazine = false,
    // false = o último golpe do combo não é finalizador (sem empurrão/visual de finalizador; ex.: disparos alternados).
    bool HasComboFinisher = true,
    // true = segurar o ataque básico repete o golpe.
    bool AutoFire = false
)
{
    // Valores efetivos: chaves weapon.<id>.<campo> do CompendiumTuning (padrao = valor do catalogo).
    private float Tuned(string field, float fallback) => CompendiumTuning.GetFloat($"weapon.{Id}.{field}", fallback);
    public float TunedBasicAttackSpeedMultiplier => Tuned("basic_attack_speed", BasicAttackSpeedMultiplier);
    public float TunedAttackArcDegrees => Tuned("arc_degrees", AttackArcDegrees);
    public float TunedHitboxEdgeForgiveness => Tuned("edge_forgiveness", HitboxEdgeForgiveness);
    public float TunedQuickAttackHitboxScale => Tuned("quick_hitbox_scale", QuickAttackHitboxScale);
    public float TunedQuickAttackDamageMultiplier => Tuned("quick_damage_mult", QuickAttackDamageMultiplier);
    public float TunedBackstabDamageMultiplier => Tuned("backstab_mult", BackstabDamageMultiplier);
    public float TunedWallImpactDamageMultiplier => Tuned("wall_impact_mult", WallImpactDamageMultiplier);
    public float TunedBasicAttackKnockbackMultiplier => Tuned("knockback_basic", BasicAttackKnockbackMultiplier);
    public float TunedQuickAttackKnockbackMultiplier => Tuned("knockback_quick", QuickAttackKnockbackMultiplier);
    public float TunedSpecialAttackKnockbackMultiplier => Tuned("knockback_special", SpecialAttackKnockbackMultiplier);
    public float TunedLineAttackLungeDistance => Tuned("lunge", LineAttackLungeDistance);
    public float TunedSpecialAttackCooldown => Tuned("special_cooldown", SpecialAttackCooldown);
    public float TunedDamageMultiplier => Tuned("damage_mult", DamageMultiplier);
    public float TunedAttackDelayMultiplier => Tuned("attack_delay_mult", AttackDelayMultiplier);
    public float TunedRangeMultiplier => Tuned("range_mult", RangeMultiplier);
    public int TunedMagazineSize => Mathf.Max(Mathf.RoundToInt(Tuned("magazine", MagazineSize)), 0);
    public float TunedReloadSeconds => Mathf.Max(Tuned("reload_time", ReloadSeconds), 0.05f);
    public string DisplayName => Localization.Text($"weapon.{Id}.name");
    public string Description => Localization.Text($"weapon.{Id}.desc");
}

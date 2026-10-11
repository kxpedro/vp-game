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
    AttackControlProfile Control = null
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
    // false = arma placeholder (WeaponCatalog.Placeholder): sem moveset próprio, só aparece como "Em breve".
    bool IsImplemented = true
)
{
    public string DisplayName => Localization.Text($"weapon.{Id}.name");
    public string Description => Localization.Text($"weapon.{Id}.desc");
}

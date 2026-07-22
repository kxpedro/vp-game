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

public sealed record WeaponAttackDefinition(
    string Name,
    float Damage,
    float HitDelay,
    float Recovery,
    float Knockback,
    float Range,
    float Width,
    float SlashTilt,
    WeaponAttackShape Shape = WeaponAttackShape.Arc,
    float StaggerDuration = 0.24f
);

public sealed record WeaponDefinition(
    string Id,
    string DisplayName,
    string Description,
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
    float SpecialAttackCooldown = 0.26f
);

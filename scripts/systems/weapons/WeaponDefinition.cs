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
    WeaponAttackShape Shape = WeaponAttackShape.Arc
);

public sealed record WeaponDefinition(
    string Id,
    string DisplayName,
    string Description,
    WeaponAttackDefinition[] BasicCombo,
    WeaponAttackDefinition QuickAttack,
    WeaponAttackDefinition SpecialAttack
);

public sealed record WeaponAttackDefinition(
    string Name,
    float Damage,
    float HitDelay,
    float Recovery,
    float Knockback,
    float Range,
    float Width,
    float SlashTilt
);

public sealed record WeaponDefinition(
    string Id,
    string DisplayName,
    string Description,
    WeaponAttackDefinition[] LightCombo,
    WeaponAttackDefinition HeavyAttack,
    WeaponAttackDefinition DashAttack
);

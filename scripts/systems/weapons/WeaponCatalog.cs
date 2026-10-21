using System.Collections.Generic;

// Nomes de armas e golpes: chaves em data/localization/<idioma>/content.json (weapon.<id>.name, weapon.<id>.desc,
// weapon.<id>.<golpe>.name). IDs são persistidos no save: não renomear sem mapear em Get.
public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Weapons =
    {
        new(
            "longsword",
            new[]
            {
                new WeaponAttackDefinition(
                    "weapon.longsword.basic_1.name", 32, 0.12f, 0.12f, 4.2f, 1.55f, 1.65f, 0.25f,
                    StaggerDuration: 0.20f,
                    Control: new(
                        InputBufferSeconds: 0.22f,
                        DodgeCancelsStartup: true,
                        DodgeCancelRecoveryStart: 0.0f,
                        SpecialCancelsStartup: false,
                        SpecialCancelRecoveryStart: 0.25f,
                        StartupMovementMultiplier: 0.42f,
                        RecoveryMovementMultiplier: 0.75f,
                        AimAssistRadius: 4.0f,
                        AimAssistConeDegrees: 34.0f,
                        AimAssistStrength: 0.72f,
                        RetargetAtImpactStrength: 0.22f)),
                new WeaponAttackDefinition(
                    "weapon.longsword.basic_2.name", 38, 0.11f, 0.11f, 4.8f, 1.75f, 1.75f, -0.25f,
                    StaggerDuration: 0.24f,
                    Control: new(
                        InputBufferSeconds: 0.20f,
                        DodgeCancelsStartup: true,
                        DodgeCancelRecoveryStart: 0.0f,
                        SpecialCancelsStartup: false,
                        SpecialCancelRecoveryStart: 0.35f,
                        StartupMovementMultiplier: 0.34f,
                        RecoveryMovementMultiplier: 0.68f,
                        AimAssistRadius: 4.2f,
                        AimAssistConeDegrees: 31.0f,
                        AimAssistStrength: 0.68f,
                        RetargetAtImpactStrength: 0.18f)),
                new WeaponAttackDefinition(
                    "weapon.longsword.basic_3.name", 52, 0.16f, 0.12f, 7.2f, 2.85f, 0.85f, 0,
                    WeaponAttackShape.Line, 0.34f,
                    Control: new(
                        InputBufferSeconds: 0.18f,
                        DodgeCancelsStartup: true,
                        DodgeCancelRecoveryStart: 0.0f,
                        SpecialCancelsStartup: false,
                        SpecialCancelRecoveryStart: 0.15f,
                        StartupMovementMultiplier: 0.16f,
                        RecoveryMovementMultiplier: 0.68f,
                        AimAssistRadius: 5.2f,
                        AimAssistConeDegrees: 22.0f,
                        AimAssistStrength: 0.82f,
                        RetargetAtImpactStrength: 0.12f))
            },
            new(
                "weapon.longsword.quick.name", 68, 0.08f, 0.18f, 7.4f, 2.45f, 2.05f, -0.1f,
                WeaponAttackShape.Radial, 0.28f,
                Control: new(
                    InputBufferSeconds: 0.20f,
                    DodgeCancelsStartup: false,
                    DodgeCancelRecoveryStart: 0.60f,
                    SpecialCancelsStartup: false,
                    SpecialCancelRecoveryStart: 0.45f,
                    StartupMovementMultiplier: 0.0f,
                    RecoveryMovementMultiplier: 0.78f,
                    AimAssistRadius: 3.8f,
                    AimAssistConeDegrees: 38.0f,
                    AimAssistStrength: 0.38f,
                    RetargetAtImpactStrength: 0.0f)),
            new(
                "weapon.longsword.special.name", 56, 0.12f, 0.30f, 11.0f, 3.15f, 3.15f, 0.0f,
                WeaponAttackShape.Radial, 0.44f,
                Control: new(
                    InputBufferSeconds: 0.18f,
                    DodgeCancelsStartup: false,
                    DodgeCancelRecoveryStart: 0.40f,
                    SpecialCancelsStartup: false,
                    SpecialCancelRecoveryStart: 1.0f,
                    StartupMovementMultiplier: 0.08f,
                    RecoveryMovementMultiplier: 0.36f,
                    AimAssistRadius: 3.4f,
                    AimAssistConeDegrees: 45.0f,
                    AimAssistStrength: 0.22f,
                    RetargetAtImpactStrength: 0.0f)),
            SpecialAttackCooldown: 1.0f),
        // Revolver Duplo (docs/design: "Novas armas do Virgil"): disparo instantaneo alternando as maos, tambor de
        // 12 balas com recarga automatica; o rapido e um tiro curto em leque; a Bala de Prata perfura e recarrega.
        new(
            "dual_revolvers",
            new[]
            {
                RevolverShot("weapon.dual_revolvers.basic_1.name"),
                RevolverShot("weapon.dual_revolvers.basic_2.name")
            },
            new(
                "weapon.dual_revolvers.quick.name", 36, 0.06f, 0.16f, 5.0f, 3.2f, 2.4f, 0.0f,
                WeaponAttackShape.Arc, 0.30f,
                Control: new(
                    InputBufferSeconds: 0.18f,
                    DodgeCancelsStartup: false,
                    DodgeCancelRecoveryStart: 0.50f,
                    SpecialCancelsStartup: false,
                    SpecialCancelRecoveryStart: 0.30f,
                    StartupMovementMultiplier: 0.0f,
                    RecoveryMovementMultiplier: 0.80f,
                    AimAssistRadius: 4.0f,
                    AimAssistConeDegrees: 40.0f,
                    AimAssistStrength: 0.45f,
                    RetargetAtImpactStrength: 0.0f)),
            new(
                "weapon.dual_revolvers.special.name", 70, 0.20f, 0.25f, 6.0f, 12.0f, 0.9f, 0.0f,
                WeaponAttackShape.Line, 0.60f,
                Control: new(
                    InputBufferSeconds: 0.18f,
                    DodgeCancelsStartup: true,
                    DodgeCancelRecoveryStart: 0.30f,
                    SpecialCancelsStartup: false,
                    SpecialCancelRecoveryStart: 1.0f,
                    StartupMovementMultiplier: 0.20f,
                    RecoveryMovementMultiplier: 0.60f,
                    AimAssistRadius: 12.0f,
                    AimAssistConeDegrees: 10.0f,
                    AimAssistStrength: 0.50f,
                    RetargetAtImpactStrength: 0.30f)),
            AttackArcDegrees: 90.0f,
            HitboxEdgeForgiveness: 0.3f,
            QuickAttackHitboxScale: 1.0f,
            QuickAttackDamageMultiplier: 1.0f,
            BackstabDamageMultiplier: 1.25f,
            BasicAttackKnockbackMultiplier: 1.0f,
            QuickAttackKnockbackMultiplier: 1.5f,
            SpecialAttackKnockbackMultiplier: 1.5f,
            LineAttackLungeDistance: 0.0f,
            SpecialAttackCooldown: 2.0f,
            RangeMultiplier: 1.0f,
            IsRanged: true,
            MagazineSize: 12,
            ReloadSeconds: 0.9f,
            SpecialReloadsMagazine: true,
            HasComboFinisher: false,
            AutoFire: true),
        Placeholder("whip")
    };

    public static IReadOnlyList<WeaponDefinition> All => Weapons;

    public static WeaponDefinition Get(string id)
    {
        // Old saves used these placeholder ids. Both now migrate to the playable Great Sword.
        if (id is "greatsword" or "dual_blades")
            id = "longsword";

        foreach (var weapon in Weapons)
            if (weapon.Id == id)
                return weapon;

        return Weapons[0];
    }

    // Disparo básico do revólver (as duas mãos têm o mesmo golpe; muda só o nome e a mão do rastro).
    private static WeaponAttackDefinition RevolverShot(string nameKey) => new(
        nameKey, 18, 0.05f, 0.10f, 1.2f, 10.0f, 0.45f, 0.0f,
        WeaponAttackShape.Line, 0.08f,
        Control: new(
            InputBufferSeconds: 0.16f,
            DodgeCancelsStartup: true,
            DodgeCancelRecoveryStart: 0.0f,
            SpecialCancelsStartup: true,
            SpecialCancelRecoveryStart: 0.0f,
            StartupMovementMultiplier: 0.80f,
            RecoveryMovementMultiplier: 0.90f,
            AimAssistRadius: 10.0f,
            AimAssistConeDegrees: 12.0f,
            AimAssistStrength: 0.55f,
            RetargetAtImpactStrength: 0.0f),
        MaxTargets: 1);

    // Textos (nome, descrição) em content.json: weapon.<id>.name/.desc. O golpe genérico usa o nome da arma.
    private static WeaponDefinition Placeholder(string id)
    {
        var attack = new WeaponAttackDefinition($"weapon.{id}.name", 32, 0.6f, 0.3f, 4, 1.5f, 1.5f, 0);
        return new(id, new[] { attack }, attack, attack, IsImplemented: false);
    }
}

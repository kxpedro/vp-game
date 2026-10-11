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
        Placeholder("dual_revolvers"),
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

    // Textos (nome, descrição) em content.json: weapon.<id>.name/.desc. O golpe genérico usa o nome da arma.
    private static WeaponDefinition Placeholder(string id)
    {
        var attack = new WeaponAttackDefinition($"weapon.{id}.name", 32, 0.6f, 0.3f, 4, 1.5f, 1.5f, 0);
        return new(id, new[] { attack }, attack, attack, IsImplemented: false);
    }
}

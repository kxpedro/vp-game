using System.Collections.Generic;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Weapons =
    {
        new(
            "longsword", "Espada Grande", "Combo de tres cortes pesados, avanco rapido e giro especial em 360 graus.",
            new[]
            {
                new WeaponAttackDefinition("Espada grande I", 32, 0.12f, 0.12f, 4.2f, 1.55f, 1.65f, 0.25f, StaggerDuration: 0.20f),
                new WeaponAttackDefinition("Espada grande II", 38, 0.11f, 0.11f, 4.8f, 1.75f, 1.75f, -0.25f, StaggerDuration: 0.24f),
                new WeaponAttackDefinition("Estocada da espada grande", 52, 0.16f, 0.2f, 7.2f, 2.85f, 0.85f, 0, WeaponAttackShape.Line, 0.34f)
            },
            new("Corte de avanco", 68, 0.08f, 0.18f, 7.4f, 2.45f, 2.05f, -0.1f, WeaponAttackShape.Radial, 0.28f),
            new("Circulo Carmesim", 56, 0.12f, 0.30f, 11.0f, 3.15f, 3.15f, 0.0f, WeaponAttackShape.Radial, 0.44f),
            SpecialAttackCooldown: 1.0f),
        Placeholder("dual_revolvers", "Revolver Duplo", "Combate a distancia, alternando disparos entre as maos."),
        Placeholder("whip", "Chicote", "Grande alcance e controle de grupos.")
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

    private static WeaponDefinition Placeholder(string id, string name, string description)
    {
        var attack = new WeaponAttackDefinition(name, 32, 0.6f, 0.3f, 4, 1.5f, 1.5f, 0);
        return new(id, name, $"{description} Moveset em desenvolvimento.", new[] { attack }, attack, attack);
    }
}

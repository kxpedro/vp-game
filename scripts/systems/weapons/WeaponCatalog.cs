using System.Collections.Generic;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Weapons =
    {
        new(
            "longsword", "Espada Longa", "Moveset equilibrado atual, com combo de tres golpes.",
            new[]
            {
                new WeaponAttackDefinition("Espada longa I", 32, 0.12f, 0.12f, 4.2f, 1.55f, 1.65f, 0.25f),
                new WeaponAttackDefinition("Espada longa II", 38, 0.11f, 0.11f, 4.8f, 1.75f, 1.75f, -0.25f),
                new WeaponAttackDefinition("Espada longa III", 52, 0.16f, 0.18f, 6.4f, 2.05f, 1.95f, 0)
            },
            new("Espada longa pesado", 78, 0.26f, 0.24f, 8.5f, 2.35f, 2.15f, -0.42f),
            new("Ataque rápido", 68, 0.12f, 0.18f, 7.4f, 2.25f, 2.05f, -0.1f)),
        Placeholder("greatsword", "Espada Grande", "Golpes lentos, amplos e de alto impacto."),
        Placeholder("dual_revolvers", "Revolver Duplo", "Combate a distancia, alternando disparos entre as maos."),
        Placeholder("whip", "Chicote", "Grande alcance e controle de grupos."),
        Placeholder("dual_blades", "Espadas Duplas", "Combos rapidos e agressivos com muitos acertos.")
    };

    public static IReadOnlyList<WeaponDefinition> All => Weapons;

    public static WeaponDefinition Get(string id)
    {
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

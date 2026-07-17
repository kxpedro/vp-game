using System.Collections.Generic;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Weapons =
    {
        new(
            "longsword", "Espada Longa", "Combo de tres cortes, avanco rapido e giro especial em 360 graus.",
            new[]
            {
                new WeaponAttackDefinition("Espada longa I", 32, 0.12f, 0.12f, 4.2f, 1.55f, 1.65f, 0.25f),
                new WeaponAttackDefinition("Espada longa II", 38, 0.11f, 0.11f, 4.8f, 1.75f, 1.75f, -0.25f),
                new WeaponAttackDefinition("Estocada da espada longa", 52, 0.16f, 0.2f, 7.2f, 2.85f, 0.85f, 0, WeaponAttackShape.Line)
            },
            new("Corte de avanco", 68, 0.08f, 0.18f, 7.4f, 2.45f, 2.05f, -0.1f, WeaponAttackShape.Radial),
            new("Circulo Carmesim", 56, 0.28f, 0.42f, 11.0f, 3.15f, 3.15f, 0.0f, WeaponAttackShape.Radial)),
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

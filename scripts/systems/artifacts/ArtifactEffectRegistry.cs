using System.Collections.Generic;

public static class ArtifactEffectRegistry
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["Damage"] = "Dano", ["MaxHealth"] = "Vida maxima", ["Heal"] = "Cura",
        ["CriticalChance"] = "Chance de critico", ["CriticalDamage"] = "Dano critico",
        ["MoveSpeed"] = "Velocidade de movimento", ["AttackSpeed"] = "Velocidade de ataque",
        ["DamageBurst"] = "Explosao de dano", ["DamageVsBoss"] = "Dano contra chefes",
        ["DamageVsElite"] = "Dano contra inimigos especiais", ["RareDropChance"] = "Chance de recompensa rara",
        ["CriticalLifeSteal"] = "Roubo de vida em criticos", ["ExecuteThreshold"] = "Limite de execucao",
        ["PreventFatalDamage"] = "Impede dano fatal uma vez", ["EnemyTimeScale"] = "Lentidao dos inimigos",
        ["SummonSpectralServants"] = "Servos espectrais", ["FearArea"] = "Medo em area",
        ["Intangible"] = "Intangibilidade", ["Duration"] = "Duracao",
        ["BasicAttackDamage"] = "Dano de ataque basico",
        ["BasicAttackLifeSteal"] = "Roubo de vida de ataque basico",
        ["BasicAttackBleed"] = "Sangramento em ataque basico",
        ["BasicAttackPestilence"] = "Pestilencia em ataque basico",
        ["BasicAttackCharmChance"] = "Chance de Encanto em ataque basico",
        ["QuickAttackDamage"] = "Dano de ataque rápido",
        ["QuickAttackCharmChance"] = "Chance de Encanto no ataque rápido",
        ["PestilenceDeathSpreadRadius"] = "Raio de contagio na morte",
        ["PestilenceAlsoBleeds"] = "Pestilencia tambem causa Sangramento",
        ["BleedingAlsoPestilence"] = "Sangramento tambem causa Pestilencia",
        ["CharmedTargetBleed"] = "Sangramento contra alvos encantados",
        ["HealOnPestilentKill"] = "Cura ao matar inimigo pestilento",
        ["LifeStealVsBleeding"] = "Roubo de vida contra alvos sangrando",
        ["SpecialAttackDamage"] = "Dano de ataque especial",
        ["SpecialAttackBloodPool"] = "Poca de sangue do ataque especial",
        ["UnlockBestialFury"] = "Desbloqueia Furia Bestial (F)",
        ["FuryDamage"] = "Dano durante Furia Bestial",
        ["FuryDuration"] = "Duracao da Furia Bestial"
    };

    public static string FriendlyName(string type) => Names.TryGetValue(type, out var name) ? name : SplitPascalCase(type);

    private static string SplitPascalCase(string value)
    {
        var result = "";
        foreach (var c in value)
        {
            if (char.IsUpper(c) && result.Length > 0) result += " ";
            result += c;
        }
        return result;
    }
}

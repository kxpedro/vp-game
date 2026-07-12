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
        ["Intangible"] = "Intangibilidade", ["Duration"] = "Duracao"
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

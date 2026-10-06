using System.Collections.Generic;
using System.Globalization;

// Nomes legíveis (PT-BR) dos tipos de efeito de artefatos e Bênçãos de Sangue, mais um formatador de valor com unidade.
// Todo "type" usado em data/artifacts/artifacts.json e data/blessings/blood_blessings.json deve ter entrada em Names;
// sem ela, FriendlyName cai no SplitPascalCase e o nome aparece em inglês na UI.
public static class ArtifactEffectRegistry
{
    private static readonly Dictionary<string, string> Names = new()
    {
        // Atributos gerais
        ["Damage"] = "Dano",
        ["MaxHealth"] = "Vida máxima",
        ["Heal"] = "Cura",
        ["CriticalChance"] = "Chance de crítico",
        ["CriticalDamage"] = "Dano crítico",
        ["NonCriticalDamage"] = "Dano de golpes não críticos",
        ["MoveSpeed"] = "Velocidade de movimento",
        ["AttackSpeed"] = "Velocidade de ataque",
        ["DamageBurst"] = "Explosão de dano",
        ["DamageVsBoss"] = "Dano contra chefes",
        ["DamageVsElite"] = "Dano contra inimigos especiais",
        ["DamageTakenFromNormal"] = "Dano sofrido de inimigos comuns",
        ["RareDropChance"] = "Chance de recompensa rara",
        ["CriticalLifeSteal"] = "Roubo de vida em críticos",
        ["ExecuteThreshold"] = "Limite de execução",
        ["ExecuteThresholdOnCritical"] = "Limite de execução após crítico",
        ["PreventFatalDamage"] = "Impede dano fatal uma vez",
        ["InvulnerabilityOnFatal"] = "Invulnerabilidade após dano fatal",
        ["DamageOnFatal"] = "Dano após dano fatal",
        ["EnemyTimeScale"] = "Lentidão dos inimigos",
        ["SummonSpectralServants"] = "Servos espectrais",
        ["FearArea"] = "Medo em área",
        ["Intangible"] = "Intangibilidade",
        ["Duration"] = "Duração",

        // Condicionais de artefatos
        ["AttackSpeedMissingHealth"] = "Velocidade de ataque por vida perdida",
        ["MoveSpeedMissingHealth"] = "Velocidade de movimento por vida perdida",
        ["AttackSpeedOnKill"] = "Velocidade de ataque ao abater",
        ["MoveSpeedOnHit"] = "Velocidade de movimento ao sofrer dano",
        ["FlawlessKillDamageStack"] = "Dano por abate sem sofrer dano",
        ["FlawlessKillLifeStealStack"] = "Roubo de vida por abate sem sofrer dano",
        ["ResetStacksOnHit"] = "Perde os acúmulos ao ser atingido",
        ["CurrentHealthCostOnRoomEnter"] = "Custo de vida atual ao entrar na sala",
        ["DamageUntilRoomClear"] = "Dano até limpar a sala",
        ["DamagePerCurrencyHeld"] = "Dano por moeda guardada",
        ["DamagePenaltyOnHeal"] = "Penalidade de dano ao se curar",

        // Ataques (Bênçãos de Sangue)
        ["BasicAttackDamage"] = "Dano do ataque básico",
        ["BasicAttackLifeSteal"] = "Roubo de vida no ataque básico",
        ["BasicAttackBleed"] = "Sangramento no ataque básico",
        ["BasicAttackPestilence"] = "Pestilência no ataque básico",
        ["BasicAttackCharmChance"] = "Chance de Encanto no ataque básico",
        ["QuickAttackDamage"] = "Dano do ataque rápido",
        ["QuickAttackLifeSteal"] = "Roubo de vida no ataque rápido",
        ["QuickAttackBleedChance"] = "Chance de Sangramento no ataque rápido",
        ["QuickAttackPestilenceChance"] = "Chance de Pestilência no ataque rápido",
        ["QuickAttackCharmChance"] = "Chance de Encanto no ataque rápido",
        ["SpecialAttackDamage"] = "Dano do ataque especial",
        ["SpecialAttackLifeSteal"] = "Roubo de vida no ataque especial",
        ["SpecialAttackPestilenceChance"] = "Chance de Pestilência no ataque especial",
        ["SpecialAttackCharmChance"] = "Chance de Encanto no ataque especial",
        ["SpecialAttackBloodPool"] = "Poça de sangue do ataque especial",
        ["UnlockBestialFury"] = "Desbloqueia Fúria Bestial (F)",
        ["FuryDamage"] = "Dano durante a Fúria Bestial",
        ["FuryDuration"] = "Eficiência da Fúria Bestial",

        // Dracul
        ["BloodOrbDropChance"] = "Chance de Gota de Sangue",
        ["BloodOrbHealMaxHealth"] = "Cura da Gota de Sangue",
        ["DamageAboveHealth"] = "Dano com a vida alta",
        ["NearbyEnemyDamage"] = "Dano de inimigos próximos",
        ["DamageAfterHealing"] = "Dano após se curar",
        ["OverhealToBarrier"] = "Excesso de cura em barreira",
        ["DamageOnEliteKill"] = "Dano ao abater elite",
        ["LifeStealVsLowHealthEnemy"] = "Roubo de vida contra alvos feridos",
        ["DamageWhileBarrier"] = "Dano com barreira",
        ["UnstoppableWhileBarrier"] = "Ininterrompível com barreira",
        ["PermanentDamageFromOverheal"] = "Dano permanente por excesso de cura",

        // Bathory
        ["BloodPoolOnKill"] = "Poça de sangue ao abater",
        ["BloodPoolHealPerSecond"] = "Cura por segundo na poça de sangue",
        ["CriticalChanceAtFullHealth"] = "Chance de crítico com a vida cheia",
        ["DamageReductionAfterHealing"] = "Redução de dano após se curar",
        ["BloodPoolArea"] = "Área da poça de sangue",
        ["BloodPoolDuration"] = "Duração da poça de sangue",
        ["AttackSpeedAfterHighHealthHeal"] = "Velocidade de ataque após cura com vida alta",
        ["BloodPoolRefreshOnKillChance"] = "Chance de estender a poça ao abater",
        ["HealOnBloodPoolKill"] = "Cura ao abater sobre sangue",
        ["DamageToRecoverableHealth"] = "Dano convertido em Ferida Recuperável",
        ["DamageOnBlood"] = "Dano sobre sangue",
        ["DamageReductionOnBlood"] = "Redução de dano sobre sangue",
        ["MoveSpeedOnBlood"] = "Velocidade de movimento sobre sangue",
        ["MinimumHealthOnBlood"] = "Vida mínima sobre sangue",

        // Orlok
        ["PestilenceDeathSpreadRadius"] = "Raio de contágio na morte",
        ["PestilenceSpreadTargets"] = "Alvos do contágio na morte",
        ["InfectedEnemyDamage"] = "Dano de inimigos pestilentos",
        ["PestilenceCloudAtStacks"] = "Nuvem pestilenta",
        ["InfectedCorpseExplosion"] = "Explosão de cadáver pestilento",
        ["DamageVsPlagueHost"] = "Dano contra Hospedeiros",
        ["PestilenceSpreadOnHostHit"] = "Contágio ao atingir Hospedeiro",
        ["PestilenceDurationLink"] = "Raio de vínculo da Pestilência",
        ["InfinitePestilenceSpread"] = "Contágio infinito da Pestilência",
        ["HealOnPestilentKill"] = "Cura ao abater inimigo pestilento",
        ["RatSummonOnPestilentKill"] = "Rato ao abater alvo pestilento",
        ["PestilenceExplosionBloodPool"] = "Poça sangrenta da explosão pestilenta",

        // Karnstein
        ["CharmedEnemyDamage"] = "Dano de inimigos encantados",
        ["CriticalChanceAfterDash"] = "Chance de crítico após esquiva",
        ["HealFromCharmedDamage"] = "Cura pelo dano de encantados",
        ["CharmedDamageVsElite"] = "Dano de encantados contra elites",
        ["DamagePerCharmedEnemy"] = "Dano por inimigo encantado",
        ["MoveSpeedPerCharmedEnemy"] = "Velocidade de movimento por inimigo encantado",
        ["CharmedDeathExplosion"] = "Explosão ao morrer encantado",
        ["CharmDamageLink"] = "Vínculo de dano do Encanto",
        ["SacrificeCharmedOnFatalDamage"] = "Sacrifício de encantado ao sofrer dano fatal",
        ["PermanentCharm"] = "Encanto permanente",
        ["FascinationDamageTaken"] = "Dano sofrido sob Fascínio",

        // Stirps (combinações)
        ["PestilenceAlsoBleeds"] = "Pestilência também causa Sangramento",
        ["BleedingAlsoPestilence"] = "Sangramento também causa Pestilência",
        ["CharmedTargetBleed"] = "Sangramento contra alvos encantados",
        ["LifeStealVsBleeding"] = "Roubo de vida contra alvos sangrando"
    };

    // Tipos cujo valor "AddFlat"/"SetFlat"/"ApplyFlat" é uma fração (0.2 = 20%).
    private static readonly HashSet<string> FlatPercentTypes = new()
    {
        "CriticalChance", "ExecuteThreshold", "ExecuteThresholdOnCritical", "CriticalChanceAtFullHealth",
        "CriticalChanceAfterDash", "BloodOrbDropChance", "BloodOrbHealMaxHealth", "BloodPoolRefreshOnKillChance",
        "HealOnBloodPoolKill", "SacrificeCharmedOnFatalDamage", "BloodPoolHealPerSecond"
    };

    // Tipos cujo valor plano é uma duração em segundos.
    private static readonly HashSet<string> SecondsTypes = new()
    {
        "Duration", "BloodPoolDuration", "InvulnerabilityOnFatal"
    };

    // Tipos cujo valor plano é "por segundo".
    private static readonly HashSet<string> PerSecondTypes = new()
    {
        "BloodPoolHealPerSecond"
    };

    public static string FriendlyName(string type)
    {
        if (string.IsNullOrEmpty(type))
            return "";
        return Names.TryGetValue(type, out var name) ? name : SplitPascalCase(type);
    }

    public static bool HasFriendlyName(string type) => type != null && Names.ContainsKey(type);

    // Valor com sinal e unidade: multiplicadores e frações em "%", durações em "s", booleanos como "ativo"/"inativo".
    // Ex.: ("Damage", "AddMultiplier", 0.25) -> "+25%"; ("CriticalChance", "AddFlat", 0.2) -> "+20%";
    //      ("BloodPoolDuration", "AddFlat", 2.5) -> "+2,5 s"; ("InvulnerabilityOnFatal", "ApplyFlat", 3) -> "3 s".
    public static string FormatValue(string type, string operation, double value)
    {
        operation ??= "";
        type ??= "";
        // Invariante + vírgula decimal: não depende de ICU/cultura instalada (pt-BR pode faltar em modo invariante).
        var culture = CultureInfo.InvariantCulture;
        // Operações que aplicam/definem um valor absoluto não levam sinal.
        var signed = !operation.StartsWith("Apply") && !operation.StartsWith("Set");

        if (operation is "Set" or "SetBool")
            return value > 0 ? "ativo" : "inativo";

        if (operation.Contains("Multiplier") || FlatPercentTypes.Contains(type))
        {
            var percent = value * 100.0;
            var pattern = System.Math.Abs(percent) < 1.0 && percent != 0.0 ? "0.##" : "0.#";
            var text = percent.ToString(signed ? $"+{pattern};-{pattern};0" : pattern, culture).Replace('.', ',') + "%";
            return PerSecondTypes.Contains(type) ? text + "/s" : text;
        }

        var flat = value.ToString(signed ? "+0.##;-0.##;0" : "0.##", culture).Replace('.', ',');
        if (SecondsTypes.Contains(type))
            return flat + " s";
        if (PerSecondTypes.Contains(type))
            return flat + "/s";
        return flat;
    }

    // Linha completa "Nome: valor". Para booleanos ativos devolve só o nome.
    public static string FormatEffect(string type, string operation, double value)
    {
        if ((operation is "Set" or "SetBool") && value > 0)
            return FriendlyName(type);
        return $"{FriendlyName(type)}: {FormatValue(type, operation, value)}";
    }

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

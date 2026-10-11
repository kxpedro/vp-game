using System.Collections.Generic;
using System.Globalization;

// Nomes legíveis dos tipos de efeito de artefatos e Bênçãos de Sangue, mais um formatador de valor com unidade.
// Todo "type" usado em data/artifacts/artifacts.json e data/blessings/blood_blessings.json deve estar em KnownTypes
// e ter a chave artifact.effect.<Tipo> em data/localization/<idioma>/content.json; sem ela, FriendlyName cai no
// SplitPascalCase e o nome aparece cru na UI.
public static class ArtifactEffectRegistry
{
    // Tipos de efeito conhecidos. O nome exibido de cada um fica em content.json, chave artifact.effect.<Tipo>
    // (os validadores conferem a chave nos três idiomas).
    private static readonly HashSet<string> KnownTypes = new()
    {
        // Atributos gerais
        "Damage",
        "MaxHealth",
        "Heal",
        "CriticalChance",
        "CriticalDamage",
        "NonCriticalDamage",
        "MoveSpeed",
        "AttackSpeed",
        "DamageBurst",
        "DamageVsBoss",
        "DamageVsElite",
        "DamageTakenFromNormal",
        "RareDropChance",
        "CriticalLifeSteal",
        "ExecuteThreshold",
        "ExecuteThresholdOnCritical",
        "PreventFatalDamage",
        "InvulnerabilityOnFatal",
        "DamageOnFatal",
        "EnemyTimeScale",
        "SummonSpectralServants",
        "FearArea",
        "Intangible",
        "Duration",

        // Condicionais de artefatos
        "AttackSpeedMissingHealth",
        "MoveSpeedMissingHealth",
        "AttackSpeedOnKill",
        "MoveSpeedOnHit",
        "FlawlessKillDamageStack",
        "FlawlessKillLifeStealStack",
        "ResetStacksOnHit",
        "CurrentHealthCostOnRoomEnter",
        "DamageUntilRoomClear",
        "DamagePerCurrencyHeld",
        "DamagePenaltyOnHeal",

        // Ataques (Bênçãos de Sangue)
        "BasicAttackDamage",
        "BasicAttackLifeSteal",
        "BasicAttackBleed",
        "BasicAttackPestilence",
        "BasicAttackCharmChance",
        "QuickAttackDamage",
        "QuickAttackLifeSteal",
        "QuickAttackBleedChance",
        "QuickAttackPestilenceChance",
        "QuickAttackCharmChance",
        "SpecialAttackDamage",
        "SpecialAttackLifeSteal",
        "SpecialAttackPestilenceChance",
        "SpecialAttackCharmChance",
        "SpecialAttackBloodPool",
        "UnlockBestialFury",
        "FuryDamage",
        "FuryDuration",

        // Dracul
        "BloodOrbDropChance",
        "BloodOrbHealMaxHealth",
        "DamageAboveHealth",
        "NearbyEnemyDamage",
        "DamageAfterHealing",
        "OverhealToBarrier",
        "DamageOnEliteKill",
        "LifeStealVsLowHealthEnemy",
        "DamageWhileBarrier",
        "UnstoppableWhileBarrier",
        "PermanentDamageFromOverheal",

        // Bathory
        "BloodPoolOnKill",
        "BloodPoolHealPerSecond",
        "CriticalChanceAtFullHealth",
        "DamageReductionAfterHealing",
        "BloodPoolArea",
        "BloodPoolDuration",
        "AttackSpeedAfterHighHealthHeal",
        "BloodPoolRefreshOnKillChance",
        "HealOnBloodPoolKill",
        "DamageToRecoverableHealth",
        "DamageOnBlood",
        "DamageReductionOnBlood",
        "MoveSpeedOnBlood",
        "MinimumHealthOnBlood",

        // Orlok
        "PestilenceDeathSpreadRadius",
        "PestilenceSpreadTargets",
        "InfectedEnemyDamage",
        "PestilenceCloudAtStacks",
        "InfectedCorpseExplosion",
        "DamageVsPlagueHost",
        "PestilenceSpreadOnHostHit",
        "PestilenceDurationLink",
        "InfinitePestilenceSpread",
        "HealOnPestilentKill",
        "RatSummonOnPestilentKill",
        "PestilenceExplosionBloodPool",

        // Karnstein
        "CharmedEnemyDamage",
        "CriticalChanceAfterDash",
        "HealFromCharmedDamage",
        "CharmedDamageVsElite",
        "DamagePerCharmedEnemy",
        "MoveSpeedPerCharmedEnemy",
        "CharmedDeathExplosion",
        "CharmDamageLink",
        "SacrificeCharmedOnFatalDamage",
        "PermanentCharm",
        "FascinationDamageTaken",

        // Stirps (combinações)
        "PestilenceAlsoBleeds",
        "BleedingAlsoPestilence",
        "CharmedTargetBleed",
        "LifeStealVsBleeding"
    };

    // Tipos cujo valor "AddFlat"/"SetFlat"/"ApplyFlat" é uma fração (0.2 = 20%).
    private static readonly HashSet<string> FlatPercentTypes = new()
    {
        "CriticalChance", "ExecuteThreshold", "ExecuteThresholdOnCritical", "CriticalChanceAtFullHealth",
        "CriticalChanceAfterDash", "BloodOrbDropChance", "BloodOrbHealMaxHealth", "BloodPoolRefreshOnKillChance",
        "HealOnBloodPoolKill", "SacrificeCharmedOnFatalDamage", "BloodPoolHealPerSecond", "HealOnPestilentKill"
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
        var key = EffectNameKey(type);
        return Localization.Has(key) ? Localization.Text(key) : SplitPascalCase(type);
    }

    public static string EffectNameKey(string type) => $"artifact.effect.{type}";

    public static bool HasFriendlyName(string type) => type != null && KnownTypes.Contains(type) && Localization.Has(EffectNameKey(type));

    public static bool IsKnownType(string type) => type != null && KnownTypes.Contains(type);

    // Tipo cujo valor plano é uma fração exibida em % (0.2 = 20%).
    public static bool IsFractionType(string type) => type != null && FlatPercentTypes.Contains(type);

    // Número com até 2 casas e o separador decimal do idioma atual (en: ponto; pt-BR/es: vírgula).
    // Invariante + troca do separador: não depende de ICU/cultura instalada.
    public static string FormatNumber(double value, string pattern = "0.##")
    {
        var text = value.ToString(pattern, CultureInfo.InvariantCulture);
        return UsesDecimalPoint() ? text : text.Replace('.', ',');
    }

    private static bool UsesDecimalPoint() => Localization.Instance?.CurrentLanguage == "en";

    // Valor com sinal e unidade: multiplicadores e frações em "%", durações em "s", booleanos como "ativo"/"inativo".
    // Ex.: ("Damage", "AddMultiplier", 0.25) -> "+25%"; ("CriticalChance", "AddFlat", 0.2) -> "+20%";
    //      ("BloodPoolDuration", "AddFlat", 2.5) -> "+2,5 s" (en: "+2.5 s"); ("InvulnerabilityOnFatal", "ApplyFlat", 3) -> "3 s".
    public static string FormatValue(string type, string operation, double value)
    {
        operation ??= "";
        type ??= "";
        // Operações que aplicam/definem um valor absoluto não levam sinal.
        var signed = !operation.StartsWith("Apply") && !operation.StartsWith("Set");

        if (operation is "Set" or "SetBool")
            return Localization.Text(value > 0 ? "artifact.effect_value.active" : "artifact.effect_value.inactive");

        if (operation.Contains("Multiplier") || FlatPercentTypes.Contains(type))
        {
            var percent = value * 100.0;
            var pattern = System.Math.Abs(percent) < 1.0 && percent != 0.0 ? "0.##" : "0.#";
            var text = FormatNumber(percent, signed ? $"+{pattern};-{pattern};0" : pattern) + "%";
            return PerSecondTypes.Contains(type) ? text + "/s" : text;
        }

        var flat = FormatNumber(value, signed ? "+0.##;-0.##;0" : "0.##");
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

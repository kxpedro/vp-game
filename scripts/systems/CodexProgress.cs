using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Categorias do diario de descobertas. Os valores sao persistidos no save: nao renomear.
/// </summary>
public static class CodexCategory
{
    /// <summary>Id = ArtifactDefinition.Id (data/artifacts/artifacts.json).</summary>
    public const string Artifact = "artifact";
    /// <summary>Id = BloodBlessingDefinition.Id (data/blessings/blood_blessings.json).</summary>
    public const string Blessing = "blessing";
    /// <summary>Id = SkillDefinition.Id (data/skills/skills.json), ex.: "bestialis.xxx".</summary>
    public const string Skill = "skill";
    /// <summary>Id = arquetipo em minusculas ("melee", "ranged", "tank") e variante "arquetipo:raridade" ("melee:rare").</summary>
    public const string Enemy = "enemy";
    /// <summary>Id = BossDefinition.Id do BossCatalog (ex.: "silver_dragonkin", "armand").</summary>
    public const string Boss = "boss";
    /// <summary>Id = golpe do moveset: "light_1", "light_2", "light_3", "special", "dash", "fury" (ver CodexAttackId).</summary>
    public const string Attack = "attack";
    /// <summary>Id = WeaponDefinition.Id resolvido por WeaponCatalog (ex.: "longsword").</summary>
    public const string Weapon = "weapon";

    public static readonly string[] All = { Artifact, Blessing, Skill, Enemy, Boss, Attack, Weapon };
}

/// <summary>
/// Ids dos golpes do moveset do jogador. Batem com o sufixo das chaves de tuning "attack.&lt;id&gt;.*"
/// usadas na aba "Ataques" do compendio (HUD.Compendium.cs).
/// </summary>
public static class CodexAttackId
{
    public const string Light1 = "light_1";
    public const string Light2 = "light_2";
    public const string Light3 = "light_3";
    public const string Special = "special";
    /// <summary>Ataque rapido (dash + ataque), PlayerAttackType.Quick.</summary>
    public const string Dash = "dash";
    /// <summary>Furia Bestial (F).</summary>
    public const string Fury = "fury";

    public static readonly string[] All = { Light1, Light2, Light3, Special, Dash, Fury };

    private static readonly string[] LightIds = { Light1, Light2, Light3 };

    /// <summary>Indice do combo basico (0-based) -> "light_1".."light_3" (sem alocar string a cada ataque).</summary>
    public static string Light(int comboIndex) => LightIds[Math.Clamp(comboIndex, 0, LightIds.Length - 1)];
}

/// <summary>
/// Diario de descobertas (compendio). Estado permanente por slot, salvo em GameSaveData.Discoveries.
///
/// Formato de id por categoria (ver CodexCategory):
///   artifact -> ArtifactDefinition.Id         | blessing -> BloodBlessingDefinition.Id
///   skill    -> SkillDefinition.Id            | boss     -> BossDefinition.Id (BossCatalog)
///   attack   -> CodexAttackId.*               | weapon   -> WeaponDefinition.Id
///   enemy    -> "&lt;arquetipo&gt;" e "&lt;arquetipo&gt;:&lt;raridade&gt;", tudo em minusculas,
///               ex.: "melee", "melee:normal", "ranged:legendary".
///               Use MarkEnemy(archetype, rarity), que grava os dois de uma vez, e
///               EnemyId(archetype) / EnemyId(archetype, rarity) para consultar.
///
/// Em build de debug (OS.IsDebugBuild()) IsDiscovered retorna sempre true, para conferir o conteudo.
/// IsDiscoveredRaw e Count ignoram esse atalho.
/// Nenhum custo por frame: MarkDiscovered de algo ja descoberto e O(1) e nao agenda save.
/// </summary>
public static class CodexProgress
{
    private static readonly Dictionary<string, HashSet<string>> Entries = new(StringComparer.Ordinal);
    private static int _debugUnlockAll = -1;

    /// <summary>Disparado quando algo e descoberto pela primeira vez: (categoria, id).</summary>
    public static event Action<string, string> Discovered;

    /// <summary>True em build de debug: tudo aparece como descoberto em IsDiscovered.</summary>
    public static bool DebugUnlockAll
    {
        get
        {
            if (_debugUnlockAll < 0)
                _debugUnlockAll = OS.IsDebugBuild() ? 1 : 0;
            return _debugUnlockAll == 1;
        }
    }

    /// <summary>Registra uma descoberta. Retorna true se for nova (e entao agenda autosave e dispara Discovered).</summary>
    public static bool MarkDiscovered(string category, string id)
    {
        if (!Add(category, id))
            return false;

        SaveGameManager.RequestAutosave();
        Discovered?.Invoke(category, id);
        return true;
    }

    /// <summary>Registra o arquetipo ("melee") e a variante ("melee:rare"). Retorna true se algum for novo.</summary>
    public static bool MarkEnemy(EnemyArchetype archetype, EnemyRarity rarity)
    {
        var baseNew = MarkDiscovered(CodexCategory.Enemy, EnemyId(archetype));
        var variantNew = MarkDiscovered(CodexCategory.Enemy, EnemyId(archetype, rarity));
        return baseNew || variantNew;
    }

    public static bool MarkBoss(string bossId) => MarkDiscovered(CodexCategory.Boss, bossId);

    public static bool MarkAttack(string attackId) => MarkDiscovered(CodexCategory.Attack, attackId);

    public static string EnemyId(EnemyArchetype archetype) => archetype.ToString().ToLowerInvariant();

    public static string EnemyId(EnemyArchetype archetype, EnemyRarity rarity)
        => $"{archetype.ToString().ToLowerInvariant()}:{rarity.ToString().ToLowerInvariant()}";

    /// <summary>Consulta para a UI: em build de debug sempre true.</summary>
    public static bool IsDiscovered(string category, string id) => DebugUnlockAll || IsDiscoveredRaw(category, id);

    /// <summary>Consulta sem o atalho de debug.</summary>
    public static bool IsDiscoveredRaw(string category, string id)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(id))
            return false;

        return Entries.TryGetValue(category, out var set) && set.Contains(id);
    }

    /// <summary>Quantidade real de descobertas na categoria (sem o atalho de debug).</summary>
    public static int Count(string category)
        => category != null && Entries.TryGetValue(category, out var set) ? set.Count : 0;

    /// <summary>Total real de descobertas em todas as categorias.</summary>
    public static int TotalCount()
    {
        var total = 0;
        foreach (var set in Entries.Values)
            total += set.Count;
        return total;
    }

    /// <summary>Ids descobertos (reais) da categoria, sem ordem garantida.</summary>
    public static IReadOnlyCollection<string> GetDiscovered(string category)
        => category != null && Entries.TryGetValue(category, out var set) ? set : Array.Empty<string>();

    public static void Reset() => Entries.Clear();

    public static Dictionary<string, List<string>> ToSaveData()
    {
        var data = new Dictionary<string, List<string>>();
        foreach (var pair in Entries)
        {
            if (pair.Value.Count == 0)
                continue;

            var ids = new List<string>(pair.Value);
            ids.Sort(StringComparer.Ordinal);
            data[pair.Key] = ids;
        }
        return data;
    }

    /// <summary>Substitui o estado atual pelo do save. Null (save antigo) = vazio. Categorias desconhecidas sao preservadas.</summary>
    public static void LoadSaveData(Dictionary<string, List<string>> data)
    {
        Reset();
        if (data == null)
            return;

        foreach (var pair in data)
        {
            if (pair.Value == null)
                continue;

            foreach (var id in pair.Value)
                Add(pair.Key, id);
        }
    }

    /// <summary>Registra sem autosave nem evento (carga de save / retrocompatibilidade).</summary>
    internal static bool MarkSilently(string category, string id) => Add(category, id);

    private static bool Add(string category, string id)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(id))
            return false;

        if (!Entries.TryGetValue(category, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            Entries[category] = set;
        }

        return set.Add(id);
    }
}

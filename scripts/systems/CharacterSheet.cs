using System.Collections.Generic;
using System.Linq;

public enum CharacterAttribute
{
    Strength,
    Dexterity,
    Vitality,
    Resolve,
    Bloodcraft
}

public readonly record struct ArtifactRecord(string Id, string Name, string Slot, string Effect, float Amount)
{
    public ArtifactRecord(string name, string effect, float amount)
        : this("", name, "Passive", effect, amount)
    {
    }
}

public static class CharacterSheet
{
    private const int StartingAttributeValue = 5;

    private static readonly Dictionary<CharacterAttribute, int> Attributes = new()
    {
        { CharacterAttribute.Strength, StartingAttributeValue },
        { CharacterAttribute.Dexterity, StartingAttributeValue },
        { CharacterAttribute.Vitality, StartingAttributeValue },
        { CharacterAttribute.Resolve, StartingAttributeValue },
        { CharacterAttribute.Bloodcraft, StartingAttributeValue }
    };

    private static readonly Dictionary<string, int> SkillRanks = new();

    /// <summary>Sangue Amaldicoado: recurso permanente (salvo) gasto na arvore de habilidades do hub. As moedas da run vivem em RunState.RunBloodCoins.</summary>
    public static int CursedBlood { get; private set; }

    public static IReadOnlyCollection<string> AcquiredSkills => SkillRanks.Keys;

    public static int GetAttribute(CharacterAttribute attribute)
    {
        return Attributes.TryGetValue(attribute, out var value)
            ? value
            : StartingAttributeValue;
    }

    public static void SetAttribute(CharacterAttribute attribute, int value)
    {
        Attributes[attribute] = System.Math.Max(value, 0);
        SaveGameManager.RequestAutosave();
    }

    public static void AddCursedBlood(int amount)
    {
        CursedBlood = System.Math.Max(CursedBlood + amount, 0);
        SaveGameManager.RequestAutosave();
    }

    public static bool SpendCursedBlood(int amount)
    {
        if (amount <= 0)
            return true;

        if (CursedBlood < amount)
            return false;

        CursedBlood -= amount;
        SaveGameManager.RequestAutosave();
        return true;
    }

    public static bool AddSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        if (!SkillCatalog.TryGet(skillId, out var definition))
            return false;

        if (!definition.IsImplemented || SkillCatalog.HasExclusiveConflict(definition))
            return false;

        if (!SkillCatalog.HasPrerequisites(definition))
            return false;

        var currentRank = GetSkillRank(skillId);
        var maxRank = CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank);
        if (currentRank >= maxRank)
            return false;

        SkillRanks[skillId] = currentRank + 1;
        CodexProgress.MarkDiscovered(CodexCategory.Skill, skillId);
        SaveGameManager.RequestAutosave();
        return true;
    }

    public static bool HasSkill(string skillId)
    {
        return GetSkillRank(skillId) > 0;
    }

    public static int GetSkillRank(string skillId)
    {
        return SkillRanks.TryGetValue(skillId, out var rank) ? rank : 0;
    }

    public static float GetDamageMultiplier()
    {
        return 1.0f + AttributeDelta(CharacterAttribute.Strength) * 0.03f;
    }

    public static float GetMaxHealthBonus()
    {
        return AttributeDelta(CharacterAttribute.Vitality) * 6.0f;
    }

    public static float GetMaxStaminaBonus()
    {
        return AttributeDelta(CharacterAttribute.Dexterity) * 5.0f;
    }

    public static float GetMoveSpeedMultiplier()
    {
        return 1.0f + AttributeDelta(CharacterAttribute.Dexterity) * 0.015f;
    }

    public static float GetFuryMultiplier()
    {
        return 1.0f + AttributeDelta(CharacterAttribute.Resolve) * 0.03f;
    }

    public static float GetBloodcraftMultiplier() => 1.0f + AttributeDelta(CharacterAttribute.Bloodcraft) * 0.03f;

    public static GameSaveData CreateSaveData()
    {
        var data = new GameSaveData
        {
            Version = GameSaveData.CurrentSaveVersion,
            CursedBlood = CursedBlood,
            SkillRanks = new Dictionary<string, int>(SkillRanks),
            EquippedWeaponId = WeaponLoadout.EquippedWeaponId,
            Discoveries = CodexProgress.ToSaveData()
        };
        foreach (var pair in Attributes) data.Attributes[pair.Key.ToString()] = pair.Value;
        return data;
    }

    public static void LoadSaveData(GameSaveData data)
    {
        ResetPermanentProgress();
        CursedBlood = System.Math.Max(data.CursedBlood, 0);
        foreach (var pair in data.Attributes ?? new Dictionary<string, int>())
            if (System.Enum.TryParse<CharacterAttribute>(pair.Key, out var attribute)) Attributes[attribute] = pair.Value;
        foreach (var pair in data.SkillRanks ?? new Dictionary<string, int>())
        {
            if (!SkillCatalog.TryGet(pair.Key, out var definition)) continue;
            var maxRank = CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank);
            var rank = System.Math.Clamp(pair.Value, 0, System.Math.Max(maxRank, 1));
            if (rank > 0) SkillRanks[pair.Key] = rank;
        }
        WeaponLoadout.Equip(data.EquippedWeaponId ?? "longsword");
        CodexProgress.LoadSaveData(data.Discoveries);
        BackfillDiscoveries();
    }

    public static void ResetPermanentProgress()
    {
        foreach (var attribute in Attributes.Keys.ToList()) Attributes[attribute] = StartingAttributeValue;
        CursedBlood = 0; SkillRanks.Clear();
        WeaponLoadout.Equip("longsword");
        CodexProgress.Reset();
    }

    /// <summary>Saves anteriores ao diario: marca como descoberto o que o jogador ja possui (sem autosave/evento).</summary>
    private static void BackfillDiscoveries()
    {
        foreach (var skillId in SkillRanks.Keys)
            CodexProgress.MarkSilently(CodexCategory.Skill, skillId);
        CodexProgress.MarkSilently(CodexCategory.Weapon, WeaponLoadout.EquippedWeaponId);
    }

    private static int AttributeDelta(CharacterAttribute attribute)
    {
        return GetAttribute(attribute) - StartingAttributeValue;
    }
}

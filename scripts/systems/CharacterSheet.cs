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
    public const int EquippedArtifactSlotCount = 3;

    private static readonly Dictionary<CharacterAttribute, int> Attributes = new()
    {
        { CharacterAttribute.Strength, StartingAttributeValue },
        { CharacterAttribute.Dexterity, StartingAttributeValue },
        { CharacterAttribute.Vitality, StartingAttributeValue },
        { CharacterAttribute.Resolve, StartingAttributeValue },
        { CharacterAttribute.Bloodcraft, StartingAttributeValue }
    };

    private static readonly Dictionary<string, int> SkillRanks = new();
    private static readonly List<ArtifactRecord> Artifacts = new();
    private static readonly string[] EquippedArtifactSlots = new string[EquippedArtifactSlotCount];

    public static int BloodCoins { get; private set; }
    public static IReadOnlyCollection<string> AcquiredSkills => SkillRanks.Keys;
    public static IReadOnlyList<ArtifactRecord> FoundArtifacts => Artifacts;

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

    public static void AddBloodCoins(int amount)
    {
        BloodCoins = System.Math.Max(BloodCoins + amount, 0);
        SaveGameManager.RequestAutosave();
    }

    public static bool SpendBloodCoins(int amount)
    {
        if (amount <= 0)
            return true;

        if (BloodCoins < amount)
            return false;

        BloodCoins -= amount;
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

    public static void AddArtifact(string artifactName, string effect, float amount)
    {
        if (string.IsNullOrWhiteSpace(artifactName))
            return;

        AddArtifact(new ArtifactRecord(artifactName, effect, amount));
    }

    public static void AddArtifact(ArtifactRecord artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.Name) || HasArtifact(artifact.Name))
            return;

        Artifacts.Add(artifact);
        SaveGameManager.RequestAutosave();
    }

    public static void CommitArtifacts(IEnumerable<ArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
            AddArtifact(artifact);
    }

    public static bool HasArtifact(string artifactName)
    {
        return FindArtifact(artifactName).Name == artifactName;
    }

    public static ArtifactRecord FindArtifact(string artifactName)
    {
        foreach (var artifact in Artifacts)
        {
            if (artifact.Name == artifactName)
                return artifact;
        }

        return default;
    }

    public static string GetEquippedArtifactSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EquippedArtifactSlots.Length)
            return "";

        return EquippedArtifactSlots[slotIndex] ?? "";
    }

    public static bool EquipArtifact(int slotIndex, string artifactName)
    {
        if (slotIndex < 0 || slotIndex >= EquippedArtifactSlots.Length || !HasArtifact(artifactName))
            return false;

        EquippedArtifactSlots[slotIndex] = artifactName;
        SaveGameManager.RequestAutosave();
        return true;
    }

    public static void UnequipArtifact(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EquippedArtifactSlots.Length)
            return;

        EquippedArtifactSlots[slotIndex] = "";
        SaveGameManager.RequestAutosave();
    }

    public static IEnumerable<ArtifactRecord> GetEquippedArtifacts()
    {
        foreach (var artifactName in EquippedArtifactSlots)
        {
            if (string.IsNullOrWhiteSpace(artifactName))
                continue;

            var artifact = FindArtifact(artifactName);
            if (!string.IsNullOrWhiteSpace(artifact.Name))
                yield return artifact;
        }
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
            BloodCoins = BloodCoins,
            SkillRanks = new Dictionary<string, int>(SkillRanks),
            Artifacts = new List<ArtifactRecord>(Artifacts),
            EquippedArtifacts = (string[])EquippedArtifactSlots.Clone(),
            EquippedWeaponId = WeaponLoadout.EquippedWeaponId
        };
        foreach (var pair in Attributes) data.Attributes[pair.Key.ToString()] = pair.Value;
        return data;
    }

    public static void LoadSaveData(GameSaveData data)
    {
        ResetPermanentProgress();
        BloodCoins = System.Math.Max(data.BloodCoins, 0);
        foreach (var pair in data.Attributes ?? new Dictionary<string, int>())
            if (System.Enum.TryParse<CharacterAttribute>(pair.Key, out var attribute)) Attributes[attribute] = pair.Value;
        foreach (var pair in data.SkillRanks ?? new Dictionary<string, int>())
        {
            if (!SkillCatalog.TryGet(pair.Key, out var definition)) continue;
            var maxRank = CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank);
            var rank = System.Math.Clamp(pair.Value, 0, System.Math.Max(maxRank, 1));
            if (rank > 0) SkillRanks[pair.Key] = rank;
        }
        Artifacts.AddRange(data.Artifacts ?? new List<ArtifactRecord>());
        var equippedArtifacts = data.EquippedArtifacts ?? System.Array.Empty<string>();
        for (var i = 0; i < EquippedArtifactSlots.Length && i < equippedArtifacts.Length; i++) EquippedArtifactSlots[i] = equippedArtifacts[i] ?? "";
        WeaponLoadout.Equip(data.EquippedWeaponId ?? "longsword");
    }

    public static void ResetPermanentProgress()
    {
        foreach (var attribute in Attributes.Keys.ToList()) Attributes[attribute] = StartingAttributeValue;
        BloodCoins = 0; SkillRanks.Clear(); Artifacts.Clear();
        System.Array.Fill(EquippedArtifactSlots, "");
        WeaponLoadout.Equip("longsword");
    }

    private static int AttributeDelta(CharacterAttribute attribute)
    {
        return GetAttribute(attribute) - StartingAttributeValue;
    }
}

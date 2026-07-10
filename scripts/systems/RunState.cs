public static class RunState
{
    public const int RoomsBeforeBoss = 6;

    public static int CurrentRoom { get; private set; } = 1;
    public static int StoryFragmentsUnlocked { get; private set; }
    public static float DamageMultiplier { get; private set; } = 1.0f;
    public static float MaxHealthBonus { get; private set; }
    public static float SavedPlayerHealth { get; private set; } = -1.0f;
    private static readonly System.Collections.Generic.List<ArtifactRecord> TemporaryArtifacts = new();
    private static readonly ArtifactRecord[] ActiveArtifactSlots = new ArtifactRecord[2];

    public static bool IsBossRoom => CurrentRoom >= RoomsBeforeBoss;
    public static System.Collections.Generic.IReadOnlyList<ArtifactRecord> RunArtifacts => TemporaryArtifacts;

    public static void StartRun()
    {
        CurrentRoom = 1;
        TemporaryArtifacts.Clear();
        ClearActiveArtifacts();
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        SavedPlayerHealth = -1.0f;
        ApplyEquippedArtifacts();
    }

    public static void AdvanceRoom()
    {
        CurrentRoom++;
    }

    public static void CompleteBoss()
    {
        CharacterSheet.CommitArtifacts(TemporaryArtifacts);
        TemporaryArtifacts.Clear();
        ClearActiveArtifacts();
        StoryFragmentsUnlocked++;
        CurrentRoom = 1;
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        SavedPlayerHealth = -1.0f;
    }

    public static void FailRun()
    {
        CurrentRoom = 1;
        TemporaryArtifacts.Clear();
        ClearActiveArtifacts();
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        SavedPlayerHealth = -1.0f;
    }

    public static void SavePlayerHealth(float currentHealth)
    {
        SavedPlayerHealth = currentHealth;
    }

    public static void AddDamageMultiplier(float amount)
    {
        DamageMultiplier += amount;
    }

    public static void AddMaxHealthBonus(float amount)
    {
        MaxHealthBonus += amount;
    }

    public static void RecalculateArtifactBonuses()
    {
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        ApplyEquippedArtifacts();

        foreach (var artifact in TemporaryArtifacts)
        {
            if (!IsActiveArtifact(artifact))
                ApplyArtifactEffect(artifact);
        }
    }

    public static void AddTemporaryArtifact(ArtifactRecord artifact)
    {
        TemporaryArtifacts.Add(artifact);

        if (!IsActiveArtifact(artifact))
            ApplyArtifactEffect(artifact);
    }

    public static ArtifactRecord GetActiveArtifactSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= ActiveArtifactSlots.Length)
            return default;

        return ActiveArtifactSlots[slotIndex];
    }

    public static bool EquipActiveArtifact(int slotIndex, ArtifactRecord artifact)
    {
        if (slotIndex < 0 || slotIndex >= ActiveArtifactSlots.Length || !IsActiveArtifact(artifact))
            return false;

        ActiveArtifactSlots[slotIndex] = artifact;
        return true;
    }

    public static void UnequipActiveArtifact(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= ActiveArtifactSlots.Length)
            return;

        ActiveArtifactSlots[slotIndex] = default;
    }

    private static void ApplyEquippedArtifacts()
    {
        foreach (var artifact in CharacterSheet.GetEquippedArtifacts())
        {
            if (!IsActiveArtifact(artifact))
                ApplyArtifactEffect(artifact);
        }
    }

    private static void ApplyArtifactEffect(ArtifactRecord artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.Id) && ArtifactCatalog.TryGet(artifact.Id, out var definition))
        {
            for (var i = 0; i < definition.Effects.Length; i++)
                ApplyEffect(definition.Id, i, definition.Effects[i]);

            return;
        }

        if (artifact.Effect == RoomArtifact.ArtifactEffect.Damage.ToString())
            AddDamageMultiplier(artifact.Amount);

        if (artifact.Effect == RoomArtifact.ArtifactEffect.MaxHealth.ToString())
            AddMaxHealthBonus(artifact.Amount);
    }

    private static void ApplyEffect(string artifactId, int effectIndex, ArtifactEffectDefinition effect)
    {
        var amount = CompendiumTuning.GetFloat($"{artifactId}.effect.{effectIndex}.value", effect.Value);

        if (effect.Type == RoomArtifact.ArtifactEffect.Damage.ToString())
            AddDamageMultiplier(amount);

        if (effect.Type == RoomArtifact.ArtifactEffect.MaxHealth.ToString())
            AddMaxHealthBonus(amount);
    }

    private static bool IsActiveArtifact(ArtifactRecord artifact)
    {
        return artifact.Slot == "Active";
    }

    private static void ClearActiveArtifacts()
    {
        for (var i = 0; i < ActiveArtifactSlots.Length; i++)
            ActiveArtifactSlots[i] = default;
    }
}

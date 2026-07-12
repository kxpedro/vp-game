public static class RunState
{
    private static readonly System.Collections.Generic.Dictionary<string, float> EffectTotals = new();
    private static readonly System.Collections.Generic.Dictionary<string, float> EffectParameters = new();
    public static int CurrentMapIndex { get; private set; }
    public static int CurrentRoom { get; private set; } = 1;
    public static int StoryFragmentsUnlocked { get; private set; }
    public static float DamageMultiplier { get; private set; } = 1.0f;
    public static float MaxHealthBonus { get; private set; }
    public static float MaxHealthMultiplier => 1.0f + GetEffectTotal("MaxHealth.Multiplier");
    public static float SavedPlayerHealth { get; private set; } = -1.0f;
    private static readonly System.Collections.Generic.List<ArtifactRecord> TemporaryArtifacts = new();
    private static readonly ArtifactRecord[] ActiveArtifactSlots = new ArtifactRecord[2];
    private static readonly System.Collections.Generic.List<BloodBlessingRecord> BloodBlessings = new();
    private static readonly System.Collections.Generic.Dictionary<string, int> BlessingStacks = new();

    public static MapDefinition CurrentMap => CampaignCatalog.Get(CurrentMapIndex);
    public static int RoomsBeforeBoss => CurrentMap.CommonRoomCount + 1;
    public static bool IsBossRoom => CurrentRoom > CurrentMap.CommonRoomCount;
    public static bool IsFinalMap => CurrentMapIndex >= CampaignCatalog.Count - 1;
    public static bool IsCampaignComplete { get; private set; }
    private static bool _fatalProtectionConsumed;
    private static float _killAttackSpeedBonus, _killAttackSpeedTimer, _moveOnHitBonus, _moveOnHitTimer;
    private static float _flawlessDamage, _flawlessLifeSteal, _fatalDamageBonus, _fatalDamageTimer, _healPenalty, _healPenaltyTimer;
    private static float _temporaryMoveSpeed, _temporaryCriticalChance, _temporaryCriticalDamage, _temporaryBuffTimer;
    private static bool _guaranteedCritical;
    public static float BlessingPermanentDamage { get; private set; }
    public static float BlessingRoomDamage { get; private set; }
    public static System.Collections.Generic.IReadOnlyList<ArtifactRecord> RunArtifacts => TemporaryArtifacts;
    public static System.Collections.Generic.IReadOnlyList<BloodBlessingRecord> RunBlessings => BloodBlessings;

    public static void StartRun()
    {
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        IsCampaignComplete = false;
        _fatalProtectionConsumed = false;
        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        ClearActiveArtifacts();
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        EffectTotals.Clear();
        EffectParameters.Clear();
        ResetContextEffects();
        SavedPlayerHealth = -1.0f;
        BlessingPermanentDamage = 0.0f;
        BlessingRoomDamage = 0.0f;
        ApplyEquippedArtifacts();
    }

    public static void AdvanceRoom()
    {
        CurrentRoom++;
        BlessingRoomDamage = 0.0f;
    }

    public static void CompleteBoss()
    {
        StoryFragmentsUnlocked++;

        if (!IsFinalMap)
        {
            CurrentMapIndex++;
            CurrentRoom = 1;
            SavedPlayerHealth = -1.0f;
            BlessingRoomDamage = 0.0f;
            return;
        }

        CharacterSheet.CommitArtifacts(TemporaryArtifacts);
        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        ClearActiveArtifacts();
        IsCampaignComplete = true;
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        EffectTotals.Clear();
        EffectParameters.Clear();
        ResetContextEffects();
        SavedPlayerHealth = -1.0f;
        BlessingPermanentDamage = 0.0f;
        BlessingRoomDamage = 0.0f;
    }

    public static void FailRun()
    {
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        IsCampaignComplete = false;
        _fatalProtectionConsumed = false;
        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        ClearActiveArtifacts();
        DamageMultiplier = 1.0f;
        MaxHealthBonus = 0.0f;
        EffectTotals.Clear();
        EffectParameters.Clear();
        ResetContextEffects();
        SavedPlayerHealth = -1.0f;
        BlessingPermanentDamage = 0.0f;
        BlessingRoomDamage = 0.0f;
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
        EffectTotals.Clear();
        EffectParameters.Clear();
        ApplyEquippedArtifacts();

        foreach (var artifact in TemporaryArtifacts)
        {
            if (!IsActiveArtifact(artifact))
                ApplyArtifactEffect(artifact);
        }

        foreach (var blessing in BloodBlessings)
            if (BloodBlessingCatalog.TryGet(blessing.Id, out var definition))
                ApplyBlessingEffects(definition, blessing.PowerMultiplier);
    }

    public static void AddTemporaryArtifact(ArtifactRecord artifact)
    {
        TemporaryArtifacts.Add(artifact);

        if (!IsActiveArtifact(artifact))
            ApplyArtifactEffect(artifact);
    }

    public static int GetBlessingStack(string blessingId) => BlessingStacks.TryGetValue(blessingId, out var stack) ? stack : 0;

    public static bool AddBloodBlessing(BloodBlessingDefinition definition)
        => AddBloodBlessing(new BloodBlessingOffer(definition, "Common", 1.0f));

    public static bool AddBloodBlessing(BloodBlessingOffer offer)
    {
        var definition = offer.Definition;
        var nextStack = GetBlessingStack(definition.Id) + 1;
        if (nextStack > definition.MaxStacks) return false;
        BlessingStacks[definition.Id] = nextStack;
        BloodBlessings.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, nextStack, offer.Rarity, offer.PowerMultiplier));
        ApplyBlessingEffects(definition, offer.PowerMultiplier);
        return true;
    }

    private static void ApplyBlessingEffects(BloodBlessingDefinition definition, float powerMultiplier)
    {
        for (var i = 0; i < definition.Effects.Length; i++)
            ApplyBlessingEffect(definition.Id, i, definition.Effects[i], powerMultiplier);
    }

    private static void ApplyBlessingEffect(string blessingId, int effectIndex, BloodBlessingEffectDefinition effect, float powerMultiplier)
    {
        var defaultValue = effect.BooleanValue ? 1.0f : effect.Value;
        var amount = CompendiumTuning.GetFloat($"blessing.{blessingId}.effect.{effectIndex}.value", defaultValue);
        if (!effect.BooleanValue) amount *= powerMultiplier;
        var suffix = effect.Operation.Contains("Multiplier") ? ".Multiplier" : ".Flat";
        AddEffectTotal(effect.Type + suffix, amount);
        foreach (var parameter in effect.NumberParameters)
            SetEffectParameterMaximum(effect.Type, parameter.Key, parameter.Value);
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
                ApplyEffect(definition.Id, i, definition.Effects[i], ArtifactCatalog.BossDropPowerMultiplier);

            return;
        }

        if (artifact.Effect == RoomArtifact.ArtifactEffect.Damage.ToString())
            AddDamageMultiplier(artifact.Amount);

        if (artifact.Effect == RoomArtifact.ArtifactEffect.MaxHealth.ToString())
            AddMaxHealthBonus(artifact.Amount);
    }

    private static void ApplyEffect(string sourceId, int effectIndex, ArtifactEffectDefinition effect, float powerMultiplier)
    {
        var amount = CompendiumTuning.GetFloat($"{sourceId}.effect.{effectIndex}.value", effect.Value) * powerMultiplier;

        if (effect.Type == "Damage" && effect.Operation.Contains("Multiplier")) AddDamageMultiplier(amount);
        else if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat") AddMaxHealthBonus(amount);
        else AddEffectTotal(effect.Type + (effect.Operation.Contains("Multiplier") ? ".Multiplier" : ".Flat"), amount);
    }

    public static float GetEffectTotal(string key) => EffectTotals.TryGetValue(key, out var value) ? value : 0.0f;
    public static float GetEffectParameter(string effectType, string parameter, float fallback = 0.0f)
        => EffectParameters.TryGetValue($"{effectType}.{parameter}", out var value) ? value : fallback;

    public static bool TryConsumeFatalProtection()
    {
        if (_fatalProtectionConsumed || GetEffectTotal("PreventFatalDamage.Flat") <= 0) return false;
        _fatalProtectionConsumed = true;
        _fatalDamageBonus = GetEffectTotal("DamageOnFatal.Multiplier");
        _fatalDamageTimer = GetEffectTotal("InvulnerabilityOnFatal.Flat");
        return true;
    }

    public static void Tick(float delta)
    {
        TickTimer(ref _killAttackSpeedTimer, ref _killAttackSpeedBonus, delta);
        TickTimer(ref _moveOnHitTimer, ref _moveOnHitBonus, delta);
        TickTimer(ref _fatalDamageTimer, ref _fatalDamageBonus, delta);
        TickTimer(ref _healPenaltyTimer, ref _healPenalty, delta);
        _temporaryBuffTimer = MathfMax(_temporaryBuffTimer - delta, 0);
        if (_temporaryBuffTimer <= 0) { _temporaryMoveSpeed = 0; _temporaryCriticalChance = 0; _temporaryCriticalDamage = 0; }
    }

    public static void NotifyEnemyKilled()
    {
        _killAttackSpeedBonus = GetEffectTotal("AttackSpeedOnKill.Multiplier"); _killAttackSpeedTimer = 4;
        _flawlessDamage += GetEffectTotal("FlawlessKillDamageStack.Multiplier");
        _flawlessLifeSteal += GetEffectTotal("FlawlessKillLifeStealStack.Multiplier");
    }

    public static void NotifyPlayerDamaged()
    {
        _moveOnHitBonus = GetEffectTotal("MoveSpeedOnHit.Multiplier"); _moveOnHitTimer = 3;
        if (GetEffectTotal("ResetStacksOnHit.Flat") > 0) { _flawlessDamage = 0; _flawlessLifeSteal = 0; }
    }

    public static void NotifyPlayerHealed() { _healPenalty = GetEffectTotal("DamagePenaltyOnHeal.Multiplier"); _healPenaltyTimer = 3; }
    public static float ContextDamageBonus => _flawlessDamage + _fatalDamageBonus + _healPenalty;
    public static float ContextLifeSteal => _flawlessLifeSteal;
    public static float ContextAttackSpeed => _killAttackSpeedBonus;
    public static float ContextMoveSpeed => _moveOnHitBonus + _temporaryMoveSpeed;
    public static float ContextCriticalChance => _temporaryCriticalChance;
    public static float ContextCriticalDamage => _temporaryCriticalDamage;
    public static void GrantGuaranteedCritical() => _guaranteedCritical = true;
    public static bool ConsumeGuaranteedCritical() { var value = _guaranteedCritical; _guaranteedCritical = false; return value; }
    public static void AddTemporaryBuff(string type, float value, float seconds = 8) { if (type == "MoveSpeed") _temporaryMoveSpeed += value; if (type == "CriticalChance") _temporaryCriticalChance += value; if (type == "CriticalDamage") _temporaryCriticalDamage += value; _temporaryBuffTimer = MathfMax(_temporaryBuffTimer, seconds); }
    public static void AddPermanentBlessingDamage(float value) => BlessingPermanentDamage += MathfMax(value, 0);
    public static void AddRoomBlessingDamage(float value) => BlessingRoomDamage += MathfMax(value, 0);

    private static void TickTimer(ref float timer, ref float value, float delta) { timer = MathfMax(timer - delta, 0); if (timer <= 0) value = 0; }
    private static float MathfMax(float a, float b) => System.MathF.Max(a, b);
    private static void ResetContextEffects()
    {
        _killAttackSpeedBonus = _killAttackSpeedTimer = _moveOnHitBonus = _moveOnHitTimer = 0;
        _flawlessDamage = _flawlessLifeSteal = _fatalDamageBonus = _fatalDamageTimer = _healPenalty = _healPenaltyTimer = 0;
        _temporaryMoveSpeed = _temporaryCriticalChance = _temporaryCriticalDamage = _temporaryBuffTimer = 0;
        _guaranteedCritical = false;
    }

    private static void AddEffectTotal(string key, float value)
    {
        EffectTotals[key] = GetEffectTotal(key) + value;
    }

    private static void SetEffectParameterMaximum(string effectType, string parameter, float value)
    {
        var key = $"{effectType}.{parameter}";
        if (!EffectParameters.TryGetValue(key, out var current) || value > current)
            EffectParameters[key] = value;
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

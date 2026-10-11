public static class RunState
{
    private static readonly System.Collections.Generic.Dictionary<string, float> EffectTotals = new();
    private static readonly System.Collections.Generic.Dictionary<string, float> EffectParameters = new();
    public static int CurrentMapIndex { get; private set; }
    public static int CurrentRoom { get; private set; } = 1;
    public static int StoryFragmentsUnlocked { get; private set; }
    public static float DamageMultiplier { get; private set; } = 1.0f;
    private static float _artifactMaxHealthBonus;
    private static float _devilsFruitMaxHealth;
    /// <summary>Frutas do Diabo coletadas na run (zera em StartRun/FailRun/fim da campanha).</summary>
    public static int DevilsFruitCount { get; private set; }
    public static float MaxHealthBonus => _artifactMaxHealthBonus + _devilsFruitMaxHealth;
    public static float MaxHealthMultiplier => 1.0f + GetEffectTotal("MaxHealth.Multiplier");
    public static float SacrificedMaxHealthMultiplier { get; private set; } = 1.0f;
    public static float SavedPlayerHealth { get; private set; } = -1.0f;
    private static readonly System.Collections.Generic.List<ArtifactRecord> TemporaryArtifacts = new();
    private static readonly System.Collections.Generic.List<BloodBlessingRecord> BloodBlessings = new();
    private static readonly System.Collections.Generic.Dictionary<string, int> BlessingStacks = new();

    public static MapDefinition CurrentMap => CampaignCatalog.Get(CurrentMapIndex);
    public static bool IsBossRoom => CurrentRoom > CurrentMap.CommonRoomCount;
    public static bool IsFinalMap => CurrentMapIndex >= CampaignCatalog.Count - 1;
    public static bool IsCampaignComplete { get; private set; }
    public static bool IsRunActive { get; private set; }
    private static bool _fatalProtectionConsumed;
    private static bool _skillFatalProtectionConsumed;
    private static int _skillRerollConsumedMap = -1;
    private static float _killAttackSpeedBonus, _killAttackSpeedTimer, _moveOnHitBonus, _moveOnHitTimer;
    private static float _flawlessDamage, _flawlessLifeSteal, _fatalDamageBonus, _fatalDamageTimer, _healPenalty, _healPenaltyTimer;
    private static float _temporaryMoveSpeed, _temporaryCriticalChance, _temporaryCriticalDamage, _temporaryBuffTimer;
    private static bool _guaranteedCritical;
    public static float BlessingPermanentDamage { get; private set; }
    public static float BlessingRoomDamage { get; private set; }
    public static System.Collections.Generic.IReadOnlyList<ArtifactRecord> RunArtifacts => TemporaryArtifacts;
    public static System.Collections.Generic.IReadOnlyList<BloodBlessingRecord> RunBlessings => BloodBlessings;

    /// <summary>
    /// Moedas de Sangue da run: saldo volatil, nunca vai para o save. Zera em StartRun, FailRun e no fim da campanha;
    /// NAO zera entre mapas (a loja da Camara fica entre mapas e gasta este saldo).
    /// </summary>
    public static int RunBloodCoins { get; private set; }

    public static void AddRunBloodCoins(int amount) => RunBloodCoins = System.Math.Max(RunBloodCoins + amount, 0);

    public static bool TrySpendRunBloodCoins(int amount)
    {
        if (amount <= 0) return true;
        if (RunBloodCoins < amount) return false;
        RunBloodCoins -= amount;
        return true;
    }

    public static void StartRun()
    {
        RunBloodCoins = 0;
        IsRunActive = true;
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        IsCampaignComplete = false;
        _fatalProtectionConsumed = false;
        _skillFatalProtectionConsumed = false;
        _skillRerollConsumedMap = -1;
        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        DamageMultiplier = 1.0f;
        _artifactMaxHealthBonus = 0.0f;
        _devilsFruitMaxHealth = 0.0f;
        DevilsFruitCount = 0;
        SacrificedMaxHealthMultiplier = 1.0f;
        EffectTotals.Clear();
        EffectParameters.Clear();
        ResetContextEffects();
        SavedPlayerHealth = -1.0f;
        BlessingPermanentDamage = 0.0f;
        BlessingRoomDamage = 0.0f;
        CodexProgress.MarkDiscovered(CodexCategory.Weapon, WeaponLoadout.EquippedWeaponId);
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
            BlessingRoomDamage = 0.0f;
            return;
        }

        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        IsCampaignComplete = true;
        IsRunActive = false;
        RunBloodCoins = 0;
        _skillFatalProtectionConsumed = false;
        _skillRerollConsumedMap = -1;
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        DamageMultiplier = 1.0f;
        _artifactMaxHealthBonus = 0.0f;
        _devilsFruitMaxHealth = 0.0f;
        DevilsFruitCount = 0;
        SacrificedMaxHealthMultiplier = 1.0f;
        EffectTotals.Clear();
        EffectParameters.Clear();
        ResetContextEffects();
        SavedPlayerHealth = -1.0f;
        BlessingPermanentDamage = 0.0f;
        BlessingRoomDamage = 0.0f;
    }

    public static void FailRun()
    {
        RunBloodCoins = 0;
        IsRunActive = false;
        CurrentMapIndex = 0;
        CurrentRoom = 1;
        IsCampaignComplete = false;
        _fatalProtectionConsumed = false;
        _skillFatalProtectionConsumed = false;
        _skillRerollConsumedMap = -1;
        TemporaryArtifacts.Clear();
        BloodBlessings.Clear();
        BlessingStacks.Clear();
        DamageMultiplier = 1.0f;
        _artifactMaxHealthBonus = 0.0f;
        _devilsFruitMaxHealth = 0.0f;
        DevilsFruitCount = 0;
        SacrificedMaxHealthMultiplier = 1.0f;
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
        _artifactMaxHealthBonus += amount;
    }

    /// <summary>
    /// Fruta do Diabo: +vida maxima acumulativa, so na run. Fica fora de _artifactMaxHealthBonus porque
    /// RecalculateArtifactBonuses (chamado ao pegar/evoluir bencao) zera aquele campo. Persiste entre salas e cenas
    /// (RunState e estatico) e zera em StartRun, FailRun e no fim da campanha. Retorna o bonus aplicado.
    /// O chamador deve reaplicar PlayerController.ApplyRuntimeTuning e curar o jogador.
    /// </summary>
    public static float AddDevilsFruit()
    {
        var amount = DevilsFruitMaxHealthAmount;
        _devilsFruitMaxHealth += amount;
        DevilsFruitCount++;
        return amount;
    }

    public static float DevilsFruitMaxHealthAmount => CompendiumTuning.GetFloat("economy.fruit.max_health", 15.0f);
    public static float DevilsFruitHealAmount => CompendiumTuning.GetFloat("economy.fruit.heal", 15.0f);

    public static void SacrificeMaxHealth(float fraction)
    {
        SacrificedMaxHealthMultiplier *= System.Math.Clamp(1.0f - fraction, 0.01f, 1.0f);
    }

    public static void RecalculateArtifactBonuses()
    {
        DamageMultiplier = 1.0f;
        _artifactMaxHealthBonus = 0.0f; // a Fruta do Diabo fica em _devilsFruitMaxHealth e sobrevive ao recalculo
        EffectTotals.Clear();
        EffectParameters.Clear();

        foreach (var artifact in TemporaryArtifacts)
        {
            ApplyArtifactEffect(artifact);
        }

        foreach (var blessing in BloodBlessings)
            if (BloodBlessingCatalog.TryGet(blessing.Id, out var definition))
                ApplyBlessingEffects(definition, blessing.EffectivePowerMultiplier);
    }

    /// <summary>
    /// Adiciona um artefato a run (sem limite de quantidade). Os artefatos da run sobrevivem entre mapas e zeram em
    /// StartRun, FailRun e no fim da campanha; nao ha colecao permanente nem slots equipados.
    /// </summary>
    public static void AddTemporaryArtifact(ArtifactRecord artifact)
    {
        TemporaryArtifacts.Add(artifact);
        CodexProgress.MarkDiscovered(CodexCategory.Artifact, artifact.Id);

        ApplyArtifactEffect(artifact);
    }

    /// <summary>O jogador ja comprou/possui este artefato na run atual (por Id ou pelo SaveName legado).</summary>
    public static bool HasRunArtifact(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return false;

        foreach (var artifact in TemporaryArtifacts)
            if (artifact.Id == artifactId || (string.IsNullOrWhiteSpace(artifact.Id) && artifact.Name == artifactId))
                return true;
        return false;
    }

    /// <summary>Ids dos artefatos da run, para ArtifactCatalog.RollShopStock.</summary>
    public static System.Collections.Generic.HashSet<string> GetRunArtifactIds()
    {
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var artifact in TemporaryArtifacts)
            if (!string.IsNullOrWhiteSpace(artifact.Id))
                ids.Add(artifact.Id);
        return ids;
    }

    /// <summary>
    /// Compra de artefato na Loja da Camara de Hemomancia: gasta <paramref name="price"/> Moedas de Sangue da run e
    /// adiciona o artefato a run. Sem limite de quantidade. Falha (sem gastar) se a definicao for nula, se o saldo nao
    /// bastar ou se o artefato ja for da run e a loja estiver configurada para nao repeti-lo
    /// (<c>economy.shop.exclude_owned</c>). O chamador deve reaplicar PlayerController.ApplyRuntimeTuning se o artefato
    /// alterar vida maxima e atualizar o HUD.
    /// </summary>
    public static bool TryBuyShopArtifact(ArtifactDefinition definition, int price)
    {
        if (definition == null || price < 0)
            return false;
        if (ArtifactCatalog.ShopExcludesOwned && HasRunArtifact(definition.Id))
            return false;
        if (!TrySpendRunBloodCoins(price))
            return false;

        AddTemporaryArtifact(ArtifactCatalog.CreateRecord(definition));
        return true;
    }

    public static int GetBlessingStack(string blessingId) => BlessingStacks.TryGetValue(blessingId, out var stack) ? stack : 0;

    public static bool AddBloodBlessing(BloodBlessingDefinition definition)
        => AddBloodBlessing(new BloodBlessingOffer(
            definition,
            definition.Rarity.Equals("Stirps", System.StringComparison.OrdinalIgnoreCase) ? "Stirps" : "Common",
            1.0f));

    public static bool AddBloodBlessing(BloodBlessingOffer offer)
    {
        var definition = offer.Definition;
        if (!string.IsNullOrWhiteSpace(definition.ExclusiveGroup))
        {
            for (var i = BloodBlessings.Count - 1; i >= 0; i--)
            {
                if (!BloodBlessingCatalog.TryGet(BloodBlessings[i].Id, out var equipped) ||
                    !equipped.ExclusiveGroup.Equals(definition.ExclusiveGroup, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                BlessingStacks.Remove(equipped.Id);
                BloodBlessings.RemoveAt(i);
            }
        }
        var nextStack = GetBlessingStack(definition.Id) + 1;
        if (nextStack > definition.MaxStacks) return false;
        BlessingStacks[definition.Id] = nextStack;
        BloodBlessings.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, nextStack, offer.Rarity, offer.PowerMultiplier));
        CodexProgress.MarkDiscovered(CodexCategory.Blessing, definition.Id);
        RecalculateArtifactBonuses();
        return true;
    }

    public static BloodBlessingRecord GetBlessingInExclusiveGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) return null;
        foreach (var blessing in BloodBlessings)
            if (BloodBlessingCatalog.TryGet(blessing.Id, out var definition) &&
                definition.ExclusiveGroup.Equals(group, System.StringComparison.OrdinalIgnoreCase))
                return blessing;
        return null;
    }

    public static bool TryUpgradeBloodBlessing(string blessingId, out BloodBlessingRecord upgradedBlessing)
    {
        upgradedBlessing = null;
        for (var i = 0; i < BloodBlessings.Count; i++)
        {
            var current = BloodBlessings[i];
            if (!current.Id.Equals(blessingId, System.StringComparison.OrdinalIgnoreCase) ||
                current.Level >= BloodBlessingProgression.MaximumLevel)
                continue;

            upgradedBlessing = current with
            {
                Level = current.Level + 1
            };
            BloodBlessings[i] = upgradedBlessing;
            RecalculateArtifactBonuses();
            return true;
        }
        return false;
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
        if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat")
        {
            AddMaxHealthBonus(amount);
            return;
        }
        var suffix = effect.Operation.Contains("Multiplier") ? ".Multiplier" : ".Flat";
        AddEffectTotal(effect.Type + suffix, amount);
        foreach (var parameter in effect.NumberParameters)
            SetEffectParameterMaximum(effect.Type, parameter.Key, parameter.Value);
    }

    private static void ApplyArtifactEffect(ArtifactRecord artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.Id) && ArtifactCatalog.TryGet(artifact.Id, out var definition))
        {
            for (var i = 0; i < definition.Effects.Length; i++)
                ApplyEffect(definition.Id, i, definition.Effects[i], ArtifactCatalog.ShopPowerMultiplier);

            return;
        }

        // Registro sem definição no catálogo (formato legado, sem Id): só Damage e MaxHealth.
        if (artifact.Effect == "Damage")
            AddDamageMultiplier(artifact.Amount);

        if (artifact.Effect == "MaxHealth")
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
    public static bool HasEffectType(string effectType)
        => GetEffectTotal(effectType + ".Flat") != 0.0f || GetEffectTotal(effectType + ".Multiplier") != 0.0f;
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

    public static bool TryConsumeSkillFatalProtection()
    {
        if (_skillFatalProtectionConsumed) return false;
        _skillFatalProtectionConsumed = true;
        return true;
    }

    public static bool CanConsumeSkillRewardReroll() => _skillRerollConsumedMap != CurrentMapIndex;

    public static bool TryConsumeSkillRewardReroll()
    {
        if (!CanConsumeSkillRewardReroll()) return false;
        _skillRerollConsumedMap = CurrentMapIndex;
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
    public static bool ConsumeGuaranteedCritical() { var value = _guaranteedCritical; _guaranteedCritical = false; return value; }
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
        var total = GetEffectTotal(key) + value;
        if (key.Contains("Chance", System.StringComparison.OrdinalIgnoreCase))
            total = System.Math.Clamp(total, 0.0f, 1.0f);
        EffectTotals[key] = total;
    }

    private static void SetEffectParameterMaximum(string effectType, string parameter, float value)
    {
        var key = $"{effectType}.{parameter}";
        if (!EffectParameters.TryGetValue(key, out var current) || value > current)
            EffectParameters[key] = value;
    }

}

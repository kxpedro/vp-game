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
    public static float MaxHealthBonus => _artifactMaxHealthBonus;
    /// <summary>Vida maxima da Fruta do Diabo: somada depois dos multiplicadores, para valer exatamente o valor da fruta.</summary>
    public static float DevilsFruitMaxHealthTotal => _devilsFruitMaxHealth;
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

    // Portas com recompensa visivel (docs/design/portas-recompensas.md). Zeram em StartRun, FailRun e no fim da
    // campanha; NAO zeram ao trocar de sala nem em recarga de cena (o conjunto sorteado fica guardado na run).
    private static readonly System.Collections.Generic.Dictionary<(int MapIndex, int OriginRoom), RoomRewardSet> RoomRewardSets = new();
    private static Godot.RandomNumberGenerator _rewardRng;
    private static int _setsWithoutBlessing;
    private static int _fruitDoorsChosen;
    private static RoomRewardDoor _pendingRoomReward;
    // Plano de bencaos da porta de Bencao entregue (ConsumePendingRoomReward), guardado ate a oferta abrir (TakeDeliveredBlessingPlan).
    private static RoomBlessingPlan _deliveredBlessingPlan;
    public static ulong RunSeed { get; private set; }
    /// <summary>Porta escolhida, a ser paga ao limpar a sala que ela leva (null se nada estiver pendente).</summary>
    public static RoomRewardDoor PendingRoomReward => _pendingRoomReward;
    /// <summary>CPs seguidas sem Bencao (pity, RoomRewardCatalog.Roll).</summary>
    public static int SetsWithoutBlessing => _setsWithoutBlessing;
    /// <summary>Frutas do Diabo de porta escolhidas na run (teto em RoomRewardCatalog.FruitDoorRunCap).</summary>
    public static int FruitDoorsChosen => _fruitDoorsChosen;

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
        ResetRoomRewards();
        SetRunSeed(((ulong)Godot.GD.Randi() << 32) | Godot.GD.Randi());
        CodexProgress.MarkDiscovered(CodexCategory.Weapon, WeaponLoadout.EquippedWeaponId);
        DialogueProgress.OnRunStarted();
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

        DialogueProgress.OnRunCompleted();
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
        ResetRoomRewards();
    }

    public static void FailRun()
    {
        DialogueProgress.OnRunEnded(IsRunActive, CurrentMapIndex, CurrentRoom, IsBossRoom);
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
        ResetRoomRewards();
    }

    /// <summary>
    /// Semente das recompensas de porta. StartRun sorteia uma nova; use SetRunSeed para reproduzir uma run
    /// (mesma semente + mesma ordem de saidas = mesmas portas).
    /// </summary>
    public static void SetRunSeed(ulong seed)
    {
        RunSeed = seed;
        RewardRandom().Seed = seed;
    }

    /// <summary>Conjunto de portas guardado para a saida (mapIndex, originRoom), ou null se ainda nao foi sorteado.</summary>
    public static RoomRewardSet GetRoomRewardSet(int mapIndex, int originRoom)
        => RoomRewardSets.TryGetValue((mapIndex, originRoom), out var set) ? set : null;

    /// <summary>
    /// Devolve o conjunto da saida, sorteando e guardando na primeira vez. Chamar ao limpar a sala de origem
    /// (origem 0 = entrada do mapa, na Sala Inicial ou na Camara), em ordem crescente de origem.
    /// Devolve null se a origem nao tem CP (boss, mapa sem salas comuns).
    /// </summary>
    public static RoomRewardSet GetOrRollRoomRewardSet(int mapIndex, int originRoom)
    {
        var existing = GetRoomRewardSet(mapIndex, originRoom);
        if (existing != null)
            return existing;

        // DEBUG: override do painel de dev ("Forcar portas"). Nao mexe no pity; o sorteio normal abaixo fica intacto.
        if (Godot.OS.IsDebugBuild() && DevForceDoors)
            return DevRollForcedSet(mapIndex, originRoom);

        var set = RoomRewardCatalog.Roll(mapIndex, originRoom, _setsWithoutBlessing, _fruitDoorsChosen, RewardRandom(), CanOfferAnvilDoor());
        if (set == null)
            return null;

        // Porta de Bencao: as 3 opcoes sao sorteadas agora e ficam no conjunto (a oferta usa essas, com reamostragem).
        if (set.HasBlessing)
            set = RoomRewardCatalog.WithBlessingPlan(set, RoomRewardCatalog.PlanBlessings(RewardRandom(), BloodBlessings.Count == 0));

        RoomRewardSets[(mapIndex, originRoom)] = set;
        _setsWithoutBlessing = set.HasBlessing ? 0 : _setsWithoutBlessing + 1;
        return set;
    }

    /// <summary>
    /// Commit da escolha ao interagir com a porta: guarda a porta como recompensa pendente (paga ao limpar a sala
    /// seguinte) e conta a Fruta de porta para o teto. Falha se ja houver recompensa pendente ou a porta for nula.
    /// </summary>
    public static bool ChooseRoomRewardDoor(RoomRewardDoor door)
    {
        if (door == null || _pendingRoomReward != null)
            return false;

        _pendingRoomReward = door;
        // Porta de conjunto forcado (dev): Fruta e Bigorna nao contam nos tetos do sorteio normal; a Bigorna so reserva a modificacao.
        var forced = Godot.OS.IsDebugBuild() && DevForcedDoors.Contains(door);
        if (door.Type == RoomRewardType.DevilsFruit)
        {
            if (!forced)
                _fruitDoorsChosen++;
        }
        else if (door.Type == RoomRewardType.Anvil)
        {
            if (forced)
                _anvilUnresolved++;
            else
                RegisterAnvilDoorChosen();
        }
        return true;
    }

    /// <summary>
    /// Entrega ao limpar a sala que a porta levou. Devolve a porta pendente e a limpa; null se nada estiver pendente
    /// (chamadas repetidas nao pagam duas vezes).
    /// </summary>
    public static RoomRewardDoor ConsumePendingRoomReward()
    {
        var door = _pendingRoomReward;
        _pendingRoomReward = null;
        // A Bencao entregue leva as opcoes que o jogador viu na porta: a oferta que abre em seguida usa estas.
        if (door?.BlessingPlan != null)
            _deliveredBlessingPlan = door.BlessingPlan;
        return door;
    }

    /// <summary>
    /// Opcoes pre-sorteadas da porta de Bencao da saida (mapIndex, originRoom), ou null se o conjunto nao tem porta de
    /// Bencao (ou ainda nao foi sorteado). Leitura para o prompt da porta e o ícone.
    /// </summary>
    public static RoomBlessingPlan GetRoomBlessingPlan(int mapIndex, int originRoom)
    {
        var set = GetRoomRewardSet(mapIndex, originRoom);
        if (set == null)
            return null;
        foreach (var door in set.Doors)
            if (door.Type == RoomRewardType.Blessing)
                return door.BlessingPlan;
        return null;
    }

    /// <summary>
    /// Devolve e limpa o plano da Bencao entregue ao pegar a recompensa no chao (null se a oferta nao veio de uma porta
    /// ou se o plano ja foi usado). A oferta chama isto uma vez, ao montar as opcoes; reroll nao reusa o plano.
    /// </summary>
    public static RoomBlessingPlan TakeDeliveredBlessingPlan()
    {
        var plan = _deliveredBlessingPlan;
        _deliveredBlessingPlan = null;
        return plan;
    }

    /// <summary>DEBUG (painel de dev): posiciona a run em (mapa, sala) sem passar pelas salas. Inicia a run se preciso e
    /// descarta a recompensa pendente. O chamador troca/recarrega a cena. No-op fora de debug.</summary>
    public static void DevJumpTo(int mapIndex, int room)
    {
        if (!Godot.OS.IsDebugBuild())
            return;
        if (!IsRunActive)
            StartRun();
        CurrentMapIndex = System.Math.Clamp(mapIndex, 0, CampaignCatalog.Count - 1);
        CurrentRoom = System.Math.Clamp(room, 1, CurrentMap.CommonRoomCount + 1);
        BlessingRoomDamage = 0.0f;
        _pendingRoomReward = null;
        _deliveredBlessingPlan = null;
        WeaponModOffer.Clear();
        _anvilUnresolved = 0;
        IsCampaignComplete = false;
    }


    // ===== Bigorna: modificacoes de arma da run (docs/design/bigorna.md) =====
    // Zeram em StartRun, FailRun e no fim da campanha (ResetRoomRewards); NAO zeram entre mapas nem em recarga de
    // cena; nada vai ao save. Cada modificacao e unica por run. Os efeitos valem so para a arma equipada no instante.
    private static readonly System.Collections.Generic.List<string> WeaponMods = new();
    private static readonly System.Collections.Generic.List<string> WeaponModOffer = new();
    private static int _anvilDoorsChosen;
    private static int _anvilUnresolved;

    /// <summary>Ids das modificacoes de arma forjadas na run, na ordem em que foram pegas (de qualquer arma).</summary>
    public static System.Collections.Generic.IReadOnlyList<string> RunWeaponMods => WeaponMods;
    /// <summary>Bigornas de porta escolhidas na run (teto economy.anvil.run_cap).</summary>
    public static int AnvilDoorsChosen => _anvilDoorsChosen;
    /// <summary>Teto de Bigornas escolhidas por run (economy.anvil.run_cap, padrao 3).</summary>
    public static int AnvilRunCap => System.Math.Max(CompendiumTuning.GetInt("economy.anvil.run_cap", 3), 0);
    /// <summary>Bigorna pendente: escolhida na porta (ou item no chao) e ainda nao resolvida por TryApplyWeaponMod/ResolveAnvilWithoutOffer.</summary>
    public static bool HasPendingAnvil => _anvilUnresolved > 0;
    /// <summary>Ha uma oferta de modificacoes sorteada e ainda nao escolhida (sobrevive a recarga de cena).</summary>
    public static bool HasWeaponModOffer => WeaponModOffer.Count > 0;

    public static bool HasWeaponMod(string modId) => WeaponMods.Contains(modId);

    /// <summary>Modificacoes ativas agora: as pegas cujo weaponId e o da arma equipada.</summary>
    public static System.Collections.Generic.List<WeaponModDefinition> GetActiveWeaponMods()
    {
        var result = new System.Collections.Generic.List<WeaponModDefinition>();
        if (WeaponMods.Count == 0)
            return result;
        var weaponId = WeaponLoadout.EquippedWeaponId;
        foreach (var id in WeaponMods)
        {
            var mod = WeaponModCatalog.Get(id);
            if (mod != null && mod.WeaponId == weaponId)
                result.Add(mod);
        }
        return result;
    }

    /// <summary>Soma do efeito (ex.: "AttackRange.Multiplier") entre as modificacoes ativas da arma equipada; 0 sem modificacoes.</summary>
    public static float GetWeaponModTotal(string effectType)
    {
        if (WeaponMods.Count == 0)
            return 0.0f;
        var total = 0.0f;
        foreach (var mod in GetActiveWeaponMods())
            total += mod.GetTotal(effectType);
        return total;
    }

    /// <summary>1 + GetWeaponModTotal(effectType): fator multiplicativo (exatamente 1 sem modificacoes).</summary>
    public static float GetWeaponModMultiplier(string effectType) => 1.0f + GetWeaponModTotal(effectType);

    /// <summary>Modificacoes da arma equipada ainda nao pegas, descontando as Bigornas pendentes (reservadas).</summary>
    public static int AvailableWeaponModCount()
    {
        var count = 0;
        foreach (var mod in WeaponModCatalog.ForWeapon(WeaponLoadout.EquippedWeaponId))
            if (!WeaponMods.Contains(mod.Id))
                count++;
        return System.Math.Max(count - _anvilUnresolved, 0);
    }

    /// <summary>Elegibilidade da Bigorna no sorteio da porta (peso 0 quando false): arma implementada, ha modificacao livre e o teto nao foi atingido.</summary>
    public static bool CanOfferAnvilDoor()
        => WeaponLoadout.EquippedWeapon.IsImplemented && _anvilDoorsChosen < AnvilRunCap && AvailableWeaponModCount() > 0;

    /// <summary>Chamar ao escolher uma porta de Bigorna (RunState.ChooseRoomRewardDoor): conta no teto e reserva uma modificacao.</summary>
    public static void RegisterAnvilDoorChosen()
    {
        _anvilDoorsChosen++;
        _anvilUnresolved++;
    }

    /// <summary>
    /// Oferta da Bigorna: ate weapon.mod.offer_choices (padrao 3) modificacoes da arma equipada ainda nao pegas, sem repeticao,
    /// sorteadas com o RNG da run. A lista fica guardada ate a escolha: chamadas seguintes (recarga de cena) devolvem as mesmas
    /// cartas. Lista vazia = sem modificacao disponivel (o chamador paga Moedas de fallback e chama ResolveAnvilWithoutOffer).
    /// </summary>
    public static System.Collections.Generic.List<WeaponModDefinition> RollWeaponModOffers()
    {
        var offer = new System.Collections.Generic.List<WeaponModDefinition>();
        if (WeaponModOffer.Count > 0)
        {
            foreach (var id in WeaponModOffer)
            {
                var kept = WeaponModCatalog.Get(id);
                if (kept != null)
                    offer.Add(kept);
            }
            if (offer.Count > 0)
                return offer;
            WeaponModOffer.Clear();
        }

        var pool = new System.Collections.Generic.List<WeaponModDefinition>();
        foreach (var mod in WeaponModCatalog.ForWeapon(WeaponLoadout.EquippedWeaponId))
            if (!WeaponMods.Contains(mod.Id))
                pool.Add(mod);

        var count = System.Math.Min(System.Math.Clamp(CompendiumTuning.GetInt("weapon.mod.offer_choices", 3), 1, 5), pool.Count);
        var rng = RewardRandom();
        for (var i = 0; i < count; i++)
        {
            var index = rng.RandiRange(0, pool.Count - 1);
            offer.Add(pool[index]);
            WeaponModOffer.Add(pool[index].Id);
            pool.RemoveAt(index);
        }
        return offer;
    }

    /// <summary>Forja a modificacao (unica por run): grava, limpa a oferta e libera a reserva da Bigorna. False se id invalido ou ja pega.</summary>
    public static bool TryApplyWeaponMod(string modId)
    {
        if (WeaponModCatalog.Get(modId) == null || WeaponMods.Contains(modId))
            return false;
        WeaponMods.Add(modId);
        WeaponModOffer.Clear();
        if (_anvilUnresolved > 0)
            _anvilUnresolved--;
        return true;
    }

    /// <summary>Bigorna paga sem modificacao disponivel (fallback em Moedas): limpa a oferta e libera a reserva.</summary>
    public static void ResolveAnvilWithoutOffer()
    {
        WeaponModOffer.Clear();
        if (_anvilUnresolved > 0)
            _anvilUnresolved--;
    }

    private static void ResetWeaponMods()
    {
        WeaponMods.Clear();
        WeaponModOffer.Clear();
        _anvilDoorsChosen = 0;
        _anvilUnresolved = 0;
    }

    // ===== DEBUG: forcar portas (painel de dev, aba Acoes). Nada disso vai ao save; zera em ResetRoomRewards. =====
    private static readonly RoomRewardType?[] _devForcedTypes = new RoomRewardType?[2];
    // Portas criadas pelo override (comparacao por referencia: o record compararia por valor).
    private static readonly System.Collections.Generic.HashSet<RoomRewardDoor> DevForcedDoors =
        new(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    /// <summary>Override ligado: toda saida com conjunto de portas (ainda nao sorteada) usa DevForcedDoorCount/DevForcedType. Sempre false em release.</summary>
    public static bool DevForceDoors { get; private set; }
    /// <summary>0 = Auto, 1 ou 2 portas.</summary>
    public static int DevForcedDoorCount { get; private set; }
    /// <summary>Tipo forcado da porta (0 = esquerda/porta 1, 1 = direita/porta 2); null = Auto.</summary>
    public static RoomRewardType? DevForcedType(int slot) => slot is 0 or 1 ? _devForcedTypes[slot] : null;

    public static void DevSetForceDoors(bool on)
    {
        if (Godot.OS.IsDebugBuild())
            DevForceDoors = on;
    }

    public static void DevSetForcedDoorCount(int count)
    {
        if (Godot.OS.IsDebugBuild())
            DevForcedDoorCount = System.Math.Clamp(count, 0, 2);
    }

    public static void DevSetForcedType(int slot, RoomRewardType? type)
    {
        if (Godot.OS.IsDebugBuild() && slot is 0 or 1)
            _devForcedTypes[slot] = type;
    }

    /// <summary>True se o conjunto foi montado pelo override (e nao pelo sorteio normal).</summary>
    public static bool DevIsForcedSet(RoomRewardSet set)
        => Godot.OS.IsDebugBuild() && set != null && set.Doors.Count > 0 && DevForcedDoors.Contains(set.Doors[0]);

    /// <summary>Ha modificacao de arma livre para a Bigorna, ignorando o teto de Bigornas e as reservas (usado para avisar no painel).</summary>
    public static bool DevAnvilPoolAvailable()
    {
        if (!WeaponLoadout.EquippedWeapon.IsImplemented)
            return false;
        foreach (var mod in WeaponModCatalog.ForWeapon(WeaponLoadout.EquippedWeaponId))
            if (!WeaponMods.Contains(mod.Id))
                return true;
        return false;
    }

    /// <summary>Plano de bencaos como o de uma porta de Bencao (RNG da run, garantia de ataque basico sem bencao).</summary>
    public static RoomBlessingPlan DevRollBlessingPlan()
        => Godot.OS.IsDebugBuild() ? RoomRewardCatalog.PlanBlessings(RewardRandom(), BloodBlessings.Count == 0) : null;

    /// <summary>Entrega o plano como se viesse da porta: a proxima abertura da oferta de Bencao o usa (TakeDeliveredBlessingPlan).</summary>
    public static void DevSetDeliveredBlessingPlan(RoomBlessingPlan plan)
    {
        if (Godot.OS.IsDebugBuild())
            _deliveredBlessingPlan = plan;
    }

    private static RoomRewardSet DevRollForcedSet(int mapIndex, int originRoom)
    {
        var set = RoomRewardCatalog.DevRollForced(mapIndex, originRoom, DevForcedDoorCount, _devForcedTypes, _fruitDoorsChosen, CanOfferAnvilDoor(), RewardRandom());
        if (set == null)
            return null;
        if (set.HasBlessing)
            set = RoomRewardCatalog.WithBlessingPlan(set, RoomRewardCatalog.PlanBlessings(RewardRandom(), BloodBlessings.Count == 0));
        foreach (var door in set.Doors)
            DevForcedDoors.Add(door);
        RoomRewardSets[(mapIndex, originRoom)] = set;
        // Pity (_setsWithoutBlessing) e tetos nao sao tocados: o sorteio normal segue como se o override nao existisse.
        return set;
    }

    /// <summary>
    /// DEBUG: descarta o conjunto guardado da saida e sorteia de novo (pelo override, se ligado; senao normal).
    /// Devolve o novo conjunto, ou null (sem CP ou fora de debug). Reverte o pity de um conjunto normal sem Bencao descartado;
    /// o RNG da run avanca (a sequencia seguinte muda). Nao mexe na recompensa pendente.
    /// </summary>
    public static RoomRewardSet DevRerollRoomRewardSet(int mapIndex, int originRoom)
    {
        if (!Godot.OS.IsDebugBuild())
            return null;
        var old = GetRoomRewardSet(mapIndex, originRoom);
        if (old != null)
        {
            RoomRewardSets.Remove((mapIndex, originRoom));
            if (!DevIsForcedSet(old) && !old.HasBlessing)
                _setsWithoutBlessing = System.Math.Max(_setsWithoutBlessing - 1, 0);
        }
        return GetOrRollRoomRewardSet(mapIndex, originRoom);
    }

    private static void ResetRoomRewards()
    {
        DevForceDoors = false;
        DevForcedDoorCount = 0;
        _devForcedTypes[0] = null;
        _devForcedTypes[1] = null;
        DevForcedDoors.Clear();
        ResetWeaponMods();
        RoomRewardSets.Clear();
        _setsWithoutBlessing = 0;
        _fruitDoorsChosen = 0;
        _pendingRoomReward = null;
        _deliveredBlessingPlan = null;
    }

    private static Godot.RandomNumberGenerator RewardRandom()
    {
        if (_rewardRng == null)
        {
            _rewardRng = new Godot.RandomNumberGenerator();
            _rewardRng.Randomize();
        }
        return _rewardRng;
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

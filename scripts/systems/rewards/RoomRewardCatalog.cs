using Godot;
using System;
using System.Collections.Generic;

// Modelo das portas com recompensa visivel (docs/design/portas-recompensas.md, secoes 1 a 3, 7.4 e 9).
// Opcao B (decisao do usuario): toda saida de sala comum tem uma Bencao de Sangue em uma das portas;
// Fruta do Diabo e Moedas de Sangue aparecem so na segunda porta, que e opcional.
// Esta classe e pura: nao le o estado da run. RunState guarda o conjunto sorteado, o pity e o contador de frutas.

public enum RoomRewardType
{
    Blessing,
    DevilsFruit,
    Coins,
    Anvil
}

// Opcoes pre-sorteadas de uma porta de Bencao: a linhagem sorteada e as bencoes (com raridade ja rolada), na ordem
// em que aparecem no prompt e no ícone da porta. A oferta ao pegar a recompensa usa estas opcoes (RunState, HUD).
public sealed record RoomBlessingPlan(string Family, IReadOnlyList<BloodBlessingOffer> Offers);

// Uma porta do conjunto. MapIndex e o mapa que recebe a sala (define o valor de moedas).
// CoinAmount so tem valor quando Type == Coins; nos outros tipos e 0.
// BlessingPlan so e preenchido na porta de Bencao (null nos demais tipos e quando nao ha bencao para oferecer).
public sealed record RoomRewardDoor(RoomRewardType Type, int MapIndex, int CoinAmount, RoomBlessingPlan BlessingPlan = null);

// Conjunto de portas (CP) de uma saida. OriginRoom 0 = entrada do mapa (Sala Inicial ou Camara).
// Doors[0] fica a esquerda e Doors[1] a direita (ordem ja sorteada 50/50 quando ha duas portas).
public sealed record RoomRewardSet(int MapIndex, int OriginRoom, IReadOnlyList<RoomRewardDoor> Doors)
{
    public bool TwoDoors => Doors.Count > 1;

    public bool HasBlessing
    {
        get
        {
            foreach (var door in Doors)
                if (door.Type == RoomRewardType.Blessing)
                    return true;
            return false;
        }
    }
}

public static class RoomRewardCatalog
{
    // Padroes da secao 9 do documento. Indexados por mapa (0 a 2); o mapa 4 (indice 3) nao tem sala comum.
    private static readonly float[] DefaultTwoDoorBase = { 0.30f, 0.35f, 0.40f };
    private const float DefaultTwoDoorStep = 0.05f;
    private const int DefaultTwoDoorStepEvery = 2;
    private const float DefaultTwoDoorCap = 0.55f;

    private static readonly int[] DefaultBlessingWeight = { 72, 68, 64 };
    private static readonly int[] DefaultFruitWeight = { 6, 8, 10 };
    private static readonly int[] DefaultCoinsWeight = { 22, 24, 26 };
    private static readonly int[] DefaultAnvilWeight = { 8, 10, 12 };
    private const float DefaultRampFromFraction = 0.5f;
    private const int DefaultRampShift = 2;

    // Opcao B: Bencao em toda CP (zero CPs seguidas sem Bencao). A opcao A seria 0, 1, 1.
    private static readonly int[] DefaultMaxSetsWithoutBlessing = { 0, 0, 0 };
    private static readonly int[] DefaultCoinPack = { 150, 130, 110 };
    private const int DefaultFruitRunCapFromDoors = 3;

    private const float DoorSwapChance = 0.5f;

    // Quantidade de bencaos pre-sorteadas por porta de Bencao (mesmo tamanho da oferta da Fonte de Sangue).
    private const int BlessingPlanSize = 3;

    /// <summary>Teto de Frutas do Diabo de porta por run (economy.fruit.run_cap_from_doors).</summary>
    public static int FruitDoorRunCap => CompendiumTuning.GetInt("economy.fruit.run_cap_from_doors", DefaultFruitRunCapFromDoors);

    /// <summary>Valor fixo das Moedas de Sangue de porta para a sala que o mapa recebe (economy.coins.room_reward_pack.*).</summary>
    public static int CoinPackAmount(int mapIndex) =>
        CompendiumTuning.GetInt($"economy.coins.room_reward_pack.{mapIndex}", Tier(DefaultCoinPack, mapIndex));

    /// <summary>Quantas CPs seguidas podem faltar Bencao (reward.blessing.max_sets_without_blessing.*).</summary>
    public static int MaxSetsWithoutBlessing(int mapIndex) =>
        CompendiumTuning.GetInt($"reward.blessing.max_sets_without_blessing.{mapIndex}", Tier(DefaultMaxSetsWithoutBlessing, mapIndex));

    /// <summary>
    /// Tem conjunto de portas a saida <paramref name="originRoom"/> do mapa? Origem 0 e a entrada do mapa;
    /// origens 1 a N-1 sao as saidas das salas comuns. A saida da sala N vai para o boss e nao e sorteada.
    /// </summary>
    public static bool HasRoomRewardSet(int mapIndex, int originRoom)
    {
        if (mapIndex < 0 || mapIndex >= CampaignCatalog.All.Count)
            return false;

        var commonRooms = CampaignCatalog.All[mapIndex].CommonRoomCount;
        return originRoom >= 0 && originRoom < commonRooms;
    }

    /// <summary>Probabilidade de duas portas na origem (secao 2.2). Teto em reward.doors.two_chance.cap.</summary>
    public static float TwoDoorChance(int mapIndex, int originRoom)
    {
        var baseChance = CompendiumTuning.GetFloat($"reward.doors.two_chance.base.{mapIndex}", Tier(DefaultTwoDoorBase, mapIndex));
        var step = CompendiumTuning.GetFloat("reward.doors.two_chance.step", DefaultTwoDoorStep);
        var stepEvery = Math.Max(1, CompendiumTuning.GetInt("reward.doors.two_chance.step_every", DefaultTwoDoorStepEvery));
        var cap = CompendiumTuning.GetFloat("reward.doors.two_chance.cap", DefaultTwoDoorCap);

        var chance = baseChance + step * (originRoom / stepEvery);
        return Math.Clamp(Math.Min(cap, chance), 0.0f, 1.0f);
    }

    /// <summary>
    /// Sorteia o conjunto de portas (secao 7.4). Nao guarda nada: quem chama (RunState) guarda o resultado,
    /// atualiza o pity com <see cref="RoomRewardSet.HasBlessing"/> e conta as frutas escolhidas.
    /// Devolve null se a origem nao tem CP.
    /// </summary>
    /// <param name="setsWithoutBlessing">CPs seguidas sem Bencao antes desta (pity, secao 3.3).</param>
    /// <param name="fruitDoorsChosen">Frutas de porta ja escolhidas na run (teto, secao 5.3).</param>
    /// <param name="anvilAvailable">Bigorna elegivel (RunState.CanOfferAnvilDoor: arma com modificacao livre e teto nao atingido); false = peso 0.</param>
    /// <param name="singleDoor">Sempre uma porta (sala da primeira Bencao). A chance de duas portas nao e sorteada.</param>
    public static RoomRewardSet Roll(int mapIndex, int originRoom, int setsWithoutBlessing, int fruitDoorsChosen, RandomNumberGenerator rng, bool anvilAvailable = false, bool singleDoor = false)
    {
        if (rng == null || !HasRoomRewardSet(mapIndex, originRoom))
            return null;

        var totalRooms = CampaignCatalog.All[mapIndex].CommonRoomCount;
        var doorCount = !singleDoor && rng.Randf() < TwoDoorChance(mapIndex, originRoom) ? 2 : 1;
        var fruitCapReached = fruitDoorsChosen >= FruitDoorRunCap;
        var types = new List<RoomRewardType>(doorCount);

        // Garantia de Bencao (3.2.1 e 3.3): pity atingido -> a primeira porta e Bencao.
        if (setsWithoutBlessing >= MaxSetsWithoutBlessing(mapIndex))
            types.Add(RoomRewardType.Blessing);

        // Demais portas (3.2.2): sem repetir tipo na mesma CP; teto de Fruta sai do sorteio (3.2.3).
        while (types.Count < doorCount)
        {
            if (!TryDrawType(mapIndex, originRoom, totalRooms, types, fruitCapReached, anvilAvailable, rng, out var type))
                break;
            types.Add(type);
        }

        if (types.Count == 0)
            return null;

        // Ordem esquerda/direita sorteada por CP (secao 6.3), para o jogador nao decorar a posicao.
        if (types.Count == 2 && rng.Randf() < CompendiumTuning.GetFloat("reward.door_swap_chance", DoorSwapChance))
            (types[0], types[1]) = (types[1], types[0]);

        var doors = new List<RoomRewardDoor>(types.Count);
        foreach (var type in types)
            doors.Add(new RoomRewardDoor(type, mapIndex, type == RoomRewardType.Coins ? CoinPackAmount(mapIndex) : 0));

        return new RoomRewardSet(mapIndex, originRoom, doors);
    }

    /// <summary>
    /// DEBUG (painel de dev, "Forcar portas"): monta o conjunto da saida a partir do override, sem pity, sem teto de Bigorna
    /// e sem checar elegibilidade (o chamador avisa). <paramref name="forcedCount"/> 0 = Auto (2 se a porta 2 tem tipo forcado;
    /// senao sorteia pela chance normal de duas portas); <paramref name="forcedTypes"/> tem 2 posicoes (null = Auto: Bencao se
    /// ainda nao ha, senao sorteio normal sem repetir). Doors[0] = esquerda, sem troca de lado. Aceita tipos repetidos.
    /// O plano de bencaos NAO e gravado aqui (RunState faz). Devolve null fora de debug ou sem CP.
    /// </summary>
    public static RoomRewardSet DevRollForced(int mapIndex, int originRoom, int forcedCount, RoomRewardType?[] forcedTypes, int fruitDoorsChosen, bool anvilAvailable, RandomNumberGenerator rng)
    {
        if (!OS.IsDebugBuild() || rng == null || forcedTypes == null || forcedTypes.Length < 2 || !HasRoomRewardSet(mapIndex, originRoom))
            return null;

        var totalRooms = CampaignCatalog.All[mapIndex].CommonRoomCount;
        var doorCount = forcedCount is 1 or 2
            ? forcedCount
            : forcedTypes[1].HasValue ? 2 : (rng.Randf() < TwoDoorChance(mapIndex, originRoom) ? 2 : 1);

        var slots = new RoomRewardType?[doorCount];
        var chosen = new List<RoomRewardType>(doorCount);
        for (var i = 0; i < doorCount; i++)
        {
            slots[i] = forcedTypes[i];
            if (slots[i].HasValue)
                chosen.Add(slots[i].Value);
        }

        var fruitCapReached = fruitDoorsChosen >= FruitDoorRunCap;
        for (var i = 0; i < doorCount; i++)
        {
            if (slots[i].HasValue)
                continue;
            RoomRewardType type;
            if (!chosen.Contains(RoomRewardType.Blessing))
                type = RoomRewardType.Blessing;
            else if (!TryDrawType(mapIndex, originRoom, totalRooms, chosen, fruitCapReached, anvilAvailable, rng, out type))
                type = RoomRewardType.Coins;
            slots[i] = type;
            chosen.Add(type);
        }

        var doors = new List<RoomRewardDoor>(doorCount);
        foreach (var type in slots)
            doors.Add(new RoomRewardDoor(type.Value, mapIndex, type.Value == RoomRewardType.Coins ? CoinPackAmount(mapIndex) : 0));
        return new RoomRewardSet(mapIndex, originRoom, doors);
    }

    /// <summary>
    /// Pre-sorteia as bencaos de uma porta de Bencao (mesma regra da Fonte de Sangue: uma linhagem, sem repetir, com
    /// a garantia de ataque basico quando a run ainda nao tem bencao). Devolve null se nao houver nenhuma para oferecer.
    /// Chamar ao criar o conjunto (RunState.GetOrRollRoomRewardSet), nao ao abrir a oferta.
    /// </summary>
    public static RoomBlessingPlan PlanBlessings(RandomNumberGenerator rng, bool guaranteeBasicAttack)
    {
        if (rng == null)
            return null;

        var offers = BloodBlessingCatalog.RollOffers(rng, Math.Max(1, CompendiumTuning.GetInt("reward.blessing.choices", BlessingPlanSize)), out var family, guaranteeBasicAttack, false);
        return offers.Count == 0 ? null : new RoomBlessingPlan(family, offers);
    }

    /// <summary>Devolve o conjunto com o plano de bencaos gravado na porta de Bencao (as demais portas ficam iguais).</summary>
    public static RoomRewardSet WithBlessingPlan(RoomRewardSet set, RoomBlessingPlan plan)
    {
        if (set == null || plan == null || !set.HasBlessing)
            return set;

        var doors = new List<RoomRewardDoor>(set.Doors.Count);
        foreach (var door in set.Doors)
            doors.Add(door.Type == RoomRewardType.Blessing ? door with { BlessingPlan = plan } : door);
        return set with { Doors = doors };
    }

    private static bool TryDrawType(
        int mapIndex,
        int originRoom,
        int totalRooms,
        List<RoomRewardType> chosen,
        bool fruitCapReached,
        bool anvilAvailable,
        RandomNumberGenerator rng,
        out RoomRewardType type)
    {
        var ramp = originRoom >= (int)Math.Floor(totalRooms * CompendiumTuning.GetFloat("reward.door_weight.ramp_from_fraction", DefaultRampFromFraction));
        var shift = ramp ? CompendiumTuning.GetInt("reward.door_weight.ramp_shift", DefaultRampShift) : 0;

        var blessing = chosen.Contains(RoomRewardType.Blessing) ? 0 : BlessingWeight(mapIndex);
        var fruit = chosen.Contains(RoomRewardType.DevilsFruit) || fruitCapReached ? 0 : FruitWeight(mapIndex) + shift;
        var coins = chosen.Contains(RoomRewardType.Coins) ? 0 : Math.Max(0, CoinsWeight(mapIndex) - shift);
        // Bigorna: so como segunda porta (nunca a unica: chosen vazio = peso 0), sem repetir tipo e fora do sorteio sem elegibilidade.
        var anvil = !anvilAvailable || chosen.Count == 0 || chosen.Contains(RoomRewardType.Anvil) ? 0 : Math.Max(0, AnvilWeight(mapIndex));
        var total = blessing + fruit + coins + anvil;

        if (total <= 0)
        {
            type = RoomRewardType.Blessing;
            return false;
        }

        var pick = rng.RandiRange(0, total - 1);
        if (pick < blessing)
            type = RoomRewardType.Blessing;
        else if (pick < blessing + fruit)
            type = RoomRewardType.DevilsFruit;
        else if (pick < blessing + fruit + coins)
            type = RoomRewardType.Coins;
        else
            type = RoomRewardType.Anvil;
        return true;
    }

    private static int AnvilWeight(int mapIndex) =>
        CompendiumTuning.GetInt($"reward.door_weight.anvil.{mapIndex}", Tier(DefaultAnvilWeight, mapIndex));

    private static int BlessingWeight(int mapIndex) =>
        CompendiumTuning.GetInt($"reward.door_weight.blessing.{mapIndex}", Tier(DefaultBlessingWeight, mapIndex));

    private static int FruitWeight(int mapIndex) =>
        CompendiumTuning.GetInt($"reward.door_weight.fruit.{mapIndex}", Tier(DefaultFruitWeight, mapIndex));

    private static int CoinsWeight(int mapIndex) =>
        CompendiumTuning.GetInt($"reward.door_weight.coins.{mapIndex}", Tier(DefaultCoinsWeight, mapIndex));

    /// <summary>Nome localizado do tipo de recompensa (reward.type.*.name).</summary>
    public static string DisplayName(RoomRewardType type) => type switch
    {
        RoomRewardType.Blessing => Localization.Text("reward.type.blessing.name"),
        RoomRewardType.DevilsFruit => Localization.Text("reward.type.devils_fruit.name"),
        RoomRewardType.Anvil => Localization.Text("reward.type.anvil.name"),
        _ => Localization.Text("reward.type.coins.name"),
    };

    /// <summary>Descricao localizada do tipo (reward.type.*.desc). <paramref name="coinAmount"/> so e usado por Moedas.</summary>
    public static string Description(RoomRewardType type, int coinAmount = 0) => type switch
    {
        RoomRewardType.Blessing => Localization.Text("reward.type.blessing.desc"),
        RoomRewardType.DevilsFruit => Localization.Text("reward.type.devils_fruit.desc"),
        RoomRewardType.Anvil => Localization.Text("reward.type.anvil.desc"),
        _ => Localization.TextFormat("reward.type.coins.desc", coinAmount),
    };

    private static T Tier<T>(T[] values, int mapIndex) => values[Math.Clamp(mapIndex, 0, values.Length - 1)];
}

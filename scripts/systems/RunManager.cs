using Godot;
using System;
using System.Collections.Generic;

public partial class RunManager : Node3D
{
    [Signal]
    public delegate void BloodCoinsChangedEventHandler(int amount);

    [Signal]
    public delegate void BloodCoinsRewardedEventHandler(int reward, int totalAmount);

    [Signal]
    public delegate void RoomClearedEventHandler();

    [Signal]
    public delegate void EnemyProgressChangedEventHandler(int defeatedEnemies, int totalEnemies);

    [Export] public NodePath ExitPortalPath { get; set; } = new("ExitPortal");
    [Export] public NodePath RoomGeneratorPath { get; set; } = new("RoomGenerator");

    /// <summary>Saldo de moedas da run (RunState.RunBloodCoins); o Sangue Amaldicoado permanente fica no CharacterSheet.</summary>
    public int BloodCoins => RunState.RunBloodCoins;
    public int LastBloodCoinsReward { get; private set; }
    public int DefeatedEnemies { get; private set; }
    public int TotalEnemies { get; private set; }
    public bool IsRoomCleared { get; private set; }

    private Node3D _exitPortal;
    private ScenePortal _scenePortal;
    private RoomGenerator _roomGenerator;
    private StandardMaterial3D _lockedPortalMaterial;
    private StandardMaterial3D _openPortalMaterial;
    private float _roomCheckTimer;
    private int _pendingDefeats;
    private int _pendingBloodCoins;
    private int _pendingCursedBlood;
    private Vector3 _lastDefeatPosition;
    private bool _roomRewardsPaid;
    private readonly RandomNumberGenerator _rng = new();
    private bool _roomConfigured;
    // Portas de recompensa desta sala (docs/design/portas-recompensas.md). Vazia = saída única (sala de boss ou sala sem conjunto).
    private readonly List<ScenePortal> _rewardDoors = new();
    // Itens do chão da sala ainda não pegos (bônus de sala limpa + recompensa da porta). Portas e saída só abrem em zero.
    private int _roomPickupsLeft;
    // A sala tem a recompensa da porta escolhida antes: o sangue é sugado ao pegá-la; sem ela, no primeiro item pego.
    private bool _roomHasPendingReward;
    private bool _roomBloodAbsorbed;
    // DEBUG: inimigos criados pelo console (DevSpawnEnemies) somados ao total da sala, que só limpa depois deles.
    private int _devExtraEnemies;
    private const string RoomScenePath = "res://scenes/game/TestArena.tscn";

    public override void _Ready()
    {
        AddToGroup("run_manager");
        _rng.Randomize();
        _exitPortal = GetNodeOrNull<Node3D>(ExitPortalPath);
        _scenePortal = _exitPortal?.GetNodeOrNull<ScenePortal>("PortalArea");
        _roomGenerator = GetNodeOrNull<RoomGenerator>(RoomGeneratorPath);
        BuildPortalMaterials();
        SetPortalOpen(false);
        if (GetTree().GetNodeCountInGroup("enemy_spawn_pending") == 0)
            ConfigureRoom();

        var roomHealthCost = RunState.GetEffectTotal("CurrentHealthCostOnRoomEnter.Multiplier");
        if (roomHealthCost > 0)
        {
            var playerHealth = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
            if (playerHealth != null) playerHealth.ApplyDamage(playerHealth.CurrentHealth * roomHealthCost);
        }

        EmitSignal(SignalName.BloodCoinsChanged, BloodCoins);
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "SyncBloodCoins", BloodCoins);
        GetTree().CallGroup("hud", "SyncEnemyProgress", DefeatedEnemies, TotalEnemies);
    }

    public override void _Process(double delta)
    {
        FlushDefeatBatch();
        if (GetTree().GetNodeCountInGroup("enemy_spawn_pending") > 0)
            return;
        if (GetTree().GetNodeCountInGroup("enemy_waves_remaining") > 0)
            return;
        if (IsRoomCleared)
            return;

        _roomCheckTimer -= (float)delta;
        if (_roomCheckTimer > 0.0f)
            return;

        _roomCheckTimer = 0.2f;

        if (GetTree().GetNodeCountInGroup("enemies") == 0)
            CompleteRoom();
    }

    public void NotifyWaveSpawned(int waveCount, int totalPlanned)
    {
        IsRoomCleared = false;
        if (!_roomConfigured)
        {
            ConfigureRoom();
            _roomConfigured = true;
        }
        else if (!RunState.IsBossRoom)
        {
            ApplyCommonEnemyTuning(GetTree().GetNodesInGroup("enemies"));
        }
        TotalEnemies = totalPlanned + _devExtraEnemies;
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "AppendGameLog", $"Wave com {waveCount} inimigos surgiu ({DefeatedEnemies}/{TotalEnemies}).");
    }

    // Chamado via CallGroup("run_manager", "QueueEnemyDefeat", moedas, sangueAmaldicoado, posicao) por EnemyBasic.OnDied. Nao renomear.
    // As moedas ja chegam finais (multiplicador de skill aplicado por inimigo; boss fixo). O Sangue Amaldicoado e permanente.
    public void QueueEnemyDefeat(int bloodCoins, int cursedBlood, Vector3 deathPosition)
    {
        _pendingDefeats++;
        _pendingBloodCoins += bloodCoins;
        _pendingCursedBlood += cursedBlood;
        _lastDefeatPosition = deathPosition;
    }

    private void FlushDefeatBatch()
    {
        if (_pendingDefeats == 0) return;
        var defeats = _pendingDefeats; var coins = _pendingBloodCoins; var cursedBlood = _pendingCursedBlood;
        _pendingDefeats = 0; _pendingBloodCoins = 0; _pendingCursedBlood = 0;
        if (coins > 0)
            AddBloodCoins(coins);
        if (cursedBlood > 0)
            AddCursedBlood(cursedBlood);
        DefeatedEnemies = Mathf.Min(DefeatedEnemies + defeats, TotalEnemies);
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "NotifyEnemyDefeated", DefeatedEnemies, TotalEnemies, coins);
        if (TotalEnemies > 0 && DefeatedEnemies >= TotalEnemies) CompleteRoom();
    }

    public void AddBloodCoins(int amount)
    {
        LastBloodCoinsReward = amount;
        RunState.AddRunBloodCoins(amount);
        EmitSignal(SignalName.BloodCoinsChanged, BloodCoins);
        EmitSignal(SignalName.BloodCoinsRewarded, amount, BloodCoins);
        GetTree().CallGroup("hud", "NotifyBloodCoinsRewarded", amount, BloodCoins);
    }

    /// <summary>Sangue Amaldicoado (permanente): vai direto ao save (CharacterSheet pede o autosave) e sobrevive a morte da run.</summary>
    private void AddCursedBlood(int amount)
    {
        CharacterSheet.AddCursedBlood(amount);
        GetTree().CallGroup("hud", "NotifyCursedBloodRewarded", amount, CharacterSheet.CursedBlood);
    }

    private void CompleteRoom()
    {
        // Idempotente: o pagamento da sala (bonus, fruta, portal) acontece uma unica vez por cena.
        if (_roomRewardsPaid)
            return;
        _roomRewardsPaid = true;
        IsRoomCleared = true;
        RunStats.NotifyRoomCleared();
        if (RunState.IsBossRoom)
        {
            GetTree().CallGroup("audio_manager", "StopBossMusic");
            GetTree().CallGroup("audio_manager", "PlayEffect", "boss_defeated");
            DialogueProgress.MarkBossDefeated(BossCatalog.Get(RunState.CurrentMapIndex).Id);
            DialogueManager.Instance?.Notify(DialogueEvent.BossDefeated);
        }
        foreach (var projectile in GetTree().GetNodesInGroup("enemy_projectiles"))
            projectile.QueueFree();

        EmitSignal(SignalName.RoomCleared);
        GetTree().CallGroup("player_skills", "OnRoomCleared");
        GetTree().CallGroup("hud", "NotifyRoomCleared");
        if (!RunState.IsBossRoom)
            DialogueManager.Instance?.Notify(DialogueEvent.RoomCleared);
        var mapIndex = Mathf.Clamp(RunState.CurrentMapIndex, 0, 3);
        // A recompensa desta sala (porta escolhida na sala anterior) e lida sem consumir: so sai da run ao ser pega no chao
        // (OnRoomPickupClaimed), assim uma recarga de cena nao perde a pendencia (docs/design/portas-recompensas.md, 7.4).
        // A sala N (saida para o boss) e o boss nao tem conjunto de portas.
        var pending = RunState.PendingRoomReward;
        if (RunState.IsBossRoom)
        {
            // Sangue Amaldicoado e moedas do boss ja foram pagos em FlushDefeatBatch (QueueEnemyDefeat).
            // Sem altar de artefato: fruta garantida (exceto no mapa final, que encerra a run) e saida liberada direto.
            SpawnBossFruit(mapIndex);
            SpawnRoomPickups(pending);
        }
        else
        {
            // As portas nascem travadas: abrem quando o chao da sala estiver limpo (SpawnRoomPickups / UnlockExitAfterReward).
            BuildRewardDoors(mapIndex);
            SpawnRoomPickups(pending);
        }
    }

    // Sorteia (ou recupera, se a cena foi recarregada) o conjunto de portas da saida desta sala e monta as portas travadas.
    // Sem conjunto (sala N), nada muda: a saida continua unica e leva ao boss.
    private void BuildRewardDoors(int mapIndex)
    {
        if (_exitPortal == null || _rewardDoors.Count > 0)
            return;

        var set = RunState.GetOrRollRoomRewardSet(mapIndex, RunState.CurrentRoom);
        if (set == null)
            return;

        _rewardDoors.AddRange(ScenePortal.BuildRewardDoors(_exitPortal, set, RoomScenePath, ScenePortal.RunPortalAction.AdvanceRoom, false, _roomGenerator?.PixelExitWall));
        foreach (var door in _rewardDoors)
            door.SetBarsMaterial(_lockedPortalMaterial);
    }

    // Monta o item do chao desta sala: a recompensa da porta escolhida na sala anterior (pending, se houver; sem o antigo
    // saco de Moedas de sala limpa). Cada item conta em _roomPickupsLeft; as portas so abrem com a contagem em zero.
    // Sem nenhum item, a saida abre na hora, como antes. Boss: so o pending (em geral nenhum).
    private void SpawnRoomPickups(RoomRewardDoor pending)
    {
        if (pending != null)
            SpawnRoomPickup(pending, true, RewardPickupPosition());
        _roomHasPendingReward = pending != null && _roomPickupsLeft > 0;

        if (_roomPickupsLeft > 0)
            ShowRewardDoorsLockHint();
        else
            AbsorbRoomBlood();
        UnlockExitAfterReward();
    }

    // Um item no chao: Fruta do Diabo (DevilsFruit, coletada por interacao) ou RewardPickup (Bencao e Moedas).
    // isPendingReward = a recompensa da porta escolhida (consumida ao pegar); falso para o bonus de sala limpa.
    private void SpawnRoomPickup(RoomRewardDoor door, bool isPendingReward, Vector3 position)
    {
        _roomPickupsLeft++;
        if (door.Type == RoomRewardType.DevilsFruit)
        {
            var fruit = DevilsFruit.Spawn(this, position);
            fruit.Collected += () => OnRoomPickupClaimed(door, isPendingReward);
            return;
        }

        var pickup = RewardPickup.Spawn(this, position, door);
        if (pickup == null)
        {
            _roomPickupsLeft--;
            if (isPendingReward)
                RunState.ConsumePendingRoomReward();
            return;
        }
        pickup.Claimed += () => OnRoomPickupClaimed(door, isPendingReward);
    }

    // Pegar um item do chao: paga a recompensa dele e, quando for o ultimo, libera as portas (UnlockExitAfterReward).
    private void OnRoomPickupClaimed(RoomRewardDoor door, bool isPendingReward)
    {
        if (isPendingReward)
            RunState.ConsumePendingRoomReward();
        _roomPickupsLeft = Mathf.Max(_roomPickupsLeft - 1, 0);
        // A Bênção suga o sangue na própria absorção (HUD.OpenBloodBlessingOffer); as outras recompensas, aqui.
        if (isPendingReward || !_roomHasPendingReward)
        {
            if (door.Type == RoomRewardType.Blessing)
                _roomBloodAbsorbed = true;
            else
                AbsorbRoomBlood();
        }

        switch (door.Type)
        {
            case RoomRewardType.Blessing:
                // A oferta chama UnlockExitAfterReward ao fechar; se ainda houver item no chao, a saida continua travada.
                GetTree().CallGroup("hud", "OpenBloodBlessingOffer");
                break;
            case RoomRewardType.Coins:
                AddBloodCoins(door.CoinAmount);
                UnlockExitAfterRewardDeferred();
                break;
            case RoomRewardType.Anvil:
                PayAnvilReward();
                break;
            default:
                // Fruta: libera as portas quando o item for pego.
                UnlockExitAfterRewardDeferred();
                break;
        }
    }

    // O Virgil suga as poças de sangue da sala (BloodPool, HUD.AbsorbRoomBlood), uma vez por sala.
    private void AbsorbRoomBlood()
    {
        if (_roomBloodAbsorbed)
            return;
        _roomBloodAbsorbed = true;
        GetTree().CallGroup("hud", "AbsorbRoomBlood");
    }

    // Bigorna: com modificacao livre, abre a oferta (obrigatoria; o HUD chama UnlockExitAfterReward ao fechar, como a Bencao).
    // Sem nenhuma (pool vazio ao pagar): vira Moedas de fallback + mensagem anvil.offer.none e as portas abrem sem oferta.
    // RollWeaponModOffers guarda as cartas no RunState, entao a oferta do HUD recebe as mesmas.
    private void PayAnvilReward()
    {
        if (RunState.RollWeaponModOffers().Count > 0)
        {
            GetTree().CallGroup("hud", "OpenAnvilOffer");
            return;
        }

        RunState.ResolveAnvilWithoutOffer();
        var fallbackCoins = CompendiumTuning.GetInt("economy.coins.blessing_fallback", EconomyDefaults.BlessingFallbackCoins);
        AddBloodCoins(fallbackCoins);
        GetTree().CallGroup("hud", "NotifySkillMessage", Localization.TextFormat("anvil.offer.none", fallbackCoins));
        UnlockExitAfterRewardDeferred();
    }

    // Adia um frame a liberacao: o aperto de "interact" que pegou o item nao pode disparar a porta no mesmo frame.
    private void UnlockExitAfterRewardDeferred()
    {
        Callable.From(() =>
        {
            if (IsInstanceValid(this))
                UnlockExitAfterReward();
        }).CallDeferred();
    }

    // Prompt de porta travada (ScenePortal, reward.door.fruit.locked): avisa que falta pegar o item do chao.
    private void ShowRewardDoorsLockHint()
    {
        foreach (var door in _rewardDoors)
        {
            if (IsInstanceValid(door))
                door.SetFruitLockHint(true);
        }
        // Saida unica (boss, sala N): o mesmo aviso no portal de saida.
        if (_rewardDoors.Count == 0)
            _scenePortal?.SetFruitLockHint(true);
    }

    // Ponto de recompensa da sala (RoomGenerator.GetArtifactSpawnPosition), ao nível do chão, como a fruta do boss.
    private Vector3 RewardPickupPosition()
    {
        var position = _roomGenerator != null
            ? _roomGenerator.GetArtifactSpawnPosition()
            : (_exitPortal?.Position ?? Vector3.Zero);
        position.Y = 0.15f;
        return position;
    }

    private void OpenRewardDoors()
    {
        foreach (var door in _rewardDoors)
        {
            if (!IsInstanceValid(door))
                continue;
            door.SetFruitLockHint(false);
            door.SetBarsMaterial(_openPortalMaterial);
            door.SetPortalEnabled(true);
            door.PlayOpenPulse();
        }
    }

    private void SpawnBossFruit(int mapIndex)
    {
        var count = RunState.IsFinalMap
            ? CompendiumTuning.GetInt("economy.fruit.boss_final_map", 0)
            : CompendiumTuning.GetInt("economy.fruit.boss_guaranteed", 1);
        for (var i = 0; i < count; i++)
        {
            // Perto de onde o boss morreu, ajustado para uma celula caminhavel com margem; sem gerador, cai no ponto de recompensa/centro.
            var desired = _lastDefeatPosition + new Vector3(i * 0.9f, 0.0f, 0.0f);
            var position = _roomGenerator != null
                ? (_lastDefeatPosition == Vector3.Zero ? _roomGenerator.GetArtifactSpawnPosition() : _roomGenerator.ClampToWalkableArea(desired, 2))
                : desired;
            position.Y = 0.15f;
            // Fruta garantida do boss conta como item de sala: a saida so abre quando ela for pega (interacao).
            SpawnRoomPickup(new RoomRewardDoor(RoomRewardType.DevilsFruit, mapIndex, 0), false, position);
        }
    }

    // Chamado pela oferta de bênção ao fechar (HUD.BlessingOffer, inclusive no fallback sem bênção) e ao pegar o último item do chão.
    // Enquanto houver item no chão da sala (bônus de sala limpa ou recompensa da porta), a saída continua travada.
    // Com conjunto de portas, abre as portas; sem ele (boss, sala N), abre a saída única como antes.
    public void UnlockExitAfterReward()
    {
        if (_exitPortal == null) return;
        // Oferta aberta pelo painel/console de dev no meio do combate tambem chama este metodo ao fechar: sem a sala paga, nada abre.
        if (!_roomRewardsPaid) return;
        if (_roomPickupsLeft > 0) return;
        if (_rewardDoors.Count > 0)
        {
            OpenRewardDoors();
        }
        else
        {
            ConfigureExitPortal();
            SetPortalOpen(true);
            _scenePortal?.SetFruitLockHint(false);
            var tween = _exitPortal.CreateTween();
            _exitPortal.Scale = Vector3.One * 0.78f;
            tween.TweenProperty(_exitPortal, "scale", Vector3.One * 1.12f, 0.18f);
            tween.TweenProperty(_exitPortal, "scale", Vector3.One, 0.12f);
        }
        // HUD: título secundário "Sala limpa" → "Portal aberto" + toast (HUD.Notifications.cs, NotifyPortalOpened).
        GetTree().CallGroup("hud", "NotifyPortalOpened");
    }

    private void ConfigureRoom()
    {
        if (RunState.IsBossRoom)
            ConfigureBossRoom();
        else
            ConfigureCommonRoom();
        DialogueManager.Instance?.Notify(DialogueEvent.RoomEntered);
    }

    private void ConfigureCommonRoom()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        TotalEnemies = enemies.Count;
        DefeatedEnemies = 0;

        ApplyCommonEnemyTuning(enemies);
    }

    private static readonly StringName CommonTuningAppliedMeta = "common_tuning_applied";

    private static float SafeRatio(float value, float defaultValue) => defaultValue > 0.0001f ? value / defaultValue : 1.0f;

    private static float Ratio(string key, float defaultValue) => SafeRatio(CompendiumTuning.GetFloat(key, defaultValue), defaultValue);

    private void ApplyCommonEnemyTuning(Godot.Collections.Array<Node> enemies)
    {
        var globalRoom = GetGlobalRoomNumber();
        var difficultyBonus = Mathf.Max(globalRoom - 1, 0);
        var mapDifficulty = RunState.CurrentMap.DifficultyMultiplier;

        for (var i = 0; i < enemies.Count; i++)
        {
            // Cada inimigo recebe o tuning uma unica vez; sobreviventes de waves anteriores nao sao curados nem acelerados de novo.
            if (enemies[i] is not EnemyBasic enemy || enemy.HasMeta(CommonTuningAppliedMeta))
                continue;
            enemy.SetMeta(CommonTuningAppliedMeta, true);

            // Ordem de aplicacao do valor final de um inimigo comum:
            //   base (enemy.basic.*) -> escala por sala (enemy.scale.*_per_room) -> dificuldade do mapa -> arquetipo (enemy.tank.*/enemy.ranged.*) -> raridade (enemy.rarity.*).
            // EnemyBasic.ConfigureVariant ja aplicou raridade/arquetipo sobre o valor da cena; aqui o valor e REESCRITO a partir da base,
            // entao raridade e arquetipo sao reaplicados UMA vez (nunca somados ao de ConfigureVariant). Vida usa o multiplicador completo.
            // Velocidade e dano de raridade/arquetipo entram so como razao (valor editado / padrao): com os padroes a razao e 1, preservando
            // o balanceamento atual (enemy.basic.* ja embute esses fatores); editar a chave altera o valor final proporcionalmente.
            var rarity = EnemyTuningCatalog.Get(enemy.Rarity);
            var rarityDefaults = EnemyTuningCatalog.DefaultMultipliers(enemy.Rarity);
            var archetypeSpeed = enemy.Archetype == EnemyArchetype.Tank
                ? Ratio("enemy.tank.speed_multiplier", 0.62f)
                : enemy.Archetype == EnemyArchetype.Ranged ? Ratio("enemy.ranged.speed_multiplier", 0.9f) : 1.0f;
            // Sem override de enemy.basic.move_speed, o fallback e enemy.MoveSpeed, que ja traz raridade/arquetipo de ConfigureVariant:
            // aplicar a razao de novo contaria o fator em dobro. So com a base editada (que "embute" esses fatores) a razao entra.
            var speedFactors = CompendiumTuning.HasOverride("enemy.basic.move_speed")
                ? archetypeSpeed * SafeRatio(rarity.Speed, rarityDefaults.Speed)
                : 1.0f;
            enemy.MoveSpeed = (CompendiumTuning.GetFloat("enemy.basic.move_speed", enemy.MoveSpeed) + difficultyBonus * CompendiumTuning.GetFloat("enemy.scale.speed_per_room", 0.2f))
                * speedFactors;
            var archetypeDamage = enemy.Archetype == EnemyArchetype.Tank
                ? CompendiumTuning.GetFloat("enemy.tank.damage_multiplier", 1.52f)
                : enemy.Archetype == EnemyArchetype.Ranged ? CompendiumTuning.GetFloat("enemy.ranged.damage_multiplier", 0.8f) : 1.0f;
            enemy.AttackDamage = (CompendiumTuning.GetFloat("enemy.basic.damage", 22.0f) + difficultyBonus * CompendiumTuning.GetFloat("enemy.scale.damage_per_room", 2.0f))
                * mapDifficulty * CompendiumTuning.GetFloat("enemy.scale.common_damage_mult", 0.81f) * archetypeDamage
                * SafeRatio(rarity.Damage, rarityDefaults.Damage);
            enemy.FuryOnDeath = CompendiumTuning.GetFloat("enemy.basic.fury_on_death", enemy.FuryOnDeath);

            var health = enemy.GetNodeOrNull<Health>("Health");
            var rarityHealth = rarity.Health;
            var archetypeHealth = enemy.Archetype == EnemyArchetype.Tank ? CompendiumTuning.GetFloat("enemy.tank.health_multiplier", 1.8f) : 1.0f;
            health?.SetMaxHealth((CompendiumTuning.GetFloat("enemy.basic.health", 110.0f) + difficultyBonus * CompendiumTuning.GetFloat("enemy.scale.health_per_room", 18.0f)) * mapDifficulty * rarityHealth * archetypeHealth, true);
        }

    }

    private void ConfigureBossRoom()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        TotalEnemies = enemies.Count > 0 ? 1 : 0;
        DefeatedEnemies = 0;

        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] is not EnemyBasic enemy)
                continue;

            if (i == 0)
            {
                var bossDefinition = BossCatalog.Get(RunState.CurrentMapIndex);
                enemy.Name = $"Boss_{bossDefinition.Id}";
                enemy.SetOverheadHealthVisible(false);
                if (enemy.GetNodeOrNull<Node3D>("Visual") is { } bossVisual)
                    bossVisual.Scale = Vector3.One * 1.55f;
                // Boss: ConfigureVariant (raridade Lendaria) e descartado aqui. Ordem: enemy.blood_mage.* (base) -> boss.speed_mult/damage_mult/windup
                // (este metodo) -> por golpe/fase em EnemyBasic.ApplyBossMoveTuning (dano e alcance). Vida: BossController (boss.hp.min / duracao-alvo).
                // Nao ha escala por sala nem multiplicador de raridade no boss.
                enemy.MoveSpeed = CompendiumTuning.GetFloat("enemy.blood_mage.move_speed", 2.7f);
                var isDragonkin = bossDefinition.Id == "silver_dragonkin";
                enemy.MoveSpeed *= isDragonkin
                    ? CompendiumTuning.GetFloat("boss.speed_mult.silver_dragonkin", 1.4f)
                    : CompendiumTuning.GetFloat("boss.speed_mult.default", 1.85f);
                // 1.35f e dificuldade do mapa (indice de mapa == 1), nao do boss especifico.
                enemy.AttackDamage = CompendiumTuning.GetFloat("enemy.blood_mage.damage", 30.0f)
                    * (RunState.CurrentMapIndex == 1 ? CompendiumTuning.GetFloat("boss.damage_mult.map_1", 1.35f) : 1.0f)
                    * CompendiumTuning.GetFloat("boss.damage_mult.base", 0.9f);
                enemy.AttackWindup = isDragonkin
                    ? CompendiumTuning.GetFloat("boss.windup.silver_dragonkin", 0.75f)
                    : CompendiumTuning.GetFloat("boss.windup.default", 0.38f);
                enemy.AttackRange = CompendiumTuning.GetFloat("enemy.blood_mage.attack_range", 2.25f);
                // Moedas e Sangue Amaldicoado do boss: valores fixos por mapa (economy.coins.boss.*, economy.cursed_blood.boss.*),
                // pagos por EnemyBasic.RollDefeatRewards. As chaves legadas enemy.blood_mage.*_blood_coins ficam sem efeito.

                var controller = new BossController { Name = "BossController" };
                enemy.AddChild(controller);
                controller.Configure(enemy, bossDefinition);
                CodexProgress.MarkBoss(bossDefinition.Id);
                // A barra de vida do boss (HUD BindBoss) so aparece quando ele acorda, depois do dialogo de encontro (BossController.Wake).
            }
            else
            {
                enemy.QueueFree();
            }
        }
    }

    private void ConfigureExitPortal()
    {
        if (_scenePortal == null)
            return;

        if (RunState.IsBossRoom)
        {
            var destination = RunState.IsFinalMap
                ? "res://scenes/game/CrowsNest.tscn"
                : "res://scenes/game/IntermissionChamber.tscn";
            // Chave (não o texto): o ScenePortal resolve na exibição e acompanha a troca de idioma.
            var prompt = RunState.IsFinalMap ? "portal.finish_campaign" : "portal.hemomancy_chamber";
            _scenePortal.ConfigureDestination(
                destination,
                prompt,
                ScenePortal.RunPortalAction.CompleteBoss
            );
            return;
        }

        _scenePortal.ConfigureDestination(
            "res://scenes/game/TestArena.tscn",
            "portal.next_room",
            ScenePortal.RunPortalAction.AdvanceRoom
        );
    }

    private static int GetGlobalRoomNumber()
    {
        var total = RunState.CurrentRoom;
        for (var i = 0; i < RunState.CurrentMapIndex; i++)
            total += CampaignCatalog.Get(i).CommonRoomCount + 1;
        return total;
    }

    private void BuildPortalMaterials()
    {
        _lockedPortalMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.18f, 0.2f, 0.42f),
            EmissionEnabled = true,
            Emission = new Color(0.12f, 0.12f, 0.14f),
            EmissionEnergyMultiplier = 0.35f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _openPortalMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.02f, 0.03f, 0.78f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 2.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    private void SetPortalOpen(bool isOpen)
    {
        if (_exitPortal == null)
            return;

        _exitPortal.Visible = true;
        _scenePortal?.SetPortalEnabled(isOpen);
        var material = isOpen ? _openPortalMaterial : _lockedPortalMaterial;

        // Pilares e lintel sao moldura de pedra; so as barras indicam se a saida esta liberada.
        foreach (var child in _exitPortal.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Name.ToString().StartsWith("Bar"))
                mesh.MaterialOverride = material;
        }
    }

    // ---------- DEBUG (painel de dev, aba Ações). No-op fora de debug. ----------

    /// <summary>DEBUG: mata (ForceDeath) todos os inimigos vivos agora. Waves futuras continuam. Devolve quantos matou.</summary>
    public int DevKillAllEnemies()
    {
        if (!OS.IsDebugBuild())
            return 0;
        var killed = 0;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            var health = node.GetNodeOrNull<Health>("Health");
            if (health == null || health.IsDead)
                continue;
            health.ForceDeath("dev");
            killed++;
        }
        return killed;
    }

    /// <summary>DEBUG: encerra a sala como se estivesse limpa (mata os inimigos, cancela as waves restantes e paga a sala:
    /// itens no chão e portas). Falso se uma wave está surgindo (aguarde) ou fora de debug.</summary>
    public bool DevCompleteRoom()
    {
        if (!OS.IsDebugBuild() || GetTree().GetNodeCountInGroup("enemy_spawn_pending") > 0)
            return false;
        if (_roomGenerator != null)
        {
            _roomGenerator.SetProcess(false);
            _roomGenerator.RemoveFromGroup("enemy_waves_remaining");
        }
        DevKillAllEnemies();
        DefeatedEnemies = TotalEnemies;
        EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
        GetTree().CallGroup("hud", "SyncEnemyProgress", DefeatedEnemies, TotalEnemies);
        CompleteRoom();
        return true;
    }

    /// <summary>
    /// DEBUG: descarta o conjunto de portas desta sala e sorteia de novo (override do painel, ou normal). Se as portas ja
    /// existem no mundo (sala limpa), recria-as e mantem o estado trancado/aberto conforme os itens do chao. Sem portas no mundo
    /// (sala em combate), so troca o conjunto: as portas nascem ao limpar a sala. Devolve o novo conjunto (null = nada a fazer).
    /// </summary>
    public RoomRewardSet DevRerollRewardDoors()
    {
        if (!OS.IsDebugBuild() || RunState.IsBossRoom)
            return null;

        var mapIndex = Mathf.Clamp(RunState.CurrentMapIndex, 0, 3);
        var set = RunState.DevRerollRoomRewardSet(mapIndex, RunState.CurrentRoom);
        if (set == null || _rewardDoors.Count == 0)
            return set;

        foreach (var door in _rewardDoors)
        {
            if (!IsInstanceValid(door))
                continue;
            var root = door.GetParent();
            root?.GetParent()?.RemoveChild(root);
            root?.QueueFree();
        }
        _rewardDoors.Clear();
        BuildRewardDoors(mapIndex);
        if (_roomPickupsLeft > 0)
            ShowRewardDoorsLockHint();
        else
            OpenRewardDoors();
        return set;
    }

    /// <summary>
    /// DEBUG (console de dev): cria <paramref name="count"/> inimigos comuns em volta de <paramref name="center"/>, com o tuning
    /// da sala atual. Raridade/arquetipo null = Normal / sorteio da sala. Com a sala ainda em combate, entram no total da sala
    /// (as portas so abrem depois deles). Devolve quantos criou (0 sem gerador de sala ou fora de debug).
    /// </summary>
    public int DevSpawnEnemies(int count, EnemyRarity? rarity, EnemyArchetype? archetype, Vector3 center)
    {
        if (!OS.IsDebugBuild() || _roomGenerator == null || count <= 0)
            return 0;
        var spawned = new Godot.Collections.Array<Node>();
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / count + _rng.RandfRange(-0.3f, 0.3f);
            var position = center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _rng.RandfRange(3.0f, 4.5f);
            position = _roomGenerator.ClampToWalkableArea(position, 1);
            position.Y = 0.2f; // altura das waves (RoomGenerator.CellToWorld)
            var enemy = _roomGenerator.DevSpawnEnemy(position, rarity ?? EnemyRarity.Normal, archetype);
            if (enemy != null)
                spawned.Add(enemy);
        }
        ApplyCommonEnemyTuning(spawned);
        if (!IsRoomCleared && spawned.Count > 0)
        {
            _devExtraEnemies += spawned.Count;
            TotalEnemies += spawned.Count;
            EmitSignal(SignalName.EnemyProgressChanged, DefeatedEnemies, TotalEnemies);
            GetTree().CallGroup("hud", "SyncEnemyProgress", DefeatedEnemies, TotalEnemies);
        }
        return spawned.Count;
    }
}

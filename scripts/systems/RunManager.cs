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
    private const string RoomScenePath = "res://scenes/game/TestArena.tscn";
    // Rotulos 3D com texto de world.json; reescritos quando o idioma muda.
    private readonly List<(Label3D Label, Func<string> Text)> _localizedLabels = new();

    public override void _Ready()
    {
        AddToGroup("run_manager");
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
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

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language)
    {
        foreach (var (label, text) in _localizedLabels)
        {
            if (IsInstanceValid(label))
                label.Text = text();
        }
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
        TotalEnemies = totalPlanned;
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
        if (RunState.IsBossRoom)
        {
            GetTree().CallGroup("audio_manager", "StopBossMusic");
            GetTree().CallGroup("audio_manager", "PlayEffect", "boss_defeated");
        }
        foreach (var projectile in GetTree().GetNodesInGroup("enemy_projectiles"))
            projectile.QueueFree();

        EmitSignal(SignalName.RoomCleared);
        GetTree().CallGroup("player_skills", "OnRoomCleared");
        GetTree().CallGroup("hud", "NotifyRoomCleared");
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
            SpawnRoomPickups(pending, 0, mapIndex);
        }
        else
        {
            // As portas nascem travadas: abrem quando o chao da sala estiver limpo (SpawnRoomPickups / UnlockExitAfterReward).
            BuildRewardDoors(mapIndex);
            var clearBonus = CompendiumTuning.GetInt($"economy.coins.room_clear_bonus.{mapIndex}", new[] { 12, 14, 16, 0 }[mapIndex]);
            SpawnRoomPickups(pending, clearBonus, mapIndex);
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

        _rewardDoors.AddRange(ScenePortal.BuildRewardDoors(_exitPortal, set, RoomScenePath, ScenePortal.RunPortalAction.AdvanceRoom, false));
        foreach (var door in _rewardDoors)
            door.SetBarsMaterial(_lockedPortalMaterial);
    }

    // Monta os itens do chao desta sala: o bonus de sala limpa (Moedas, valor do mapa) e a recompensa da porta escolhida
    // na sala anterior (pending, se houver). Cada item conta em _roomPickupsLeft; as portas so abrem com a contagem em zero.
    // Sem nenhum item, a saida abre na hora, como antes. Boss: so o pending (em geral nenhum).
    private void SpawnRoomPickups(RoomRewardDoor pending, int clearBonus, int mapIndex)
    {
        var slot = 0;
        if (clearBonus > 0)
            SpawnRoomPickup(new RoomRewardDoor(RoomRewardType.Coins, mapIndex, clearBonus), false, RewardPickupPosition(slot++));
        if (pending != null)
            SpawnRoomPickup(pending, true, RewardPickupPosition(slot));

        if (_roomPickupsLeft > 0)
            ShowRewardDoorsLockHint();
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
            default:
                // Fruta: libera as portas quando o item for pego.
                UnlockExitAfterRewardDeferred();
                break;
        }
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
    // Itens extras (slot > 0) andam 0,9 m em X, dentro da área caminhável.
    private Vector3 RewardPickupPosition(int slot)
    {
        var position = _roomGenerator != null
            ? _roomGenerator.GetArtifactSpawnPosition()
            : (_exitPortal?.Position ?? Vector3.Zero);
        if (slot > 0)
        {
            position += Vector3.Right * (slot * 0.9f);
            if (_roomGenerator != null)
                position = _roomGenerator.ClampToWalkableArea(position, 2);
        }
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
    }

    private void ConfigureCommonRoom()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        TotalEnemies = enemies.Count;
        DefeatedEnemies = 0;

        ApplyCommonEnemyTuning(enemies);

        CreateRoomLabel(() => Localization.TextFormat("room.label.common", RunState.CurrentMap.Name, RunState.CurrentRoom, RunState.CurrentMap.CommonRoomCount));
    }

    private static readonly StringName CommonTuningAppliedMeta = "common_tuning_applied";

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

            enemy.MoveSpeed = CompendiumTuning.GetFloat("enemy.basic.move_speed", enemy.MoveSpeed) + difficultyBonus * 0.2f;
            var archetypeDamage = enemy.Archetype == EnemyArchetype.Tank ? 1.52f : enemy.Archetype == EnemyArchetype.Ranged ? 0.8f : 1.0f;
            enemy.AttackDamage = (CompendiumTuning.GetFloat("enemy.basic.damage", 22.0f) + difficultyBonus * 2.0f) * mapDifficulty * 0.81f * archetypeDamage;
            enemy.FuryOnDeath = CompendiumTuning.GetFloat("enemy.basic.fury_on_death", enemy.FuryOnDeath);

            var health = enemy.GetNodeOrNull<Health>("Health");
            var rarityHealth = EnemyTuningCatalog.Get(enemy.Rarity).Health;
            var archetypeHealth = enemy.Archetype == EnemyArchetype.Tank ? 1.8f : 1.0f;
            health?.SetMaxHealth((CompendiumTuning.GetFloat("enemy.basic.health", 110.0f) + difficultyBonus * 18.0f) * mapDifficulty * rarityHealth * archetypeHealth, true);
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
                enemy.MoveSpeed = CompendiumTuning.GetFloat("enemy.blood_mage.move_speed", 2.7f);
                enemy.MoveSpeed *= RunState.CurrentMapIndex == 0 ? 1.4f : 1.85f;
                enemy.AttackDamage = CompendiumTuning.GetFloat("enemy.blood_mage.damage", 30.0f) * (RunState.CurrentMapIndex == 1 ? 1.35f : 1.0f) * 0.9f;
                if (RunState.CurrentMapIndex == 0) enemy.AttackWindup = 0.75f;
                else enemy.AttackWindup = 0.38f;
                enemy.AttackRange = CompendiumTuning.GetFloat("enemy.blood_mage.attack_range", 2.25f);
                // Moedas e Sangue Amaldicoado do boss: valores fixos por mapa (economy.coins.boss.*, economy.cursed_blood.boss.*),
                // pagos por EnemyBasic.RollDefeatRewards. As chaves legadas enemy.blood_mage.*_blood_coins ficam sem efeito.

                var controller = new BossController { Name = "BossController" };
                enemy.AddChild(controller);
                controller.Configure(enemy, bossDefinition);
                CodexProgress.MarkBoss(bossDefinition.Id);
                GetTree().CallGroup("hud", "BindBoss", enemy, bossDefinition.DisplayName);
            }
            else
            {
                enemy.QueueFree();
            }
        }

        CreateRoomLabel(() => Localization.TextFormat("room.label.boss", RunState.CurrentMap.Name, BossCatalog.Get(RunState.CurrentMapIndex).DisplayName));
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

    private void CreateRoomLabel(Func<string> text)
    {
        // Título de lugar em Gold, não vermelho (guia §2.2: vermelho vivo só para perigo).
        var label = new Label3D
        {
            Name = "RoomTitle",
            Text = text(),
            Position = new Vector3(0.0f, 2.2f, -7.7f)
        };
        _localizedLabels.Add((label, text));
        InteractPrompt.StyleTitle(label);
        AddChild(label);
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
}

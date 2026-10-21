using Godot;
using System;

// Chaves que ja existiam no CompendiumTuning (inventario secao a), chaves por ID dos catalogos e adaptador do GameTuning.
// Nao registradas de proposito: attack.light_*/attack.dash.* (chaves mortas; o PlayerCombat le weapon.<arma>.*).
public static partial class DevParameterRegistry
{
    private const string GEconomy = "Economia";
    private const string GRewards = "Recompensas / Portas";
    private const string GCombat = "Combate";
    private const string GEnemies = "Inimigos";
    private const string GWeapons = "Armas";  // prefixo: o grupo de cada arma e "Armas: <nome>"
    private const string GQuick = "Ajustes rápidos (GameTuning)";
    private const string GSkills = "Skills (Fonte de Sangue)";
    private const string GArtifacts = "Artefatos";
    private const string GBlessings = "Bênçãos";

    private static void P(string key, string label, string group, double def, double min, double max, double step, bool isInt, DevApplyMode apply = DevApplyMode.Live, string note = "")
        => Register(new DevParameter(key, label, group, def, min, max, step, isInt, apply, note));

    private static void RegisterLegacy()
    {
        RegisterEconomyLegacy();
        RegisterRewardsLegacy();
        RegisterCombatLegacy();
        RegisterEnemiesLegacy();
        RegisterWeapons(); // DevParameters.Player.cs: um grupo por arma do WeaponCatalog
        RegisterQuickTuning();
        RegisterByIdKeys();
    }

    private static void RegisterEconomyLegacy()
    {
        RegisterPerMap("economy.coins.boss.{m}", "Moedas do boss", GEconomy, new double[] { 100, 130, 160, 0 }, 0, 5000, 10, true);
        RegisterPerMap("economy.cursed_blood.boss.{m}", "Sangue Amaldiçoado do boss", GEconomy, new double[] { 40, 60, 80, 120 }, 0, 2000, 5, true);
        P("economy.cursed_blood.legendary_drop_chance", "Chance de Sangue Amaldiçoado (Lendário)", GEconomy, 1.0, 0, 1, 0.05, false);
        RegisterPerMap("economy.cursed_blood.legendary.{m}", "Sangue Amaldiçoado do Lendário", GEconomy, new double[] { 0, 6, 8, 8 }, 0, 500, 1, true);
        P("economy.coins.enemy.base_min", "Moedas base do inimigo (mín.)", GEconomy, 3, 0, 200, 1, true);
        P("economy.coins.enemy.base_max", "Moedas base do inimigo (máx.)", GEconomy, 7, 0, 200, 1, true);
        P("economy.coins.archetype.tank", "Multiplicador de Moedas: Tank", GEconomy, 1.5, 0, 20, 0.1, false);
        P("economy.coins.archetype.ranged", "Multiplicador de Moedas: Ranged", GEconomy, 1.0, 0, 20, 0.1, false);
        P("economy.coins.archetype.melee", "Multiplicador de Moedas: Melee", GEconomy, 1.0, 0, 20, 0.1, false);
        P("economy.coins.rarity.normal", "Multiplicador de Moedas: Normal", GEconomy, 1.0, 0, 50, 0.5, false);
        P("economy.coins.rarity.magic", "Multiplicador de Moedas: Mágico", GEconomy, 2.0, 0, 50, 0.5, false);
        P("economy.coins.rarity.rare", "Multiplicador de Moedas: Raro", GEconomy, 4.0, 0, 50, 0.5, false);
        P("economy.coins.rarity.legendary", "Multiplicador de Moedas: Lendário", GEconomy, 8.0, 0, 100, 0.5, false);
        RegisterPerMap("economy.coins.map_factor.{m}", "Fator de Moedas do mapa", GEconomy, new[] { 1.0, 0.6, 0.5, 0.5 }, 0, 10, 0.1, false);
        P("economy.coins.blessing_fallback", "Moedas no lugar de Bênção sem opções", GEconomy, 75, 0, 2000, 5, true);
        RegisterPerMap("economy.coins.room_clear_bonus.{m}", "Bônus de sala limpa (Moedas)", GEconomy, new double[] { 12, 14, 16, 0 }, 0, 500, 1, true);
        RegisterPerMap("economy.coins.room_reward_pack.{m}", "Moedas da porta de Moedas", GEconomy, new double[] { 150, 130, 110 }, 0, 5000, 10, true, DevApplyMode.NextRoom);
        P("economy.ledger.max_bonus", "Teto do bônus de dano por Moedas (Ledger)", GEconomy, 1.0, 0, 20, 0.1, false);
        P("economy.shop.power_multiplier", "Multiplicador de poder dos artefatos", GEconomy, 1.75, 0, 10, 0.05, false, DevApplyMode.Reapply);
        P("economy.shop.exclude_owned", "Loja exclui artefatos já possuídos (0/1)", GEconomy, 1, 0, 1, 1, true);
        P("economy.shop.allow_duplicates_in_stock", "Loja permite duplicatas no estoque (0/1)", GEconomy, 0, 0, 1, 1, true, DevApplyMode.NextRoom, "Estoque pode ser sorteado uma vez por visita.");
        P("economy.shop.stock_min", "Estoque da loja (mín.)", GEconomy, 3, 0, 12, 1, true, DevApplyMode.NextRoom);
        P("economy.shop.stock_max", "Estoque da loja (máx.)", GEconomy, 4, 0, 12, 1, true, DevApplyMode.NextRoom);
        P("economy.shop.stock_extra_chance", "Chance de item extra no estoque", GEconomy, 0.5, 0, 1, 0.05, false, DevApplyMode.NextRoom);
        string[] rarities = { "common", "uncommon", "rare", "epic", "legendary", "mythic" };
        double[] prices = { 250, 420, 700, 1100, 1700, 2400 };
        for (var i = 0; i < rarities.Length; i++)
            P($"economy.shop.price.{rarities[i]}", $"Preço na loja: {rarities[i]}", GEconomy, prices[i], 0, 20000, 10, true);
        P("economy.fruit.boss_final_map", "Fruta do boss no mapa final", GEconomy, 0, 0, 5, 1, true);
        P("economy.fruit.boss_guaranteed", "Frutas do boss (demais mapas)", GEconomy, 1, 0, 5, 1, true);
        P("economy.fruit.run_cap_from_doors", "Teto de Frutas de porta por run", GEconomy, 3, 0, 20, 1, true);
        P("economy.fruit.max_health", "Fruta do Diabo: vida máxima", GEconomy, 15, 0, 200, 1, false, DevApplyMode.Live, "Aplicado na coleta; frutas já coletadas usam o valor no ApplyRuntimeTuning.");
        P("economy.fruit.heal", "Fruta do Diabo: cura", GEconomy, 15, 0, 200, 1, false);
    }

    private static void RegisterRewardsLegacy()
    {
        RegisterPerMap("reward.blessing.max_sets_without_blessing.{m}", "Máx. de conjuntos seguidos sem Bênção", GRewards, new double[] { 0, 0, 0 }, 0, 10, 1, true, DevApplyMode.NextRoom);
        RegisterPerMap("reward.doors.two_chance.base.{m}", "Chance base de duas portas", GRewards, new[] { 0.30, 0.35, 0.40 }, 0, 1, 0.05, false, DevApplyMode.NextRoom, "Só vale para salas ainda não sorteadas. Para testar já: aba Ações > Portas de recompensa > Forçar portas.");
        P("reward.doors.two_chance.step", "Chance de duas portas: incremento", GRewards, 0.05, 0, 1, 0.01, false, DevApplyMode.NextRoom);
        P("reward.doors.two_chance.step_every", "Chance de duas portas: a cada N salas", GRewards, 2, 1, 20, 1, true, DevApplyMode.NextRoom);
        P("reward.doors.two_chance.cap", "Chance de duas portas: teto", GRewards, 0.55, 0, 1, 0.05, false, DevApplyMode.NextRoom);
        P("reward.door_weight.ramp_from_fraction", "Peso das portas: rampa a partir da fração", GRewards, 0.5, 0, 1, 0.05, false, DevApplyMode.NextRoom);
        P("reward.door_weight.ramp_shift", "Peso das portas: deslocamento da rampa", GRewards, 2, 0, 50, 1, true, DevApplyMode.NextRoom);
        RegisterPerMap("reward.door_weight.blessing.{m}", "Peso da porta de Bênção", GRewards, new double[] { 72, 68, 64 }, 0, 200, 1, true, DevApplyMode.NextRoom, "Só vale para salas ainda não sorteadas. Para testar já: aba Ações > Portas de recompensa > Forçar portas.");
        RegisterPerMap("reward.door_weight.fruit.{m}", "Peso da porta de Fruta", GRewards, new double[] { 6, 8, 10 }, 0, 200, 1, true, DevApplyMode.NextRoom, "Só vale para salas ainda não sorteadas. Para testar já: aba Ações > Portas de recompensa > Forçar portas.");
        RegisterPerMap("reward.door_weight.coins.{m}", "Peso da porta de Moedas", GRewards, new double[] { 22, 24, 26 }, 0, 200, 1, true, DevApplyMode.NextRoom, "Só vale para salas ainda não sorteadas. Para testar já: aba Ações > Portas de recompensa > Forçar portas.");
        P("reward.door.close_seconds", "Tempo de fechamento da porta (s)", GRewards, 0.3, 0, 5, 0.05, false);
        P("reward.door.two_door_offset_cells", "Afastamento das duas portas (células)", GRewards, 1.0, 0, 5, 0.1, false, DevApplyMode.NewObjects);
        P("reward.door.two_door_frame_scale", "Escala do batente (duas portas)", GRewards, 0.6, 0.1, 2, 0.05, false, DevApplyMode.NewObjects);
    }

    private static void RegisterCombatLegacy()
    {
        P("attack.fury.damage_multiplier", "Fúria: multiplicador de dano", GCombat, 1.75, 0, 20, 0.05, false);
        P("attack.fury.drain_per_second", "Fúria: drenagem por segundo", GCombat, 10, 0, 200, 1, false);
        P("attack.fury.max_fury", "Fúria: máximo", GCombat, 100, 1, 1000, 5, false, DevApplyMode.Live, "Multiplicado pelo atributo Resolve da ficha.");
    }

    private static void RegisterEnemiesLegacy()
    {
        P("enemy.basic.health", "Inimigo comum: vida base", GEnemies, 110, 1, 5000, 5, false, DevApplyMode.NewObjects);
        P("enemy.basic.damage", "Inimigo comum: dano base", GEnemies, 22, 0, 500, 1, false, DevApplyMode.NewObjects);
        P("enemy.basic.move_speed", "Inimigo comum: velocidade", GEnemies, 3.2, 0, 20, 0.1, false, DevApplyMode.NewObjects);
        P("enemy.basic.fury_on_death", "Inimigo comum: Fúria ao morrer", GEnemies, 12, 0, 200, 1, false, DevApplyMode.NewObjects);
        P("enemy.blood_mage.move_speed", "Boss: velocidade base (nome legado blood_mage)", GEnemies, 2.7, 0, 20, 0.1, false, DevApplyMode.NextRoom);
        P("enemy.blood_mage.damage", "Boss: dano base (nome legado blood_mage)", GEnemies, 30, 0, 500, 1, false, DevApplyMode.NextRoom, "Base do dano de cada golpe: x (1 + mapa x boss.damage_map_scale) x dano do golpe x fase x boss.damage_final_multiplier (ApplyBossMoveTuning). Nome legado preservado.");
        P("enemy.blood_mage.attack_range", "Boss: alcance base (nome legado blood_mage)", GEnemies, 2.25, 0, 20, 0.05, false, DevApplyMode.NextRoom, "Base do alcance de cada golpe: x multiplicador de alcance do golpe (ApplyBossMoveTuning, mín. 1). Nome legado preservado.");
    }

    private static bool _gameTuningDirty;
    private static int _gameTuningSaveGeneration;
    private static bool _gameTuningExitHooked;

    // Agrupa as gravacoes do game_tuning.cfg (o slider dispara ValueChanged a cada passo): grava apos ~0,5 s sem alteracao e ao sair da arvore.
    private static void ScheduleGameTuningSave()
    {
        _gameTuningDirty = true;
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            FlushGameTuningSave();
            return;
        }

        if (!_gameTuningExitHooked && tree.Root != null)
        {
            _gameTuningExitHooked = true;
            tree.Root.TreeExiting += FlushGameTuningSave;
        }

        var generation = ++_gameTuningSaveGeneration;
        tree.CreateTimer(0.5, true, false, true).Timeout += () =>
        {
            if (generation == _gameTuningSaveGeneration)
                FlushGameTuningSave();
        };
    }

    private static void FlushGameTuningSave()
    {
        if (!_gameTuningDirty)
            return;

        _gameTuningDirty = false;
        GameTuning.Save();
    }

    // Os 11 percentuais do GameTuning: armazenamento externo (user://game_tuning.cfg), fora do dev_tuning.json.
    private static void RegisterQuickTuning()
    {
        void Q(string key, string label, int def, Func<int> get, Action<int> set, string note = "")
        {
            Register(new DevParameter(
                key, label, GQuick, def, 1, 250, 1, true, DevApplyMode.Live, note,
                () => get(),
                v =>
                {
                    set((int)Math.Round(v));
                    ScheduleGameTuningSave();
                }));
        }

        Q("game.walk", "Velocidade de caminhada (%)", 100, () => GameTuning.WalkSpeedPercent, GameTuning.SetWalkSpeedPercent);
        Q("game.run", "Velocidade de corrida (%)", 100, () => GameTuning.RunSpeedPercent, GameTuning.SetRunSpeedPercent);
        Q("game.dash_speed", "Velocidade do dash (%)", 100, () => GameTuning.DashSpeedPercent, GameTuning.SetDashSpeedPercent);
        Q("game.dash_distance", "Distância do dash (%)", 70, () => GameTuning.DashDistancePercent, GameTuning.SetDashDistancePercent);
        Q("game.camera_zoom", "Distância da câmera (%)", 100, () => GameTuning.CameraZoomDistancePercent, GameTuning.SetCameraZoomDistancePercent);
        Q("game.damage", "Dano do jogador (%)", 100, () => GameTuning.DamagePercent, GameTuning.SetDamagePercent);
        Q("game.health", "Vida do jogador (%)", 100, () => GameTuning.HealthPercent, GameTuning.SetHealthPercent);
        Q("game.animation_speed", "Velocidade das animações (%)", 100, () => GameTuning.AnimationSpeedPercent, GameTuning.SetAnimationSpeedPercent);
        Q("game.attack_speed", "Velocidade de ataque (%)", 100, () => GameTuning.AttackSpeedPercent, GameTuning.SetAttackSpeedPercent);
        Q("game.attack_delay", "Atraso de ataque (%)", 100, () => GameTuning.AttackDelayPercent, GameTuning.SetAttackDelayPercent);
        Q("game.attack_range", "Alcance de ataque (%)", 65, () => GameTuning.AttackRangePercent, GameTuning.SetAttackRangePercent, "Multiplicador efetivo = valor / 50.");
    }

    // Chaves por ID, geradas a partir dos catalogos (padroes vindos do JSON).
    private static void RegisterByIdKeys()
    {
        try
        {
            foreach (var skill in SkillCatalog.All)
            {
                P($"{skill.Id}.cost", $"{skill.Id}: custo", GSkills, skill.Cost, 0, Math.Max(skill.Cost * 10, 100), 1, true);
                P($"{skill.Id}.max_rank", $"{skill.Id}: rank máximo", GSkills, skill.MaxRank, 1, Math.Max(skill.MaxRank * 4, 10), 1, true);
            }

            foreach (var artifact in ArtifactCatalog.All)
            {
                P($"{artifact.Id}.weight", $"{artifact.Id}: peso de sorteio", GArtifacts, artifact.Weight, 0, Math.Max(artifact.Weight * 10, 100), 1, true);
                for (var i = 0; i < artifact.Effects.Length; i++)
                {
                    var effect = artifact.Effects[i];
                    RegisterEffectValue($"{artifact.Id}.effect.{i}.value", $"{artifact.Id}: efeito {i} ({effect.Type})", GArtifacts, effect.Value, DevApplyMode.Reapply);
                }
            }

            foreach (var blessing in BloodBlessingCatalog.All)
            {
                for (var i = 0; i < blessing.Effects.Length; i++)
                {
                    var effect = blessing.Effects[i];
                    if (effect.BooleanValue)
                        continue; // efeitos booleanos (liga/desliga, default 1): decidido NAO registrar (T6); desligar a bencao e remover do catalogo, nao ajuste numerico

                    RegisterEffectValue($"blessing.{blessing.Id}.effect.{i}.value", $"{blessing.Id}: efeito {i} ({effect.Type})", GBlessings, effect.Value, DevApplyMode.Reapply);
                }
            }
        }
        catch (Exception exception)
        {
            Godot.GD.PushError($"DevParameterRegistry: falha ao registrar chaves por ID: {exception.Message}");
        }
    }

    private static void RegisterEffectValue(string key, string label, string group, double def, DevApplyMode apply)
    {
        var max = Math.Max(Math.Abs(def) * 10, 1.0);
        var step = Math.Pow(10, Math.Floor(Math.Log10(max)) - 2);
        Register(new DevParameter(key, label, group, def, -max, max, step, false, apply));
    }
}

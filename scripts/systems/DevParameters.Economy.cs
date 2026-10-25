// Parametros de economia e conteudo (T6): constantes de RoomRewardCatalog, ArtifactCatalog, BloodBlessingCatalog,
// BloodBlessingRuntime, RunManager (escala de dificuldade e boss), altar e Fonte de Sangue
// (alem das chaves por ID dinamicas ja registradas em DevParameters.Legacy.cs).

// Tabela unica de padroes de economia por mapa (indice = mapa 0..3). RunManager ja a usa; EnemyBasic e HUD.Compendium
// ainda repetem estes literais e podem passar a ler daqui (valores identicos aos registrados em DevParameters.Legacy.cs).
public static class EconomyDefaults
{
    public static readonly int[] BossCoins = { 20, 30, 40, 0 };               // economy.coins.boss.{m}
    public static readonly int[] BossCursedBlood = { 40, 60, 80, 120 };       // economy.cursed_blood.boss.{m}
    public static readonly int[] LegendaryCursedBlood = { 0, 6, 8, 8 };       // economy.cursed_blood.legendary.{m}
    public static readonly float[] CoinMapFactor = { 1.0f, 0.6f, 0.6f, 0.6f }; // economy.coins.map_factor.{m}
    public static readonly int[] RoomCoinPack = { 40, 45, 50 };               // economy.coins.room_reward_pack.{m}
    public const int EnemyCoinsMin = 1;                                       // economy.coins.enemy.base_min
    public const int EnemyCoinsMax = 1;                                       // economy.coins.enemy.base_max
    public const int BlessingFallbackCoins = 25;                              // economy.coins.blessing_fallback
    public static readonly int[] ShopPrices = { 110, 150, 200, 270, 360, 480 }; // economy.shop.price.{raridade}
    public const float FountainCostStep = 0.5f;                               // fountain.cost_step
}

public static partial class DevParameterRegistry
{
    static partial void RegisterEconomy()
    {
        RegisterShopExtra();
        RegisterRewardsExtra();
        RegisterRunScale();
        RegisterBlessingTuning();
        RegisterAnvilTuning();

        P("fountain.cost_step", "Fonte de Sangue: acréscimo de custo por rank", GEconomy, EconomyDefaults.FountainCostStep, 0, 5, 0.05, false, DevApplyMode.Live, "Custo = base x (1 + rank x este valor).");
        P("altar.sacrifice_fraction", "Altar: fração da vida máxima sacrificada", GEconomy, 0.05, 0, 0.9, 0.01, false, DevApplyMode.Live, "Lido ao abrir o altar e ao confirmar o sacrifício.");
    }

    private static void RegisterShopExtra()
    {
        P("economy.shop.max_discount", "Loja: desconto máximo (skill)", GEconomy, 0.9, 0, 1, 0.05, false);
        P("economy.shop.first_slot_max_rarity", "Loja: raridade máx. do 1º slot (0=Common ... 5=Mythic)", GEconomy, 1, 0, 5, 1, true, DevApplyMode.NextRoom);

        string[] rarities = { "common", "uncommon", "rare", "epic", "legendary", "mythic" };
        double[][] weights =
        {
            new[] { 45.0, 35.0, 25.0 },
            new[] { 33.0, 30.0, 28.0 },
            new[] { 15.0, 20.0, 22.0 },
            new[] { 6.0, 10.0, 15.0 },
            new[] { 1.0, 4.0, 8.0 },
            new[] { 0.0, 1.0, 2.0 },
        };
        for (var r = 0; r < rarities.Length; r++)
        {
            for (var v = 0; v < 3; v++)
                P($"economy.shop.rarity_weight.{rarities[r]}.{v}", $"Loja: peso de raridade {rarities[r]} (visita {v + 1})", GEconomy, weights[r][v], 0, 200, 0.5, false, DevApplyMode.NextRoom);
        }
    }

    // Bigorna (docs/design/bigorna.md). Os pesos de porta e o teto sao lidos por RoomRewardCatalog/RunState; os valores das
    // modificacoes por WeaponModDefinition.GetValue (weapon.mod.<id>.<field>, padrao = weapon_mods.json).
    private const string GAnvil = "Bigorna";

    private static void RegisterAnvilTuning()
    {
        RegisterPerMap("reward.door_weight.anvil.{m}", "Peso da porta de Bigorna", GAnvil, new double[] { 4, 8, 4 }, 0, 100, 1, true, DevApplyMode.NextRoom, "Só vale para salas ainda não sorteadas. Para testar já: aba Ações > Portas de recompensa > Forçar portas.");
        P("economy.anvil.run_cap", "Teto de Bigornas escolhidas por run", GAnvil, 3, 0, 10, 1, true, DevApplyMode.NextRoom, "O painel Forçar portas ignora este teto (e não o conta).");
        P("weapon.mod.offer_choices", "Cartas por oferta de Bigorna", GAnvil, 3, 1, 5, 1, true);
        P("weapon.mod.longsword_reach.value", "Lâmina Alongada: alcance", GAnvil, 0.15, 0, 1, 0.01, false);
        P("weapon.mod.longsword_edge.value", "Fio Afiado: dano básico/rápido", GAnvil, 0.15, 0, 2, 0.01, false);
        P("weapon.mod.longsword_tempo.value", "Cabo Equilibrado: redução de tempo", GAnvil, 0.12, 0, 0.5, 0.01, false);
        P("weapon.mod.longsword_weight.value", "Contrapeso de Aço: empurrão", GAnvil, 0.30, 0, 3, 0.05, false);
        P("weapon.mod.longsword_weight.value2", "Contrapeso de Aço: atordoamento", GAnvil, 0.40, 0, 3, 0.05, false);
        P("weapon.mod.longsword_whirl.value", "Impacto Ampliado: raio do especial", GAnvil, 0.20, 0, 2, 0.01, false);
        P("weapon.mod.longsword_whirl.value2", "Impacto Ampliado: redução de recarga", GAnvil, 0.20, 0, 0.9, 0.01, false);
        P("weapon.mod.revolver_cylinder.value", "Tambor Largo: balas extras", GAnvil, 4, 0, 24, 1, false);
        P("weapon.mod.revolver_speedloader.value", "Carregador Rápido: redução da recarga", GAnvil, 0.35, 0, 0.9, 0.01, false);
        P("weapon.mod.revolver_hollow.value", "Ponta Oca: dano básico/rápido", GAnvil, 0.15, 0, 2, 0.01, false);
        P("weapon.mod.revolver_pierce.value", "Bala Perfurante: inimigos extras atravessados", GAnvil, 1, 0, 10, 1, false);
        P("weapon.mod.revolver_silver.value", "Prata Pura: alcance da Bala de Prata", GAnvil, 0.20, 0, 2, 0.01, false);
        P("weapon.mod.revolver_silver.value2", "Prata Pura: redução de recarga", GAnvil, 0.25, 0, 0.9, 0.01, false);
    }

    private static void RegisterRewardsExtra()
    {
        P("reward.door_swap_chance", "Chance de trocar a ordem das duas portas", GRewards, 0.5, 0, 1, 0.05, false, DevApplyMode.NextRoom);
        P("reward.blessing.choices", "Opções de Bênção por porta", GRewards, 3, 1, 8, 1, true, DevApplyMode.NextRoom, "Só vale para conjuntos de portas ainda não sorteados. A oferta da Fonte (HUD.BlessingOffer) também lê esta chave.");
    }

    // Escala por sala dos inimigos comuns e ajustes de boss em RunManager (nome legado enemy.blood_mage.* preservado nas outras chaves).
    private static void RegisterRunScale()
    {
        P("enemy.scale.speed_per_room", "Escala: velocidade por sala", GEnemies, 0.2, 0, 2, 0.05, false, DevApplyMode.NewObjects);
        P("enemy.scale.damage_per_room", "Escala: dano por sala", GEnemies, 2.0, 0, 50, 0.5, false, DevApplyMode.NewObjects);
        P("enemy.scale.health_per_room", "Escala: vida por sala", GEnemies, 18.0, 0, 500, 1, false, DevApplyMode.NewObjects);
        P("enemy.scale.common_damage_mult", "Escala: multiplicador de dano comum", GEnemies, 0.81, 0, 5, 0.01, false, DevApplyMode.NewObjects);
        // Vida/dano/velocidade de Tank e Ranged e de raridade vem de enemy.tank.*, enemy.ranged.* e enemy.rarity.* (DevParameters.Enemies.cs);
        // RunManager.ApplyCommonEnemyTuning le essas mesmas chaves (as antigas enemy.scale.{tank,ranged}_*_mult foram removidas por duplicarem).

        P("boss.speed_mult.default", "Boss: multiplicador de velocidade", GEnemies, 1.85, 0, 10, 0.05, false, DevApplyMode.NextRoom);
        P("boss.speed_mult.silver_dragonkin", "Boss Silver Dragonkin: multiplicador de velocidade", GEnemies, 1.4, 0, 10, 0.05, false, DevApplyMode.NextRoom);
        P("boss.damage_mult.base", "Boss: multiplicador de dano base", GEnemies, 0.9, 0, 5, 0.05, false, DevApplyMode.NextRoom, "O dano por golpe é reescrito por ApplyBossMoveTuning; vale só para o valor inicial.");
        P("boss.damage_mult.map_1", "Boss do mapa 2: multiplicador de dano", GEnemies, 1.35, 0, 5, 0.05, false, DevApplyMode.NextRoom);
        P("boss.windup.default", "Boss: windup base (s)", GEnemies, 0.38, 0.05, 3, 0.01, false, DevApplyMode.NextRoom);
        P("boss.windup.silver_dragonkin", "Boss Silver Dragonkin: windup base (s)", GEnemies, 0.75, 0.05, 3, 0.01, false, DevApplyMode.NextRoom);
    }

    private static void RegisterBlessingTuning()
    {
        P("blessing.selection.stirps_weight", "Bênçãos: peso da linhagem Stirps no sorteio", GBlessings, 0.45, 0, 5, 0.05, false, DevApplyMode.NextRoom);
        P("blessing.selection.direct_attack_weight", "Bênçãos: peso das que modificam ataque direto", GBlessings, 1.75, 0, 10, 0.05, false, DevApplyMode.NextRoom);
        P("blessing.level_bonus", "Bênçãos: bônus por nível adicional", GBlessings, 0.25, 0, 2, 0.05, false, DevApplyMode.Reapply);
        P("blessing.rat.damage_ratio", "Ratos de sangue: fração do dano do ataque básico", GBlessings, 0.5, 0, 5, 0.05, false);
        P("blessing.recoverable.step", "Vida recuperável: intervalo de cada passo (s)", GBlessings, 0.25, 0.05, 2, 0.05, false);
        P("blessing.pool.heal_tick", "Poça de sangue: intervalo da cura (s)", GBlessings, 0.25, 0.05, 2, 0.05, false);
        P("blessing.pool.bleed_tick", "Poça sangrenta: intervalo do sangramento (s)", GBlessings, 1.0, 0.1, 5, 0.1, false);
        P("blessing.pool.base_radius", "Poça de sangue: raio base", GBlessings, 1.8, 0.2, 10, 0.1, false);
        P("blessing.corpse_explosion.radius", "Explosão de cadáver infectado: raio", GBlessings, 2.5, 0.2, 15, 0.1, false);
        P("blessing.corpse_explosion.base_damage", "Explosão de cadáver infectado: dano base", GBlessings, 32, 0, 1000, 1, false);
        P("blessing.charmed_explosion.radius", "Explosão de encantado: raio", GBlessings, 2.8, 0.2, 15, 0.1, false);
        P("blessing.charmed_explosion.base_damage", "Explosão de encantado: dano base", GBlessings, 32, 0, 1000, 1, false);
        P("blessing.pestilence.base_spread_targets", "Pestilência: alvos base da propagação na morte", GBlessings, 3, 0, 20, 1, true);
        P("blessing.nearby_enemy_radius", "Bênçãos: raio de 'inimigo próximo'", GBlessings, 4.0, 0.5, 20, 0.5, false);
    }
}

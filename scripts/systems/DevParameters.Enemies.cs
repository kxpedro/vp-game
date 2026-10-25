// Parametros de inimigos, bosses e salas (T5): EnemyBasic, EnemyTuningCatalog, BossController/BossCatalog, RoomGenerator, CampaignCatalog.
// As chaves enemy.basic.* e enemy.blood_mage.* ficam em DevParameters.Legacy.cs (nome legado preservado).
// Os padroes abaixo espelham o codigo; o codigo le CompendiumTuning.GetFloat/GetInt(chave, padrao) no momento do uso.
public static partial class DevParameterRegistry
{
    private const string GEnemyBase = "Inimigos – Base";
    private const string GEnemyArchetypes = "Inimigos – Arquétipos";
    private const string GEnemyRarity = "Inimigos – Raridade";
    private const string GBosses = "Bosses";
    private const string GRooms = "Salas e waves";
    private const string GCampaign = "Campanha";

    static partial void RegisterEnemies()
    {
        RegisterEnemyBase();
        RegisterEnemyArchetypes();
        RegisterEnemyRarity();
        RegisterBossGlobals();
        RegisterBossMoves();
        RegisterBossCatalog();
        RegisterRoomsAndCampaign();
    }

    private static void RegisterEnemyBase()
    {
        const DevApplyMode N = DevApplyMode.NewObjects;
        const string G = GEnemyBase;
        P("enemy.base.detection_range", "Alcance de detecção", G, 22.0, 0, 100, 0.5, false, N);
        P("enemy.base.patrol_speed", "Velocidade de patrulha", G, 1.65, 0, 20, 0.05, false, N);
        P("enemy.base.patrol_radius", "Raio de patrulha", G, 12.0, 0, 60, 0.5, false, N);
        P("enemy.base.attack_range", "Alcance do ataque (melee)", G, 1.65, 0.1, 30, 0.05, false, N, "Ranged usa enemy.ranged.attack_range; Tank multiplica por enemy.tank.range_multiplier.");
        P("enemy.base.attack_windup", "Windup do ataque (s)", G, 0.85, 0.05, 5, 0.05, false, N, "Telegraph: tempo até o golpe cair.");
        P("enemy.base.attack_cooldown", "Cooldown do ataque (s)", G, 1.55, 0.25, 10, 0.05, false, N, "Frequência de ataque de todos os inimigos comuns: menor = mais ataques. Cada arquétipo multiplica por enemy.<arquétipo>.cooldown_multiplier; o mapa por enemy.map.{m}.attack_cooldown_multiplier.");
        P("enemy.base.spawn_attack_delay", "Atraso do 1º ataque após nascer (s)", G, 2.0, 0, 20, 0.1, false, N);
        P("enemy.base.attack_width", "Largura do ataque", G, 1.25, 0.1, 10, 0.05, false, N);
        P("enemy.base.attack_depth_bonus", "Profundidade extra do ataque", G, 0.75, 0, 10, 0.05, false, N);
        P("enemy.base.tactical_speed_multiplier", "Multiplicador de velocidade tática", G, 0.82, 0, 3, 0.02, false, N);
        P("enemy.base.ranged_preferred_distance", "Ranged: distância preferida", G, 6.25, 0, 30, 0.25, false, N);
        P("enemy.base.ranged_retreat_distance", "Ranged: distância de recuo", G, 4.6, 0, 30, 0.25, false, N);
        P("enemy.base.contact_damage", "Dano de contato (fração do dano de ataque)", G, 0.05, 0, 1, 0.01, false, N, "Zero no TrainingDummy.");
        P("enemy.base.contact_range", "Alcance do dano de contato", G, 0.95, 0, 5, 0.05, false, N);
        P("enemy.base.contact_cooldown", "Cooldown do dano de contato (s)", G, 0.8, 0.1, 10, 0.05, false, N);
        P("enemy.base.body_radius_ratio", "Raio do corpo (colisão com paredes) x raio do alvo", G, 0.8, 0.3, 1.5, 0.05, false, N, "A cápsula nunca fica menor que a da cena (0,32); vale para Tank e chefes, que são mais largos.");
        P("enemy.base.knockback_resistance", "Resistência a knockback (fator aplicado à força)", G, 0.45, 0, 2, 0.05, false, N);
        P("enemy.base.max_pestilence_stacks", "Máx. de acúmulos de pestilência", G, 5, 1, 20, 1, true, N);
        P("enemy.base.damage_per_status_stack", "Dano extra recebido por acúmulo de status", G, 0.05, 0, 1, 0.01, false, N);

        // Lidos a cada uso.
        P("enemy.knockback_decay", "Desaceleração do knockback", G, 18.0, 1, 100, 1, false);
        P("enemy.boss.knockback_factor", "Fator de knockback recebido pelo boss", G, 0.05, 0, 1, 0.01, false);
        P("enemy.stagger.rarity_multiplier", "Fator de stagger em inimigos não-normais", G, 0.72, 0, 2, 0.02, false);
        P("enemy.backstab_dot", "Limiar de ataque pelas costas (produto escalar)", G, -0.45, -1, 1, 0.05, false);
        P("enemy.status.pestilence_refresh_seconds", "Pestilência: duração mínima ao ser acertado (s)", G, 3.0, 0, 30, 0.5, false);
        P("enemy.status.bleeding_refresh_seconds", "Sangramento: duração mínima ao ser acertado (s)", G, 2.0, 0, 30, 0.5, false);
        P("enemy.status.pestilence_tick_seconds", "Pestilência: intervalo do tick (s)", G, 1.0, 0.1, 10, 0.1, false);
        P("enemy.status.bleeding_tick_seconds", "Sangramento: intervalo do tick (s)", G, 1.0, 0.1, 10, 0.1, false);
        P("enemy.status.pestilence_spread_radius", "Pestilência infinita: raio de contágio", G, 6.0, 0, 30, 0.5, false);
        P("enemy.projectile.speed", "Projétil: velocidade", G, 5.2, 0.5, 40, 0.1, false);
        P("enemy.projectile.hit_radius", "Projétil: raio de acerto no jogador (centro do jogador)", G, 0.55, 0.1, 5, 0.02, false);
        P("enemy.projectile.range_multiplier", "Projétil: alcance (x alcance de ataque)", G, 1.5, 0.5, 5, 0.05, false, DevApplyMode.NewObjects, "Também define o tamanho do telegraph do Ranged.");
        RegisterPerMap("enemy.map.{m}.attack_cooldown_multiplier", "Multiplicador de cooldown do ataque", G, new double[] { 1, 1, 1, 1 }, 0.1, 10, 0.02, false, N, "Menor = inimigos do mapa atacam com mais frequência. Não afeta bosses.");
        P("enemy.legendary.double_hit_delay", "Lendário: atraso do 2º golpe (s)", G, 0.16, 0, 2, 0.02, false);
        P("enemy.legendary.double_hit_damage", "Lendário: dano do 2º golpe (x dano)", G, 0.55, 0, 3, 0.05, false);
    }

    private static void RegisterEnemyArchetypes()
    {
        const string G = GEnemyArchetypes;
        const DevApplyMode N = DevApplyMode.NewObjects;

        // Melee (sem multiplicadores de estatística: usa a base).
        P("enemy.melee.cooldown_multiplier", "Melee: multiplicador de cooldown do ataque", G, 1.0, 0.1, 10, 0.02, false, N, "Menor = ataca com mais frequência. Multiplica enemy.base.attack_cooldown.");
        P("enemy.melee.hit_radius", "Melee: raio do alvo (acerto do jogador)", G, 0.48, 0.1, 3, 0.02, false, N);
        P("enemy.melee.tactical_range_pad", "Melee: folga da zona tática", G, 0.9, 0, 10, 0.1, false);
        P("enemy.melee.retreat_weight", "Melee: peso do recuo tático", G, 0.32, 0, 2, 0.02, false);
        P("enemy.melee.post_attack_move_min", "Melee: movimento pós-ataque (mín. s)", G, 0.28, 0, 5, 0.02, false);
        P("enemy.melee.post_attack_move_max", "Melee: movimento pós-ataque (máx. s)", G, 0.58, 0, 5, 0.02, false);

        // Ranged.
        P("enemy.ranged.attack_range", "Ranged: alcance do ataque", G, 11.0, 1, 40, 0.5, false, N);
        P("enemy.ranged.damage_multiplier", "Ranged: multiplicador de dano", G, 0.8, 0, 5, 0.05, false, N);
        P("enemy.ranged.speed_multiplier", "Ranged: multiplicador de velocidade", G, 0.9, 0, 3, 0.05, false, N);
        P("enemy.ranged.cooldown_multiplier", "Ranged: multiplicador de cooldown do ataque", G, 1.0, 0.1, 10, 0.02, false, N, "Menor = atira com mais frequência. Multiplica enemy.base.attack_cooldown.");
        P("enemy.ranged.hit_radius", "Ranged: raio do alvo (acerto do jogador)", G, 0.48, 0.1, 3, 0.02, false, N);
        P("enemy.ranged.aim_lock_seconds", "Ranged: travamento da mira antes do tiro (s)", G, 0.25, 0, 3, 0.05, false, N, "Também afeta o aviso de travamento do telegraph.");
        P("enemy.ranged.tactical_range_pad", "Ranged: folga da zona tática", G, 2.0, 0, 10, 0.1, false);
        P("enemy.ranged.flee_chance", "Ranged: chance de fugir após movimento tático", G, 0.34, 0, 1, 0.02, false);
        P("enemy.ranged.flee_min_seconds", "Ranged: fuga (mín. s)", G, 1.15, 0, 10, 0.05, false);
        P("enemy.ranged.flee_max_seconds", "Ranged: fuga (máx. s)", G, 2.25, 0, 10, 0.05, false);
        P("enemy.ranged.post_attack_move_min", "Ranged: movimento pós-ataque (mín. s)", G, 0.9, 0, 5, 0.05, false);
        P("enemy.ranged.post_attack_move_max", "Ranged: movimento pós-ataque (máx. s)", G, 1.35, 0, 5, 0.05, false);

        // Tank (multiplicadores aplicados sobre o valor atual: ConfigureVariant nao e idempotente).
        P("enemy.tank.health_multiplier", "Tank: multiplicador de vida", G, 1.8, 0.1, 10, 0.05, false, N);
        P("enemy.tank.speed_multiplier", "Tank: multiplicador de velocidade", G, 0.62, 0, 3, 0.02, false, N);
        P("enemy.tank.damage_multiplier", "Tank: multiplicador de dano", G, 1.52, 0, 10, 0.02, false, N);
        P("enemy.tank.windup_multiplier", "Tank: multiplicador de windup", G, 1.65, 0.1, 10, 0.05, false, N);
        P("enemy.tank.range_multiplier", "Tank: multiplicador de alcance", G, 1.18, 0.1, 5, 0.02, false, N);
        P("enemy.tank.width_multiplier", "Tank: multiplicador de largura do golpe", G, 1.65, 0.1, 10, 0.05, false, N);
        P("enemy.tank.depth_bonus_add", "Tank: profundidade extra do golpe (+)", G, 0.85, -2, 10, 0.05, false, N);
        P("enemy.tank.cooldown_multiplier", "Tank: multiplicador de cooldown do ataque", G, 1.18, 0.1, 10, 0.02, false, N, "Menor = ataca com mais frequência. Multiplica enemy.base.attack_cooldown.");
        P("enemy.tank.knockback_multiplier", "Tank: multiplicador de resistência a knockback", G, 0.3, 0, 5, 0.05, false, N);
        P("enemy.tank.hit_radius", "Tank: raio do alvo (acerto do jogador)", G, 0.68, 0.1, 3, 0.02, false, N);
        P("enemy.tank.retreat_weight", "Tank: peso do recuo tático", G, 0.16, 0, 2, 0.02, false);
        P("enemy.tank.post_attack_move_min", "Tank: movimento pós-ataque (mín. s)", G, 0.45, 0, 5, 0.05, false);
        P("enemy.tank.post_attack_move_max", "Tank: movimento pós-ataque (máx. s)", G, 0.8, 0, 5, 0.05, false);
    }

    private static void RegisterEnemyRarity()
    {
        const string G = GEnemyRarity;
        const DevApplyMode N = DevApplyMode.NewObjects;
        (string id, string name, double health, double damage, double speed)[] rarities =
        {
            ("magic", "Mágico", 1.45, 1.3, 1.08),
            ("rare", "Raro", 2.35, 1.75, 1.14),
            ("legendary", "Lendário", 6.0, 2.8, 1.22)
        };
        foreach (var (id, name, health, damage, speed) in rarities)
        {
            P($"enemy.rarity.{id}.health", $"{name}: multiplicador de vida", G, health, 0.1, 30, 0.05, false, N);
            P($"enemy.rarity.{id}.damage", $"{name}: multiplicador de dano", G, damage, 0.1, 20, 0.05, false, N);
            P($"enemy.rarity.{id}.speed", $"{name}: multiplicador de velocidade", G, speed, 0.1, 5, 0.02, false, N);
        }

        // Faixas de rolagem de raridade: interpolam de "início" a "fim" conforme a progressão das salas do mapa.
        // Mapa 1: estouro cai em Raro; demais mapas: em Lendário.
        (string set, string label, (string id, string name, double start, double end)[] rows)[] sets =
        {
            ("first", "Mapa 1", new[] { ("normal", "Normal", 0.90, 0.72), ("magic", "Mágico", 0.10, 0.23), ("rare", "Raro", 0.0, 0.05) }),
            ("later", "Mapas 2+", new[] { ("normal", "Normal", 0.68, 0.45), ("magic", "Mágico", 0.25, 0.32), ("rare", "Raro", 0.07, 0.18) })
        };
        foreach (var (set, label, rows) in sets)
        {
            foreach (var (id, name, start, end) in rows)
            {
                P($"enemy.roll.{set}.{id}_start", $"Chance de {name} ({label}) – 1ª sala", G, start, 0, 1, 0.01, false, DevApplyMode.NextRoom, "Probabilidade acumulada: normal, depois mágico, depois raro; o resto vira Raro (mapa 1) ou Lendário.");
                P($"enemy.roll.{set}.{id}_end", $"Chance de {name} ({label}) – última sala", G, end, 0, 1, 0.01, false, DevApplyMode.NextRoom);
            }
        }
    }

    private static void RegisterBossGlobals()
    {
        const string G = GBosses;
        const DevApplyMode N = DevApplyMode.NewObjects;
        P("boss.hp.min", "Vida mínima do boss", G, 1000, 1, 1000000, 100, false, N);
        P("boss.first_move_delay", "Atraso do 1º golpe (s)", G, 1.1, 0, 10, 0.1, false, N);
        P("boss.encounter.radius", "Distância do jogador que acorda o boss e abre o diálogo", G, 7.5, 1, 30, 0.25, false);
        P("boss.cooldown_min", "Cooldown mínimo entre golpes (s)", G, 0.75, 0.05, 10, 0.05, false);
        P("boss.cooldown_phase_reduction", "Redução de cooldown por fase", G, 0.12, 0, 0.9, 0.01, false);
        P("boss.attack_cooldown_min", "Cooldown mínimo do ataque do EnemyBasic (s)", G, 0.35, 0.05, 10, 0.05, false);
        P("boss.phase2_threshold", "Fase 2 abaixo de (fração da vida)", G, 0.66, 0, 1, 0.01, false);
        P("boss.phase3_threshold", "Fase 3 abaixo de (fração da vida)", G, 0.33, 0, 1, 0.01, false);
        P("boss.phase_speed_multiplier", "Multiplicador de velocidade a cada fase", G, 1.12, 0.5, 3, 0.02, false);
        P("boss.phase_move_delay", "Atraso do golpe após trocar de fase (máx. s)", G, 0.45, 0, 10, 0.05, false);
        P("boss.phase_break_seconds", "Pausa na transição de fase (s, invulnerável)", G, 0.5, 0, 5, 0.05, false);
        P("boss.minions.first_delay", "Lacaios: 1ª invocação após o boss acordar (s)", G, 5, 0, 120, 0.5, false);
        P("boss.minions.interval", "Lacaios: intervalo entre invocações (s, cai 15% por fase)", G, 12, 1, 120, 0.5, false);
        P("boss.minions.count_base", "Lacaios por invocação (fase 1; +1 por fase)", G, 2, 0, 10, 1, false);
        P("boss.minions.max_alive", "Lacaios vivos ao mesmo tempo (máx.)", G, 4, 0, 20, 1, false);
        P("boss.minions.on_phase", "Lacaios: invocar na troca de fase (1 = sim)", G, 1, 0, 1, 1, false);
        P("boss.minions.distance_min", "Lacaios: distância mínima do boss ao surgir (m)", G, 6, 0, 30, 0.5, false);
        P("boss.minions.distance_max", "Lacaios: distância máxima do boss ao surgir (m)", G, 8.5, 0, 30, 0.5, false);
        P("boss.minions.warning", "Lacaios: aviso no chão antes de surgir (s)", G, 0.9, 0.1, 5, 0.05, false);
        P("boss.minions.fury_on_death", "Lacaios: Fúria do jogador por abate", G, 20, 0, 200, 1, false, DevApplyMode.NewObjects, "20 = barra cheia (100) a cada 5 lacaios.");
        P("boss.minions.health_mult", "Lacaios: multiplicador de vida (sobre o inimigo comum da sala)", G, 0.7, 0.05, 5, 0.05, false, DevApplyMode.NewObjects);
        P("boss.warning.single_circle", "Aviso mínimo: círculo isolado (s)", G, 0.5, 0.05, 5, 0.05, false);
        P("boss.warning.sequence_first", "Aviso mínimo: 1º círculo de sequência (s)", G, 0.6, 0.05, 5, 0.05, false);
        P("boss.warning.line", "Aviso mínimo: carga/linha (s)", G, 0.6, 0.05, 5, 0.05, false);
        P("boss.warning.claw_first", "Aviso mínimo: 1º golpe do claw_combo (s)", G, 0.45, 0.05, 5, 0.05, false);
        P("boss.warning.claw_follow", "Aviso mínimo: golpes 2+ do claw_combo (s)", G, 0.35, 0.05, 5, 0.05, false);
        P("boss.warning.teleport", "Aviso do teleporte (s)", G, 0.4, 0.05, 5, 0.05, false);
        P("boss.damage_map_scale", "Dano do boss: acréscimo por mapa", G, 0.22, 0, 3, 0.01, false, DevApplyMode.Live, "Dano = enemy.blood_mage.damage x (1 + mapa x este valor) x golpe x fase x final.");
        P("boss.damage_phase_scale", "Dano do boss: acréscimo por fase", G, 0.18, 0, 3, 0.01, false);
        P("boss.damage_final_multiplier", "Dano do boss: multiplicador final", G, 0.9, 0, 5, 0.05, false);
        P("boss.attack_width_base", "Largura base do ataque do boss", G, 1.5, 0.1, 20, 0.05, false);
        foreach (var id in new[] { "invisible_woman", "labyrinth_keeper", "silver_dragonkin", "armand" })
            P($"boss.{id}.cast_multiplier", $"{id}: multiplicador do tempo de aviso", G, id == "silver_dragonkin" ? 1.3 : 1.0, 0.1, 5, 0.05, false);
    }

    private static void RegisterBossMoves()
    {
        const string G = GBosses;

        // claw_combo
        P("boss.claw_combo.hits", "claw_combo: nº de golpes", G, 3, 1, 10, 1, true);
        P("boss.claw_combo.reach", "claw_combo: distância do 1º golpe", G, 1.35, 0, 10, 0.05, false);
        P("boss.claw_combo.reach_step", "claw_combo: avanço por golpe", G, 0.18, 0, 5, 0.02, false);
        P("boss.claw_combo.radius", "claw_combo: raio", G, 1.45, 0.1, 10, 0.05, false);
        P("boss.claw_combo.windup", "claw_combo: windup (s)", G, 0.28, 0.05, 3, 0.02, false);
        P("boss.claw_combo.damage_first", "claw_combo: dano dos golpes iniciais (x dano)", G, 0.72, 0, 5, 0.02, false);
        P("boss.claw_combo.damage_last", "claw_combo: dano do último golpe (x dano)", G, 1.15, 0, 5, 0.02, false);
        P("boss.claw_combo.gap", "claw_combo: pausa entre golpes (s)", G, 0.1, 0, 3, 0.02, false);

        // radial: tail_sweep e force_outbreak
        P("boss.tail_sweep.radius", "tail_sweep: raio", G, 3.6, 0.5, 20, 0.1, false);
        P("boss.tail_sweep.windup", "tail_sweep: windup (s)", G, 0.62, 0.05, 3, 0.02, false);
        P("boss.tail_sweep.pulses", "tail_sweep: nº de pulsos", G, 1, 1, 10, 1, true);
        P("boss.force_outbreak.radius", "force_outbreak: raio máximo", G, 5.0, 0.5, 20, 0.1, false);
        P("boss.force_outbreak.windup", "force_outbreak: windup (s)", G, 0.48, 0.05, 3, 0.02, false);
        P("boss.force_outbreak.pulses_base", "force_outbreak: pulsos base (+fase)", G, 2, 0, 10, 1, true);
        P("boss.force_outbreak.band_width", "force_outbreak (Guardiã): largura do anel de onda", G, 1.3, 0.2, 10, 0.05, false);
        P("boss.radial.damage_factor", "Pulsos radiais: divisor de dano por pulso", G, 0.72, 0.1, 3, 0.02, false);
        P("boss.radial.pulse_gap", "Pulsos radiais: pausa entre pulsos (s)", G, 0.12, 0, 3, 0.02, false);

        // volleys
        P("boss.dragon_volley.shots_base", "dragon_volley: tiros base (+fase)", G, 3, 0, 20, 1, true);
        P("boss.dragon_volley.radius", "dragon_volley: raio", G, 1.25, 0.1, 10, 0.05, false);
        P("boss.dragon_volley.cadence", "dragon_volley: cadência/windup (s)", G, 0.46, 0.05, 3, 0.02, false);
        P("boss.psionic_volley.shots_base", "psionic_volley: tiros base (+fase)", G, 4, 0, 20, 1, true);
        P("boss.psionic_volley.radius", "psionic_volley: raio", G, 1.15, 0.1, 10, 0.05, false);
        P("boss.psionic_volley.cadence", "psionic_volley: cadência/windup (s)", G, 0.38, 0.05, 3, 0.02, false);
        P("boss.volley.damage", "Volleys: dano por tiro (x dano)", G, 0.58, 0, 5, 0.02, false);
        P("boss.volley.lead", "Volleys: antecipação da mira (x velocidade do jogador)", G, 0.22, 0, 2, 0.02, false);
        P("boss.volley.scatter", "Volleys: dispersão", G, 0.65, 0, 5, 0.05, false);
        P("boss.volley.gap", "Volleys: pausa entre tiros (s)", G, 0.08, 0, 3, 0.02, false);

        // cargas
        P("boss.blood_charge.distance", "blood_charge: distância", G, 6.4, 0.5, 30, 0.1, false);
        P("boss.blood_charge.windup", "blood_charge: windup (s)", G, 0.58, 0.05, 3, 0.02, false);
        P("boss.charge.width", "Cargas: largura", G, 1.7, 0.2, 10, 0.05, false);
        P("boss.charge.recovery", "Cargas: recuperação (s)", G, 0.18, 0, 3, 0.02, false);
        P("boss.mirror_dash.teleport_distance", "mirror_dash: distância do teleporte", G, 4.8, 0, 20, 0.1, false);
        P("boss.mirror_dash.radius", "mirror_dash: raio do golpe na saída", G, 1.8, 0.1, 10, 0.05, false);
        P("boss.mirror_dash.damage", "mirror_dash: dano do golpe na saída (x dano)", G, 0.55, 0, 5, 0.02, false);
        P("boss.mirror_dash.windup", "mirror_dash: windup do golpe na saída (s)", G, 0.38, 0.05, 3, 0.02, false);
        P("boss.mirror_dash.charge_distance", "mirror_dash: distância da carga", G, 7.0, 0.5, 30, 0.1, false);
        P("boss.mirror_dash.charge_windup", "mirror_dash: windup da carga (s)", G, 0.38, 0.05, 3, 0.02, false);

        // meteoros
        P("boss.meteor_requiem.count", "meteor_requiem: nº de meteoros", G, 6, 1, 30, 1, true);
        P("boss.meteor_requiem.radius", "meteor_requiem: raio", G, 1.35, 0.1, 10, 0.05, false);
        P("boss.meteor_requiem.windup", "meteor_requiem: windup (s)", G, 0.82, 0.05, 5, 0.02, false);
        P("boss.meteor_requiem.damage", "meteor_requiem: dano por meteoro (x dano)", G, 0.72, 0, 5, 0.02, false);
        P("boss.meteor_requiem.spread_min", "meteor_requiem: distância mínima ao alvo", G, 2.4, 0, 20, 0.1, false);
        P("boss.meteor_requiem.spread_max", "meteor_requiem: distância máxima ao alvo", G, 4.8, 0, 20, 0.1, false);

        // singularity
        P("boss.singularity.pulses_base", "singularity: pulsos base (+fase)", G, 3, 0, 20, 1, true);
        P("boss.singularity.radius", "singularity: raio inicial", G, 1.4, 0.1, 10, 0.05, false);
        P("boss.singularity.radius_step", "singularity: acréscimo de raio por pulso", G, 0.8, 0, 5, 0.05, false);
        P("boss.singularity.damage", "singularity: dano por pulso (x dano)", G, 0.42, 0, 5, 0.02, false);
        P("boss.singularity.windup", "singularity: windup por pulso (s)", G, 0.34, 0.05, 3, 0.02, false);
        P("boss.singularity.band_width", "singularity (Guardiã): largura das ondulações", G, 1.0, 0.2, 10, 0.05, false);
        P("boss.invisible_woman.dormant_alpha", "Guardiã: opacidade enquanto espera", G, 0.2, 0, 1, 0.05, false);
        P("boss.invisible_woman.materialize_seconds", "Guardiã: tempo para se materializar (s)", G, 0.8, 0.05, 5, 0.05, false);
        P("boss.invisible_woman.appear_seconds", "Guardiã: tempo para reaparecer após teleporte (s)", G, 0.15, 0.02, 2, 0.01, false);
        P("boss.invisible_woman.orb_height", "Guardiã: altura de saída dos orbes", G, 1.7, 0, 5, 0.05, false);

        // kinetic_shift
        P("boss.kinetic_shift.shield_base", "kinetic_shift: escudos base (+fase)", G, 2, 0, 20, 1, true);
        P("boss.kinetic_shift.radius", "kinetic_shift: raio do 1º golpe", G, 2.4, 0.1, 10, 0.05, false);
        P("boss.kinetic_shift.damage", "kinetic_shift: dano do 1º golpe (x dano)", G, 0.45, 0, 5, 0.02, false);
        P("boss.kinetic_shift.windup", "kinetic_shift: windup do 1º golpe (s)", G, 0.42, 0.05, 3, 0.02, false);
        P("boss.kinetic_shift.teleport_distance", "kinetic_shift: distância do teleporte", G, 5.2, 0, 20, 0.1, false);
        P("boss.kinetic_shift.strike_radius", "kinetic_shift: raio do golpe final", G, 1.55, 0.1, 10, 0.05, false);
        P("boss.kinetic_shift.strike_damage", "kinetic_shift: dano do golpe final (x dano)", G, 0.65, 0, 5, 0.02, false);
        P("boss.kinetic_shift.strike_windup", "kinetic_shift: windup do golpe final (s)", G, 0.35, 0.05, 3, 0.02, false);
    }

    // Vida (duração-alvo x DPS esperado) e, por golpe, cooldown/dano/alcance/fase mínima (padrões lidos do BossCatalog).
    private static void RegisterBossCatalog()
    {
        const string G = GBosses;
        foreach (var boss in BossCatalog.All)
        {
            P($"boss.{boss.Id}.target_seconds", $"{boss.Id}: duração-alvo da luta (s)", G, boss.TargetDurationSeconds, 10, 3600, 5, false, DevApplyMode.NewObjects, "Vida = duração-alvo x DPS esperado (mín. boss.hp.min).");
            P($"boss.{boss.Id}.expected_dps", $"{boss.Id}: DPS esperado do jogador", G, boss.ExpectedPlayerDps, 1, 1000, 1, false, DevApplyMode.NewObjects);
            foreach (var move in boss.Moves)
            {
                var key = $"boss.{boss.Id}.move.{move.Id}";
                var label = $"{boss.Id}/{move.Id}";
                P($"{key}.cooldown", $"{label}: cooldown (s)", G, move.Cooldown, 0.1, 20, 0.05, false);
                P($"{key}.damage_mult", $"{label}: multiplicador de dano", G, move.DamageMultiplier, 0, 10, 0.05, false);
                P($"{key}.range_mult", $"{label}: multiplicador de alcance", G, move.RangeMultiplier, 0.1, 10, 0.05, false);
                P($"{key}.min_phase", $"{label}: fase mínima", G, move.MinimumPhase, 1, 3, 1, true);
            }
        }
    }

    private static void RegisterRoomsAndCampaign()
    {
        const string G = GRooms;
        const DevApplyMode R = DevApplyMode.NextRoom;
        P("room.waves.count", "Nº de waves por sala", G, 4, 1, 20, 1, true, R);
        P("room.waves.size", "Inimigos por wave", G, 5, 1, 40, 1, true, R);
        P("room.waves.interval", "Intervalo entre waves (s)", G, 9.0, 1, 120, 0.5, false);
        P("room.spawn.delay", "Atraso do spawn após o aviso (s)", G, 3.0, 0, 30, 0.5, false);
        P("room.spawn.wall_padding", "Folga das paredes no spawn (células)", G, 2, 0, 6, 1, true, R);
        P("room.spawn.min_spacing", "Distância mínima entre spawns", G, 2.35, 0, 10, 0.05, false, R);
        P("room.spawn.attempts", "Tentativas de posição de spawn", G, 18, 1, 100, 1, true, R);
        P("room.spawn.jitter", "Variação aleatória da posição de spawn", G, 0.45, 0, 2, 0.05, false, R);
        P("room.spawn.central_fraction", "Fração das células de spawn mais centrais", G, 0.62, 0.1, 1, 0.02, false, R);
        // Pesos relativos de arquétipo por mapa (chance = peso / soma dos três; tudo zero = Melee).
        const string weightNote = "Peso relativo: chance = peso / (Melee + Ranged + Tank) do mesmo mapa. Vale para as próximas waves.";
        RegisterPerMap("room.archetype.melee_weight.{m}", "Spawn: peso Melee", G, new double[] { 36.67, 36.67, 36.67, 36.67 }, 0, 100, 1, false, R, weightNote);
        RegisterPerMap("room.archetype.ranged_weight.{m}", "Spawn: peso Ranged", G, new double[] { 26.67, 26.67, 26.67, 26.67 }, 0, 100, 1, false, R, weightNote);
        RegisterPerMap("room.archetype.tank_weight.{m}", "Spawn: peso Tank", G, new double[] { 36.67, 36.67, 36.67, 36.67 }, 0, 100, 1, false, R, weightNote);

        // CampaignCatalog: salas comuns e multiplicador de dificuldade por mapa.
        double[] rooms = { 6, 8, 10, 0 };
        double[] difficulty = { 1.0, 1.35, 1.75, 2.25 };
        const string roomsNote = "Vale melhor entre runs: o mapa em andamento já tem o número de salas definido.";
        RegisterPerMap("campaign.map.{m}.rooms", "Salas comuns", GCampaign, rooms[..3], 1, 40, 1, true, R, roomsNote);
        // Mapa final: padrão 0 (vai direto ao boss), por isso o mínimo continua 0 nele.
        Register(new DevParameter("campaign.map.3.rooms", "Salas comuns (Mapa 4)", GCampaign, rooms[3], 0, 40, 1, true, R, roomsNote));
        RegisterPerMap("campaign.map.{m}.difficulty", "Multiplicador de dificuldade", GCampaign, difficulty, 0.1, 10, 0.05, false, DevApplyMode.NewObjects);
    }
}

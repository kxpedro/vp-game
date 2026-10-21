# Inventário de parâmetros de gameplay (base do painel de dev)

Levantamento feito para o plano `20261012-0131-dev-tools` (T1, só leitura). Números de linha vêm de grep/leitura direta; os marcados "~" precisam ser confirmados ao implementar.

## Resumo

- `CompendiumTuning.Get*`: ~75 chamadas fora de `HUD.Compendium`, em 14 arquivos, ~60 padrões de chave distintos, mais famílias por ID (skill, artefato, bênção) e por golpe de arma. Armazenamento sempre `double`; `GetInt` arredonda e trava em >= 0 (`CompendiumTuning.cs:22`).
- Constantes de gameplay ainda fixas: ~150 (jogador ~30, combate/arma ~25, inimigos ~45, bosses ~35, salas/economia ~20).
- **Já existe um sistema paralelo**: `scripts/systems/GameTuning.cs` (`user://game_tuning.cfg`), com 11 percentuais (walk, run, dash_speed, dash_distance, camera_zoom, damage, health, animation_speed, attack_speed, attack_delay, attack_range) e evento `Changed` que reaplica `PlayerController`, `PlayerAnimator` e `IsoCamera`. O painel F1 deve **incorporá-lo ao registro**, sem duplicar nem quebrar o arquivo atual.
- Chaves mortas com editor no Compêndio: `attack.light_1|2|3|special|dash.*` e `attack.dash.damage_multiplier` (`HUD.Compendium.cs:749-754,795-799`): nenhum código as lê (o `PlayerCombat` lê `weapon.<arma>.*`). Remover ou apontar para as chaves reais.
- `enemy.blood_mage.damage` e `.attack_range` estão efetivamente mortas para golpes de boss: `ApplyBossMoveTuning` (`EnemyBasic.cs:1207-1214`) reescreve `AttackDamage = 30*(1+0.22*mapa)*...` e `AttackRange = 2.25*mult` a cada golpe. Só `move_speed` funciona (`RunManager.cs:433`). Manter o nome legado das chaves (valores salvos no compêndio).
- `GetTunedAttack` (`PlayerCombat.cs:1214`): `range` é multiplicado por `GameTuning.GetAttackRangeMultiplier()` (padrão 65/50 = 1,3); o valor do editor não é o efetivo.

## (a) Chaves já tunáveis

Colunas: chave | padrão | tipo | arquivo:linha | editor no Compêndio.

### economy
| Chave | Padrão | Tipo | Arquivo:linha | Editor |
|---|---|---|---|---|
| economy.coins.boss.{m} | 100,130,160,0 | int | EnemyBasic.cs:1355 | Sim, "Moedas da run (fixo)" (HUD.Compendium.cs:846) |
| economy.cursed_blood.boss.{m} | 40,60,80,120 | int | EnemyBasic.cs:1356 | Sim (:847) |
| economy.cursed_blood.legendary_drop_chance | 1.0 | float | EnemyBasic.cs:1364 | Não |
| economy.cursed_blood.legendary.{m} | 0,6,8,8 | int | EnemyBasic.cs:1366 | Não |
| economy.coins.enemy.base_min / base_max | 3 / 7 | int | EnemyBasic.cs:1372-1373 | Sim (:885-886) |
| economy.coins.archetype.tank / ranged / melee | 1.5 / 1.0 / 1.0 | float | EnemyBasic.cs:1379-1381 | Não |
| economy.coins.rarity.magic / rare / legendary / normal | 2 / 4 / 8 / 1 | float | EnemyBasic.cs:1385-1388 | Não |
| economy.coins.map_factor.{m} | 1.0, 0.6, 0.5, 0.5 | float | EnemyBasic.cs:1390 | Não |
| economy.coins.blessing_fallback | 75 | int | HUD.BlessingOffer.cs:335 | Não |
| economy.coins.room_clear_bonus.{m} | 12,14,16,0 | int | RunManager.cs:189 (~208) | Não |
| economy.coins.room_reward_pack.{m} | 150,130,110 | int | RoomRewardCatalog.cs:73 | Não |
| economy.ledger.max_bonus | 1.0 | float | PlayerCombat.cs:274,596 | Não |
| economy.shop.power_multiplier | 1.75 | float | ArtifactCatalog.cs:43 | Não |
| economy.shop.exclude_owned | 1 | int | ArtifactCatalog.cs:48 | Não |
| economy.shop.price.{common..mythic} | 250,420,700,1100,1700,2400 | int | ArtifactCatalog.cs:118 | Não |
| economy.shop.allow_duplicates_in_stock | 0 | int | ArtifactCatalog.cs:149 | Não |
| economy.shop.stock_min / stock_max | 3 / 4 | int | ArtifactCatalog.cs:164-165 | Não |
| economy.shop.stock_extra_chance | 0.5 | float | ArtifactCatalog.cs:166 | Não |
| economy.fruit.boss_final_map / boss_guaranteed | 0 / 1 | int | RunManager.cs:329-330 | Não |
| economy.fruit.run_cap_from_doors | 3 | int | RoomRewardCatalog.cs:69 | Não |
| economy.fruit.max_health / heal | 15 / 15 | float | RunState.cs:316-317 | Não |

### reward (portas)
| Chave | Padrão | Tipo | Arquivo:linha |
|---|---|---|---|
| reward.blessing.max_sets_without_blessing.{m} | 0,0,0 | int | RoomRewardCatalog.cs:77 |
| reward.doors.two_chance.base.{m} | .30,.35,.40 | float | :95 |
| reward.doors.two_chance.step / step_every / cap | .05 / 2 / .55 | float, int, float | :96-98 |
| reward.door_weight.ramp_from_fraction / ramp_shift | .5 / 2 | float, int | :182-183 |
| reward.door_weight.blessing / fruit / coins.{m} | 72,68,64 / 6,8,10 / 22,24,26 | int | :207,210,213 |
| reward.door.close_seconds | 0.3 | float | ScenePortal.cs:41 |
| reward.door.two_door_offset_cells / two_door_frame_scale | 1.0 / 0.6 | float | ScenePortal.cs:286-287 |

Nenhuma tem editor no Compêndio.

### attack, weapon e inimigos
| Chave | Padrão | Tipo | Arquivo:linha | Editor |
|---|---|---|---|---|
| attack.fury.damage_multiplier | 1.75 | float | PlayerCombat.cs:277,353,604 | Sim (:766) |
| attack.fury.drain_per_second | 10 | float | PlayerCombat.cs:920; HUD.StatusBars.cs:501 (default duplicado) | Sim (:767) |
| attack.fury.max_fury | 100 | float | PlayerCombat.cs:1232 | Sim (:768) |
| weapon.{id}.{basic_1\|basic_2\|basic_3\|quick\|special}.{damage,hit_delay,recovery,knockback,range,width} | do WeaponCatalog | float | PlayerCombat.cs:1218-1223 | Sim (:933-938) |
| weapon.{id}.{golpe}.stagger | 0.20/0.24/0.34/0.28/0.44 | float | PlayerCombat.cs:1226 | Não |
| enemy.basic.health / damage / move_speed / fury_on_death | 110 / 22 / 3.2 / 12 | float | RunManager.cs:402,404,405,410 | Sim (:882-887) |
| enemy.blood_mage.move_speed / damage / attack_range | 2.7 / 30 / 2.25 | float | RunManager.cs:433,436,439 | Sim (:842-844); damage e range sem efeito real |

### por ID
| Chave | Padrão | Arquivo:linha | Editor |
|---|---|---|---|
| {skillId}.cost / .max_rank | skills.json | HUD.BloodFountain.cs:926,931; CharacterSheet.cs:88,159 | Sim (:739-740) |
| {artifactId}.weight | artifacts.json | ArtifactCatalog.cs:220,228,246,376 | Sim (:662) |
| {artifactId}.effect.{i}.value | artifacts.json | RunState.cs:500; ArtifactCatalog.cs:75 | Sim (:666) |
| blessing.{id}.effect.{i}.value | blood_blessings.json | RunState.cs:467; BloodBlessingDefinition.cs:136 | Não (leitura em :703-709) |

## (b) Constantes ainda fixas (chave sugerida)

### Jogador
- `PlayerController.cs:8-18`: WalkSpeed 3.6 (`player.walk_speed`), SprintSpeed 7.4 (`player.sprint_speed`), DodgeSpeed 13.5 (`player.dodge.speed`), DodgeDuration 0.22 (`player.dodge.duration`), DashAttackGraceSeconds 0.5 (`player.dodge.grace`), DodgeCooldownSeconds 0.18 (`player.dodge.cooldown`), DodgeInputBufferSeconds 0.28 (`player.dodge.buffer`), MaxStamina 100 (`player.stamina.max`), regen 28/s (`player.stamina.regen`), DodgeCost 28 (`player.dodge.cost`), DamageStaggerDuration 0.24 (`player.hit_stagger`).
- Vida base 100 em `scenes/player/Player.tscn:40` (`player.max_health`); `_baseMaxHealth` capturado no `_Ready` (`PlayerController.cs:62`).
- `PlayerCombat.cs:11-18`: ComboResetTime 1.0 (`combat.combo.reset_time`), QuickAttackInputBufferSeconds 0.32 (`combat.quick.buffer`), FuryEndBurstDamageRatio 0.15, FuryEndBurstRadius 3.5, AutoAimRadius 4.25 (`combat.aim.radius`).
- Crítico base 1.5 (`PlayerCombat.cs:455,532`: `combat.crit.base_multiplier`); knockback Tank x0.5 e finalizador x1.2 (`:636-637`); explosão da Fúria 2.5 (`:959`); ratos de sangue 0.5x (`BloodBlessingRuntime.cs:358`).
- Ficha (`CharacterSheet.cs:110-133`): Força 0.03 dano/ponto, Vitalidade 6 vida, Destreza 5 stamina e 0.015 velocidade, Resolve 0.03 Fúria, Bloodcraft 0.03 (`sheet.attr.*`).
- Barreira (`BloodBlessingRuntime.cs:9-10`): decay 0.25/s, delay 8s; `recoveryStep` 0.25 (`:199`), tick de poça 0.25 (`:384`), sangramento de poça 1.0 (`:391`).
- `Health.cs:163`: invulnerabilidade fatal mín. 0.5s (`player.fatal_invuln_min`); `Health.cs:14` MaxHealth 100.
- Parâmetros de skills já vêm do JSON via `EffectParameter(...)` (`PlayerSkillController.cs:47-116`); não precisam de chave nova.

### Câmera (`IsoCamera.cs:6-10`; também sob `GameTuning`)
Yaw 45, pitch 60, StartZoomDistance 15, FollowSpeed 18, ShakeFrequency 32 (`camera.*`).

### Inimigos (`EnemyBasic.cs:10-34`)
MoveSpeed 3.2, DetectionRange 22, PatrolSpeed 1.65, PatrolRadius 12, AttackRange 1.65, AttackDamage 22, AttackWindup 0.85, AttackCooldown 1.55, SpawnAttackDelay 2.0, RangedPreferredDistance 6.25, RangedRetreatDistance 4.6, TacticalMoveSpeedMultiplier 0.82, AttackWidth 1.25, AttackDepthBonus 0.75, ContactDamage 0.05, ContactDamageRange 0.95, ContactDamageCooldown 0.8, KnockbackResistance 0.45, FuryOnDeath 12, MaxPestilenceStacks 5, DamageTakenPerStatusStack 0.05. Chaves sugeridas `enemy.base.*` e `enemy.melee|ranged|tank.*`.
- `EnemyBasic.cs:61-63` (const): AimLockSeconds 0.25, WindupFlashSeconds 0.1, ProjectileRangeMultiplier 1.5. Projétil: speed 5.2 (`:1269`; default em `EnemyProjectile.cs:7`).
- Variantes (`ConfigureVariant`, `:181-210`, **não idempotente**: multiplica o valor atual): Tank vida x1.8, vel x0.62, dano x1.52, windup x1.65, alcance x1.18, largura x1.65, cooldown x1.18 (`:199-209`); Ranged alcance 11, dano x0.8, vel x0.9 (`:197`).
- Raridade (`EnemyTuningCatalog.cs:16-18`): vida x1.45/2.35/6.0, dano x1.3/1.75/2.8, velocidade x1.08/1.14/1.22. Faixas de rolagem (`:36-38`): normal 0.90→0.72 e 0.68→0.45, magic 0.10→0.23 e 0.25→0.32, rare 0→0.05 e 0.07→0.18; no mapa 1 estouro cai em Rare, nos outros em Legendary (`:43`).
- Status/stagger: resistência de boss a knockback 0.05 (`:1011`); stagger x0.72 em raridade não-normal (`:1021`); `HasStaggerArmor` (`:141-143`); sangramento mín. 2.0s (`:700`); pestilência mín. 3.0s (`:699`); nuvem de peste raio 6 (`:768`); backstab dot < -0.45 (`:1001`).
- Escala por dificuldade (`RunManager.cs:402-410`): +0.2 velocidade, +2 dano e +18 vida por sala; x0.81 no dano comum; fatores de arquétipo 1.52 / 0.8 / 1.8 (duplicam os de `EnemyBasic`). `MapDefinition.DifficultyMultiplier` 1.0/1.35/1.75/2.25 (`CampaignCatalog.cs:24-28`). Chaves sugeridas `enemy.scale.*`, `campaign.map.{m}.difficulty`.
- `Health.MaxHealth` base do inimigo vem do `.tscn` (110, `EnemyBasic.tscn:44`).

### Bosses (`BossController.cs`, `BossCatalog.cs`)
- Vida = `TargetDurationSeconds` x `ExpectedPlayerDps`: 300x16.67, 280x50, 300x58, 360x70 (mín. 1000, `BossController.cs:49`). Cooldowns, DamageMultiplier, RangeMultiplier e MinimumPhase por golpe (`BossCatalog.cs:29-60`). Chaves `boss.{id}.hp_duration`, `boss.{id}.move.{id}.{cooldown|damage_mult|range_mult}`.
- Consts (`BossController.cs:9-15`): MinSingleCircleWarning 0.50, MinSequenceFirstWarning 0.60, MinLineWarning 0.60, MinClawFirstWarning 0.45, MinClawFollowWarning 0.35, TeleportWarningSeconds 0.40, PhaseBreakSeconds 0.50. `CastTimeMultiplier` x1.3 só para silver_dragonkin (`:40`); primeira janela 1.1 (`:52`); cooldown mín. 0.75 e redução por fase 0.12 (`:97`); fases em 0.66 e 0.33, +12% de velocidade por fase (`:581,584`).
- Golpes (`ExecuteMove`, `:116-129`): tail_sweep raio 3.6/windup 0.62; dragon_volley 3+fase tiros, raio 1.25, cadência 0.46; blood_charge distância 6.4, windup 0.58, largura 1.7 (`:178`); meteor_requiem 6 meteoros, raio 1.35, windup 0.82, dano x0.72; psionic_volley 4+fase, raio 1.15, cadência 0.38; force_outbreak raio 5.0, windup 0.48, 2+fase pulsos; claw_combo 3 golpes, dano 0.72/0.72/1.15 (`:140`); singularity 3+fase pulsos, raio 1.4+0.8n, dano 0.42; kinetic_shift escudo 2+fase (`:196`), teleporte 5.2 (`:200`); mirror_dash teleporte 4.8, carga 7.0/0.38.
- No `RunManager.cs:433-439`: velocidade x1.4 (silver_dragonkin) ou x1.85, dano x1.35 no mapa 1, x0.9, windup 0.75/0.38. Dano base do boss `EnemyBasic.cs:1210`: 30x(1+0.22xmapa)x(1+0.18x(fase-1))x0.9; AttackWidth 1.5xrange_mult (`:1213`). O 30.0/2.25 aparece também em `RunManager.cs:436,439`.

### Sala (`RoomGenerator.cs`)
`[Export]` em `:6-12`: EnemyBlocksPerRoom 2, EnemySpawnWallPaddingCells 2, EnemySpawnDelaySeconds 3, WaveIntervalSeconds 9, EnemiesPerWave 5, WaveCount 4 (`room.waves.count|size|interval`, `room.spawn.delay`). Salas 9x9 comuns (`:159`), 21x21 de boss (`:225`); espaçamento mín. de spawn 2.35 (`:374`), 18 tentativas (`:367`); pesos de arquétipo 26.67% ranged / 36.67% melee / resto tank (`:355-357`, `room.archetype.*`). Salas comuns por mapa 6, 8, 10, 0 (`CampaignCatalog.cs:24-28`, `campaign.map.{m}.rooms`). Geometria (CellSize, WallHeight, FloorHeight) é fora de escopo.

### Run e economia
`ArtifactCatalog.cs:15,19,32`: `_basePowerMultiplier` 1.75, `DefaultShopPrices`, `MaxShopDiscount` 0.9. `BloodBlessingCatalog.cs:9-10`: `StirpsFamilySelectionWeight` 0.45, `DirectAttackBlessingWeightMultiplier` 1.75; raridade 50/28/14/6/2 e multiplicadores 1/2/3/4/6.6667 vêm de `rarityScaling` no JSON (fallback `:368-369`). `RoomRewardCatalog.cs:47-66`: `Default*`, `DoorSwapChance` 0.5, `BlessingPlanSize` 3 (`reward.blessing.choices`; também `HUD.BlessingOffer.cs:331`). Altar: `BlessingSacrificeFraction` 0.05 (`IntermissionChamber.cs:11`, `altar.sacrifice_fraction`). Fonte de Sangue: custo = base x (1 + rank x 0.5) (`HUD.BloodFountain.cs:933`, `fountain.cost_step`).

### Armas (`WeaponCatalog.cs`, `WeaponDefinition.cs`)
Longsword (dano / hit_delay / recovery / knockback / range / width): basic_1 32, 0.12, 0.12, 4.2, 1.55, 1.65; basic_2 38, 0.11, 0.11, 4.8, 1.75, 1.75; basic_3 52, 0.16, 0.12, 7.2, 2.85, 0.85 (linha); quick 68, 0.08, 0.18, 7.4, 2.45, 2.05; special 56, 0.12, 0.30, 11.0, 3.15, 3.15; `SpecialAttackCooldown` 1.0 (`:89`). Campos de `WeaponDefinition` (`:65-81`): BasicAttackSpeedMultiplier 1.0, AttackArcDegrees 118, HitboxEdgeForgiveness 1.15, QuickAttackHitboxScale 0.5, QuickAttackDamageMultiplier 1.35, BackstabDamageMultiplier 1.5, WallImpactDamageMultiplier 0.3, knockback basic/quick/special 3.0/2.7/2.5, LineAttackLungeDistance 0.8. Chaves sugeridas `weapon.{id}.backstab_mult` etc. e, para o `AttackControlProfile`, `weapon.{id}.{golpe}.{buffer|startup_move|recovery_move|aim_radius|aim_cone|aim_strength}`.

## (c) Valores em JSON

`data/blessings/blood_blessings.json` (JSONC), `data/skills/skills.json`, `data/artifacts/artifacts.json`, lidos por `BloodBlessingCatalog.EnsureLoaded`, `SkillCatalog` e `ArtifactCatalog.EnsureLoaded` (try/catch + `PushError`). Já têm override via ID (efeitos, peso, custo, rank). Parâmetros de efeito (`NumberParameters`, `EffectParameter`), pesos e MaxStacks de bênção **não** são tunáveis hoje.

## (d) Reaplicação em runtime

`ApplyCompendiumTuning` (`HUD.Compendium.cs:1037-1043`): `RunState.RecalculateArtifactBonuses()`, `GetTree().CallGroup("player", "ApplyRuntimeTuning")`, `RefreshCharacterSheet()`; debounce `CompendiumApplyDelay` (`:1020`). Não chama `PlayerAnimator`, `IsoCamera` nem o HUD de barras (os eventos de `GameTuning.Changed` cuidam disso para as 11 chaves dele).
- **Reaplicado pelo botão:** `{artifact}.effect.i.value`, `blessing.{id}.effect.i.value`, `economy.shop.power_multiplier`, fruta acumulada (`DevilsFruitMaxHealthTotal` e `MaxHealthBonus` em `PlayerController.ApplyRuntimeTuning`, `:216-231`).
- **Lido a cada uso (sem reaplicar):** `weapon.*` (`GetTunedAttack`), `attack.fury.*` (`MaxFury` em `PlayerCombat.cs:69,117,896`), `economy.coins.*`, `economy.cursed_blood.*`, `economy.ledger.max_bonus`, `economy.fruit.heal|max_health` (na coleta), `economy.shop.*`, skill `cost`/`max_rank`.
- **Lido na criação do objeto (vale só para objetos novos ou próxima sala):** `enemy.basic.*` (uma vez por inimigo, meta `common_tuning_applied`, `RunManager.cs:387-411`), `enemy.blood_mage.*` e `ConfigureBossRoom` (uma vez por sala, `:415`), `reward.door.two_door_*`, `reward.*` de sorteio (`GetOrRollRoomRewardSet`: só salas ainda não sorteadas), estoque da loja (possível cache; não verificado).
- **Capturado no `_Ready` (precisa trocar por leitura da chave dentro de `ApplyRuntimeTuning`):** `_base*` de `PlayerController` (`:51-62`) e `PlayerCombat` (`:55,68`).

## (e) Riscos e cuidados

- Preservar nomes legados `enemy.blood_mage.*`.
- `const` e `static readonly` que precisam virar propriedade: `RoomRewardCatalog.cs:47-66`, `ArtifactCatalog.cs:15,19,32`, `BloodBlessingCatalog.cs:9-10`, `BossController.cs:9-15`, `EnemyBasic.cs:61-63`, `PlayerWeaponVisual.cs:18-19`, `SummonedRat.cs:11`. `[Export]` do `.tscn` sobrescreve o default C# (só se achou override de MaxHealth; confirmar os demais).
- Defaults duplicados: 10.0 de `attack.fury.drain_per_second` (`PlayerCombat.cs:14,920`, `HUD.StatusBars.cs:501`); arrays `{12,14,16,0}` e `{100,130,160,0}` em 2 ou 3 lugares (`RunManager.cs:189`, `EnemyBasic.cs:1355`, `HUD.Compendium.cs:846`). O padrão deve vir de uma tabela única (o registro).
- Chaves por mapa: `m` travado em 0..3; `RoomRewardCatalog` usa só 3 tiers (`Tier(...)`).
- Formatos: `GetInt` arredonda e não aceita negativo; chaves de skill/artefato/bênção usam o ID cru; estado global em memória; `Reset()` limpa tudo.
- Não lidos nesta rodada (podem ter constantes de gameplay): `PlayerSkillController` inteiro, ~830 linhas de `BloodBlessingRuntime`, `ScenePortal`, `DevilsFruit`, `RewardPickup`, `TrainingDummy`, `BloodBlessingProgression`.

## Camadas de valores e "Promover para o jogo"

Cada parâmetro é lido em três camadas, nesta ordem:

1. **Locais** (`user://dev_tuning.json` e `user://game_tuning.cfg`): o que o F1, o console (`tune`) e a pausa gravam. Só valem neste PC; o `dev_tuning.json` só é lido em debug.
2. **Do jogo** (`res://data/tuning/tuning_overrides.json`): versionado no git e lido em qualquer build, inclusive no export. Formato `{"version": 1, "values": {"chave": número}}`, com as chaves `game.*` dos ajustes rápidos incluídas.
3. **Padrão do código** (`Default` em `DevParameters.*.cs`).

O botão **"Promover para o jogo"** do F1 (ou o comando `promote` do console) copia os parâmetros alterados para a camada do jogo, tira do arquivo os que voltaram ao padrão do código e limpa os locais. Só funciona rodando a partir do projeto (`OS.HasFeature("editor")`). Depois, faça commit do arquivo. No F1, "do jogo" marca uma chave promovida, e "Resetar" volta ao valor do jogo, não ao do código. `py tools/validate_dev_tuning.py` confere as chaves e faixas do arquivo.

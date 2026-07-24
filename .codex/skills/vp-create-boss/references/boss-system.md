# Sistema de bosses

## Arquivos centrais

- `scripts/bosses/BossCatalog.cs`: `BossDefinition`, orçamento de vida e lista de golpes.
- `scripts/bosses/BossController.cs`: fases, seleção e execução dos golpes.
- `scripts/enemies/EnemyBasic.cs`: corpo, alvo, movimentação, animações e bloqueio de ação.
- `scripts/enemies/EnemyVisualCatalog.cs`: cena, escala, animação idle e orientação por mapa.
- `scripts/systems/CampaignCatalog.cs`: boss associado a cada mapa.
- `scripts/systems/RoomGenerator.cs`: arena e instanciação do único inimigo Legendary.
- `scripts/systems/RunManager.cs`: configuração final, barra do HUD, morte, artefato e destino do portão.
- `scripts/ui/HUD.cs`: barra e nome do boss.

O repositório pode conter apenas o `.uid` do smoke test de telegraph. Executá-lo somente quando `scripts/tests/CodexBossTelegraphSmoke.cs` existir; build e abertura headless da arena continuam obrigatórios.

## Modelo de definição

`BossDefinition` contém ID, nome, duração-alvo, DPS esperado e golpes. `EstimatedMaxHealth` é o produto arredondado de duração e DPS.

`BossMoveDefinition` contém:

- `Id`: chave usada no dispatcher e nas animações;
- `DisplayName`: texto do log e fonte do dano;
- `Cooldown`: intervalo-base após execução;
- `DamageMultiplier` e `RangeMultiplier`: tuning aplicado pelo corpo;
- `MinimumPhase`: primeira fase em que o golpe entra no pool.

## Ciclo

1. `RoomGenerator` instancia um `EnemyBasic` Legendary na arena.
2. `RunManager.ConfigureBossRoom` escolhe `BossCatalog.Get(CurrentMapIndex)`.
3. O inimigo recebe nome `Boss_<id>`, tuning, visual e `BossController`.
4. `BossController.Configure` calcula vida, conecta sinais e habilita o moveset.
5. A fase muda em 66% e 33% de vida.
6. A seleção evita repetir o último golpe quando há alternativa.
7. A morte cria o Altar de Artefatos; interação e escolha consomem o altar e liberam o portão.

## Executores existentes

- `claw_combo`: três golpes próximos;
- `tail_sweep` / `force_outbreak`: pulsos radiais;
- `dragon_volley` / `psionic_volley`: áreas sucessivas no alvo;
- `blood_charge`: investida em segmento;
- `meteor_requiem`: campo de impactos;
- `kinetic_shift`: shield, pulso e reposicionamento;
- `mirror_dash`: reposicionamento e investida;
- `singularity`: pulsos crescentes.

Adicionar um case em `ExecuteMove` para todo ID novo. Sem case, o fallback vira um pulso radial genérico.

## Telegraphs e segurança

- Construir círculos sobre `max(center.Y, boss.Y) + offset` para respeitar pisos procedurais.
- Dimensionar o tween pelo windup real.
- Verificar `CanContinue()` depois de cada await.
- Liberar telegraphs mesmo quando a ação é cancelada.
- Manter `SetBossActionLocked(true)` durante a execução.
- Usar `Health.ApplyDamage` com o nome do golpe como fonte.
- Passar destinos de `kinetic_shift`, `mirror_dash` e outros teleportes por `RoomGenerator.ClampToWalkableArea(destination, 2)`.
- Manter investidas por `MoveAndCollide` para que a colisão física limite o deslocamento.

## Checklist

- ID e mapa alinhados;
- visual e animações existentes;
- vida estimada coerente com duração-alvo;
- ao menos um golpe por fase;
- telegraph e hitbox equivalentes;
- janela de reação justa;
- sem repetição indevida;
- boss imune a stagger/Encanto e vulnerável a Fascínio quando aplicável;
- barra superior sem barra aérea;
- Altar de Artefatos aparece após a morte e só abre a oferta mediante interação;
- portão leva à Câmara de Hemomancia ou ao final;
- build, arena e smoke test sem erros.

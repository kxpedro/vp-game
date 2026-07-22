# Especificação da geração de salas

Este documento descreve a estrutura atual da campanha e da geração procedural de Vampire's Prey.

## Estrutura da campanha

| Etapa | Salas comuns antes do boss | Transição |
| --- | ---: | --- |
| Calabouços de Armand | 6 | Câmara de Hemomancia |
| Jardim Sangrento | 8 | Câmara de Hemomancia |
| Ala Superior do Castelo | 10 | Câmara de Hemomancia |
| Trono de Armand | 0 | Final da run |

`CampaignCatalog` controla quantidade de salas, boss, regra especial e multiplicador de dificuldade. A vida atual é preservada entre salas, bosses e mapas.

Antes da primeira sala comum da run, `RunStartBlessingRoom.tscn` apresenta uma fonte de sangue central. O portão permanece fechado até o jogador interagir, abrir a oferta aleatória e escolher uma bênção inicial. Essa sala não se repete nos outros mapas.

Após cada boss não final, `IntermissionChamber.tscn` permite:

- usar uma vez a fonte para recuperar 35% da vida máxima;
- usar uma vez o Altar do Sacrifício para gastar 20% da vida máxima e elevar uma bênção equipada em uma raridade;
- comparar os efeitos atuais e futuros antes de confirmar o aprimoramento.

## Modelo de geração

`RoomGenerator` mantém o piso navegável em `HashSet<Vector2I> _walkableCells`. Cada célula gera um tile de piso e paredes apenas nas bordas sem vizinho navegável.

As cinco famílias de layout são:

- `BranchingKeep`;
- `LairWithVault`;
- `CrossDungeon`;
- `SplitHalls`;
- `WindingCrypt`.

Antes da construção, cada layout recebe rotação em quartos de volta e pode ser espelhado. Depois, `RepairDisconnectedMapPieces()` garante a conectividade.

## Escala e composição

- `CellSize = 1.64`;
- `MainRoomScale = 2`;
- `RegionSpacing = 2`;
- salas principais usam `AddMainRoom`;
- entradas, saídas, tesouros e becos usam `AddRoom`;
- corredores são criados por `ConnectCells`;
- existe 35% de chance de somente uma sala lateral procedural adicional;
- becos sem saída continuam como pontos intencionais;
- o nome da ala não é desenhado sobre o mapa;
- a antiga saída circular vermelha foi substituída por um portão legível.

## Spawns e waves

- três waves por sala comum;
- 5 inimigos por wave, totalizando 15 planejados;
- primeira wave iniciada ao carregar a sala;
- intervalo máximo entre inícios de wave: 15 segundos;
- uma nova wave também pode começar antes caso todos os inimigos atuais sejam derrotados;
- telegraph de spawn: 5 segundos;
- inimigos são instanciados um por frame para reduzir hitch;
- waves podem coexistir quando o intervalo se esgota;
- a sala só termina quando não restam inimigos nem spawns pendentes;
- células próximas de paredes, entrada, saída e pontos reservados são evitadas;
- `EnemySpawnWallPaddingCells = 2` mantém o spawn centralizado;
- posições planejadas procuram manter pelo menos 2,35 unidades de distância entre inimigos;
- ranged possui aproximadamente 26,67% de chance no pool, 20% abaixo da antiga participação de um terço.

A raridade é sorteada usando mapa e progresso dentro dele. O primeiro mapa não gera inimigos Legendary. Arquétipo e raridade são definidos antes da aplicação do visual.

## Arquétipos e leitura visual

| Arquétipo | Geometria provisória | Cor | Comportamento distinto |
| --- | --- | --- | --- |
| Melee | Cilindro | Ciano | Persegue e pressiona a curta distância |
| Ranged | Prisma triangular | Rosa-choque | Mantém distância e pode recuar |
| Tank | Cubo | Amarelo | Mais vida, windup maior e ataque ampliado |

Todos usam emissão e um halo na cor do arquétipo. Ranged dispara flechas emissivas mais lentas e de curso longo. O indicador `!` de preparação permanece, mas a antiga caixa que revelava a hitbox do projétil não é exibida.

## Salas de boss

Salas de boss usam arena própria com pilares, entrada e portão final. Apenas um inimigo Legendary é planejado e depois configurado por `RunManager`, `BossCatalog` e `BossController`.

O boss usa a barra de vida superior do HUD e não exibe barra aérea. Ao morrer, libera a escolha de artefato; a transição só prossegue depois da recompensa.

## Pontos de interesse

O gerador mantém posições para:

- spawn do jogador;
- portão de saída;
- tesouro/artefato;
- beco sem saída;
- arena do boss.

O portão permanece fechado durante o combate e, em salas comuns, continua bloqueado até o jogador escolher a Bênção de Sangue.

## Métodos centrais

- `BuildMap()`;
- `BuildBossMap()`;
- `AddRoom()`;
- `AddMainRoom()`;
- `ConnectCells()`;
- `AddProceduralSideRooms()`;
- `RepairDisconnectedMapPieces()`;
- `GenerateMapGeometry()`;
- `BuildEnemySpawnCells()`;
- `GenerateEnemies()`;
- `SpawnNextWave()`;
- `GetArtifactSpawnPosition()`.

## Formato para novos templates

```text
Nome do template:
Entrada:
Saída:
Salas principais:
Salas laterais:
Tesouro:
Becos sem saída:
Conexões:
Regras especiais:
```

Ao adicionar um template, verificar visualmente rotação e espelhamento, conectividade, distância do spawn às paredes e aos demais inimigos, acesso ao portão e ausência de corredores órfãos.

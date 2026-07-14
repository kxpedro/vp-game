# Especificação da geração de salas

Este documento descreve a estrutura atual de geração procedural de Vampire's Prey.

## Estrutura da campanha

- Mapa 1: 3 salas comuns e boss.
- Mapa 2: 4 salas comuns e boss.
- Mapa 3: 5 salas comuns e boss.
- Mapa 4: somente o boss final.
- `CampaignCatalog` controla quantidade de salas, boss, regra especial e multiplicador de dificuldade.
- A vitória contra um boss avança diretamente para o mapa seguinte preservando o HP atual.

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
- becos sem saída continuam existindo como pontos intencionais, sem cadeias extras sem propósito.

## Spawns e waves

- três waves por sala comum;
- oito inimigos por wave, 24 planejados no total;
- primeira wave iniciada ao carregar a sala;
- intervalo total entre inícios de wave: 15 segundos;
- telegraph de spawn: 5 segundos;
- inimigos são instanciados um por frame para reduzir hitch;
- waves não esperam a eliminação completa da wave anterior;
- a sala só termina quando não restam inimigos nem spawns pendentes;
- células próximas de paredes, entrada, saída e pontos reservados são evitadas;
- `EnemySpawnWallPaddingCells = 2` mantém o spawn mais centralizado.

A raridade é sorteada usando mapa e progresso dentro dele. O primeiro mapa não gera inimigos Legendary. Arquétipo e raridade são definidos antes da aplicação do visual.

## Salas de boss

Salas de boss usam uma arena própria com pilares, entrada e portão final. Apenas um inimigo Legendary é planejado e depois configurado por `RunManager`, `BossCatalog` e `BossController`.

O boss utiliza barra de vida no topo do HUD e não exibe barra aérea.

## Pontos de interesse

O gerador mantém posições para:

- spawn do jogador;
- saída/portal;
- tesouro/artefato;
- beco sem saída;
- arena do boss.

O portal de saída permanece fechado durante o combate e, em salas comuns, continua bloqueado até o jogador escolher a Bênção de Sangue.

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

Ao adicionar um template, verificar visualmente rotação e espelhamento, conectividade, distância do spawn às paredes, acesso ao portal e ausência de corredores órfãos.

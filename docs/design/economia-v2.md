# Economia v2: Moedas de Sangue (run), Sangue Amaldiçoado (permanente), Fruta do Diabo e Loja

Documento de design da tarefa T1 do plano `20261011-0550-economia-recompensas`. Serve de fonte única de números para as tarefas T3 (Fruta), T4 (estoque da loja), T5 (drops), T6 (loja) e T8 (custos da árvore). Escrito em 2026-10-11.

**Status de todos os números abaixo: proposta a validar jogando.** Eles saem de contas sobre o código e os dados atuais, não de playtest. Os pontos que mais dependem de teste estão marcados com **[VALIDAR]** e reunidos na seção 10.

As decisões D1 a D6 do usuário (Moedas só na run, Sangue Amaldiçoado permanente, Fruta de +15, boss entrega Sangue + Fruta, artefatos vendidos sem limite, saves v1 excluídos) são premissas fixas e não são reabertas aqui.

> **Rebalanceamento de 2026-10-25 (substitui os números de Moedas e preços das seções 2, 6 e 9).** Pedido do Pedro: chegava à primeira Câmara com ~5.000 moedas e comprava a loja inteira; o alvo agora é **no máximo 2 artefatos por visita**. A causa principal era um valor promovido no F1 (`economy.coins.boss.0` = 4.390 em `data/tuning/tuning_overrides.json`), removido; além disso a escala inteira caiu ~4×:
>
> | Parâmetro | Antes | Agora |
> |---|---|---|
> | `economy.coins.enemy.base_min` / `base_max` | 3 / 7 | 1 / 1 (inimigo Normal = 1 moeda; os multiplicadores de arquétipo e raridade continuam) |
> | `economy.coins.map_factor.{0..3}` | 1,0 / 0,6 / 0,5 / 0,5 | 1,0 / 0,6 / 0,6 / 0,6 |
> | `economy.coins.boss.{0..3}` | 100 / 130 / 160 / 0 | 20 / 30 / 40 / 0 |
> | `economy.coins.room_reward_pack.{0..2}` (porta de Moedas) | 150 / 130 / 110 | 40 / 45 / 50 |
> | `economy.coins.blessing_fallback` | 75 | 25 |
> | `economy.shop.price.*` (Common … Mythic) | 250 / 420 / 700 / 1.100 / 1.700 / 2.400 | 110 / 150 / 200 / 270 / 360 / 480 |
> | `ledger_of_unpaid_blood` (`DamagePerCurrencyHeld`) | 0,001 por moeda | 0,003 por moeda (mantém o teto de +100% alcançável na nova escala) |
>
> Renda simulada (3.000 runs, limpando o mapa inteiro, porta de Moedas em ~18% das salas): mapa 1 ≈ **250** (p10 210, p90 300), mapa 2 ≈ 320, mapa 3 ≈ 410. Preço médio de um slot: visita 1 ≈ 149, visita 2 ≈ 172, visita 3 ≈ 192, ou seja **≈ 1,7 a 2 compras por visita**. Os três artefatos mais baratos da visita 1 custam 370 (só há 2 Common), acima do p90 da renda: comprar 3 só guardando de uma visita para a outra. Tudo continua ajustável no F1 (grupo Economia). **[VALIDAR]** jogando.

---

## 1. Levantamento do estado atual (base das contas)

| Item | Valor hoje | Onde |
|---|---|---|
| Campanha | 4 mapas; salas comuns 6, 8, 10 e 0; todos terminam em boss | `CampaignCatalog.cs` |
| Sala comum | 4 waves × 5 inimigos = 20 inimigos | `RoomGenerator.cs:10-12` |
| Inimigos por mapa | 120, 160, 200 e 0 (480 por run completa) + 4 bosses | derivado |
| Arquétipos | Ranged 26,67% / Melee 36,67% / Tank 36,67% | `RoomGenerator.RollEnemyArchetype` |
| Raridade (mapa 0) | Normal 90→72%, Mágico 10→23%, Raro 0→5%, sem Lendário | `EnemyTuningCatalog.Roll` (progresso linear da sala 1 à última) |
| Raridade (mapas 1 e 2) | Normal 68→45%, Mágico 25→32%, Raro 7→18%, Lendário 0→5% | idem |
| Moeda por inimigo hoje | sorteio inteiro entre `Min` e `Max` (padrão 1 a 10) × raridade (Normal 1, Mágico 2, Raro 4, Lendário 8); sem variação por arquétipo nem por mapa | `EnemyBasic.RollBloodCoinsReward` |
| Moeda do boss hoje | 8 a 10 × 8 (o boss nasce Lendário) = 64 a 80 | `RunManager.ConfigureBossRoom` |
| Árvore hoje | 16 skills (8 Bestialis + 8 Humanitas), custo base 300 a 2500, custo do rank = base × (1 + 0,5 × rank atual) | `skills.json`, `HUD.BloodFountain.GetSkillUpgradeCost` |
| Custo total da árvore hoje | 45.225 Moedas (48 compras) | soma de base × (1+1,5+2+2,5+3 = 10 para maxRank 5; 1+1,5+2 = 4,5 para maxRank 3; 1 para maxRank 1) |
| Vida base do jogador | 100 | `Player.tscn`, `Health.cs` |
| Artefatos | 20: Common 2, Uncommon 4, Rare 4, Epic 6, Legendary 3, Mythic 1 | `artifacts.json` |
| Poder do artefato | efeito × `BossDropPowerMultiplier` = 1,75 | `ArtifactCatalog.cs:12` |

### Divergências encontradas

1. **A árvore tem 16 skills, não 18** (`skills.json`: 8 `bestialis.*` e 8 `humanitas.*`). Todas as contas usam 16. Se existirem 2 skills planejadas fora do JSON, elas entram depois com a mesma régua da seção 4.
2. **A faixa real de moedas por inimigo é 1 a 11, não 1 a 10.** `RollBloodCoinsReward` chama `GD.RandRange(min, max + 1)`, e a versão inteira dessa função é inclusiva nos dois lados. A média real do Normal é 6, não 5,5. A v2 define faixa exata: o implementador deve sortear `[min, max]` sem o `+1`.
3. **Mythic sem multiplicador no `ArtifactCatalog.GetRarityMultiplier`** (cai no `_ => 1.0f`), enquanto Legendary usa 0,08. Isso só afeta o sorteio antigo de boss. O estoque da loja usa a tabela da seção 6 e não esse método.
4. **`GetRarityMultiplier` penaliza duas vezes** (peso por artefato × multiplicador da raridade). A loja separa os dois níveis: a raridade é sorteada por classe (seção 6.2) e o campo `weight` do JSON só desempata dentro da classe.
5. O `GDD.txt` não define economia; a seção "Câmara de Hemomancia" de `docs/gameplay_systems.md` (cura 35% e altar) precisa ganhar a Loja quando a T6 terminar.

---

## 2. Moedas de Sangue (recurso da run)

### 2.1 Fantasia e papel

Moeda de curto prazo: o jogador a ganha matando, decide na Câmara de Hemomancia entre mapas o que comprar e perde o que sobrar ao fim da run. O saldo persiste de um mapa ao outro dentro da run (a Câmara fica entre mapas, D1). Não há outro gasto na v2 além da Loja.

### 2.2 Fórmula do ganho por inimigo

```
moedas = max(1, arredondar( sorteio[base_min, base_max]
                            × mult_arquétipo
                            × mult_raridade
                            × fator_do_mapa
                            × BloodCoinMultiplier ))
```

- `sorteio[3, 7]` inteiro e inclusivo, média 5. Parte da ordem de grandeza atual (média 5,5 a 6), então o "+N" na tela continua parecido com o do jogo de hoje.
- `mult_raridade` mantém os valores que já existem: Normal 1, Mágico 2, Raro 4, Lendário 8 (a vida sobe 1; 1,45; 2,35; 6, ou seja, a moeda cresce mais que a vida; é a recompensa pelo risco e fica como está).
- `mult_arquétipo` é novo: Melee 1,0; Ranged 1,0; Tank 1,5. Justificativa: o Tank tem 1,8× a vida e 1,52× o dano do Melee, levando mais tempo e risco para morrer. O Ranged mantém 1,0: tem a mesma vida do Melee e dano 0,8× (`RunManager.ApplyCommonEnemyTuning`), então não há razão para pagar mais. Valor esperado do multiplicador de arquétipo: 0,2667×1 + 0,3667×1 + 0,3667×1,5 = **1,1833**.
- `fator_do_mapa` é novo e **decrescente** (1,0 / 0,6 / 0,5). Os mapas 2 e 3 têm mais salas (8 e 10) e mais raros/lendários (multiplicador médio de raridade 1,835 contra 1,24 no mapa 1). Sem compensação a renda de drops por mapa explodiria (cerca de 880, 1.740 e 2.170 com o sorteio 3 a 7 e fator 1 nos três mapas). Com o fator, a renda por mapa fica quase plana e a progressão de poder de compra vem da raridade do estoque (seção 6). **[VALIDAR]** se jogadores estranham "kills" valendo menos nos mapas finais; a alternativa é manter o fator 1 e subir os preços, o que dá o mesmo resultado econômico.
- `BloodCoinMultiplier` (skill `bestialis.blood_hunt`, +30% durante a Fúria) continua multiplicando o lote de moedas. Na conta abaixo o efeito é ignorado (a Fúria cobre uma parte do tempo; efeito estimado de +5 a +8% de renda para quem tem a skill).
- O arredondamento deve ser feito só no final da conta (nunca multiplicando um inteiro já arredondado). Há um viés conhecido: com fator 0,5, os valores 1,5 a 3,5 arredondam para cima em metade dos casos, elevando a média do Normal do mapa 3 em até 10%. **[VALIDAR]** medir a renda real por mapa.

Raridade média esperada por mapa (média linear sobre as salas, pesos da tabela da seção 1): mapa 1 = 0,81×1 + 0,165×2 + 0,025×4 = **1,24**; mapas 2 e 3 = 0,565×1 + 0,285×2 + 0,125×4 + 0,025×8 = **1,835**.

### 2.3 Valor por inimigo (tabela de consulta)

Média do sorteio = 5. Valores médios antes do arredondamento e do multiplicador de Fúria.

| Mapa (fator) | Raridade | Melee / Ranged | Tank |
|---|---|---|---|
| 1 (×1,0) | Normal | 5,0 | 7,5 |
| 1 | Mágico | 10 | 15 |
| 1 | Raro | 20 | 30 |
| 2 (×0,6) | Normal | 3,0 | 4,5 |
| 2 | Mágico | 6 | 9 |
| 2 | Raro | 12 | 18 |
| 2 | Lendário | 24 | 36 |
| 3 (×0,5) | Normal | 2,5 | 3,75 |
| 3 | Mágico | 5 | 7,5 |
| 3 | Raro | 10 | 15 |
| 3 | Lendário | 20 | 30 |

(Mapa 1 = `armands_dungeons`, 2 = `blood_garden`, 3 = `upper_castle`; o mapa 4, `armands_throne`, só tem o boss.)

Média por inimigo (todas as raridades e arquétipos): mapa 1 = 5 × 1,1833 × 1,24 × 1,0 = **7,34**; mapa 2 = 5 × 1,1833 × 1,835 × 0,6 = **6,51**; mapa 3 = 5 × 1,1833 × 1,835 × 0,5 = **5,43**.

### 2.4 Bônus por sala limpa e moeda do boss

- **Bônus de sala limpa** (soma automática ao fechar a sala comum, com o "+N" da HUD): **12 / 14 / 16** nos mapas 1, 2 e 3. Representa 8%, 11% e 15% da renda de drops do mapa. Serve para dar um piso visível por sala e um sinal de "sala completa". Não aplica no boss.
- **Moeda do boss** (valor fixo, ignora `mult_raridade` para não ficar escondida atrás do ×8): **100 / 130 / 160 / 0** nos mapas 1 a 4. No mapa 4 a run termina e o saldo some, então não há moeda. Referência: hoje o boss paga 64 a 80; a nova faixa fecha o mapa com um pagamento visível de cerca de 10% da renda do mapa, sem competir com o Sangue Amaldiçoado, que é a recompensa principal do boss.
- **Pacote de moedas como recompensa de sala** (a ser usado pelo plano `portas-recompensas`): **150 / 130 / 110** nos mapas 1 a 3, ou seja, quase exatamente a renda média de drops de uma sala do mapa (146,7 / 130,3 / 108,6). Escolher a porta de moedas dobra o ganho daquela sala. Com a hipótese de 20% das salas oferecendo e o jogador escolhendo a porta de moedas, o ganho extra é de cerca de +600 por run completa (ver 2.5).
- **Fallback de "todas as bênçãos encontradas"** (`HUD.BlessingOffer.cs:308`, hoje 25 moedas): passa para **75** (metade de uma sala do mapa 1), porque 25 vira ruído na nova escala.

### 2.5 Renda por mapa e por run (jogador que limpa o mapa inteiro)

| Mapa | Salas | Inimigos | Drops | Bônus de sala | Boss | **Total do mapa** | Acumulado bruto |
|---|---|---|---|---|---|---|---|
| 1 `armands_dungeons` | 6 | 120 | 120 × 7,34 = 880 | 6 × 12 = 72 | 100 | **1.052** | 1.052 |
| 2 `blood_garden` | 8 | 160 | 160 × 6,51 = 1.042 | 8 × 14 = 112 | 130 | **1.284** | 2.336 |
| 3 `upper_castle` | 10 | 200 | 200 × 5,43 = 1.086 | 10 × 16 = 160 | 160 | **1.406** | 3.742 |
| 4 `armands_throne` | 0 | 0 | 0 | 0 | 0 | 0 (sem visita à Loja) | 3.742 |

Visitas à Loja: depois dos bosses 1, 2 e 3 (a Câmara não existe depois do boss final). Renda bruta de uma run completa: cerca de **3.740 moedas** (o saldo que sobrar ao fim da run some). Com portas de moedas (hipótese: 20% das salas oferecem e o jogador escolhe a de moedas): +180 / +208 / +220 → 1.232 / 1.492 / 1.626 por mapa. A conta principal da Loja usa os valores **sem** portas, o caso conservador.

Variância (estimativa, não medida): o desvio da renda do mapa vem quase só dos sorteios de raridade e deve ficar abaixo de ±8%. Um jogador que chega à Câmara deve ter pelo menos cerca de 950 moedas na primeira visita.

---

## 3. Sangue Amaldiçoado (recurso permanente)

### 3.1 Fantasia e regras

Sangue Amaldiçoado é o resíduo da maldição de Armand: só o que é grande ou raro o deixa cair. Vai direto ao save quando coletado, sobrevive à morte e é o único recurso da árvore no hub. Escasso por desenho: **a maior parte da renda vem dos bosses** (cerca de 80%), e um Lendário comum paga 1/10 do que paga o boss do mesmo mapa.

### 3.2 Valores

| Fonte | Mapa 1 | Mapa 2 | Mapa 3 | Mapa 4 |
|---|---|---|---|---|
| Boss (garantido) | **40** | **60** | **80** | **120** |
| Inimigo Lendário (garantido, 100% ao morrer) | não ocorre | **6** | **8** | 8 (reserva, o mapa não tem salas comuns) |

- **Progressão do boss:** +20 por mapa nos três primeiros e um salto para 120 no Armand, que é a vitória da campanha (a única ocasião em que o boss encerra a run). Proporcional à dificuldade: vida estimada 5.000 / 14.000 / 17.400 / 25.200 (`TargetDurationSeconds × ExpectedPlayerDps`) e multiplicador de mapa 1,0 / 1,35 / 1,75 / 2,25.
- **Lendário:** 6 e 8 são exatamente 1/10 do boss do mesmo mapa (60 e 80) e escalam com a mesma tendência. O Lendário tem 6× a vida, 2,8× o dano e 5 modificadores; o valor precisa ser claramente maior que 1 para valer o risco, mas não pode substituir o boss.
- O boss em si nasce com raridade Lendária, mas **não** paga o valor de Lendário: só o do boss (implementador da T5, cuidado com o duplo pagamento).
- Valores inteiros e fixos, sem sorteio, para o jogador poder planejar. A variância da renda vem apenas do número de Lendários que o gerador sorteia.

### 3.3 Quantos Lendários aparecem

Chance de Lendário por inimigo = 5% × progresso da sala, com progresso = (sala − 1)/(salas − 1). Média por mapa: 2,5% × inimigos.

| Mapa | Inimigos | Lendários esperados | Observação |
|---|---|---|---|
| 1 | 120 | 0 | `firstMap` limita em Raro |
| 2 | 160 | 4,0 | |
| 3 | 200 | 5,0 | |
| Total | | **9,0** | |

Eles são concentrados no fim do mapa: só 21% dos Lendários de um mapa de 8 salas aparecem nas primeiras 4 salas (fração acumulada = k(k−1)/(n(n−1)) para as k primeiras salas de n).

### 3.4 Renda por run (Sangue Amaldiçoado)

| Marco alcançado | Boss | Lendários (esperado) | Acumulado |
|---|---|---|---|
| Morreu antes do boss 1 | 0 | 0 | **0** |
| Derrotou o boss 1 | 40 | 0 | **40** |
| Derrotou o boss 2 | 60 | 4 × 6 = 24 | **124** |
| Derrotou o boss 3 | 80 | 5 × 8 = 40 | **244** |
| Derrotou o boss 4 (campanha completa) | 120 | 0 | **364** |

Perfis de jogador, com a probabilidade de derrotar cada boss como **hipótese** (não medida) **[VALIDAR]**:

| Perfil | P(boss 1 / 2 / 3 / 4) | Bosses | Lendários | **Sangue por run** |
|---|---|---|---|---|
| Iniciante | 35% / 10% / 2% / 0% | 21,6 | 6,4 | **≈ 28** |
| Médio | 75% / 50% / 30% / 10% | 96 | 28,9 | **≈ 125** |
| Veterano | 95% / 85% / 70% / 45% | 199 | 51,3 | **≈ 250** |
| Vitória completa | 100% | 300 | 64 | **364** |

(Lendários dos perfis: contabilizo 100% dos Lendários dos mapas cujo boss foi vencido e 35% dos mapas alcançados sem vencer o boss, pelo viés de fim de mapa da seção 3.3.)

---

## 4. Árvore de habilidades em Sangue Amaldiçoado

### 4.1 Regra de custo

Mantém-se a fórmula já implementada: `custo do rank N = base × (1 + 0,5 × (N−1))` (o código usa o rank atual antes da compra). Multiplicadores por rank: 1; 1,5; 2; 2,5; 3. Isso permite que a T8 mexa **só** em `skills.json` (campo `progression.cost` = base) e a T7 só troque a moeda no código. Todas as bases abaixo são números pares para os rounds do código darem inteiros exatos.

A nova escala é a escala antiga dividida por cerca de 14, arredondada para pares (razão efetiva entre 1/15 e 1/13,4). Preserva a hierarquia entre skills que o autor já tinha escolhido.

### 4.2 Custos exatos por skill e por rank

| ID | Árvore | maxRank | Base (`cost`) | Rank 1 | Rank 2 | Rank 3 | Rank 4 | Rank 5 | **Total** | Antes (base) |
|---|---|---|---|---|---|---|---|---|---|---|
| `bestialis.vampire_fangs` | Bestialis | 5 | 20 | 20 | 30 | 40 | 50 | 60 | **200** | 300 |
| `bestialis.opening_strike` | Bestialis | 5 | 24 | 24 | 36 | 48 | 60 | 72 | **240** | 350 |
| `bestialis.shadow_attack` | Bestialis | 3 | 50 | 50 | 75 | 100 | | | **225** | 700 |
| `bestialis.marked_prey` | Bestialis | 3 | 56 | 56 | 84 | 112 | | | **252** | 750 |
| `bestialis.predatory_reflex` | Bestialis | 3 | 46 | 46 | 69 | 92 | | | **207** | 650 |
| `bestialis.terminal_frenzy` | Bestialis | 1 | 130 | 130 | | | | | **130** | 1800 |
| `bestialis.bestial_transformation` | Bestialis | 1 | 130 | 130 | | | | | **130** | 1800 |
| `bestialis.blood_hunt` | Bestialis | 1 | 160 | 160 | | | | | **160** | 2200 |
| `humanitas.survival_instinct` | Humanitas | 5 | 20 | 20 | 30 | 40 | 50 | 60 | **200** | 300 |
| `humanitas.thick_skin` | Humanitas | 5 | 24 | 24 | 36 | 48 | 60 | 72 | **240** | 350 |
| `humanitas.methodical_recovery` | Humanitas | 5 | 20 | 20 | 30 | 40 | 50 | 60 | **200** | 300 |
| `humanitas.pocket_watch` | Humanitas | 3 | 50 | 50 | 75 | 100 | | | **225** | 700 |
| `humanitas.iron_will` | Humanitas | 3 | 56 | 56 | 84 | 112 | | | **252** | 750 |
| `humanitas.collector_eye` | Humanitas | 3 | 50 | 50 | 75 | 100 | | | **225** | 700 |
| `humanitas.rewritten_fate` | Humanitas | 1 | 130 | 130 | | | | | **130** | 1800 |
| `humanitas.last_stand` | Humanitas | 1 | 180 | 180 | | | | | **180** | 2500 |

Totais: Bestialis **1.544**, Humanitas **1.652**, árvore inteira **3.196 Sangue Amaldiçoado** (48 compras; hoje são 45.225 Moedas, razão 1/14,15).

Notas de coerência:
- As relações originais foram mantidas: `shadow_attack`/`pocket_watch`/`collector_eye` iguais (700), `marked_prey`/`iron_will` iguais (750), `predatory_reflex` abaixo (650), capstones de rank 1 entre 1.800 e 2.500.
- Cada compra isolada vai de 20 a 180. Uma vitória no boss 1 (40) compra o rank 1 de duas skills de entrada, ou de uma de entrada mais um rank 2: o jogador sente progresso já na primeira vitória. O capstone mais caro (180) equivale a cerca de 1,4 runs de jogador médio.

### 4.3 Quantas runs para comprar a árvore inteira

`runs = 3.196 / Sangue por run` (sem considerar que a própria árvore deixa o jogador mais forte):

| Perfil | Sangue por run | Runs para completar |
|---|---|---|
| Vitória completa (sempre) | 364 | **8,8 (≈ 9)** |
| Veterano | ≈ 250 | **≈ 13** |
| **Médio (referência de design)** | ≈ 125 | **≈ 26** |
| Iniciante | ≈ 28 | ≈ 114 (inviável: o iniciante melhora antes disso) |

**Alvo de design: cerca de 25 runs de um jogador médio (e de 9 a 13 para quem vence com frequência).** Como o jogador médio fica mais forte à medida que compra, a expectativa realista é de **18 a 22 runs**. A alavanca é uma só (escala do custo ou valor do boss); ver 9.7 e a seção 10.

Marcos de progressão para o jogador médio (125 por run):

| Marco | Sangue acumulado | Runs |
|---|---|---|
| Primeira compra (rank 1 de uma skill de entrada) | 20 | 1 vitória no boss 1 (≈ 1,3 run) |
| Rank 5 das duas skills de entrada (`vampire_fangs` + `survival_instinct`) | 400 | ≈ 3 |
| Metade da árvore | ≈ 1.600 | ≈ 13 |
| Todos os capstones (rank 1) | 730 + pré-requisitos | depende da ordem, após ≈ 15 |
| Árvore completa | 3.196 | ≈ 26 |

### 4.4 Riscos específicos da árvore

- **Run sem boss paga zero.** Um jogador que morre antes do boss 1 não progride na árvore. A decisão D2 manda ser escasso, então é o preço do desenho; o primeiro boss (40) é gentil o bastante para compensar. Opção para o usuário na seção 8 (bônus de primeira vitória).
- **Efeito das skills sobre a economia nova:** `humanitas.collector_eye` (`ArtifactDropChance` +5% por rank) e a parte de artefatos de `humanitas.rewritten_fate` (`artifacts: true`) perdem o sentido, porque artefatos não caem mais. Isto é decisão de conteúdo, não de custo; ver seção 10, item D-A.
- **Quando a árvore for completada,** o Sangue Amaldiçoado fica sem destino até o plano dos personagens desbloqueáveis. Registrado como risco; não precisa de ação agora.

---

## 5. Fruta do Diabo

### 5.1 Regra

Item de run: **+15 de vida máxima e cura de 15 imediatamente** (D4), acumulativo, zerado ao fim da run. O ganho entra em `RunState.MaxHealthBonus` (somado antes dos multiplicadores `MaxHealthMultiplier` e `SacrificedMaxHealthMultiplier`, conforme `PlayerController.cs:224-227`). Portanto os multiplicadores de bênção valorizam a fruta e o Altar de Hemomancia (−5% de vida máxima) a reduz proporcionalmente, sem tratamento especial.

### 5.2 Quantas por run

| Fonte | Quantidade | Observação |
|---|---|---|
| Boss 1, 2 e 3 (garantida, D5) | **3** | um por boss, solta no chão com o Sangue Amaldiçoado |
| Boss 4 (Armand) | **0** | a run termina; não soltar a fruta (a T5 decide como evitar o nó inútil) |
| Recompensa de sala (plano `portas-recompensas`) | alvo esperado **≈ 2 por run completa** | sugestão: a Fruta aparece como opção em ~12% das salas comuns (≈ 2,9 ofertas em 24 salas) e o jogador a escolhe em ~70% das vezes; teto sugerido de 4 por run |

Total de uma run completa: **≈ 5 frutas = +75 de vida máxima** (de 100 para 175, ou +75%) mais o que a árvore e as bênçãos derem. Jogador médio (≈ 14 salas, 1,5 boss): **≈ 2,7 frutas = +40** de vida.

Justificativa dos 15: é o valor escolhido pelo usuário. A escala é coerente com a árvore: `humanitas.survival_instinct` dá +20 por rank (até +100 permanentes), então 5 frutas (+75) equivalem a quase uma árvore de vida inteira, mas só dentro da run. Bênçãos como Sangue Espesso também mexem na vida máxima, então a fruta não é a única fonte. A dificuldade dos inimigos vem do multiplicador do mapa e do número da sala (`ApplyCommonEnemyTuning`) e não depende da vida do jogador, portanto mais vida do jogador é ganho líquido de segurança.

Risco: três frutas garantidas até o boss 4 deixam os bosses 2 a 4 mais confortáveis do que a tuning atual previu. **[VALIDAR]** nos bosses 3 e 4: se a vida máxima total (com árvore e bênçãos) passar de cerca de 250, baixar a oferta de sala.

---

## 6. Loja da Câmara de Hemomancia (artefatos)

### 6.1 Estrutura

- A loja vende artefatos para a run, **sem limite de quantidade total**, pagos em Moedas de Sangue (D6).
- Estoque sorteado a cada visita: **3 a 4 artefatos**, tamanho = 3 + (50% de chance de +1). Média 3,5.
- Sem repetição dentro do mesmo estoque.
- **Primeiro slot garantido barato:** o primeiro artefato do estoque é sorteado apenas entre Common e Uncommon, para o jogador nunca encontrar uma loja que não pode pagar.
- **Recomendação (decisão pendente D-B):** não oferecer um artefato que o jogador já tenha comprado na run. Com 20 artefatos e cerca de 7 compras por run completa, o pool não esvazia. Se o usuário quiser empilhar duplicatas, basta desligar o parâmetro `economy.shop.exclude_owned`; o preço fica igual.
- A visita à Câmara acontece depois dos bosses 1, 2 e 3: são **3 visitas** por run.

### 6.2 Sorteio por raridade

Dois níveis: (1) sorteia a classe de raridade pela tabela abaixo; (2) sorteia o artefato dentro da classe usando o `weight` de `artifacts.json` (já existente). A tabela depende da visita (mapa que acabou de ser concluído), subindo a raridade ao longo da run.

| Visita (após o mapa) | Common | Uncommon | Rare | Epic | Legendary | Mythic |
|---|---|---|---|---|---|---|
| 1 (após `armands_dungeons`) | 45 | 33 | 15 | 6 | 1 | 0 |
| 2 (após `blood_garden`) | 35 | 30 | 20 | 10 | 4 | 1 |
| 3 (após `upper_castle`) | 25 | 28 | 22 | 15 | 8 | 2 |

Cada linha soma 100. Classes sem artefato elegível (por exemplo, Mythic depois que o único Mythic foi comprado) têm o peso redistribuído proporcionalmente.

Chance de o estoque trazer pelo menos um artefato Rare ou melhor (média de 2,5 slots livres, descontado o primeiro slot restrito a Common/Uncommon; conta 1 − (1−p)^2,5): visita 1 ≈ 46%; visita 2 ≈ 66%; visita 3 ≈ 80%. Chance de trazer Epic ou melhor: visita 1 ≈ 17%; visita 2 ≈ 33%; visita 3 ≈ 51%. **[VALIDAR]**.

Comparativo com o sorteio antigo de boss: pesos efetivos eram Common 46,8%, Uncommon 38,0%, Rare 10,1%, Epic 3,9%, Legendary 0,4%, Mythic 0,7%. Como agora o jogador paga, a oferta mais generosa em raridades altas é intencional.

### 6.3 Preço por raridade

| Raridade | Preço (Moedas) | Qtd no catálogo | Razão com Common | Justificativa |
|---|---|---|---|---|
| Common | **250** | 2 | 1,0 | ≈ 1,7 sala de drops do mapa 1: uma compra barata e sempre possível |
| Uncommon | **420** | 4 | 1,7 | ≈ 3 salas de drops |
| Rare | **700** | 4 | 2,8 | meia renda do mapa 1; exige escolha |
| Epic | **1.100** | 6 | 4,4 | ≈ renda de um mapa inteiro; exige economizar na visita 1 ou comprar só ele na 2 |
| Legendary | **1.700** | 3 | 6,8 | exige economizar de uma visita para a outra |
| Mythic | **2.400** | 1 | 9,6 | só com poupança de duas visitas; é a meta de quem guarda |

Preço fixo por raridade (não por mapa), para o jogador aprender os valores. Os preços escalam de forma mais lenta que a raridade do sorteio, porque a escassez já cobra parte do custo.

### 6.4 Quanto o jogador pode comprar por visita

Premissa: limpou o mapa todo, gastou tudo na visita anterior e carrega ~100 de resto, sem portas de moedas, sem `blood_hunt`.

| Visita | Carteira | Preço médio de um slot (pesos da 6.2) | Compras esperadas | Estoque |
|---|---|---|---|---|
| 1 | 1.052 | 439 | **≈ 2,4** | 3 a 4 |
| 2 | 1.284 + 100 = 1.384 | 556 | **≈ 2,5** | 3 a 4 |
| 3 | 1.406 + 100 = 1.506 | 683 | **≈ 2,2** | 3 a 4 |

Preço médio por slot = soma(peso × preço): visita 1 = 0,45×250 + 0,33×420 + 0,15×700 + 0,06×1.100 + 0,01×1.700 = 439; visita 2 = 556; visita 3 = 683.

Resultado: dentro da faixa pedida de **1 a 3 artefatos por visita** (média 2,4), totalizando **≈ 7 artefatos em uma run completa** e ≈ 2,4 para quem só chega à primeira Câmara. O primeiro slot garantido (≤ 420) assegura pelo menos uma compra a qualquer jogador com 950+ moedas. Para comprar um Legendary na visita 2 (1.700 contra uma carteira de 1.384) o jogador precisa ter deixado cerca de 320 sem gastar na visita 1: poupar é uma decisão legítima.

### 6.5 Riscos da loja

- **Poder acumulado.** Antes o jogador chegava a cerca de 3 artefatos por run (um por boss, com 3 equipados); agora são ≈ 7, sem limite, cada um com o multiplicador de 1,75 (`BossDropPowerMultiplier`). Este é o maior risco de balanceamento deste documento. Alavancas em ordem de preferência: (1) subir todos os preços em 25%; (2) reduzir o multiplicador na loja para 1,25 (parâmetro `economy.shop.power_multiplier`); (3) reduzir os fatores da seção 2. **[VALIDAR]** em jogo, com 3 a 4 artefatos de dano acumulados.
- **`ledger_of_unpaid_blood`** (Epic): `DamagePerCurrencyHeld` 0,001 por moeda × 1,75 hoje lê `CharacterSheet.BloodCoins` (saldo permanente, milhares). Com saldo de run, segurando 700 moedas, dá +122% de dano, e comprar algo cai esse bônus. É um dilema de design interessante, mas o artefato precisa ler o saldo da run (T2/T4) e provavelmente de um teto. Sugestão: limitar a +50% de dano (≈ 286 moedas efetivas). **[VALIDAR]**.
- **Preço não é por mapa:** se a renda real ficar abaixo de 80% da estimada, o jogador fica sem comprar o Rare na visita 1; se ficar acima de 120%, compra tudo. Os dois casos aparecem nos parâmetros `economy.coins.map_factor.*` e `economy.shop.price.*`.

---

## 7. Simulação consolidada de uma run completa

| Recurso | Mapa 1 | Mapa 2 | Mapa 3 | Mapa 4 | **Total** |
|---|---|---|---|---|---|
| Moedas de Sangue (drops + sala + boss) | 1.052 | 1.284 | 1.406 | 0 | **3.742** (gastas em 3 visitas) |
| Sangue Amaldiçoado (boss + Lendários) | 40 | 84 | 120 | 120 | **364** |
| Frutas do Diabo (boss) | 1 | 1 | 1 | 0 | **3** (+ ≈ 2 por salas = ≈ 5) |
| Vida máxima ganha por frutas | +15 | +30 | +45 | +45 | **+45 a +75** |
| Artefatos comprados na Câmara ao fim do mapa (esperado) | +2,4 | +2,5 | +2,2 | 0 (sem Câmara) | **≈ 7** |

(Sangue Amaldiçoado: mapa 1 = 40 do boss, sem Lendários; mapa 2 = 60 + 24 de Lendários; mapa 3 = 80 + 40; mapa 4 = 120. A Fruta do boss 4 não é solta.)

---

## 8. Itens extras da Loja (OPCIONAL: sugestão, não aprovada, não implementar)

Nada desta seção entra no pacote de implementação sem aprovação expressa do usuário (A2). Os parâmetros da seção 9 **não** incluem nenhum item abaixo.

1. **Reroll do estoque.** Preço 150 na primeira troca da visita, +100 a cada reroll seguinte (150, 250, 350). Calibrado para ser um quarto de uma Rare; evita que alguém passe a visita rerrolando até achar um Legendary (para 1.700 de preço a cada 4 rerolls o retorno cai). Risco: some com parte da identidade de "estoque limitado".
2. **Fruta do Diabo à venda.** Preço 900, no máximo 1 por visita. Dá uma saída para moeda excedente, mas devolve cerca de +15 de vida por 900 moedas, bem menos eficiente que um Rare de 700; é um "sumidouro" para quem já tem tudo. Risco: sobreposição com a Fruta garantida do boss.
3. **Cura.** Não recomendada: a Fonte da Câmara já cura 35% de graça.
4. **Bônus de primeira vitória do boss** (fora da Loja): +50% de Sangue Amaldiçoado só na primeira vez que cada boss cai (+20 / +30 / +40 / +60 = 150 uma única vez no save). Ameniza o início e as runs que terminam sem vencer. Mexe em `CharacterSheet` (precisa de um registro por boss), por isso é opcional.

---

## 9. Parâmetros para implementar

Os nomes de chave abaixo seguem o padrão `CompendiumTuning` do projeto (`grupo.subgrupo.nome`). Sugestão: uma classe estática nova `EconomyTuning` (em `scripts/systems/`) com estes padrões e leitura por `CompendiumTuning.GetFloat/GetInt`. Nenhum implementador deve inventar valor fora desta lista. Onde um índice de mapa aparece (`.0` a `.3`), 0 = `armands_dungeons`, 1 = `blood_garden`, 2 = `upper_castle`, 3 = `armands_throne`.

### 9.1 Moedas de Sangue (T2, T5; consumo em `EnemyBasic.RollBloodCoinsReward`)

| Chave | Valor | Observação |
|---|---|---|
| `economy.coins.enemy.base_min` | 3 | faixa **inclusiva** `[min, max]`; corrigir o `max + 1` atual |
| `economy.coins.enemy.base_max` | 7 | |
| `economy.coins.archetype.melee` | 1.0 | |
| `economy.coins.archetype.ranged` | 1.0 | |
| `economy.coins.archetype.tank` | 1.5 | |
| `economy.coins.rarity.normal` | 1 | já existe no código |
| `economy.coins.rarity.magic` | 2 | já existe |
| `economy.coins.rarity.rare` | 4 | já existe |
| `economy.coins.rarity.legendary` | 8 | já existe |
| `economy.coins.map_factor.0` | 1.0 | |
| `economy.coins.map_factor.1` | 0.6 | |
| `economy.coins.map_factor.2` | 0.5 | |
| `economy.coins.map_factor.3` | 0.5 | reserva |
| ~~`economy.coins.room_clear_bonus.*`~~ | — | removido (2026-10): a sala limpa não solta mais o saco de Moedas, só a recompensa da porta |
| `economy.coins.boss.0` | 100 | valor fixo, ignora a raridade Lendário do boss |
| `economy.coins.boss.1` | 130 | |
| `economy.coins.boss.2` | 160 | |
| `economy.coins.boss.3` | 0 | |
| `economy.coins.room_reward_pack.0` | 150 | porta de moedas (plano seguinte) |
| `economy.coins.room_reward_pack.1` | 130 | |
| `economy.coins.room_reward_pack.2` | 110 | |
| `economy.coins.blessing_fallback` | 75 | substitui o 25 de `HUD.BlessingOffer.cs:308` |

As chaves legadas `enemy.basic.min_blood_coins`, `enemy.basic.max_blood_coins`, `enemy.blood_mage.min_blood_coins` e `enemy.blood_mage.max_blood_coins` ficam sem efeito (a decisão de removê-las ou deixá-las no Compêndio é do implementador da T5; o save antigo não existe mais).

### 9.2 Sangue Amaldiçoado (T2, T5)

| Chave | Valor |
|---|---|
| `economy.cursed_blood.boss.0` | 40 |
| `economy.cursed_blood.boss.1` | 60 |
| `economy.cursed_blood.boss.2` | 80 |
| `economy.cursed_blood.boss.3` | 120 |
| `economy.cursed_blood.legendary.0` | 0 (o mapa 0 não gera Lendário comum) |
| `economy.cursed_blood.legendary.1` | 6 |
| `economy.cursed_blood.legendary.2` | 8 |
| `economy.cursed_blood.legendary.3` | 8 |
| `economy.cursed_blood.legendary_drop_chance` | 1.0 |

### 9.3 Fruta do Diabo (T3, T5)

| Chave | Valor |
|---|---|
| `economy.fruit.max_health` | 15 |
| `economy.fruit.heal` | 15 |
| `economy.fruit.boss_guaranteed` | 1 (por boss dos mapas 0 a 2) |
| `economy.fruit.boss_final_map` | 0 (o boss 4 não solta fruta) |
| `economy.fruit.room_offer_chance` | 0.12 (sugestão para o plano das portas) |
| `economy.fruit.run_cap_from_rooms` | 4 (sugestão) |

### 9.4 Loja (T4, T6)

| Chave | Valor |
|---|---|
| `economy.shop.stock_min` | 3 |
| `economy.shop.stock_max` | 4 |
| `economy.shop.stock_extra_chance` | 0.5 |
| `economy.shop.first_slot_max_rarity` | Uncommon (o primeiro slot só sorteia Common e Uncommon) |
| `economy.shop.exclude_owned` | 1 (ver decisão D-B) |
| `economy.shop.allow_duplicates_in_stock` | 0 |
| `economy.shop.power_multiplier` | 1.75 (igual ao `BossDropPowerMultiplier` atual; ver risco 6.5) |
| `economy.shop.price.common` | 250 |
| `economy.shop.price.uncommon` | 420 |
| `economy.shop.price.rare` | 700 |
| `economy.shop.price.epic` | 1100 |
| `economy.shop.price.legendary` | 1700 |
| `economy.shop.price.mythic` | 2400 |

Pesos de raridade por visita (`v` = índice do mapa que acabou de terminar, 0 a 2; como `RunState.CompleteBoss` já incrementou `CurrentMapIndex`, o valor é `CurrentMapIndex − 1`):

| Chave | v=0 | v=1 | v=2 |
|---|---|---|---|
| `economy.shop.rarity_weight.common.v` | 45 | 35 | 25 |
| `economy.shop.rarity_weight.uncommon.v` | 33 | 30 | 28 |
| `economy.shop.rarity_weight.rare.v` | 15 | 20 | 22 |
| `economy.shop.rarity_weight.epic.v` | 6 | 10 | 15 |
| `economy.shop.rarity_weight.legendary.v` | 1 | 4 | 8 |
| `economy.shop.rarity_weight.mythic.v` | 0 | 1 | 2 |

O sorteio dentro da classe usa o campo `weight` de cada artefato em `artifacts.json` e **não** deve aplicar o `GetRarityMultiplier` antigo.

### 9.5 Árvore (T8: só `data/skills/skills.json`, campo `progression.cost`)

| ID | `cost` novo |
|---|---|
| `bestialis.vampire_fangs` | 20 |
| `bestialis.opening_strike` | 24 |
| `bestialis.shadow_attack` | 50 |
| `bestialis.marked_prey` | 56 |
| `bestialis.predatory_reflex` | 46 |
| `bestialis.terminal_frenzy` | 130 |
| `bestialis.bestial_transformation` | 130 |
| `bestialis.blood_hunt` | 160 |
| `humanitas.survival_instinct` | 20 |
| `humanitas.thick_skin` | 24 |
| `humanitas.methodical_recovery` | 20 |
| `humanitas.pocket_watch` | 50 |
| `humanitas.iron_will` | 56 |
| `humanitas.collector_eye` | 50 |
| `humanitas.rewritten_fate` | 130 |
| `humanitas.last_stand` | 180 |

- `"currency"` em `skills.json` (hoje `"blood_coins"`) passa a `"cursed_blood"`; a T8 deve conferir se algum código lê esse campo antes de trocar.
- `economy.skill.rank_cost_step` = 0.5 (valor já hard-coded em `HUD.BloodFountain.GetSkillUpgradeCost`; a fórmula não muda).
- Não alterar `maxRank`, pré-requisitos nem efeitos.

### 9.6 Textos e identificadores novos (para as tarefas de UI e conteúdo)

IDs sugeridos, em snake_case estável: recurso `cursed_blood`, item `devils_fruit`, serviço `hemomancy_shop`. Todo texto novo precisa de `pt-BR`, `en` e `es`.

### 9.7 Alavancas de ajuste

| Alavanca | Efeito | Quando usar |
|---|---|---|
| `economy.cursed_blood.boss.*` ×1,25 / ×0,8 | muda o ritmo da árvore sem mexer em cada skill | se o jogador médio levar bem mais ou menos de 25 runs |
| `economy.coins.map_factor.*` | muda a renda por mapa | se a renda medida divergir mais de 15% da seção 2.5 |
| `economy.shop.price.*` ×1,25 | reduz artefatos por run de ≈ 7 para ≈ 5,6 | se o poder acumulado dos artefatos estiver alto |
| `economy.shop.power_multiplier` 1.75 → 1.25 | reduz o poder de cada artefato | idem, se os preços já estiverem corretos |

---

## 10. Premissas, riscos e o que validar jogando

### 10.1 Premissas

- A probabilidade de vencer cada boss por perfil (seção 3.4) é **estimativa**; não há telemetria no jogo.
- O jogador limpa todas as salas de cada mapa antes do boss (é obrigatório para abrir o portal) e a Loja só abre após o boss, então a carteira da visita é a renda do mapa inteiro.
- As salas oferecerão no máximo uma recompensa de moedas ou fruta por sala; o plano das portas pode alterar o valor de uma sala (por isso as frequências de portas estão marcadas como sugestão).
- O `BloodCoinMultiplier` da skill `bestialis.blood_hunt` aumenta a renda em ~5 a 8%, ignorado nas contas.

### 10.2 Riscos de balanceamento (ordem de gravidade)

1. **Poder acumulado dos artefatos** (≈ 7 por run, sem limite, ×1,75). Ver 6.5.
2. **Mapa 1 sem Sangue em runs ruins:** runs que morrem antes do boss 1 não progridem na árvore; pode afastar iniciantes. Mitigação opcional na seção 8 (item 4).
3. **Skills mortas:** `humanitas.collector_eye` e o aspecto artefato de `humanitas.rewritten_fate` não têm mais função (decisão D-A).
4. **`ledger_of_unpaid_blood`** passa a depender do saldo da run (precisa de ajuste de leitura e, de preferência, teto).
5. **Viés de arredondamento** nas moedas dos mapas 2 e 3 (fator 0,6 e 0,5 sobre faixa 3 a 7): renda do mapa 3 pode ficar até 10% acima do previsto.
6. **Fruta + Altar:** o Altar reduz a vida máxima em 5% e a fruta sobe; a combinação não é problema, mas a leitura de "vida máxima" no HUD deve mostrar o valor total atualizado.
7. **Sumidouro após a árvore completa:** sem destino para o Sangue Amaldiçoado (aguarda o plano dos personagens).

### 10.3 Decisões pendentes para o usuário

- **D-A:** o que fazer com `humanitas.collector_eye` (hoje "+5% de chance de artefato por rank") e com `artifacts: true` em `humanitas.rewritten_fate`. Proposta do game designer, que não entra sem aprovação: `collector_eye` vira "desconto nos preços da Loja" (−5% / −10% / −15% por rank, efeito novo `ShopPriceDiscount`), e `rewritten_fate` passa a dar 1 reroll do estoque por visita (isso exige o item 1 da seção 8). Alternativa mínima: deixar `collector_eye` como skill "Planejada" e o `artifacts: true` ignorado.
- **D-B:** duplicatas na Loja. Recomendado: não oferecer o que o jogador já possui (parâmetro `exclude_owned`). Se o usuário quiser empilhar duplicatas, o efeito do segundo exemplar precisa ser definido (soma linear?).
- **D-C:** aceitar a escala dos preços/poder dos artefatos (6.5) ou já reduzir `power_multiplier` para 1,25 antes do primeiro teste.

### 10.4 O que validar jogando

1. Renda real de Moedas por mapa e se o jogador chega à Câmara com cerca de 1.000 a 1.400 (seção 2.5), com atenção ao viés de arredondamento do mapa 3.
2. Quantos artefatos o jogador realmente compra por visita (alvo 2 a 3) e se o primeiro slot barato é suficiente.
3. O poder dos artefatos acumulados no boss 3 e no Armand (desafio vs. tempo-alvo de 300 s e 360 s dos bosses).
4. Quantas runs o jogador médio leva para completar a árvore (alvo 18 a 26) e quantas runs ficam sem nenhum Sangue.
5. Número de Lendários por mapa e o Sangue deles (esperado 4 e 5 nos mapas 2 e 3).
6. Se a vida com 3 a 5 frutas desequilibra os bosses 2, 3 e 4 (vida máxima total acima de ~250).
7. Se o `fator_do_mapa` decrescente (1,0 / 0,6 / 0,5) passa a sensação de "moeda que perde valor"; alternativa: fator 1 em todos os mapas e preços ×1,7.
8. O que o jogador faz com o piso de 12 a 16 moedas de sala limpa: se aparece como "+N" claro na HUD e se o total cabe na leitura rápida.

# Portas com recompensa visível (fase 2)

Documento de design da tarefa T1 do plano `20261011-0550-portas-recompensas`. Serve de fonte única de números para as tarefas T2 (modelo de recompensas, `RoomRewardCatalog`), T3 (fluxo de portas) e T4 (ícones). Escrito em 2026-10-11.

**Status de todos os números abaixo: proposta a validar jogando.** Eles saem de contas sobre o código e os dados atuais e de uma simulação de Monte Carlo (30 mil runs por cenário, fora do repositório). Não há playtest. Os pontos que mais dependem de teste estão marcados com **[VALIDAR]** e reunidos na seção 12.

Premissas fixas (decisões do usuário, não reabertas): D7 (1 ou 2 portas por sala limpa, quantidade e tipo sorteados), D8 (tipos: Bênção de Sangue, Fruta do Diabo e Moedas de Sangue; Sangue Amaldiçoado não é tipo de porta), recompensa da sala = a da porta cruzada, salas de boss com uma porta e recompensa fixa, ícones em geometria provisória com rótulo curto. Os valores de moeda, Sangue Amaldiçoado e Fruta vêm de `docs/design/economia-v2.md` (seções 2, 3, 5, 6 e 9).

## Revisão de 2026-10-24 (vigente; prevalece sobre as seções abaixo)

Pedido do Pedro: duas portas mais comuns que uma; a Bênção de Sangue é a recompensa mais comum, e o caso normal é escolher entre **duas Bênçãos diferentes**, uma em cada porta; Moedas e Fruta aparecem menos; a Bigorna é rara. A sala da primeira Bênção (`RunStartBlessingRoom`) continua com uma porta só.

| Regra | Antes | Agora |
|---|---|---|
| Chance de duas portas (base por mapa) | 30% / 35% / 40% | **70% / 75% / 80%** |
| Degrau e teto | +5 a cada 2 salas, teto 55% | +5 a cada 2 salas, **teto 90%** |
| Primeira porta | Bênção (garantia, pity 0) | igual |
| Segunda porta pode ser Bênção? | não (tipos nunca repetem) | **sim**, de **linhagem diferente** da primeira |
| Pesos da segunda porta: Bênção | 72 / 68 / 64 (sem efeito: excluída) | **70 / 66 / 62** |
| Moedas | 22 / 24 / 26 | **18 / 20 / 22** |
| Fruta | 6 / 8 / 10 | **8 / 10 / 12** |
| Bigorna (só segunda porta, só se elegível) | 8 / 10 / 12 (e 100 promovido em `tuning_overrides.json` para teste) | **4 / 4 / 4** (override de teste removido) |

- A rampa da segunda metade do mapa (2 pontos de Moedas para Fruta) continua.
- Fruta, Moedas e Bigorna continuam sem repetir no mesmo conjunto; o teto de Frutas (3 por run) e o da Bigorna não mudaram.
- Duas portas de Bênção: cada uma tem seu plano de 3 opções (`RoomRewardCatalog.PlanBlessingsForSet`); a segunda sorteia a linhagem excluindo a da primeira (`BloodBlessingCatalog.RollOffers`, parâmetro `excludedFamilies`). Se não sobrar outra linhagem elegível, a segunda porta vira Moedas.
- O override de dev "Forçar portas" (aba Ações do F1) também grava um plano por porta de Bênção, com linhagens diferentes.

Números esperados por conjunto de portas (conta direta sobre os pesos, Bigorna sempre elegível, sem o teto de Fruta):

| Mapa | Duas portas | Duas Bênçãos | Bênção + Moedas | Bênção + Fruta | Bênção + Bigorna |
|---|---|---|---|---|---|
| 1 (inclui a sala da primeira Bênção, sempre 1 porta) | 63% | 44% | 11% | 6% | 2,5% |
| 1 (só salas comuns) | 76% | 53% | 13% | 7% | 3% |
| 2 | 83% | 54% | 16% | 9% | 3% |
| 3 | 87% | 54% | 18% | 11% | 3,5% |

Impacto: o jogador continua recebendo uma recompensa por sala; o que muda é que a escolha entre duas Bênçãos é o caso típico, então a run deve terminar com mais Bênçãos e um pouco menos de Moedas e Frutas de porta. **[VALIDAR]** jogando.

---

## 0. Resumo das decisões

| Tema | Decisão proposta | Seção |
|---|---|---|
| Unidade de sorteio | 1 conjunto de portas (CP) por saída; cada CP decide a recompensa da sala seguinte | 1 |
| Quantidade | 1 ou 2 portas; P(2 portas) de 70% a 90% (revisão 2026-10-24; era 30% a 55%), sobe com a sala e com o mapa | 2 e revisão |
| Tipos | Bênção, Fruta e Moedas com pesos por mapa e rampa na 2ª metade do mapa; duas portas nunca do mesmo tipo | 3.1 e 3.2 |
| Garantia de Bênção | Mapa 1: Bênção em toda CP. Mapas 2 e 3: nunca duas CPs seguidas sem Bênção | 3.3 |
| Impacto em bênçãos | Ofertas por run (incluindo a Sala Inicial) caem de 25 (hoje) para cerca de 22. Bênçãos escolhidas: 22 (jogador que prefere Bênção), 17 (escolha neutra), 12 (jogador que evita Bênção). Alternativas B e C na 3.4 | 3.4 |
| Moedas via porta | 150 / 130 / 110 (mapas 1 a 3), a renda média de uma sala do mapa, como em economia-v2 §9.1. Não usar 12/14/16 | 4 |
| Fruta via porta | Cerca de 15% das CPs oferecem Fruta; média de 2,1 frutas de porta por run (escolha neutra); **teto de 3** por run (economia-v2 sugeria 4) | 5 |
| Interface | Ícone sempre visível; rótulo curto ao aproximar; 1 porta no centro da saída, 2 portas simétricas (±1 célula); sem escolha = sala espera, sem punição | 6 |
| Escolha e entrega | Entrar na porta confirma a escolha (guardada na run). A recompensa é paga ao **limpar a sala seguinte**, como hoje | 7 |
| Ícones | Bênção = losango dourado claro; Fruta = esfera vermelho-escuro com haste; Moedas = pilha de discos bronze. Forma, luminância e cor distintas | 8 |

---

## 1. Modelo: conjuntos de portas e recompensas

### 1.1 Conceito

- **Conjunto de portas (CP)**: as 1 ou 2 portas que aparecem na saída de uma sala (ou na entrada de um mapa). Cada CP é sorteada uma vez e fica guardada na run.
- **Regra central**: a CP da saída da sala *k* decide a recompensa da sala *k+1*. A recompensa só é paga quando a sala *k+1* for limpa (seção 7).
- Consequência: cada sala comum recebe exatamente uma recompensa, como hoje, e a run tem 24 CPs sorteadas (6 + 8 + 10 salas comuns, `CampaignCatalog.cs`).

### 1.2 Mapeamento completo

| Mapa | Entrada (CP de origem) | Saídas com sorteio | Saída para o boss |
|---|---|---|---|
| 1 `armands_dungeons` (6 salas) | Sala Inicial (`RunStartBlessingRoom`) → gera Sala 1 | Sala 1 a 5 → geram Salas 2 a 6 | Sala 6 → boss, porta única |
| 2 `blood_garden` (8 salas) | Câmara após boss 1 → gera Sala 1 | Sala 1 a 7 → geram Salas 2 a 8 | Sala 8 → boss, porta única |
| 3 `upper_castle` (10 salas) | Câmara após boss 2 → gera Sala 1 | Sala 1 a 9 → geram Salas 2 a 10 | Sala 10 → boss, porta única |
| 4 `armands_throne` (0 salas) | Câmara após boss 3 → leva ao Armand | nenhuma | — |

- Total de CPs sorteadas por run: 6 + 8 + 10 = **24**.
- Salas de boss: não têm CP sorteada e a saída para o boss é porta única (A9). A saída do boss continua indo para a Câmara ou para o Ninho, sem ícone.
- A Câmara antes do mapa 4 tem uma porta única para o Armand, sem sorteio, como hoje.

### 1.3 Índice usado nas tabelas

`k` = índice da origem da CP: `k = 0` é a entrada do mapa (Sala Inicial ou Câmara); `k = 1..N-1` é a saída da sala *k*. `N` = salas comuns do mapa. A saída da sala `N` vai para o boss e não é sorteada. O `mapa` da CP é o mapa cujas salas ela precede, e é esse mapa que define pesos e valor de moeda.

---

## 2. Quantas portas (1 ou 2) por sala

### 2.1 Tabela de probabilidade de 2 portas

| Mapa | Origem `k=0` (entrada) | `k=1` | `k=2` | `k=3` | `k=4` | `k=5` | `k=6` | `k=7` | `k=8` | `k=9` |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 (6 salas) | 30% | 30% | 35% | 35% | 40% | 40% | — | — | — | — |
| 2 (8 salas) | 35% | 35% | 40% | 40% | 45% | 45% | 50% | 50% | — | — |
| 3 (10 salas) | 40% | 40% | 45% | 45% | 50% | 50% | 55% | 55% | 55% | 55% |
| 4 (0 salas) | sem sorteio | | | | | | | | | |

Leitura: para o mapa 2, a saída da sala 4 (`k=4`) tem 45% de chance de duas portas.

### 2.2 Fórmula (para implementar)

```
P2(mapa, k) = min(cap, base[mapa] + step × floor(k / step_every))
base = 0,30 / 0,35 / 0,40  (mapas 1, 2, 3)
step = 0,05 ; step_every = 2 ; cap = 0,55
```

### 2.3 Justificativa

- **Base 30% a 40% por mapa:** a maior parte das salas continua com uma porta, para o ritmo de run não virar leitura de menu. Com 2 portas em 30% a 40% das saídas no início de cada mapa, a decisão aparece sem virar regra. [VALIDAR]
- **Subida de +5 pontos a cada 2 salas:** dá mais escolhas no fim do mapa, quando a Fruta e as Moedas pesam mais (seção 3.5). Dois degraus por sala evitam saltos bruscos.
- **Teto de 55%:** mantém pelo menos 45% das saídas com porta única. Sem o teto, as últimas salas do mapa 3 chegariam a 60%.

### 2.4 Números esperados

| Mapa | Portas esperadas | CPs com 2 portas (esperado) | CPs com 2 portas (fração) |
|---|---|---|---|
| 1 | 8,1 | 2,1 de 6 | 35% |
| 2 | 11,4 | 3,4 de 8 | 43% |
| 3 | 14,9 | 4,9 de 10 | 49% |
| **Run** | **34,4** | **10,4 de 24** | **43%** |

Ou seja, cerca de 10 decisões reais por run e cerca de 14 saídas com porta única.

---

## 3. Tipos de porta: pesos, regras e garantias

### 3.1 Pesos base por mapa (por porta, antes das regras da 3.2)

| Mapa | Bênção | Fruta | Moedas | Rampa (origens `k ≥ N÷2`) |
|---|---|---|---|---|
| 1 (N = 6) | 72 | 6 → **8** | 22 → **20** | `k = 3..5` |
| 2 (N = 8) | 68 | 8 → **10** | 24 → **22** | `k = 4..7` |
| 3 (N = 10) | 64 | 10 → **12** | 26 → **24** | `k = 5..9` |

- Os pesos são somados por porta sorteada; a soma é 100 em cada faixa.
- Na rampa, 2 pontos saem de Moedas e vão para Fruta. Bênção não muda dentro do mapa.
- Mapa 4 não tem CP sorteada.

### 3.2 Regras de sorteio (ordem de aplicação)

1. **Garantia de Bênção (3.3):** se a regra do mapa exige Bênção nesta origem, a primeira porta é Bênção.
2. **Demais portas:** sorteadas pelo peso da faixa, excluindo os tipos já escolhidos na CP. Assim, **duas portas nunca têm o mesmo tipo** (revisão 2026-10-24: exceto Bênção, que pode repetir com outra linhagem). Quando a segunda porta é sorteada, os pesos restantes são renormalizados.
3. **Teto de Fruta (5.3):** se o número de Frutas de porta já escolhidas na run chegou a 3, Fruta sai do sorteio e os pesos são renormalizados.
4. **Sorteio fixo:** a CP é sorteada uma única vez, quando a origem é alcançada, e fica guardada na run. Recarregar a cena não rola de novo (mesmo critério da bênção da Sala Inicial, que não concede outra ao recarregar).
5. **RNG:** semente derivada da semente da run (`RunState`), para ser reproduzível em teste.

### 3.3 Garantia de Bênção (pity)

Contador `sets_without_blessing` na run: conta quantas CPs seguidas (inclusive as da Câmara) não tiveram Bênção.

| Mapa | Máximo de CPs seguidas sem Bênção | Significado |
|---|---|---|
| 1 | 0 | toda CP do mapa 1 tem Bênção (a 2ª porta, quando existe, é sorteada) |
| 2 | 1 | nunca duas CPs seguidas sem Bênção |
| 3 | 1 | idem |

- Pior caso no mapa 2 e 3: uma Bênção a cada duas salas, que é o `N = 2` pedido na demanda.
- Justificativa do N: no mapa 1 a Bênção é a porta garantida porque ele é o mapa de montar o build (as primeiras ofertas são as que definem o moveset, e a primeira precisa de `basic_attack`). Nos mapas 2 e 3 o build já existe, então o sorteio pode faltar por uma sala, desde que não falte por duas.
- A garantia é de **oferta**, não de escolha. Um jogador que sempre pega Fruta ou Moedas não recebe a Bênção; é a regra de escolha dele. Ver risco 11.2.

### 3.4 Impacto na progressão de bênçãos

Hoje: a sala comum sempre oferece Bênção (`RunManager.CompleteRoom`, chamada de `OpenBloodBlessingOffer`). Por run: 24 ofertas de sala + 1 da Sala Inicial = **25**. Por mapa: 6, 8 e 10 ofertas.

Simulação (3 políticas de escolha, 30 mil runs). Os números incluem a bênção fixa da Sala Inicial, que existe em todos os cenários:

| Cenário | Ofertas de Bênção por run | Bênçãos escolhidas por run: prefere Bênção / neutro / evita Bênção | Bênçãos escolhidas (neutro) por mapa, sem a Sala Inicial |
|---|---|---|---|
| Hoje (referência) | 25 (1 + 6 + 8 + 10) | 25 | 6 / 8 / 10 (ofertas) |
| **A (recomendada)**: pity `0, 1, 1` | 22,0 (1 + 6,0 / 6,6 / 8,3) | 21,8 / 16,9 / 12,0 | 5,0 / 5,0 / 6,0 |
| **B (sem perda)**: Bênção em toda CP (`0, 0, 0`) | 25,0 (1 + 24) | 25,0 / 19,8 / 14,6 | 5,0 / 6,3 / 7,5 |
| **C (pity uniforme)**: `1, 1, 1` | 21,0 | 20,9 / 16,0 / 11,1 | — |

Leitura:
- Em todos os cenários, o número de bênçãos efetivamente equipadas depende da escolha do jogador. A regra só garante a **oferta**.
- Com jogador neutro, A perde cerca de 8 bênçãos por run contra o hoje (17 contra 25), e B perde cerca de 5 (20 contra 25). A parte mais sensível é o mapa 2 e o mapa 3, que caem de 8 e 10 ofertas para cerca de 5 a 6 escolhidas em A.
- Em B, Fruta e Moedas só aparecem na segunda porta (43% das salas). Em A, a regra de pity deixa algumas salas com Fruta ou Moedas como única opção de escolha, mas oferece 3 bênçãos a menos por run.
- A é a recomendação porque mantém o pity do pedido (uma Bênção a cada duas salas nos mapas 2 e 3) e deixa Fruta e Moedas mais presentes. B é a alternativa se a progressão de bênçãos for prioridade absoluta; o risco está na seção 11.1. [VALIDAR]

### 3.5 Justificativa dos pesos

- **Bênção 72 → 64 de um mapa para o outro:** o mapa 1 é onde o build é montado, então Bênção vale mais e Fruta e Moedas valem menos (a Renda do mapa 1 já é a menor, e o boss 1 paga Sangue e fruta). Nos mapas seguintes, o jogador já tem várias bênçãos equipadas e o pool de 63 bênçãos começa a ficar repetitivo, então a Bênção vale um pouco menos por sala. [VALIDAR]
- **Fruta 6 → 12 (com rampa):** Fruta cresce com o mapa e com a 2ª metade do mapa, quando a vida pesa mais para o boss do mapa (bosses 2 e 3 têm vida estimada de 14.000 e 17.400 contra 5.000 do boss 1).
- **Moedas 22 → 26:** cresce porque as visitas à Loja 2 e 3 compram Epic e Legendary (economia-v2 §6.4), que dependem de saldo.

---

## 4. Moedas de Sangue pela porta

### 4.1 Valor

| Mapa (de quem recebe a sala) | Valor da porta | Chave | Referência |
|---|---|---|---|
| 1 | 150 | `economy.coins.room_reward_pack.0` | renda média de drops de uma sala do mapa 1 (146,7) |
| 2 | 130 | `economy.coins.room_reward_pack.1` | renda média de uma sala do mapa 2 (130,3) |
| 3 | 110 | `economy.coins.room_reward_pack.2` | renda média de uma sala do mapa 3 (108,6) |

- **Por que não 12 / 14 / 16:** esses valores são o bônus de sala limpa (`economy.coins.room_clear_bonus.*`), que toda sala paga de qualquer forma. Como opção de porta, a Moedas valeria pouco mais que nada e nunca seria escolhida. Eles continuam sendo pagos ao limpar a sala, ao lado do pacote.
- **Por que o valor da renda de uma sala:** escolher a porta de Moedas dobra o ganho daquela sala. É uma escolha que se paga sem virar o jogo, e casa com a hipótese da economia-v2 (§2.4 e §9.1). Um valor de metade de sala (75, o mesmo do fallback de bênção) ficaria abaixo de um artefato comum da Loja (250) e pouco acima do ruído da HUD.
- O valor é fixo por mapa (não sorteado), para o jogador ler o número no ícone ao aproximar (seção 6.2).

### 4.2 Renda esperada por run (Moedas de porta)

| Política de escolha | Moedas de porta por run | Mapa 1 / 2 / 3 |
|---|---|---|
| Nunca pega Moedas | 0 | 0 / 0 / 0 |
| Prefere Bênção (quando há) | 258 | 0 / 127 / 131 |
| **Neutra (uniforme entre as portas)** | **734** (p10 = 440; p90 = 1.060) | **117 / 282 / 335** |
| Sempre pega a não-Bênção | 1.267 | 235 / 446 / 585 |

- A economia-v2 (§2.5) assumiu +600 por run (20% das salas com porta de Moedas, escolhida). O cenário neutro dá +734, um pouco acima.
- Em relação à renda base da run (3.742 em economia-v2 §2.5), o cenário neutro soma cerca de +20%. Como a Loja não contava com isso, o poder de artefatos vai subir (ver 11.3). A alavanca é o `room_reward_pack` ou o preço da Loja (economia-v2 §9.7). [VALIDAR]

### 4.3 Pagamento

- Pago ao limpar a sala seguinte, junto do bônus de sala limpa (`RunManager.CompleteRoom`, via `AddBloodCoins`), com o "+N" da HUD.
- Conta no saldo da run (`RunState.AddRunBloodCoins`), nunca no Sangue Amaldiçoado.
- Mapa usado: o mapa da sala que recebe (seção 1.3).

---

## 5. Fruta do Diabo pela porta

### 5.1 Regra

Igual à fruta do boss (economia-v2 §5.1): +15 de vida máxima acumulativa, na run, e cura de 15 ao coletar. Nasce na mesma posição do ponto de recompensa da sala (`RoomGenerator.GetArtifactSpawnPosition`, o mesmo do boss), com o `DevilsFruit.Spawn` existente.

### 5.2 Frequência

| Métrica (run completa, 24 CPs) | Uniforme (neutra) | Prefere Bênção | Sempre não-Bênção |
|---|---|---|---|
| CPs que oferecem Fruta | 3,5 (≈ 15%) | 4,0 (≈ 17%) | 2,8 (≈ 12%) |
| Frutas de porta escolhidas | **2,1** | 1,0 | 2,7 (com teto) |
| Runs sem nenhuma fruta de porta | 7% | 33% | 1% |
| Runs em que o teto é atingido | 44% | 7% | 77% |

- Com as frutas dos três bosses (economia-v2 §5.2, D5), o total de uma run neutra fica em **5,1 frutas (+75 de vida máxima)**, o que bate com o "≈ 5 frutas" da economia-v2.
- O alvo de "≈ 2 por run pelas salas" (economia-v2 §5.2) é atingido pelo jogador neutro. O jogador que prefere Bênção recebe cerca de 1; o que evita Bênção, cerca de 2,7.

### 5.3 Teto: 3 por run (ajuste)

- **Economia-v2 sugeria teto 4** (`economy.fruit.run_cap_from_rooms`). Propomos **3** (`economy.fruit.run_cap_from_doors`).
- Justificativa: com teto 4, o máximo teórico chega a 7 frutas (+105 de vida máxima); com teto 3, a 6 (+90). A economia-v2 (§5.2) já marcou como risco passar de cerca de 250 de vida máxima total (com árvore e bênçãos), e 4 frutas de porta somadas às 3 dos bosses fazem esse limite aparecer mais cedo.
- Efeito do teto 3 no jogador neutro: ele é atingido em 44% das runs. Com teto 4, seria atingido em cerca de 22%. O teto 3 é o que deixa a curva de vida previsível; se os bosses 3 e 4 ficarem fáceis, o teto sobe para 4 (lever). [VALIDAR]
- Quando o teto é atingido, Fruta sai do sorteio (regra 3.2.3). A porta de Fruta nunca aparece sem poder ser paga.

### 5.4 Ajuste na economia-v2

| Item da economia-v2 | Hoje (documento) | Proposta |
|---|---|---|
| §5.2: "Recompensa de sala ~12% das salas" | `economy.fruit.room_offer_chance = 0.12` | **substituída** pelos pesos por porta (3.1). Resultado: ≈ 15% das CPs oferecem Fruta |
| §5.2 e §9.3: teto 4 | `economy.fruit.run_cap_from_rooms = 4` | **3**, com a chave `economy.fruit.run_cap_from_doors` |

A substituição não muda o alvo de "≈ 2 por run" (seção 5.2). Só troca a unidade do parâmetro: por porta e não por sala.

---

## 6. O que o jogador vê antes de escolher

### 6.1 Ícones (sempre visíveis)

- Um ícone 3D por porta, acima do lintel (o lintel fica a 2,7 m; o ícone a cerca de 3 m). Visível de qualquer ponto da sala de onde a porta é vista, sem aproximar.
- Cada ícone mostra o tipo pela silhueta, pela forma e pela cor (seção 8).

### 6.2 Rótulo e prompt (ao aproximar)

- Ao entrar na área da porta (mesma regra de `ScenePortal`, `Area3D` + `InteractPrompt`), aparece um rótulo curto e o prompt de interação.
- Rótulo curto: `reward.door.<tipo>.short` (seção 9.4). Moedas mostra o valor: "Moedas +150".
- Prompt: `reward.door.prompt` = "Atravessar: {0}", com o rótulo como argumento.
- Hint de uma linha, mostrado junto do rótulo ao aproximar: Bênção "escolha uma de três"; Fruta "+15 de vida máxima e cura"; Moedas "+150 nesta run". O prompt só traz a ação e o rótulo, para não repetir o texto.

### 6.3 Quantas portas e onde

| Número | Posição | Tamanho |
|---|---|---|
| 1 porta | centro da saída: `RoomGenerator.ExitPortalPosition` (a mesma posição de hoje) | moldura atual |
| 2 portas | simétricas em relação ao centro, a ±1 célula (≈ 1,45 m, `CellSize`), na mesma parede | 60% da moldura atual cada |

- Qual tipo fica à esquerda ou à direita é sorteado (50/50) por CP. Não é fixo por tipo, para o jogador ler o ícone e não decorar a posição.
- As duas portas precisam de 3 células livres na parede da saída. Se a sala não tiver espaço (`RoomGenerator`), a CP vira porta única (ver risco 11.4). [VALIDAR na captura]
- Os valores de 60% e de ±1 célula são sugestões visuais para T4, a validar com `--ui-batch`/TestArena.

### 6.4 Se o jogador não entra em nenhuma porta

- A sala fica limpa e as portas ficam abertas. **Não há timer, não há spawn, não há punição.**
- Nada é pago nem perdido. A recompensa só é paga quando a sala seguinte for limpa.
- A linha de título da sala mostra "Sala limpa · escolha uma porta" (`hud.room.choose_door`). Hoje a linha mostra "Portal aberto" (`NotifyPortalOpened`); essa troca fica para T3/T4.

### 6.5 Quando as portas abrem

As portas só ficam disponíveis depois que a recompensa da sala atual foi resolvida:

- **Bênção:** depois de escolher a Bênção (o overlay é obrigatório, como hoje; `HUD.BlessingOffer` chama `UnlockExitAfterReward`, que passa a abrir as portas).
- **Moedas:** depois do "+N" (é imediato).
- **Fruta:** depois que a Fruta for coletada (proposta, [VALIDAR]). Se a porta abrisse antes, o jogador poderia cruzar a porta e perder a fruta, que fica na sala que já não está mais ativa. A fruta é gerada em célula caminhável com margem (`ClampToWalkableArea`), então não há risco de trava.

### 6.6 Visual: o portão do hub em todas as portas

Toda saída de sala (portas de recompensa, saída única da sala N, saída do boss, sala da primeira Bênção e Câmara) usa o
mesmo portão do início da run no hub: uma grade de ferro que sobe (`PortcullisGate`, 8 quadros no 2D).

- **Travada** (sala em combate ou recompensa da sala ainda no chão): grade fechada, mesmo com o jogador perto.
- **Liberada:** a grade continua fechada e sobe inteira quando o jogador chega a até 2,8 m dela; ao se afastar, desce de novo.
- **Atravessar:** com a grade aberta, entrar no vão (até 0,75 m da face da parede) é a escolha da porta. Não há prompt nem botão de interagir; o medalhão mostra o que a porta dá.
- **Porta não escolhida:** a grade desce (0,3 s, 7.2).
- **3D:** a grade fica entre as colunas da moldura (as barras fixas e o disco do chão saíram), e o gate antigo (`gate.glb`) não aparece mais.
- **2D:** cada porta fica num arco da parede do kit (borda `arch`), com a grade do kit por cima. Uma porta usa o tile do meio da parede de saída; duas usam os tiles vizinhos (o do meio fica liso). A sala nasce com dois arcos quando vai ter conjunto de portas; se o sorteio der uma, a parede é redesenhada em segundo plano (`PixelDoorWall`, `PixelRoom.RecomposeAsync`). No 2D a saída fica sempre numa parede do fundo (ne/nw), as únicas com arco e grade no kit.
- A sala do boss também passou a ter a saída no meio da parede norte, com o portão.

---

## 7. Regras de escolha e entrega

### 7.1 Decisão: entrega ao limpar a sala seguinte

- **Escolha** (commit): ao interagir com a porta. Guardada em `RunState` como `PendingRoomReward` (tipo, mapa e valor, quando for Moedas), antes da troca de cena.
- **Entrega**: quando a sala que a porta leva for limpa (`RunManager.CompleteRoom`).

Justificativa com o fluxo atual:

1. Hoje a recompensa é paga em `RunManager.CompleteRoom` (`AddBloodCoins` e `OpenBloodBlessingOffer`), que é idempotente por cena (`_roomRewardsPaid`). Esse é o único ponto de pagamento. Pagar ao entrar exigiria um segundo ponto, dentro da cena nova, antes de `RunManager.ConfigureRoom` rodar no primeiro `NotifyWaveSpawned`.
2. `ScenePortal` troca de cena com `ChangeSceneToFile` logo ao interagir (`ApplyPortalAction` → `RunState.AdvanceRoom`). Entregar a Bênção ao entrar faria o overlay aparecer antes da luta, com a sala ainda carregando.
3. A ordem atual "limpar → recompensa → portal" continua igual. Só muda a decisão, que sai da sala limpa e vai para a porta. O jogador já lê o ícone antes de decidir.
4. O boss segue o mesmo princípio: a fruta e o Sangue são pagos ao morrer o boss (`RunManager.CompleteRoom`, `SpawnBossFruit`).

Custo assumido: se o jogador morrer na sala que a porta levou, a recompensa pendente se perde junto com a run. É o mesmo destino da run, e a regra fica clara no texto da porta.

### 7.2 Entrar numa porta fecha a outra

- Ao confirmar a porta, a outra fecha (animação de grade descendo, 0,3 s; ver 9.5) antes da troca de cena. A porta fechada não aparece na sala seguinte, que é outra cena.
- Só a porta escolhida gera `PendingRoomReward`. A outra é descartada.
- Sem confirmação extra. A escolha é irreversível, e o rótulo já mostra o que se ganha (risco 11.5).

### 7.3 Casos especiais

| Caso | Regra |
|---|---|
| Sala Inicial (mapa 1) | CP_0 é sorteada na saída da sala inicial, após a Bênção fixa da fonte. Não conta como recompensa da sala inicial. Sempre uma porta só (sem chance de duas portas). |
| Câmara (mapas 2 e 3) | CP_0 é sorteada ao chegar à Câmara após o boss, sempre com um portão só e **sem revelar o bônus** (sem medalhão; o prompt é o do portal comum). O bônus é pago ao limpar a Sala 1, como nas outras portas. A Câmara continua com Fonte, Altar e Loja. |
| Boss | Sem CP, sem ícone. Recompensa fixa (Sangue Amaldiçoado e fruta), paga na morte do boss. Porta única para a Câmara ou o Ninho. |
| Morte ou "Desistir da run" | `RunState.FailRun` zera `PendingRoomReward`, o contador de pity e o de frutas. |
| Recarregar cena | A CP e a escolha pendente ficam em `RunState`; recarregar não rola de novo nem apaga a recompensa. |

### 7.4 Ordem de eventos (para T2 e T3)

```
Limpar sala k
  -> paga a recompensa pendente (PendingRoomReward, se houver)
       Bênção: OpenBloodBlessingOffer; Fruta: DevilsFruit.Spawn; Moedas: AddBloodCoins(pack)
  -> (o bônus de sala limpa em Moedas foi removido: a sala só solta a recompensa da porta)
  -> sorteia CP_k (se k < N), guarda em RunState
  -> ao coletar a fruta (se houver) ou concluir a Bênção, abre as portas da CP_k
Jogador interage com a porta
  -> PendingRoomReward = tipo escolhido; fecha a outra porta (0,3 s)
  -> ScenePortal.ApplyPortalAction (RunState.AdvanceRoom) e ChangeSceneToFile
```

Algoritmo do sorteio (para `RoomRewardCatalog.Roll`, T2):

```
Roll(mapa, k, streak, frutasEscolhidas, rng):
  dois = rng < P2(mapa, k)
  n = dois ? 2 : 1
  tipos = []
  if streak >= maxSemBencao(mapa): tipos += Bencao
  enquanto len(tipos) < n:
      pesos = pesos(mapa, k) sem tipos já escolhidos
      se frutasEscolhidas >= teto: remove Fruta
      tipos += sorteia(pesos)
  streak = Bencao em tipos ? 0 : streak + 1
  devolve tipos, streak
```

---

## 8. Ícones: descrição visual

Cada ícone precisa ser lido em tons de cinza e com daltonismo (protanopia e deuteranopia). A leitura não depende só da cor: a forma e a luminância também diferenciam os três.

| Tipo | Silhueta | Forma | Cor (hex aproximado) | Luminância relativa | Emissão | Rótulo curto |
|---|---|---|---|---|---|---|
| Bênção de Sangue | losango vertical (diamante) com aro fino escuro | ponta em cima e embaixo, borda reta | dourado claro `#E2B94A` | alta (≈ 70%) | dourada suave | Bênção / Blessing / Bendición |
| Fruta do Diabo | esfera com haste curta no topo | círculo, sem ângulos | vermelho-sangue escuro `#8E0F1A` com brilho especular | baixa (≈ 20%) | vermelha fraca, para leitura no piso escuro | Fruta / Fruit / Fruta |
| Moedas de Sangue | três discos achatados empilhados | retângulo escalonado, horizontal | bronze `#B9733B` com aro mais claro | média (≈ 45%) | nenhuma | Moedas +N / Coins +N / Monedas +N |

Regras de leitura:

- **Forma primeiro:** losango, esfera com haste e pilha de discos são formas distintas e sem ambiguidade.
- **Luminância segundo:** dourado claro (alto), bronze (médio) e vermelho escuro (baixo) ficam em três faixas. Em protanopia e deuteranopia, vermelho escuro e bronze podem ficar parecidos em cor, mas não em forma nem em luminância.
- **Emissão só na Fruta:** a Fruta é a única com brilho próprio, para não sumir no piso. Ela não usa o vermelho do portal de saída aberto (`_openPortalMaterial`, em `RunManager.BuildPortalMaterials`), para não ser confundida com a saída de sala.
- **Duas portas, dois ícones diferentes:** garantido pela regra de tipos distintos (3.2).
- A geometria é provisória: losango com `BoxMesh` girado, esfera com `SphereMesh` e cilindro curto, pilha com três `CylinderMesh` achatados. Sem asset externo (A10).
- Teste de validação: capturar as três silhuetas em escala de cinza e com filtro de deuteranopia (ferramenta de simulação de daltonismo), com o piso da sala. [VALIDAR]

---

## 9. Parâmetros para implementar

Convenção: `CompendiumTuning.GetFloat/GetInt` (como em economia-v2 §9). Sugestão: a mesma classe `EconomyTuning` de economia-v2 §9, ou `RoomRewardCatalog` (T2) lendo as mesmas chaves. Nenhum valor abaixo está fora desta lista. Índices de mapa: `.0` a `.2`, com 0 = `armands_dungeons`, 1 = `blood_garden`, 2 = `upper_castle`.

### 9.1 Quantidade de portas

| Chave | Valor | Justificativa (seção) |
|---|---|---|
| `reward.doors.two_chance.base.0` | 0.70 (era 0.30) | revisão 2026-10-24 |
| `reward.doors.two_chance.base.1` | 0.75 (era 0.35) | revisão 2026-10-24 |
| `reward.doors.two_chance.base.2` | 0.80 (era 0.40) | revisão 2026-10-24 |
| `reward.doors.two_chance.step` | 0.05 | 2.3 |
| `reward.doors.two_chance.step_every` | 2 | 2.3 (degrau a cada 2 origens) |
| `reward.doors.two_chance.cap` | 0.90 (era 0.55) | revisão 2026-10-24 (ao menos 10% de porta única) |

### 9.2 Pesos por tipo (por porta, em pontos)

| Chave | Valor | Justificativa (seção) |
|---|---|---|
| `reward.door_weight.blessing.0` | 70 (era 72) | revisão 2026-10-24 |
| `reward.door_weight.blessing.1` | 66 (era 68) | revisão 2026-10-24 |
| `reward.door_weight.blessing.2` | 62 (era 64) | revisão 2026-10-24 |
| `reward.door_weight.fruit.0` | 8 (era 6) | revisão 2026-10-24 |
| `reward.door_weight.fruit.1` | 10 (era 8) | revisão 2026-10-24 |
| `reward.door_weight.fruit.2` | 12 (era 10) | revisão 2026-10-24 |
| `reward.door_weight.coins.0` | 18 (era 22) | revisão 2026-10-24 |
| `reward.door_weight.coins.1` | 20 (era 24) | revisão 2026-10-24 |
| `reward.door_weight.coins.2` | 22 (era 26) | revisão 2026-10-24 |
| `reward.door_weight.ramp_from_fraction` | 0.5 | 3.1 (rampa vale para origens `k ≥ N÷2`) |
| `reward.door_weight.ramp_shift` | 2 | 3.1 (pontos que saem de Moedas e vão para Fruta na rampa; Bênção não muda) |
| `reward.door_weight.anvil.0` a `.2` | 4 / 4 / 4 (era 8 / 10 / 12) | revisão 2026-10-24 (Bigorna rara, só na segunda porta) |
| `reward.door.distinct_types` | — | não implementado; desde a revisão 2026-10-24 só a Bênção pode repetir (linhagens diferentes) |

### 9.3 Garantias

| Chave | Valor | Justificativa (seção) |
|---|---|---|
| `reward.blessing.max_sets_without_blessing.0` | 0 | 3.3 (toda CP do mapa 1 tem Bênção) |
| `reward.blessing.max_sets_without_blessing.1` | 1 | 3.3 |
| `reward.blessing.max_sets_without_blessing.2` | 1 | 3.3 |
| `economy.coins.blessing_fallback` | 75 | já existe (economia-v2 §9.1); vale também para a Bênção de porta sem oferta |

### 9.4 Fruta e Moedas

| Chave | Valor | Justificativa (seção) |
|---|---|---|
| `economy.fruit.run_cap_from_doors` | 3 | 5.3 (**substitui** `economy.fruit.run_cap_from_rooms = 4`) |
| `economy.fruit.max_health` | 15 | já existe (economia-v2 §9.3) |
| `economy.fruit.heal` | 15 | já existe |
| `economy.coins.room_reward_pack.0` | 150 | 4.1 (já existe, economia-v2 §9.1) |
| `economy.coins.room_reward_pack.1` | 130 | 4.1 (já existe) |
| `economy.coins.room_reward_pack.2` | 110 | 4.1 (já existe) |
| `economy.fruit.room_offer_chance` | — | **remover** (substituída pelos pesos da 9.2) |

### 9.5 Interface (para T3 e T4; valores visuais, a validar)

| Chave | Valor | Justificativa |
|---|---|---|
| `reward.door.two_door_offset_cells` | 1 | 6.3 (±1 célula ≈ 1,45 m) |
| `reward.door.two_door_frame_scale` | 0.6 | 6.3 (60% da moldura para caber duas) |
| `reward.door.close_seconds` | 0.3 | 7.2 (tempo de fechamento visível antes da troca de cena) |
| `reward.door.unlock_after_fruit_pickup` | 1 | 6.5 (booleano: portas abrem depois de coletar a fruta) |

### 9.6 Textos (nomes de chave; valores nos três idiomas)

| Chave | pt-BR | en | es |
|---|---|---|---|
| `reward.door.blessing.short` | Bênção | Blessing | Bendición |
| `reward.door.fruit.short` | Fruta | Fruit | Fruta |
| `reward.door.coins.short` | Moedas +{0} | Coins +{0} | Monedas +{0} |
| `reward.door.blessing.hint` | Bênção de Sangue: escolha uma de três. | Blood Blessing: choose one of three. | Bendición de Sangre: elige una de tres. |
| `reward.door.fruit.hint` | Fruta do Diabo: +{0} de vida máxima e cura. | Devil's Fruit: +{0} max health and healing. | Fruta del Diablo: +{0} de vida máxima y curación. |
| `reward.door.coins.hint` | Moedas de Sangue: +{0} nesta run. | Blood Coins: +{0} this run. | Monedas de Sangre: +{0} en esta run. |
| `reward.door.prompt` | Atravessar: {0} | Enter: {0} | Cruzar: {0} |
| `hud.room.choose_door` | Sala limpa · escolha uma porta | Room cleared · choose a door | Sala limpia · elige una puerta |
| `reward.door.fruit.locked` | Pegue a fruta para destravar as portas | Pick up the fruit to unlock the doors | Recoge la fruta para desbloquear las puertas |

Obs.: "Moedas +{0}" usa o valor do mapa que recebe (seção 4.1). Os nomes de chave seguem `reward.*` (conteúdo) e `hud.*` (interface), como em `ui.json`/`content.json`. T2 decide em qual arquivo cada uma fica. Validar com `py -I tools/validate_localization.py` (paridade e placeholders `{0}`).

---

## 10. Divergências entre GDD, docs e código

| # | Fonte | O que diz | Realidade ou proposta | O que atualizar |
|---|---|---|---|---|
| 1 | `GDD.txt` (linhas 325, 566 e 1264) | "Sempre que concluir uma sala, o jogador poderá escolher levar consigo um item", sorteado entre "artefatos roubados, moedas de sangue e melhorias de atributos" | Este modelo de portas concretiza essa ideia, com preview do tipo. "Artefatos" saem das salas: passam a ser vendidos na Loja (economia-v2 §6) e saem do altar do boss. "Melhorias de atributos" viram Bênção de Sangue e Fruta. | Atualizar o GDD ou o adendo quando o plano fechar. O GDD não precisa ser alterado agora. |
| 2 | `GDD.txt` (linhas 559 a 561) | Portas especiais: fechadura de sangue, arena de sacrifício, chave da gárgula | Fora de escopo. Ideias de portas com desafio, não de recompensa. | Nenhuma ação agora. |
| 3 | `docs/design/economia-v2.md` §5.2 e §9.3 | Fruta de sala: 12% das salas e teto 4 | Proposta: pesos por porta (≈15% das CPs) e teto 3 (seção 5). | Atualizar economia-v2 quando aprovado. |
| 4 | `docs/design/economia-v2.md` §10.1 | "No máximo uma recompensa de moedas ou fruta por sala" | Continua válido: a sala pode oferecer as duas, mas só uma é paga. | Nenhuma. |
| 5 | `docs/gameplay_systems.md` (Bênções e Sala limpa) | "Portal aberto" depois da recompensa | Vira "portas abertas" depois da recompensa (seção 6.5). | Atualizar com T3/T4. |
| 6 | Plano, A8 e A9 | A8: recompensa da porta cruzada, entregue ao limpar a sala em que o jogador entrou; A9: sala inicial e Câmara rolam portas | Confirmado com a seção 1 e 7. A seção 1.2 deixa explícito que a CP da Câmara gera a recompensa da Sala 1. | Nenhuma. |
| 7 | `RunManager.CompleteRoom` (código atual) | Sala comum sempre oferece Bênção | Com o novo modelo, a Bênção sai do sorteio (pity, 3.3). | T3. |
| 8 | `docs/room_generation.md` (linha 80) | "A transição só prossegue depois da escolha" (altar de artefatos) | Não muda: o altar é do boss, e a porta de sala também só abre depois da escolha (6.5). | Nenhuma. |

---

## 11. Riscos

### 11.1 Progressão de bênçãos (o maior risco)

- Hoje 25 bênçãos por run (incluindo a Sala Inicial). Com A: 22 ofertas, e 17 escolhidas por um jogador neutro (p10 = 14). Com B: 25 ofertas, 20 escolhidas por neutro.
- Se o playtest mostrar builds fracos no boss 2 ou 3, a alternativa é B, com as Frutas e Moedas em cerca de 43% das salas (segunda porta). Custo de B: Fruta média de 1,4 e Moedas de cerca de 470 por run (neutro).
- Métrica a acompanhar: bênçãos equipadas no boss 2 e no boss 3, por jogador.

### 11.2 Jogador que evita Bênção

- Um jogador que sempre pega Fruta ou Moedas terá 12 bênçãos por run (A) ou 15 (B), contra 25 hoje. Isso é escolha dele, mas é o caso que pode quebrar o build. Mitigação: nenhuma por enquanto; observar nos playtests. Não há penalidade artificial.

### 11.3 Poder dos artefatos e da Loja

- Moedas de porta somam cerca de +20% na renda neutra (734 por run). Com a Loja de economia-v2 (§6.4 e §6.5), isso equivale a cerca de 1 a 1,5 artefato extra por run acima dos 7 previstos. Alavancas: `economy.coins.room_reward_pack.*` × 0,75 (110 / 100 / 80) ou `economy.shop.price.*` × 1,25 (economia-v2 §9.7). Não aplicar as duas juntas sem medir. [VALIDAR]

### 11.4 Geometria: duas portas na mesma parede

- A saída atual tem moldura de 2,3 m (pilares a ±1,15 m, `TestArena.tscn`). Duas portas a ±1 célula precisam de 3 células livres na parede. O gerador (`RoomGenerator`) pode não ter espaço em todas as salas. Se não tiver, a CP vira porta única, o que muda a taxa de 2 portas. Medir a fração de salas sem espaço na captura.

### 11.5 Escolha irreversível e sem confirmação

- Um clique errado custa uma recompensa. Mitigação: rótulo e valor visíveis ao aproximar; a outra porta fecha com 0,3 s de aviso. Se o playtest mostrar cliques errados, adicionar confirmação de segurar o botão (fora desta fase).

### 11.6 Fruta antes da porta

- Com `unlock_after_fruit_pickup = 1`, a porta espera a fruta. Se a fruta cair em um lugar que o jogador não alcança (improvável com `ClampToWalkableArea`), a sala trava. Teste em todas as salas com o gerador de seed fixa.

### 11.7 Fallback de Bênção

- Se o pool de bênçãos elegíveis esvaziar, a porta de Bênção paga `economy.coins.blessing_fallback` (75), como o "todas as bênçãos encontradas" de hoje. Com cerca de 21 ofertas por run e 63 bênçãos, isso deve ser raro. Verificar quantas runs chegam a esse caso.

### 11.8 Limitações da simulação

- A escolha do jogador foi modelada por três políticas simples (preferir Bênção, uniforme, evitar Bênção), sem dependência de vida, de build ou de estado da sala.
- A simulação não considera morte no meio da run (que zera o pity e as frutas) nem o efeito das frutas e dos artefatos sobre a dificuldade. Os números são de oferta e escolha, não de dificuldade.

---

## 12. O que validar jogando

1. **Progressão de bênçãos:** quantas bênçãos o jogador tem no boss 2 e no boss 3, incluindo a Sala Inicial. Com jogo neutro e cenário A, o esperado é cerca de 11 no boss 2 (hoje 15) e cerca de 17 no boss 3 (hoje 25). Se ficar abaixo disso de forma consistente, trocar para B (seção 3.4).
2. **Escolha:** a fração de vezes em que o jogador escolhe Bênção quando há duas portas. Se for quase sempre Bênção, Fruta e Moedas são decorativas; se for quase sempre a outra, a Bênção perde valor.
3. **Frutas:** quantas frutas de porta por run (alvo: 2 com jogo neutro) e se o teto de 3 é atingido com frequência. Se os bosses 3 e 4 ficarem fáceis com 5 a 6 frutas, subir o teto para 4.
4. **Moedas:** renda real de Moedas de porta (alvo: cerca de 730 por run neutro) e o número de artefatos comprados por visita (economia-v2, seção 10.4, item 2).
5. **Leitura:** se o jogador entende as portas sem olhar o rótulo (ícone de forma, cor e luminância) e se o rótulo ao aproximar aparece a tempo.
6. **Posição:** se o jogador lê as duas portas da mesma sala sem confusão e se o lado sorteado não ensina posição.
7. **Sala parada:** se o jogador entende que pode ficar na sala sem punição, ou se acha que o jogo travou.
8. **Fruta antes da porta:** se o jogador entende que precisa pegar a fruta para abrir as portas.
9. **Daltonismo:** capturas em escala de cinza e com simulação de deuteranopia e protanopia (seção 8).
10. **Geometria:** fração de salas com espaço para duas portas (seção 11.4).

---

*Fim do documento. Próximo passo: T2 (`RoomRewardCatalog` e `RunState`) segue as seções 3.2, 7.4 e 9.*

# Comparativo mecânico: Vampire's Prey x Hades x Sworn

Escrito em 2026-10-11 (tarefa T4 do plano `20261011-0815-referencia-hades-sworn`). Base: pesquisa T1 (Hades), T2 (Sworn) e inventário T3 (VP, lido do código).

Convenção de confiança, herdada da pesquisa:
- **[F]** = confirmado em fonte escrita.
- **[C]** = conhecimento do autor, hipótese não confirmada.
- **NV** = não verificado.
- Itens do VP sem marca vêm do código atual. Nada foi testado em jogo.

Regra do projeto: o Hades é a referência primária de decisão. O Sworn entra como segunda opinião, sobretudo em telegraph e coop. O Sworn **não** é jogo de vampiros (Camelot, coop 1 a 4).

---

## 1. Resumo executivo

1. O VP é parecido com o Hades na estrutura (roguelite de ação isométrico, salas com portas e recompensa visível, bênçãos por rodada, meta-progressão no hub), mas bem mais raso em profundidade de combate e de meta.
2. Com o Sworn o parentesco é menor: o Sworn herda do Hades coop, classes, spells e árvores por arma. O VP herda só a estrutura de run.
3. Maior diferença 1: o VP tem **uma arma** (Espada Grande); o Hades tem 6 armas x 4 aspectos [F] e o Sworn 4 classes x 4 armas [F].
4. Maior diferença 2: o VP não tem **Cast**, deflect nem Call; só combo, ataque rápido pós-dash, Especial `Q` e Fúria. O Hades tem 5 capacidades [F].
5. Maior diferença 3: o **telegraph ranged** do VP não tem indicador no chão (só "!"); melee e boss têm. Hades [C] e Sworn [F] marcam o chão.
6. Maior diferença 4: **não há NPC nem diálogo**; o Hades tem 22.000+ linhas [F] e o hub é o motor de retenção.
7. Maior diferença 5: **não há dificuldade ajustável** (Heat/pacto, God Mode, tiers do Sworn).
8. Maior diferença 6: salas e inimigos são uniformes (mesmo material nos 4 mapas, 3 arquétipos, `ModifierCount` sem efeito); Hades e Sworn têm miniboss, elites, eventos, armadilhas e variedade de bioma.
9. Maior diferença 7: o jogador **não sente o dano que toma** (sem hit-stop/shake ao ser atingido, sem som de dano). Efeito pequeno de implementar e grande no feeling.
10. O VP já faz bem: portas com recompensa visível, bênçãos com linhagens e níveis, dash sem custo e hit-stop de golpe. Isso deve ser preservado (seção 4).

---

## 2. Tabela principal

Viabilidade e esforço são estimativas para o VP (solo dev, Godot C#). Esforço: P = até 1 dia, M = alguns dias, G = semanas. Risco: o que pode dar errado no design ou no código.

| Categoria | Hades | Sworn | VP hoje | Diferença | Viab. | Esf. | Risco |
|---|---|---|---|---|---|---|---|
| Combate do jogador | 5 capacidades: Attack, Special, Cast, Call, Dash; boon por slot [F]. Cancel por special/cast/dash [C] | Leve, pesado, spell, dash, ultimate, companion [F]; feeling mais pesado/lento que Hades [F, reviews] | Combo de 3 golpes (32/38/52), ataque rápido só via dash, Especial `Q` (56, cd 1,0 s), Fúria `F` só com bênção; sem cast, bloqueio, deflect | Faltam Cast e ao menos mais uma capacidade; só 1 slot de Especial | Média | G | Quebra a regra "bênção modifica um ataque do moveset" se Cast entrar sem `exclusiveGroup` novo |
| Dash / esquiva | Invencibilidade "generosa" (duração NV) [F]; +1 dash por Mirror [F]; atacar durante o dash encerra a imunidade [F]; dash-strike [F] | Dash com cooldown curto [F]; i-frames NV | Vel. 13,5, 0,154 s (~2,08 u), cd ≈ 0,334 s, buffer 0,28 s, sem cargas e sem custo; invencível também na janela de "dash attack grace" 0,5 s (total ≈ 0,65 s, a confirmar); esquiva perfeita dispara `OnPerfectDodge` sem consumidor | Dash infinito e com janela longa de imunidade; Hades limita por cargas e encerra a imunidade ao atacar | Alta | P | Mexer na janela muda toda a dificuldade; só depois de confirmar em jogo |
| Armas | 6 armas x 4 aspectos (3 visíveis + 1 oculto) [F]; Hammer na run [F] | 4 classes x 4 armas, árvore por arma com nós "flip" [F] | 1 arma (`longsword`); `dual_revolvers` e `whip` placeholders ("Em breve"); visual só aparece no ataque | 1 contra 6 (ou 16) armas; sem aspectos nem upgrade de arma na run | Média | G | Cada arma exige moveset, animação, bênçãos e balanço; escopo explode |
| Movimento / câmera | Isométrico fixo, 8 direções, sem stamina [C] | Isométrico; terreno com lama, água, divisores [F] | Corrida 7,4 u/s, 8 direções (entrada rotacionada 45°), câmera yaw 45°/pitch 60°/dist. 15, shake por seno; sem zoom dinâmico nem lock-on; estamina morta | Equivalente em essência; falta variação de terreno | Alta | P | Baixo |
| Telegraph / leitura | NV em fonte; [C] indicadores vermelhos no chão e armaduras marcadas | "Claramente telegrafados", windups exagerados, marca no chão onde cai [F]; cores/tempos NV | Melee/tank: caixa vermelha que cresce no chão + "!"/"!!", wind-up 0,85 s (tank ~1,40 s), dano no fim da janela travada. **Ranged: só "!" pulsando, sem indicador no chão**, projétil 5,2 u/s. Boss: círculo/linha, wind-up 0,34 a 0,82 s | Falta indicador no chão no ranged; tempo de aviso dos bosses é curto (0,34 s) | Alta | M | Aumentar wind-up muda dificuldade; aplicar a regra "todo perigo tem telegraph" |
| Inimigos | Minibosses com armadura e variados (Doomstone, Inferno Bomber, Wretched Sneak) [F]; Pest voador, Wringer que prende [F] | Comuns com vários padrões, "quase mini-bosses"; arenas pequenas e cheias [F] | 3 arquétipos (Ranged 26,7%, Melee 36,7%, Tank 36,7%); raridades Normal/Magic/Rare/Legendary (vida x1/1,45/2,35/6); `ModifierCount` sem modificador real; dano de contato 5% a cada 0,8 s | Poucos comportamentos; sem elite com sala própria, sem voador, sem inimigo que prende/invoca | Média | M a G | Cada arquétipo novo precisa de telegraph, tuning, visual e entrada no `RollEnemyArchetype` |
| Bosses | 1 por bioma, fases por limiar (Megaera libera golpes aos 75%) [F]; Bone Hydra invulnerável em momentos [F]; Theseus + Asterius [F]; outros NV | Gawain, Percival, Bedivere, Arthur; variedade baixa; salto de dificuldade antes do 1º boss [F] | 4 bosses (vida 5.000 / 14.000 / 17.400 / 25.200); 3 fases (66%/33%), ~5 golpes cada; sem cutscene, diálogo nem invulnerabilidade entre fases | Boss do VP não fala nem reage entre fases; Hades tem diálogo de boss (via sistema de falas) | Alta | M | Vida de boss vs duração-alvo precisa de confirmação (seção 5) |
| Estrutura de salas / run | 4 biomas em ordem fixa, ~12 câmaras por bioma (Tartarus ~14) [F]; salas feitas à mão e recombinadas [F]; armadilhas por bioma [F] | 4 regiões; arenas pequenas, ondas, sobrevivência, parkour, salas de desafio [F] | 4 mapas (6/8/10 salas + boss; mapa 4 só boss); 4 waves x 5 inimigos por sala; layout procedural em 4 regiões 9x9; mesmo material nos 4 mapas (`SpecialRule` não é lido); sem armadilha, evento, baú, destrutível, perigo ambiental, minimapa | Falta variedade de sala e identidade por mapa | Média | G | Gerador procedural não tem modos de sala; mexer nele afeta ondas e portas |
| Portas / recompensas | Porta mostra símbolo; caveira = elite; exclamação = NPC; sala de Charon [F]; Fated Authority (reroll) [F] | Portas mostram recompensa; portais custam HP; portas corrompidas (1.2) [F]; escolha entre 2 Fae Lords [F] | 1 a 2 portas, tipos Bênção/Fruta/Moedas, ícone provisório; recompensa no chão na sala seguinte; toda saída tem Bênção (fase atual) | Conceito igual. VP tem 3 tipos; Hades tem ~10 e salas especiais; falta porta de elite/NPC/risco | Alta | M | Ver `portas-recompensas.md` (números ainda "a validar") |
| Boons / bênçãos | 9 deuses, 4 raridades, Pom sobe nível; Duo, Legendary, Chaos [F]; somem ao morrer [F]; Mirror aumenta chance de raridade [F] | 200+ bênçãos, 8 Fae Lords, duo, ultimates, raridade pela árvore Devotion [F] | 63 bênçãos, 4 linhagens + Stirps híbridas; 5 raridades (poder x1/2/3,5/5,5/7); níveis N1-N10 (+25%/nível) só no Altar; oferta = 3 de uma só linhagem; reroll só pela skill (1/mapa) | VP é mais rico em raridade/nível; falta "Pom" na run (nível fora do Altar), Legendary com pré-requisito, Chaos | Média | M | Mais um caminho de nível pode exagerar poder; respeitar bênção única por run |
| Artefatos / itens | Keepsakes por Nectar [F]; Hammer [F]; loja do Charon [F] | Chifres reforçam bênçãos, bigornas modificam armas [F] | 20 artefatos passivos; só na Loja da Câmara; raridade Common a Mythic (x1,75) | Falta item equipável de relacionamento (keepsake) e mod de arma | Média | M | Artefato é só passivo (regra do projeto) |
| Meta-progressão | Mirror com versões primária/alternativa; 7 moedas; Pact of Punishment com 15 modificadores, Heat máx. 61 [F] | Árvores do Merlin, Nimue (flip); 6 moedas; Codex/wishlist (1.2) [F] | 16 skills permanentes (8 Bestialis + 8 Humanitas), custo base x (1+0,5 x rank) em Sangue Amaldiçoado; sem Heat/pacto, sem keepsakes; morte = volta ao hub sem penalidade | Falta camada de rejogabilidade após a 1ª vitória (Heat) | Média | M a G | Heat exige tuning de todos os inimigos e bosses |
| Dificuldade / acessibilidade | God Mode: 20% de resistência, +2% por morte, teto 80% [F]; Hell Mode desativa [F]; outras opções NV | 5 tiers (Squire a Sovereign) + Corruption [F] | Sem seletor de dificuldade, remapeamento, legendas, daltonismo, escala de UI, redução de shake ou vibração | Nada de acessibilidade de jogo; só idioma, resolução, tela cheia e volume | Alta | M | Escalar dano do inimigo mexe no TTK de todos os mapas |
| Hub / NPCs | House of Hades, NPCs, keepsakes, Nectar [F] | Carmarthen: Merlin, Nimue, Guinevere, Fisher King, Lancelot; serviços destravam ao resgatar corte [F] | Caixão de Virgil, Fonte de Sangue, bancada de armas, portal, sala de treino, 3 sarcófagos placeholder (Selina, Magdalena, Dominicus); **sem NPC, diálogo ou cutscene** | O hub é funcional mas sem personagens | Alta | M | Depende do plano de diálogo; `personagens-futuros.md` define o elenco |
| Diálogo / narrativa | 22.000+ linhas dubladas, escolhidas por estado do jogo [F]; prioridade/cooldown [C]; aspecto oculto liberado por conversa [F] | Narrativa enxuta, poucos diálogos [F] | Mínima (flavor texts); sem tela de vitória/final | VP está abaixo do próprio Sworn aqui | Alta | M | Escrita é o trabalho caro; comece com 1 NPC |
| UI / HUD | NV em fonte | Ruído visual (às vezes não se sabe o que acertou) [F, reviews]; HUD NV | HUD por código; 3 idiomas; teclado, mouse, Xbox, PlayStation; Compêndio; pausa | Estável; falta legenda, remapeamento e opções de acessibilidade | Alta | M | Baixo |
| Áudio / game feel | Hit-pause curto/shake leve [C]; voz dublada [F] | NV | Hit-stop por `Engine.TimeScale` 0,18 (22 ms leve / 38 ms pesado, +10 ms crítico); shake 0,035/0,07 s (leve), 0,06/0,10 s (pesado); número de dano 3D; flash vermelho no inimigo; **sem hit-stop/shake ao jogador ser atingido, sem som de dano, passos, UI ou portas, sem rumble** (trecho lido parcial) | Feeling do golpe dado é bom; o do golpe recebido é nulo | Alta | P | Baixo; shake ao ser atingido precisa de opção de redução |
| Coop | Não tem (solo) | 1 a 4 jogadores, revive custa HP máx. do grupo [F] | Solo | Fora de escopo | Baixa | G | Reescrita de rede, câmera e escala; não recomendado |

---

## 3. Principais diferenças destacadas

Ordenadas por impacto no feeling dividido pelo custo.

### 3.1 Telegraph do ataque à distância sem indicador no chão
- **Difere:** melee/tank/boss têm marca no chão; ranged só tem "!" e projétil de 5,2 u/s. O Sworn marca o chão [F] e o Hades usa indicadores [C].
- **Por que importa:** o projétil ranged é a causa mais provável de "tomei dano sem ver". Quebra a regra do projeto "todo perigo tem telegraph".
- **Implementar:** linha (ou cone fino) no chão na direção do disparo, crescendo durante o wind-up, na mesma classe que desenha a caixa do melee (`EnemyBasic`, `EnemyProjectile` em `scripts/enemies` e `scripts/combat`). Dono: `inimigos-e-bosses`.
- **Vale?** Sim. Coerente com o design atual e barato. Plano `combate-leitura-visual` T3.

### 3.2 Tempo de aviso dos bosses e dos inimigos
- **Difere:** wind-up de boss de 0,34 a 0,82 s; comum 0,85 s; tank ~1,40 s. Duração de aviso no Hades e no Sworn: NV.
- **Por que importa:** 0,34 s fica no limite do tempo de reação. É dificuldade por surpresa, não por habilidade.
- **Implementar:** definir um aviso mínimo em `BossCatalog` (por golpe) e em `EnemyTuningCatalog`; qualquer aumento de wind-up é registrado e revisto aqui, pois muda o balanço.
- **Vale?** Sim, com revisão de balanço do game-designer. Não copiar valor do Hades (NV).

### 3.3 Dano sofrido sem feedback (hit-stop, shake, som)
- **Difere:** o VP tem feedback forte ao dar dano e quase nenhum ao receber (só vinheta e stagger 0,24 s). Hades: hit-pause curto/shake leve [C].
- **Por que importa:** o jogador não aprende o que dói e o combate parece "mudo".
- **Implementar:** em `CombatFeedback` (autoload) um `OnPlayerHit` com hit-stop ~30 a 50 ms, shake curto e SFX de dano; chamado pelo `Health`/`PlayerController` do jogador. Opção "reduzir shake" em settings.
- **Vale?** Sim. Plano `combate-leitura-visual` T2.

### 3.4 Dash: i-frames longos, sem cargas e sem custo
- **Difere:** dash do VP é infinito e a imunidade pode durar ~0,65 s por "grace" (a confirmar). Hades dá "janela generosa" [F] mas limita por cargas (Mirror +1) e encerra a imunidade ao atacar [F].
- **Por que importa:** com dash quase contínuo e invencível, o telegraph perde valor; é o maior fator oculto de dificuldade.
- **Implementar:** primeiro medir em jogo. Se confirmado, encurtar a janela de grace em `PlayerCombat`/`PlayerController`, ou dar 2 cargas com recarga em vez de cooldown fixo; usar `DodgeCost`/estamina já existentes como base. Dono: game-designer.
- **Vale?** Sim, depois de medir. Não tocar antes.

### 3.5 Segundo e terceiro botões: Cast e Call
- **Difere:** Hades tem Cast (ataque à distância com munição, recarrega) [F] e Call (ultimate). Sworn tem spell com cooldown [F]. O VP só tem Especial `Q`.
- **Por que importa:** é o que dá ao jogador ranged um jeito de punir inimigos ranged e variedade de moveset.
- **Implementar:** novo `Skill` ou ataque em `PlayerCombat` (projétil do jogador reaproveitando `EnemyProjectile`), com carga e recarga; nova entrada de `exclusiveGroup` para bênçãos de Cast; mapeamento de input e slot no HUD. Dono: game-designer + `conteudo-run` + `ui-hud`.
- **Vale?** Sim, mas é a mudança de design maior. Combina com Bathory/Karnstein (alcance e sedução). Exige plano próprio.

### 3.6 Armas e aspectos
- **Difere:** 1 arma contra 6 x 4 aspectos [F].
- **Por que importa:** é o motor de rejogabilidade do Hades; no VP a build só varia pelas bênçãos.
- **Implementar:** `WeaponCatalog.cs` (array C#) e `WeaponVisualCatalog.cs`; por arma, moveset em `PlayerCombat`. Começar por 1 arma nova (a mais barata em animação) e 1 "aspecto" como variação de números do `longsword`.
- **Vale?** Vale, em escala pequena. Arma nova completa é esforço G; aspecto como variação de arma existente é M.

### 3.7 Heat / Pacto (dificuldade opcional)
- **Difere:** Pact of Punishment, 15 modificadores, Heat máx. 61 [F]; Sworn tem tiers e Corruption [F]; VP não tem nada.
- **Por que importa:** é o que dá motivo para rejogar depois da primeira vitória.
- **Implementar:** `GameTuning`/`RunState` com lista de modificadores (+dano, +vida, +velocidade, relógio) aplicados em `EnemyTuningCatalog` e `BossCatalog`; menu no hub (sarcófago ou fonte). Usar `ModifierCount` já presente como gancho.
- **Vale?** Sim, mas só depois que o combate estiver estável. Tema encaixa: "pacto de sangue".

### 3.8 God Mode / acessibilidade de dificuldade
- **Difere:** Hades: 20% de resistência, +2% por morte, teto 80% [F]. VP: nada.
- **Por que importa:** retém jogadores casuais; baixo custo.
- **Implementar:** campo em `CharacterSheet` (contador de mortes e opção ligada) lido por `Health` ao aplicar dano ao jogador. Opção em `UiComponents.OptionsPanel`, textos nos 3 idiomas.
- **Vale?** Sim, ganho rápido. Cuidado com conquistas (não existem hoje).

### 3.9 NPCs, diálogo e hub vivo
- **Difere:** Hades tem 22.000+ linhas [F] e o diálogo libera mecânica (aspecto oculto) [F]. VP não tem NPC.
- **Por que importa:** é o laço emocional entre as runs; sem ele o hub é só menu 3D.
- **Implementar:** já planejado em `sistema-dialogo` (catálogo em `scripts/systems/dialogue/`, UI em `HUD.Dialogue.cs`). Elenco de `personagens-futuros.md`.
- **Vale?** Sim. Fazer 1 NPC e falas de run curtas primeiro.

### 3.10 Salas e inimigos variados (elites, eventos, armadilhas, identidade por mapa)
- **Difere:** os 4 mapas usam o mesmo material (`SpecialRule` não é lido); sem elite com sala própria, evento, baú, armadilha. Hades tem caveira = elite, sala de NPC, armadilhas por bioma [F]; Sworn tem salas de desafio [F].
- **Por que importa:** cada mapa parece o mesmo; a decisão de porta só tem 3 tipos.
- **Implementar:** ler `SpecialRule` em `RoomGenerator`; material por mapa (`CampaignCatalog`); porta de Elite como 4º tipo em `RoomRewardCatalog`; armadilha simples (espinhos com telegraph).
- **Vale?** Sim em partes: material por mapa e `SpecialRule` primeiro (P/M); elite e evento depois.

### 3.11 Boss que reage (fase, invulnerabilidade, fala)
- **Difere:** Hades tem invulnerabilidade em momentos [F] e libera golpes por limiar [F]; falas de boss pelo sistema de diálogo. VP: transição sem pausa.
- **Por que importa:** momento de pico da run; dá leitura de fase.
- **Implementar:** pausa curta com invulnerabilidade e flash em `BossController` ao cruzar 66%/33%, mais gancho para falas do `sistema-dialogo`.
- **Vale?** Sim. Barato e coerente com "fases legíveis".

### 3.12 Skills ligadas ao dash (esquiva perfeita)
- **Difere:** `OnPerfectDodge` já dispara, mas nenhuma skill usa. Hades tem Ruthless Reflex (+50% dano/esquiva por 2 s) [F].
- **Por que importa:** recompensa a esquiva no tempo certo, o que reforça a leitura de telegraph.
- **Implementar:** uma skill Humanitas em `skills.json` e consumo em `PlayerSkillController`. Dono: `conteudo-run`.
- **Vale?** Sim; só depois de decidir os i-frames (3.4), senão a esquiva perfeita fica trivial.

---

## 4. O que o VP já faz igual ou melhor (não mexer)

| Item | Por quê |
|---|---|
| Portas com recompensa visível e escolha | Mesmo conceito do Hades [F] e do Sworn [F]; o desenho em `portas-recompensas.md` está bem fundamentado |
| Sistema de bênçãos: linhagens, 5 raridades, níveis N1-N10, Stirps | Mais camadas que o Hades (4 raridades) [F]; identidade temática forte |
| Hit-stop e shake do golpe dado | Valores definidos e diferenciados (leve, pesado, crítico) |
| Dash sem custo, buffer de 0,28 s e cancel dos golpes | Responsivo; evita o "sluggish" apontado no Sworn [F, reviews] |
| Aim assist por golpe e entrada de teclado/mouse/2 controles | Acessível; Sworn só liga/desliga [F] |
| Telegraph melee/tank com caixa que cresce | Já bom; só estender ao ranged |
| Localização em 3 idiomas, com validador | Hades/Sworn não são referência aqui |
| Skills permanentes e artefatos passivos, sem ativação | Regra de design própria, mantém o combate limpo |

---

## 5. Achados do inventário T3 a tratar como bugs ou inconsistências

Todos **a confirmar em jogo** (ou com leitura mais detalhada do código). Nenhum foi reproduzido.

| # | Achado | Onde | Hipótese | Ação |
|---|---|---|---|---|
| 1 | Dano de raridade possivelmente sobrescrito por `ApplyCommonEnemyTuning` | `EnemyBasic`/`EnemyTuningCatalog` | Inimigo Magic/Rare/Legendary pode não causar o dano da raridade | A confirmar em jogo: comparar dano de um Normal e de um Legendary da mesma classe |
| 2 | I-frames do dash ≈ 0,65 s (0,154 s do dash + 0,5 s de "dash attack grace") | `PlayerCombat`/`PlayerController` | Janela de imunidade bem maior que a do Hades pode tornar o telegraph irrelevante | A confirmar em jogo: medir quanto tempo o jogador não leva dano após o dash |
| 3 | Estamina existe (100, regen 28/s) mas `DodgeCost = 28` nunca é consumido | `PlayerController` | Código morto, ou decisão de design de dash livre | A confirmar: decidir remover ou usar (ver 3.4) |
| 4 | Vida de boss (5.000 / 14.000 / 17.400 / 25.200) muito abaixo da duração-alvo | `BossCatalog` | Fórmula `TargetDurationSeconds x ExpectedPlayerDps`; DPS nominal do combo ~165 contra o DPS esperado usado | A confirmar em jogo: tempo real de cada luta contra a duração-alvo |
| 5 | `SpecialRule` do mapa não é lido | `CampaignCatalog`/`RoomGenerator` | Regra especial por mapa nunca é aplicada | A confirmar: ler o uso; decidir implementar (3.10) ou remover |
| 6 | Docs desatualizadas | `docs/room_generation.md:80` (altar de artefatos do boss, removido); `perfil.md` cita `ArtifactChest`/`ArtifactOffer` inexistentes; README com pesos de raridade de bênção diferentes do JSON; `economia-v2.md` cita sorteio antigo | Docs defasadas | Corrigir texto (não é bug de jogo) |
| 7 | Esquiva perfeita (`OnPerfectDodge`) sem consumidor | `PlayerCombat` | Efeito pronto, sem skill | A confirmar: ver 3.12 |
| 8 | `ModifierCount` só usado no Compêndio | `EnemyTuningCatalog` | Raridades Magic/Rare não têm modificador real | Ver 3.7 e 3.10 |

---

## 6. Priorização e ligação com os planos

### 6.1 Níveis

| Nível | Item | Esf. | Plano / dono |
|---|---|---|---|
| **Ganho rápido** | Hit-stop/shake/som ao ser atingido (3.3) | P | `combate-leitura-visual` T2 |
| | Indicador no chão no ranged (3.1) | P a M | `combate-leitura-visual` T3 |
| | Aviso mínimo dos bosses (3.2) | P | `combate-leitura-visual` T3 + revisão do game-designer |
| | Medir i-frames, dano de raridade, vida de boss (seção 5) | P | game-designer, em jogo |
| | God Mode simples (3.8) | P a M | plano novo (acessibilidade) |
| | Material por mapa e leitura de `SpecialRule` (3.10) | P a M | plano novo (variedade) |
| | Corrigir docs desatualizadas (seção 5, item 6) | P | game-designer |
| **Médio** | Telegraph e animações do jogador (anticipation, trail) | M | `combate-leitura-visual` T2 |
| | NPC do hub e diálogo (3.9) | M | `sistema-dialogo` |
| | Boss com pausa de fase e falas (3.11) | M | plano novo ou `combate-leitura-visual` T3 (parte visual) + `sistema-dialogo` (falas) |
| | Esquiva perfeita como skill (3.12) | M | plano novo, depois de decidir os i-frames |
| | Dash com cargas e janela de grace revisada (3.4) | M | plano novo (balanço do jogador) |
| | Elite como tipo de porta, 1 armadilha com telegraph (3.10) | M | plano novo (variedade), com `portas-recompensas` |
| **Grande** | Cast (3.5) | G | plano novo |
| | Novas armas e aspectos (3.6) | G | plano novo |
| | Heat/pacto (3.7) | G | plano novo |
| | Variedade de inimigos (voador, "prende", invocador) | G | plano novo |
| | Coop | G | descartado |

### 6.2 O que alimenta cada plano irmão

**`20261011-0815-combate-leitura-visual` (telegraph e animações)**
- Indicador no chão também para ranged, com preenchimento progressivo e mesmo código visual do melee (3.1).
- Aviso mínimo de ~0,5 s? Não há valor de referência verificado: o Hades e o Sworn têm tempos NV. Definir pelo teste de leitura no VP; qualquer mudança de wind-up passa pelo game-designer.
- Hit-stop, shake e SFX ao jogador ser atingido (3.3), com opção "reduzir shake".
- Cores por tipo (melee, ranged, tank, boss) e silhueta; não depender só de cor (daltonismo).
- Anticipation e trail do golpe do jogador; hoje o arco/círculo vermelho dura só ~0,14 a 0,16 s e o visual da arma só aparece durante o ataque.
- Ruído visual com muitos inimigos (Sworn [F, reviews]): conferir se 5 inimigos por wave + projéteis ainda leem bem.

**`20261011-0815-sistema-dialogo` (diálogo)**
- Estrutura de prioridade: falas por requisito + prioridade, com repetíveis (cooldown) e únicas [C]; o Hades escolhe por estado do jogo [F].
- Gatilhos mínimos: primeira vez no hub, boss derrotado, número da run, morte. O VP já sabe tudo isso em `RunState`/`CharacterSheet`.
- Falas de run curtas na entrada de sala e na fase do boss (3.11).
- Diálogo que libera mecânica (aspecto oculto, Hades [F]): guardar para quando houver armas/aspectos.
- Elenco e tom: `docs/design/personagens-futuros.md`. O Sworn tem narrativa enxuta [F]; começar com 1 NPC é suficiente.

**Também existe `20261011-0815-revisao-graficos`** (não lido por completo nesta tarefa): resolução, vsync/FPS e qualidade gráfica, que o inventário diz que não existem, ficam com ele. Daltonismo, escala de UI e redução de shake podem cair aqui ou num plano de acessibilidade; decidir para não duplicar.

### 6.3 O que **não** está coberto por nenhum plano salvo

- Cast / ataque à distância do jogador (3.5).
- Armas novas e aspectos (3.6).
- Heat / pacto de dificuldade (3.7) e God Mode (3.8).
- Elites, eventos, armadilhas e variedade entre mapas (3.10).
- Dash: cargas, janela de grace, uso de estamina (3.4) e skill de esquiva perfeita (3.12).
- Variedade de inimigos e de bosses (voador, prisão, invocador).
- Remapeamento, legendas e vibração (parcialmente dependem de `revisao-graficos`).
- Tela de vitória/final e narrativa pós-Armand.
- Confirmação em jogo dos achados da seção 5.

### 6.4 Recomendação para os próximos planos

1. Fechar `combate-leitura-visual` e `sistema-dialogo` como estão, com os ajustes da seção 6.2.
2. Plano novo A, "balanço de combate do jogador": medir e decidir o dash (3.4), dano de raridade, vida de boss; só então esquiva perfeita (3.12).
3. Plano novo B, "variedade e acessibilidade": material por mapa, `SpecialRule`, God Mode, opção reduzir shake.
4. Plano novo C, "Cast + 1 arma nova": o item de maior ganho de rejogabilidade; depende do A.
5. Heat/pacto e elites só depois de C.

---

## 7. Limitações da pesquisa

- **Sem vídeo e sem jogo:** nada foi assistido nem jogado; Hades e Sworn vêm de texto e o VP vem de leitura de código, sem execução.
- **Fontes bloqueadas:** Fandom e Prima ficaram inacessíveis para o Hades; as fontes usadas foram Fextralife, RPG Site, Kotaku, Gamepressure, GDC/Gamasutra e Can I Play That.
- **Valores do Hades marcados [C] (hipótese):** cancel de animação por special/cast/dash, hit-pause curto e shake leve, sem stamina, isométrico fixo em 8 direções, indicadores vermelhos no chão e armaduras marcadas, prioridade/cooldown/repetição de falas.
- **NV no Hades:** duração e distância exata dos i-frames do dash; wind-ups, combos e hit-stop por arma; tempos e cores de telegraph; fases dos bosses em detalhe; duração e tamanho de sala; outras opções de acessibilidade além do God Mode; HUD.
- **NV no Sworn:** existência de stamina e parry (provavelmente não); números de dano, i-frames e cooldowns; hit-stop e shake; tempos e cores de telegraph; mapa de rotas; HUD; fases de boss; detalhes de narrativa.
- **VP:** o inventário T3 foi lido do código; o trecho de áudio foi lido parcialmente. Os achados da seção 5 não foram reproduzidos em jogo, e os números de portas e economia seguem como "proposta a validar" em `portas-recompensas.md`.
- **Não lidos por completo nesta tarefa:** `docs/design/economia-v2.md` e `docs/design/personagens-futuros.md` (usados só pelo resumo do material de pesquisa).
- Viabilidade, esforço e risco da seção 2 são estimativas do autor.

# Auditoria visual e de UX da UI — Vampire's Prey

> Fase 1 do plano `20261004-1829-hud-auditoria`, tarefa T1. Data: 2026-10-04. Commit de referência: `0c387de`.
> Escopo: todas as telas e overlays do jogo. Esta fase **não altera código**: é o diagnóstico que alimenta o guia de estilo (T3, `docs/design/ui-guia.md`).
> Inventário técnico complementar (métodos, `CallGroup`, pausa, foco): `docs/design/ui-inventario.md` (T2).

## Método

- **Leitura do código de construção:** `scripts/ui/HUD*.cs`, `MainMenu.cs`, `UIThemeFactory.cs`, `ControllerUiSupport.cs`, `GameCursor.cs`, `scripts/systems/IntermissionChamber.cs` e os `Label3D` de prompts (`ArtifactChest.cs`, `BloodFountain.cs`, `WeaponBench.cs`, `ScenePortal.cs`, `RunManager.cs`, `RoomGenerator.cs`, cenas `.tscn`). As telas que exigem interação (ficha, ofertas, fonte, baú, bancada, compêndio, game over) foram avaliadas pelo código: offsets, tamanhos, cores, fontes e StyleBoxes.
- **Capturas** com `Godot.exe --path . --resolution <res> --fixed-fps 30 --write-movie <scratch>/f.png --quit-after 300 res://scenes/<cena>.tscn`. Os PNGs ficaram fora do repositório. Nomes citados nas tabelas:

| Captura | Cena e resolução | O que mostra |
|---|---|---|
| `menu/f280` | `ui/MainMenu`, 1600×900 | página principal com foco em "Jogar" |
| `arena/f060` | `game/TestArena`, 1600×900 | menu de pausa aberto (com o HUD ao fundo) |
| `arena/f150` | `game/TestArena`, 1600×900 | HUD completo sem combate, log com 1 entrada |
| `arena/f245` | `game/TestArena`, 1600×900 | telegraph vermelho de investida e "!" de alerta |
| `arena/f256` | `game/TestArena`, 1600×900 | dano recebido: flash vermelho em tela cheia e número "-18" |
| `arena/f270` | `game/TestArena`, 1600×900 | vida 83/100, painel vazio e log com 2 entradas |
| `nest/f270` | `game/CrowsNest`, 1600×900 | hub: painel de recursos e rótulos 3D |
| `blessing/f230` | `game/RunStartBlessingRoom`, 1600×900 | sala da primeira bênção |
| `inter/f280` | `game/IntermissionChamber`, 1600×900 | câmara com o título sobre a moldura do HUD |
| `arena720/f270` | `game/TestArena` (2ª execução, com outro sorteio de inimigos) | log do canto inferior direito **sobre inimigos** (anéis rosa e ciano), projétil e telegraph laranja |
| `menu720/f280`, `menu1080/f280` | `ui/MainMenu` | idênticas à `menu/f280` (ver nota) |

- **Nota sobre as resoluções:** com `--resolution 1280x720` e `1920x1080`, o Movie Maker continuou gravando em 1600×900 (o log diz "recording movie in 1600×900"), porque grava o viewport base e não a janela. Por isso a escala em 720p e 1080p (§21) foi avaliada pelo cálculo do stretch, não por imagem.
- **Ignorado:** o contador de FPS das capturas vem distorcido pela gravação.

**Severidade**

| Nível | Significado |
|---|---|
| **Alta** | Atrapalha jogar ou ler o combate, perde progresso, ou bloqueia o jogador de controle. |
| **Média** | Problema de usabilidade, consistência ou entendimento. |
| **Baixa** | Polimento. |

---

## Resumo executivo — os 10 problemas principais

| # | Sev. | Problema | Onde |
|---|---|---|---|
| 1 | Alta | **O vermelho em tela cheia apaga os telegraphs.** O flash de dano (alpha 0,28) e a "vinheta" de vida crítica são `ColorRect` vermelhos e uniformes que cobrem a tela inteira (a vinheta chega a ~0,26 de alpha pulsando). No instante do golpe, e o tempo todo com vida baixa, o piso inteiro fica avermelhado, justamente na cor dos telegraphs. | `HUD.StatusBars.cs:102-117,399,469`; `arena/f256` |
| 2 | Alta | **Vermelho com papéis demais.** O mesmo vermelho saturado serve para vida do HUD, telegraph, prompts 3D de interação, número de dano recebido, raridade Stirps, botão "Desistir" e o título "SANGUIS PURGATUS". O jogador não aprende que "vermelho no chão = perigo". | `HUD.StatusBars.cs:288`; `ArtifactChest.cs:33`; `HUD.BlessingOffer.cs:386`; `arena/f245`, `blessing/f230` |
| 3 | Alta | **Informação essencial só aparece com mouse.** Ficam inacessíveis no controle: os tooltips da barra de bênçãos, da fúria, das linhas da ficha e dos termos destacados (`[hint]`); os detalhes de skill (só com clique direito); e o zoom e o pan da Fonte de Sangue. | `HUD.BlessingBar.cs:205`; `HUD.BloodFountain.cs:165-172`; `HUD.cs:326-352`; `HUD.BlessingOffer.cs:486-516` |
| 4 | Alta | **Aviso de wave e mensagens de jogo dividem um único toast** de 0,55 s, sem fundo, no centro-alto da tela. O "PROXIMA WAVE" some antes de ser lido e é sobrescrito pelo próximo "+1 inimigo derrotado". | `HUD.Notifications.cs:25,47,89,95,159-181`; `HUD.StatusBars.cs:227-243` |
| 5 | Alta | **Ações destrutivas sem confirmação:** "Excluir" slot no menu principal (apaga o save na hora), "Desistir da run" e "Voltar ao menu principal" na pausa. | `MainMenu.cs:133-134`; `HUD.Pause.cs:114-127,391-415` |
| 6 | Alta / Média | **O HUD de recursos e o log têm cara de debug.** O log do canto inferior direito, sem fundo, **cobre inimigos** quando o combate chega ao canto (Alta). Ele também mostra o horário do sistema ("[18:37:45]"), quebra linhas e fica a 55% de opacidade. O painel superior esquerdo é texto cru em 3 linhas ("Inimigos: 0/0" até no hub). O painel do jogador tem ~60 px vazios abaixo da vida. | `HUD.StatusBars.cs:19-20,119-163`; `HUD.Notifications.cs:12`; `arena720/f270`, `arena/f270`, `nest/f270` |
| 7 | Média | **As telas não seguem o mesmo padrão.** Só a oferta de bênçãos e o altar usam `ChoiceCard` com moldura. Oferta de artefatos, baú, bancada, ficha, fonte, game over e pausa são `ColorRect` sem moldura, com título em 17 px e branco (sem o dourado). A oferta de artefatos é uma lista de `Button` de texto puro, sem ícone nem cor de raridade. | `HUD.ArtifactOffer.cs:366-376`; `HUD.ArtifactChest.cs:11-35`; `HUD.WeaponBench.cs:301-323`; `HUD.Pause.cs:11-64` |
| 8 | Média | **Ferramentas de desenvolvimento expostas ao jogador no build de release.** A pausa traz "Parametros de gameplay" (sliders de dano e vida do jogador), e o "Dicionario" mostra caminhos de arquivo (`data/artifacts/artifacts.json`), IDs internos e `SpinBox` que editam o balanceamento. | `HUD.Pause.cs:112,123`; `HUD.Compendium.cs:184,190,199,223,246` |
| 9 | Média | **Idioma quebrado.** O menu principal está localizado, mas todo o HUD e os overlays estão em português fixo e sem acentos ("Opcoes", "Bencao", "BAU", "Forca"). Também aparecem enums crus em inglês ("Rare", "Weapon", "[Epic]"). Quem escolhe `en`/`es` vê o jogo em português. | `HUD.Pause.cs:111-127`; `HUD.CharacterSheet.cs:113-127,183`; `HUD.BlessingOffer.cs:322`; `HUD.ArtifactOffer.cs:370` |
| 10 | Média | **O foco do controle é fraco e incompleto.** O foco de `Button` é só uma borda dourada de 1 px. `OptionButton`, `CheckButton`, `HSlider` e `SpinBox` caem no tema padrão do Godot (cinza). O card de bênção em foco não se destaca, só o botão "Escolher". O "Trocar oferta" não é alcançável pelo d-pad. Nenhuma tela mostra dicas de botão (A/X confirmar, B/Círculo voltar). | `UIThemeFactory.cs:26-36`; `HUD.cs:299-306`; `HUD.BlessingOffer.cs:271-277` |

---

## 1. HUD em jogo — painel do jogador (vida, barreira, fúria)

`HUD.StatusBars.cs:19-88, 284-337, 432-490`. Capturas `arena/f150`, `arena/f270`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | A moldura tem 124 px de altura (`-142..-18`), mas o conteúdo ocupa ~50 px: rótulo de 22 px, barra de 22 px e a fúria escondida até ser desbloqueada. Sobra um retângulo vazio de ~60 px. | `HUD.StatusBars.cs:19,30-33,70`; `arena/f270` | Altura da moldura dinâmica, no tamanho do conteúdo (`PanelContainer` envolvendo o `VBox`, em vez de duas camadas com offsets independentes). Sem fúria: ~64 px; com fúria: ~92 px. |
| Média | A barra de fúria não tem rótulo, ícone, estado "pronta" nem dica de tecla. Fica laranja com 22 px, igual à vida. A explicação só aparece no tooltip (mouse). | `HUD.StatusBars.cs:69-70,321-336,478-482` | Barra mais fina (10-12 px) logo abaixo da vida, com rótulo curto "FÚRIA". Ao encher: brilho ou pulso na borda e dica do botão (`F` / `R1`). Quando ativa: drenagem visível (borda mais clara). |
| Média | O rótulo "VIRGIL" (13 px, tom pardo) ocupa uma linha inteira e não informa nada durante o combate. | `HUD.StatusBars.cs:37-45` | Remover, ou fundir numa linha discreta com os status ativos e os ícones de buff. Reaproveitar o espaço para a fúria. |
| Média | A vida usa o mesmo vermelho saturado `(0.86,0.02,0.04)` dos telegraphs e prompts. Com vida cheia, a barra é um bloco vermelho de 380 px no canto da tela. | `HUD.StatusBars.cs:288`; `arena/f245` | Vida em carmesim mais escuro e menos saturado (ex. `#9E1020`) com realce superior claro. Reservar o vermelho-alaranjado vivo para perigo (telegraphs). Ver padrão transversal T1. |
| Baixa | O fundo vazio da barra é `(0.12,0,0)` sólido, sem borda. Não fica claro onde a barra termina quando a vida está baixa. | `HUD.StatusBars.cs:313-320`; `arena/f270` | Borda de 1 px no trilho e marcações discretas a cada 25%. |
| Baixa | Não há "chip" de dano (faixa que esvazia com atraso) e o valor muda instantaneamente. O dano recebido só fica legível pelo número flutuante. | `HUD.StatusBars.cs:432-452` | Faixa clara atrás da vida que acompanha o valor com 0,3-0,5 s de atraso. É feedback barato e muito legível. |
| Baixa | O texto "83 + 20 / 100" com barreira muda a cor do texto inteiro para azul claro. Lido rápido, parece que a vida máxima mudou. | `HUD.StatusBars.cs:447-450` | Manter "83/100" em branco e mostrar "+20" num rótulo azul separado, à direita da barra. |
| — | A barreira azul integrada à barra de vida funciona e se distingue bem do vermelho. | `HUD.StatusBars.cs:305-312,441-444` | Preservar (ver "Não mexer"). |

## 2. HUD — painel de recursos (superior esquerdo)

`HUD.StatusBars.cs:20,129-163`. Capturas `arena/f150`, `nest/f270`, `blessing/f230`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | São três linhas de texto cru ("Moedas de Sangue: 0 / Arma: Espada Grande / Inimigos: 0/20") numa caixa, sem ícones nem hierarquia. Parece um painel de debug. | `HUD.StatusBars.cs:142-163`; `arena/f150` | Moedas: ícone (gota ou moeda) + número grande. Progresso de inimigos: movido para junto do título da sala ("Sala 1/6 · 5/20"). Arma: só na ficha, ou ícone pequeno. |
| Média | "Inimigos: 0/0" aparece no hub e na sala da primeira bênção, onde não há combate. | `nest/f270`; `blessing/f230` | Esconder o progresso de inimigos fora das salas de combate (mesmo critério do `showCombatHud`, `HUD.StatusBars.cs:86`). |
| Baixa | "Arma: …" é texto fixo em português, montado em três lugares diferentes. | `HUD.StatusBars.cs:152`; `HUD.WeaponBench.cs:343` | Chave `hud.weapon` em `game_texts.json`. |
| Baixa | O texto de moedas é reescrito sem localização após comprar skill ("Moedas de Sangue: {n}" hardcoded). | `HUD.BloodFountain.cs:268` | Usar `OnBloodCoinsChanged` / `hud.blood_coins`. |

## 3. HUD — título da sala

`HUD.StatusBars.cs:21,165-198`; `HUD.Notifications.cs:69-74`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Baixa | O divisor (`y 52-56`) fica sobre a borda inferior da moldura (`y 14-58`) e forma uma linha dupla escura. | `HUD.StatusBars.cs:21,180-195`; `arena/f150` | Pôr o divisor dentro da moldura (acima da borda) ou removê-lo quando a moldura existir. |
| Baixa | O título não tem destaque: 17 px em bege, sem o dourado da identidade. "BOSS" vem colado ao nome do mapa com travessão. | `HUD.StatusBars.cs:177-178`; `HUD.Notifications.cs:71-73` | Nome do mapa em dourado (cor de título) e "Sala 1/6" em texto secundário. Na sala de boss, o rótulo "CHEFE" localizado. |
| Baixa | O nome do mapa "Calaboucos" está sem cedilha (dado de campanha). | `CampaignCatalog.cs:18`; `arena/f150` | Corrigir o texto (dono: `game-designer`/conteúdo) e passar a usar chave de localização. |
| Média | Após limpar a sala, o título vira "Sala concluida - portal vermelho liberado": frase longa, sem acento e fixa em PT. Ela fica para sempre no lugar do nome da sala. | `HUD.StatusBars.cs:523` | Manter o nome da sala e mostrar a instrução numa linha secundária, ou só no banner. Usar chave localizada. |

## 4. HUD — barra do boss

`HUD.StatusBars.cs:213-225`; `HUD.Notifications.cs:51-67`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | Não há moldura nem trilho. O preenchimento `(0.52,0.015,0.025)` sobre `(0.04,0.01,0.012)` é escuro, e a borda de 2 px em cima e embaixo tem a mesma cor do preenchimento. Sobre o chão escuro, o fim da barra se perde. | `HUD.StatusBars.cs:222-224` | Usar `HudPanel` como trilho, com borda dourada fina e preenchimento carmesim mais claro. Adicionar faixa de "chip" de dano e marcas de fase, se houver. |
| Baixa | O nome e "1234/5678" ficam na mesma string. O nome é extraído por `Split('—')`, o que é frágil se o nome tiver travessão. | `HUD.Notifications.cs:66` | Separar em dois rótulos: nome à esquerda, valor à direita, em texto secundário. |
| Baixa | Com 24 px, o nome do chefe compete com o título da sala logo acima (`y 67` e `y 14-58`). | `HUD.StatusBars.cs:215,220` | Na sala de boss, esconder o título da sala ou fundir os dois (o nome do boss vira o título). |

## 5. HUD — log, toasts, banner de sala limpa e aviso de wave

`HUD.StatusBars.cs:119-127,227-243`; `HUD.Notifications.cs:9-15,22-49,109-181`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | O aviso de wave usa o mesmo `Label` do "+1 inimigo derrotado", de skills e de artefatos. O fade é de 0,55 s e qualquer mensagem nova cancela a anterior. Com várias kills seguidas, o aviso de wave some. | `HUD.Notifications.cs:25,47,89,95,170-176`; `HUD.BlessingOffer.cs:408` | Separar três canais: **(a)** alerta de combate (wave chegando): faixa própria, ~2 s, ícone de perigo e contagem regressiva; **(b)** recompensa (moedas, artefato, bênção): toast com fundo, ~1,5 s, em fila; **(c)** kill: só o "+N" junto ao contador de moedas, sem frase. |
| Média | Os toasts não têm fundo (só o outline do tema). Ficam a 28% da altura, sobre a área de combate acima do jogador. | `HUD.StatusBars.cs:227-241` | Pôr os toasts de recompensa abaixo do título da sala (~y 90-120), com `HudPanel`. A área central fica livre para o combate. |
| Alta | O log fica sobre a área jogável, sem fundo: em `arena720/f270`, as linhas passam por cima de dois inimigos (ranged rosa e melee ciano) no canto inferior direito. Ele também mostra o horário do relógio do sistema ("[18:37:45]"), fica sempre visível a 55%, quebra linha e repete o que o toast já disse ("Virgil recebeu 18 de dano"). | `HUD.Notifications.cs:12`; `HUD.StatusBars.cs:119-127`; `arena/f270`, `arena720/f270` | Esconder o log por padrão. Opções: só no build de debug, ou como histórico na pausa ou na ficha. Se ficar visível, tirar o horário, limitar a 3 linhas e esmaecer cada entrada após ~6 s. |
| Média | O banner "SALA LIMPA" é um `ColorRect` vermelho-escuro com 72% de alpha e texto de 17 px: o momento de vitória tem pouco destaque. | `HUD.Notifications.cs:109-138` | Usar `HudPanel` com borda dourada, título em 32-36 px dourado e linha secundária "O portal foi aberto". A animação de entrada atual pode ficar. |
| Baixa | O texto do toast é montado em PT fixo e sem acento ("PROXIMA WAVE", "moedas"). | `HUD.Notifications.cs:25,47` | Usar chaves `hud.toast.*` com `TextFormat`. |

## 6. HUD — barra de bênçãos (inferior central)

`HUD.BlessingBar.cs:9-92`. Avaliada pelo código, porque não havia bênçãos nas capturas.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | O nome, a raridade e o efeito só aparecem no tooltip (mouse). No controle, a barra é ilegível. | `HUD.BlessingBar.cs:205-207` | Botão de ficha (`Select`/`Share`) com uma aba "Bênçãos" navegável. Na barra, mostrar a raridade pela cor da borda do slot e o nível. |
| Média | Os ícones são por **linhagem**, não por bênção (`FamilyIconPath`). Várias bênçãos da mesma família aparecem idênticas lado a lado. | `BloodBlessingCatalog.cs:189,265` | Sem arte nova: diferenciar pela borda da raridade, pelo número de nível e por uma letra ou marca de tipo. Arte própria por bênção fica para o futuro. |
| Média | O fundo do slot é o `simple_panel.png` (384×128) esticado em `TextureRect` 44×44 com `StretchMode.Scale`. As bordas ficam deformadas. | `HUD.BlessingBar.cs:209-218` | `NinePatchRect` (como `AddSimpleFrame`) ou um `StyleBoxFlat` de slot. |
| Baixa | O selo de nível "N2" tem 13 px, sem fundo, e fica sobre a arte vermelha do ícone. | `HUD.BlessingBar.cs:232-252` | Selo com fundo escuro arredondado e texto 13-14 px em dourado. |
| Baixa | O "+" vermelho de 30 px indica que há mais de 7 bênçãos. A contagem só aparece no tooltip. | `HUD.BlessingBar.cs:257-273` | Mostrar "+3" visível, no mesmo formato de slot. |

## 7. HUD — vida crítica, flash de dano e número de dano recebido

`HUD.StatusBars.cs:102-117,391-420,454-470`. Capturas `arena/f256`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | A "vinheta" de vida crítica é um `ColorRect` uniforme que cobre a tela inteira, não uma vinheta. Abaixo de 30% de vida, todo o campo fica tingido de vermelho (alpha 0,08-0,255, pulsando) e os telegraphs vermelhos perdem contraste. | `HUD.StatusBars.cs:109-117,469` | Vinheta real: gradiente radial (`GradientTexture2D` radial, como o `BuildBackdropGlow` do menu) com o centro 100% transparente e só as bordas tingidas. |
| Alta | O flash de dano em tela cheia (alpha 0,28 por 0,16 s) avermelha o piso no momento em que o jogador mais precisa ler o próximo telegraph. | `HUD.StatusBars.cs:399-401`; `arena/f256` | Mesmo gradiente radial nas bordas, alpha ≤ 0,2 e ≤ 0,12 s. Opcionalmente, um piscar na barra de vida em vez da tela. |
| Média | O número "-18" nasce fixo a 66% da altura da tela, não sobre o Virgil. No `arena/f256`, ele cai em cima dos inimigos e dos anéis ciano. | `HUD.StatusBars.cs:408-416` | Projetar a posição do jogador na tela (`Camera3D.UnprojectPosition`) e fazer o número subir de lá, ou pôr o número junto da barra de vida. |
| Baixa | O rótulo do número é filho do `CanvasLayer`, não do `Root` com tema. Fica sem o outline do tema e com fonte padrão, o que reduz a legibilidade sobre o chão. | `HUD.StatusBars.cs:414` | Adicionar ao `Root` (herda o tema) ou definir outline explícito. |

## 8. Rótulos 3D e prompts de interação

Prompts (`ArtifactChest.cs:27-38`, `BloodFountain.cs:27`, `WeaponBench.cs:13-24`, `ScenePortal.cs:61-72`), rótulos de cena (`CrowsNest.tscn:133-258`, `IntermissionChamber.tscn:184-241`, `RunStartBlessingRoom.tscn:177-186`), `RoomGenerator.cs:662-672`, `RunManager.cs:341-372`. Capturas `nest/f270`, `blessing/f230`, `arena/f150`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | São pequenos: `pixel_size 0.0085` com fonte 25-34 dá ~11-13 px de altura na tela na câmera atual. "Fonte de Sangue" e "Fonte da Primeira Herança" quase não se leem. | `nest/f270`; `blessing/f230` | Para títulos de objetos: fonte 48-56 (ou `pixel_size` ~0,012) e outline 10-12. Para prompts: um prompt 2D no HUD, ancorado acima do objeto ou fixo no centro-baixo, com o mesmo estilo do HUD. |
| Média | Os prompts e títulos são vermelho puro `(1,0.08,0.04)`, muitas vezes sobre o brilho vermelho do próprio objeto (fonte, portal). O contraste fica baixo e o vermelho volta a carregar o papel de "perigo". | `ArtifactChest.cs:33`; `blessing/f230` | Títulos de objeto em dourado claro ou osso (`#E8D9B0`). Prompts em branco com a tecla ou o botão destacado em dourado. |
| Média | Quatro formatos de prompt convivem: "E - abrir…", "Interagir — …", "E / X (A) - invocar…", "Pressione E". Os de teclado não mostram o botão do controle, e o "Interagir" não mostra tecla nenhuma. | `ArtifactChest.cs:5`; `ScenePortal.cs:14`; `RoomArtifact.cs:50`; `CrowsNest.tscn:244` | Um formato único: "[ícone/rótulo do botão] Verbo". O rótulo do botão segue o último dispositivo usado (teclado: `E`; controle: `A`/`✕`). |
| Média | O rótulo "Entrada" (fonte 30, cor do marcador) fica sob o disco branco do marcador e atrás do jogador no spawn: ilegível. | `RoomGenerator.cs:662-672`; `arena/f150` | Subir o rótulo (offset Y maior) ou removê-lo: o disco já comunica o spawn. |
| Baixa | "Caixao de Virgil" é escondido pelo próprio Virgil no spawn do hub e está sem til. | `CrowsNest.tscn:213-220`; `nest/f270` | Elevar o rótulo, ou só mostrar com o jogador por perto. Corrigir o texto. |
| Baixa | As cores de título variam sem regra: dourado ("SALA DE TREINAMENTO"), vermelho, rosa ("ALTAR DE ARTEFATOS" `(1,0.68,0.76)`), lilás ("Altar do Sacrifício") e cinza ("Caixão"). | `RunManager.cs:348`; `IntermissionChamber.tscn:234`; `nest/f270` | Paleta de rótulos 3D: dourado para lugares e objetos, branco para prompts, vermelho só para alerta. |
| — | O "!" vermelho de alerta e os ícones de status de inimigo (`images/effects`) com `NoDepthTest` funcionam. Os ícones distinguem os status por forma, além da cor. | `EnemyBasic.cs:1340-1379,1425-1433`; `arena/f245` | Preservar. Avaliar se o "!" (fonte 72-78, ~14 px na tela) precisa crescer ~30%. |

## 9. Menu principal (principal, slots, opções, créditos)

`MainMenu.cs`. Capturas `menu/f280`, `menu720/f280`, `menu1080/f280`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | "Excluir" apaga o save na hora, sem confirmação. O botão fica colado no slot (95 px) e se alcança com um único "direita + A". | `MainMenu.cs:133-134` | Confirmação modal ("Excluir Slot 2? Esta ação não pode ser desfeita." com [Cancelar] em foco por padrão e [Excluir] em vermelho). Alternativa: segurar o botão por 1 s. |
| Média | Na página de opções, o seletor de resolução não tem rótulo e os sliders de volume não mostram valor. O `OptionButton`, o `CheckButton` e o `HSlider` usam o estilo e o foco padrão do Godot (cinza), destoando dos botões. | `MainMenu.cs:153-169`; `UIThemeFactory.cs:21-36` | Linha padrão "Rótulo · controle · valor" (ex. "Música  ▬▬▬▬○──  70"). Completar no tema: `focus`/`hover`/`pressed` de `OptionButton`/`CheckButton`, `grabber`/`grabber_highlight` e `focus` de `HSlider`, o popup do `OptionButton` (`PopupMenu`) e o `SpinBox`. |
| Média | Os títulos das páginas ("Escolha um slot", "Opções", "Créditos") têm 17 px, a mesma cor do corpo, e não têm destaque. | `MainMenu.cs:66,92,126` | Cabeçalho de página: 26-28 px, dourado, com divisor. |
| Baixa | Há um vão de ~105 px entre o divisor (`y 292`) e o primeiro botão (`y 397`). Os botões têm 600 px de largura (a página força a largura), largos demais para rótulos de 1 palavra. | `MainMenu.cs:115,121`; `menu/f280` | Página com 420-460 px de largura e início em ~y 330. O peso visual sobe para o título. |
| Baixa | O emblema `icon.png` é o ícone do aplicativo, com fundo quadrado arredondado próprio, e parece "colado" sobre o gradiente. | `MainMenu.cs:37-44`; `menu/f280` | Reduzir para 96 px e baixar a opacidade, ou recortar só o "VP" (sem fundo) quando houver versão com alpha e origem registrada. |
| Baixa | Os créditos ficam num `ScrollContainer` de 600×300 que não recebe foco. Quando o texto crescer, não vai rolar pelo controle. | `MainMenu.cs:93-100` | Tornar o scroll focável (ou rolar com o analógico direito) e mostrar a barra quando houver overflow. |
| Baixa | Não há rodapé com dicas de controle nem versão do jogo. | `menu/f280` | Rodapé discreto: "[A] Confirmar  [B] Voltar", com a versão à direita. |
| — | Identidade forte: título dourado com sombra carmesim, gradiente radial vermelho e foco visível em "Jogar". | `MainMenu.cs:45-58,71-88`; `menu/f280` | Preservar como referência visual das demais telas. |

## 10. Pausa, opções da pausa e parâmetros de gameplay (tuning)

`HUD.Pause.cs:66-360,417-511`. Captura `arena/f060`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | "Desistir da run" mata o personagem na hora, sem confirmação. A única distinção do botão é o texto vermelho. "Voltar ao menu principal" também não confirma (e a run, que é volátil, se perde). | `HUD.Pause.cs:114-122,127,391-415` | Diálogo de confirmação com [Cancelar] em foco por padrão. Separar as ações destrutivas no fim da lista, com um divisor. |
| Média | Não há painel. Os botões flutuam sobre o jogo escurecido (`0.68`). O título "PAUSADO" tem 17 px e fica em `y≈55`, **sobreposto à moldura do título da sala**. | `HUD.Pause.cs:79-101`; `arena/f060` | Painel central (`HudPanel`, ~480×560) com título dourado de 28 px dentro dele. Esconder o título da sala e o log enquanto a pausa estiver aberta. |
| Média | "Parametros de gameplay" aparece também no release. São sliders de velocidade, dano, vida e alcance do **jogador**, ou seja, ferramenta de balanceamento, não opção. | `HUD.Pause.cs:112,174-184` | Mostrar só com `OS.IsDebugBuild()` (como "Reiniciar teste") ou mover para um menu de desenvolvedor. |
| Média | Os sliders de tuning vão até 180/200/250, mas o `ValueChanged` limita a 100. O slider passa de 100 e o rótulo trava em "100". | `HUD.Pause.cs:177-184,351` | Bug de apresentação (dono: `ui-hud`). Alinhar o `MaxValue` ao limite real ou o limite ao `MaxValue`. |
| Média | A página "OPCOES" da pausa é outra implementação das opções do menu: sem seletor de idioma, textos fixos em PT, seletor de resolução sem rótulo. | `HUD.Pause.cs:269-301` vs `MainMenu.cs:140-169` | Um componente de opções compartilhado entre o menu e a pausa, com as mesmas chaves de localização. |
| Baixa | O rodapé de tuning tem 5 botões de 180-220 px (`Continuar`, `Dicionario`, `Reiniciar teste`, `Salvar parametros`, `Voltar`) somando ~1 100 px. Em `es` passa a largura útil. | `HUD.Pause.cs:186-241` | Se continuar existindo (só debug), basta "Salvar" e "Voltar". |
| Baixa | Os rótulos estão sem acento ("Parametros", "Opcoes", "Dicionario") e o título é em CAIXA ALTA crua. | `HUD.Pause.cs:111-127,277`; `arena/f060` | Chaves `pause.*` com os textos corretos. |

## 11. Game over

`HUD.Pause.cs:9-64,362-389`. Avaliado pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | "GAME OVER" tem 17 px, branco, sem painel: o momento mais dramático da run tem a mesma tipografia de um rótulo de opção. | `HUD.Pause.cs:37-44` | Título de 48-56 px em carmesim/dourado com sombra (mesmo tratamento do título do menu), dentro de um painel. |
| Média | O subtítulo dinâmico ("Virgil foi derrotado por: X. Último golpe: N de dano.") está num `Label` de 360 px sem autowrap. A frase de ~70 caracteres alarga o `VBox` além dos offsets e descentraliza o bloco. | `HUD.Pause.cs:46-52,369` | Autowrap e largura de ~560 px. Separar em duas linhas: causa, depois dano. |
| Média | Não há resumo da run (salas, mapa, moedas ganhas, bênçãos) e há um único botão. | `HUD.Pause.cs:55-63` | Bloco de resumo em 3-4 linhas com ícones. Botões: [Voltar ao Ninho] (foco) e [Menu principal]. |
| Baixa | O overlay (`0.82`) deixa o HUD visível por baixo (vida 0, log, barra do boss). | `HUD.Pause.cs:11-19` | Esconder o HUD de combate ao entrar em game over. |

## 12. Dicionário (compêndio)

`HUD.Compendium.cs`. Avaliado pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | É uma ferramenta de dev apresentada como tela do jogador. Mostra caminhos e nomes de arquivo ("cadastrados em data/artifacts/artifacts.json", "EnemyBasic.tscn", "SkillCatalog"), IDs internos, `SpinBox` que alteram dano, peso e custo em tempo real, e o botão "Redefinir valores originais". | `HUD.Compendium.cs:184,190,194-200,223,235-236,245-267` | Duas camadas: **Bestiário/Codex** para o jogador (descrição, flavor, efeitos legíveis, só o que já foi descoberto) e **Editor de tuning** só em `OS.IsDebugBuild()`. |
| Média | A aba selecionada só se distingue por `Modulate` avermelhado no botão. Fica parecida com o hover e não há sublinhado nem fundo. | `HUD.Compendium.cs:156-157` | Componente de aba: fundo preenchido e borda dourada inferior na aba ativa, troca com `L1/R1`. |
| Média | Os cards de entrada usam o `PanelContainer` padrão do tema, com título de 17 px igual ao corpo. Os efeitos aparecem como `effect.Type (Operation)` crus. A raridade não tem cor. | `HUD.Compendium.cs:199,335-391` | Reusar o layout de `ChoiceCard`, com ícone de 64 px, título de 20-22 px na cor da raridade e efeitos com `FriendlyName`. |
| Baixa | Skills, ataques e inimigos usam `generic.png` (ankh branco grande) como ícone. Fica um ponto branco brilhante repetido em toda a lista. | `HUD.Compendium.cs:233,248,278-319` | Na falta de arte, usar um placeholder neutro escuro (silhueta ou monograma do `IconText` sobre um disco escuro) em vez do ankh branco. |

## 13. Ficha do personagem (C / Select)

`HUD.CharacterSheet.cs`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | O fundo é um `ColorRect` (alpha 0,86) de 96%×92% da tela, sem moldura. As 4 colunas não têm painel nem divisor, e o título (17 px, branco) se confunde com o conteúdo. | `HUD.CharacterSheet.cs:11-44,66-83` | Cada coluna num `HudPanel` com cabeçalho dourado, e título da tela de 28 px. |
| Média | Não há botão "Fechar" nem dica visível: fecha só com `C`/`Esc`/B, e o jogador precisa saber disso. | `HUD.CharacterSheet.cs:9-64` | Rodapé "[B] Fechar" clicável, igual em todos os overlays. |
| Média | Linhas de dev ou internas aparecem para o jogador: "Dano tuning: x1.00", efeitos crus ("{artifact.Effect} +{Amount}", com o nome interno da chave). "Artefatos da run" e "Impacto dos artefatos" repetem a mesma lista. | `HUD.CharacterSheet.cs:123,244,256,268` | Esconder "tuning" fora do debug. Mostrar efeitos via `ArtifactEffectRegistry.FriendlyName` + `FormatArtifactOperation` e juntar as duas listas numa só. |
| Média | Raridade em inglês e cru ("[Rare] Nome — N2"), sem a cor de raridade. As linhas de bênção não recebem foco, então o tooltip com a descrição não chega ao controle. | `HUD.CharacterSheet.cs:170-189` | Linha focável (botão flat) com borda ou texto na cor da raridade, nome localizado da raridade e um painel de detalhes lateral que mostra a descrição do item em foco. |
| Baixa | Sem acentos: "Forca", "Determinacao", "Inventario", "Colecao permanente", "vazio". | `HUD.CharacterSheet.cs:58,114-118,131,279` | Chaves `sheet.*`. |
| Baixa | As colunas fixas (230 + 220 + 270 + 270) não têm scroll, exceto a de bênçãos. Com muitos artefatos, as colunas 3 e 4 estouram para baixo. | `HUD.CharacterSheet.cs:46-63` | `ScrollContainer` em todas as colunas, ou uma estrutura em abas. |

## 14. Fonte de Sangue (árvore de skills) e popup de detalhes

`HUD.BloodFountain.cs`; zoom e pan em `HUD.cs:326-352`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Alta | Os detalhes da skill (descrição, custo, pré-requisitos, estado) só abrem com **clique direito**. No controle, o jogador compra sem saber o que compra nem quanto custa. | `HUD.BloodFountain.cs:161-172,432-463` | Painel de detalhes fixo (lateral ou inferior) que mostra a skill **em foco** (`FocusEntered`), com "[A] Comprar (N moedas)". O clique direito vira atalho opcional. |
| Média | O estado do nó (disponível, comprada, máxima, bloqueada, conflito, planejada) é comunicado **só por `Modulate`**, em tons próximos: rosa, lilás, verde-claro, cinza, cinza-escuro, roxo. Depende de cor e é difícil de distinguir. | `HUD.BloodFountain.cs:320-336` | Estados com forma, além da cor: cadeado (bloqueada), borda dourada cheia (máxima), borda tracejada ou opacidade baixa (planejada), "X" ou faixa (conflito exclusivo), custo visível no nó (disponível). Usar `StyleBox` por estado em vez de `Modulate`. |
| Média | O ícone do nó é uma sigla de 2 letras ("PS", "AT", "ME"). Sem custo no nó, o jogador só vê "1/3". | `HUD.BloodFountain.cs:156,321`; `data/skills/skills.json` (`iconText`) | Enquanto não houver ícones: sigla maior em dourado, "rank/max" num selo e custo abaixo do nome quando comprável. |
| Média | Os nomes das skills ocupam 184 px de largura numa grade de 130 px. Nomes longos ("Presas que Bebem o Sangue", "Entre o Céu e o Inferno") se sobrepõem aos vizinhos de coluna. | `HUD.BloodFountain.cs:146,182-185` | Grade de 190-200 px por coluna, ou rótulo limitado a 124 px com 2 linhas. |
| Média | Zoom (roda) e pan (arrastar) são só de mouse. Os links entre nós são linhas de 4 px vermelho-escuro, sem estado (não acendem quando o caminho está liberado). | `HUD.cs:326-352`; `HUD.BloodFountain.cs:199-218` | Zoom no analógico direito ou nos gatilhos, foco que move a câmera até o nó, links acesos (dourado) quando o pré-requisito foi comprado. |
| Média | A tela inteira usa `ColorRect` de 0,88 sem moldura. O título "FONTE DE SANGUE" tem 17 px e os títulos "Bestialis"/"Humanitas" são texto simples. O status de duas linhas (`\n` + moedas) passa da altura de 32 px e invade o botão de cheat. | `HUD.BloodFountain.cs:11-64,112-122,340-344` | Cabeçalho padrão (título dourado e moedas com ícone no canto) e um painel por árvore com o nome e uma cor de acento própria (Bestialis = carmesim, Humanitas = osso/dourado). |
| Baixa | O popup de detalhes é texto corrido de tamanho único: nome, árvore e tier, descrição, flavor, nível, custo, estado e pré-requisitos com o mesmo peso. | `HUD.BloodFountain.cs:454-461` | Nome em 22 px dourado, metadados em texto secundário, flavor em itálico ou cor apagada, custo destacado. |
| Baixa | Não pausa (fora de `RefreshModalPause`). No hub não há risco, mas é inconsistente com a ficha. | `HUD.Pause.cs:466-475` | Decidir a regra (todo overlay de menu pausa) e documentar no guia. |

## 15. Baú de artefatos (hub)

`HUD.ArtifactChest.cs`. Avaliado pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | A coluna de slots está **escondida** (`Visible = false`), mas cada item diz "> Equipar no slot {n}". O jogador não vê nem escolhe o slot e não sabe o que já está equipado. | `HUD.ArtifactChest.cs:56,66,227` | Mostrar os slots (à esquerda, como cards pequenos com o item equipado) e destacar o selecionado com uma borda dourada, não com `Modulate` vermelho (`:170`). |
| Média | Cada item é um `Button` de 880×96 com 3-4 linhas de texto alinhadas à esquerda, sem ícone nem cor de raridade. O cabeçalho é cru: "[COLECAO PERMANENTE] Nome \| Rare". | `HUD.ArtifactChest.cs:209-234` | Card de lista: ícone de 48 px, nome na cor da raridade, selo de origem ("Run" / "Coleção") e efeitos em texto secundário. |
| Baixa | O título "BAU DE ARTEFATOS" está sem acento, em 17 px, num `ColorRect` sem moldura (1 040×600). | `HUD.ArtifactChest.cs:11-35` | Cabeçalho e painel padrão. |

## 16. Oferta de artefatos (altar ao fim do mapa)

`HUD.ArtifactOffer.cs`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | Não segue o formato de card da bênção: são `Button` de texto puro (1 080×112), sem ícone, sem borda de raridade e com a raridade e o slot como enums crus ("Nome \| Rare \| Weapon"). | `HUD.ArtifactOffer.cs:366-376` | O mesmo componente de card de escolha da bênção e do altar: ícone, nome, raridade colorida, descrição, efeitos e botão "Escolher". |
| Média | O "Trocar oferta" (`x 980-1180`, `y 68-106`) ocupa a mesma faixa vertical do status (`y 58-92`, centralizado em 1 240 px). Com o texto longo de `:354`, ainda mais em `en`/`es`, os dois se encostam ou se sobrepõem. O botão aparece desabilitado mesmo quando o jogador não tem a skill. | `HUD.ArtifactOffer.cs:303-320,343,354` | Mover o "Trocar oferta" para o rodapé, ao lado de uma dica "[Y] Trocar", e escondê-lo (em vez de desabilitar) sem a skill "Espelho do Destino". |
| Média | `Esc`/B fecha a oferta sem escolher (`CloseTopmostOverlay`), mas não há botão visível equivalente. Também não fica claro se a recompensa continua disponível depois. | `HUD.Pause.cs:445` | Ou é obrigatória (como a bênção: `Esc` só mostra um aviso) ou tem "[B] Decidir depois" visível. Recomendo o comportamento da bênção, por consistência. |
| Baixa | Com 4 opções (skill "Ás das Relíquias"), 4×112 + separações = ~460 px numa área de 458 px: o último card encosta na borda. | `HUD.ArtifactOffer.cs:322-329,340` | Lista em `ScrollContainer`, ou cards de 100 px quando houver 4. |
| Baixa | O título "TESOURO DE ARTEFATOS" tem 17 px, contra 27 px na oferta de bênção. | `HUD.ArtifactOffer.cs:293-301` vs `HUD.BlessingOffer.cs:261` | Mesmo cabeçalho de tela de escolha. |

## 17. Oferta de bênçãos (Hereditas Sanguinis) e absorção "Sanguis Purgatus"

`HUD.BlessingOffer.cs`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | No controle, o foco fica só no botão "Escolher" (borda dourada de 1 px). O card inteiro não reage: com 3 cards iguais, não se vê qual está em foco. | `HUD.BlessingOffer.cs:313-361`; `UIThemeFactory.cs:29` | No `FocusEntered` do botão, trocar o StyleBox do card para uma versão "em foco" (borda de 3 → 4 px mais clara, fundo levemente mais claro, marcador "▶"). Também no hover do mouse. |
| Média | O d-pad cima/baixo é capturado em `_Input` e só circula entre os "Escolher". O "Trocar oferta" (canto superior direito) nunca recebe foco pelo controle. | `HUD.cs:299-306`; `HUD.BlessingOffer.cs:271-277` | Atalho dedicado (`Y`/`△`) com dica visível, ou incluir o botão no ciclo de foco. |
| Média | A raridade é mostrada crua em inglês ("\| Rare") ao lado do nome. A cor de raridade aparece só na borda de 3 px. | `HUD.BlessingOffer.cs:322` | Nome localizado da raridade, num selo com a cor da raridade (forma + cor), e borda mantida. |
| Média | Algumas cores de raridade colidem com os papéis de status: Incomum `(0.2,0.9,0.3)` ≈ Pestilência `#78d957`; Rara `(0.2,0.48,1)` ≈ Barreira `#63c7ff`; Stirps `(1,0.08,0.22)` ≈ Sangramento `#ff4055` e a vida. | `HUD.BlessingOffer.cs:384-392,498-516` | Definir a paleta de raridade no guia (T3) com tons distintos dos status, ou garantir que a raridade venha sempre com forma ou rótulo (selo), nunca só a cor. |
| Média | Os termos destacados (`[hint]`) e o aviso "SUBSTITUI" no status dependem de hover do mouse. O aviso também aparece inline no card, o que é bom, mas o texto do status muda só no mouse. | `HUD.BlessingOffer.cs:312,331-335,486-516` | Mostrar o aviso de substituição no `FocusEntered` também. Para os termos, um glossário na tela (rodapé com os termos presentes nos cards). |
| Baixa | O texto do status está sem acento ("familia", "bencao unica") e mostra o ID ou nome cru da família. | `HUD.BlessingOffer.cs:307,335` | Chave localizada. |
| Baixa | A moldura `AddSimpleFrame` tem modulate `(0.12,0.055,0.07)`, quase invisível sobre o fundo `(0.025,0,0.008)`. | `HUD.BlessingOffer.cs:233-239` | Borda mais clara (dourado-escuro a ~40%) para dar contorno à janela. |
| — | Melhor tela do jogo hoje: card com borda de raridade, ícone de 92 px, título de 22 px, descrição com números já calculados e d-pad cíclico. Na oferta obrigatória, `Esc` mostra o aviso "Escolha uma Bencao…" em vez de fechar. | `HUD.BlessingOffer.cs:313-361`; `HUD.Pause.cs:446-451` | **Modelo** para a oferta de artefatos e o baú (ver "Não mexer"). |

## 18. Bancada de armas (hub)

`HUD.WeaponBench.cs`. Avaliada pelo código.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | O botão de cada arma junta nome e descrição numa linha só (~120 caracteres, ~1 000 px a 17 px). A largura útil é ~740 px (50% de 1 600 − 60), então o botão alarga o `VBox` e passa do painel. | `HUD.WeaponBench.cs:330`; `WeaponCatalog.cs:8` | Card de arma: nome em destaque e descrição em autowrap abaixo, como nas ofertas. |
| Média | Não há estado "equipada" no botão: só a linha "Equipada: X" no topo. Também não há moldura, e o título é texto simples de 17 px. | `HUD.WeaponBench.cs:301-325` | Selo "EQUIPADA" e borda dourada no card da arma atual, e painel e cabeçalho padrão. |
| Baixa | Todos os textos são fixos em PT ("Bancada de Armas", "Equipada:"). | `HUD.WeaponBench.cs:294,323-324,344` | Chaves `bench.*`. |

## 19. Câmara de Hemomancia — Altar do Sacrifício e Fonte Restauradora

`IntermissionChamber.cs:295-391`; `HUD.StatusBars.cs:197-198`. Captura `inter/f280`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | O status da câmara é um `Label` num `CanvasLayer` próprio, **sem o tema do `UIThemeFactory`** (sem o outline do tema), desenhado sobre a moldura do título do HUD (44 px). Em repouso cabe numa linha ("Câmara de Hemomancia • Vida 100/100", `inter/f280`). Com uma mensagem de fonte ou altar (`SetMessage` adiciona `\n` + frase longa), vira 2-3 linhas (até 66 px) e transborda a moldura. Ele também repete a vida que o painel do jogador já mostra. | `IntermissionChamber.cs:299-307,382-391`; `HUD.StatusBars.cs:21,198`; `inter/f280` | Título "Câmara de Hemomancia" via HUD (`_roomLabel`). Mensagens de fonte e altar como toast de recompensa (canal (b) do §5). Remover a vida do título. |
| Média | O overlay do altar é construído fora do HUD, com `BlessingRarityColor` duplicado. O fechamento por `ui_cancel` é paralelo ao `CloseTopmostOverlay`, e a pausa é feita direto em `GetTree().Paused`. | `IntermissionChamber.cs:68-74,196,257-265,289-293` | Na implementação: mover para um `HUD.Altar.cs` (ou um componente de card compartilhado) e usar a paleta de raridade do tema. Dono: `ui-hud`. |
| Baixa | O "Voltar" fica no canto inferior direito do painel. Na oferta de bênção não existe "Voltar", e em outras telas o fechar fica no canto superior direito (fonte) ou centralizado embaixo (baú). | `IntermissionChamber.cs:366-379`; `HUD.BloodFountain.cs:90-104`; `HUD.ArtifactChest.cs:117-130` | Regra única: rodapé com "[B] Voltar" à esquerda e ação primária à direita. |
| Baixa | O card mostra "Raridade" crua ("\| Rare \|") e a seta "N1 → N2". A comparação antes/depois é clara, mas o custo (5% da vida máxima) só está no texto do topo. | `IntermissionChamber.cs:175,347` | Selo de custo no botão: "Aprimorar · −5 vida máx." em vermelho-osso. |
| — | Os cards do altar reutilizam `ChoiceCard`, ícone de 92 px, título de 22 px e o mesmo layout da oferta de bênção, com a prévia "atual → próximo" por efeito. | `IntermissionChamber.cs:152-190,200-235` | Preservar o padrão. |

## 20. Cursor e navegação por controle (transversal)

`GameCursor.cs`, `ControllerUiSupport.cs`, `HUD.cs:289-353`.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | Nenhuma tela mostra dicas de botão. O `ControlsHint` existe, mas com `Visible = false`, em PT fixo e com mapeamento genérico. | `HUD.StatusBars.cs:199-211` | Barra de dicas de rodapé por tela ("[A] Escolher  [Y] Trocar  [B] Voltar"), com glifo conforme o último dispositivo usado. |
| Média | O cursor do mouse continua visível durante o jogo no controle (o Godot não esconde sozinho). | `GameCursor.cs:7-18` | Esconder o cursor com input de joypad e mostrar de novo ao mover o mouse. Dono: `ui-hud`. |
| Baixa | O cursor de 64×64 não escala com a resolução: é grande em 720p e pequeno em 4K. Os cursores `Help` e `Arrow` são iguais, então as áreas com tooltip não se distinguem. | `GameCursor.cs:12-16`; `assets/ui/kenney_hud/cursor_pointer.png` (64×64) | Cursor de 32-48 px. Opcionalmente, uma variação `Help` com "?" se houver asset CC0 no pacote Kenney Cursor (já creditado). |
| Média | `FocusFirst` foca o primeiro controle visível. Na pausa isso funciona. Em telas com o "Fechar" no topo (Fonte de Sangue), a ordem da árvore decide e o foco nem sempre começa na ação principal. | `ControllerUiSupport.cs:23-33`; `HUD.BloodFountain.cs:106-107` | Cada tela declara o foco inicial explicitamente (ação primária). `FocusFirst` fica como fallback. |
| Baixa | Os overlays não restauram o foco ao fechar (ex.: sair do Dicionário volta à pausa sem foco em "Dicionario"). | `HUD.Compendium.cs:138-143` | Guardar o controle focado ao abrir e devolver ao fechar. |

## 21. Resoluções (1280×720 e 1920×1080)

Com `canvas_items` + `expand` e base 1600×900, as duas resoluções mantêm 16:9, então o layout lógico é o mesmo e só a escala muda (0,8× em 720p, 1,2× em 1080p). Avaliação analítica: o Movie Maker grava sempre em 1600×900 (ver Método). Nada se reposiciona nem se sobrepõe por causa da resolução: os problemas de layout das seções anteriores valem para as três.

| Sev. | Problema | Evidência | Recomendação |
|---|---|---|---|
| Média | Em 720p o corpo de 17 px vira ~13,6 px físicos. "VIRGIL" (13 px) cai para ~10 px, e o log (17 px a 55% de alpha) fica no limite da leitura. | `UIThemeFactory.cs:10`; `HUD.StatusBars.cs:44,125` | Tamanho mínimo de texto de HUD: 16 px lógicos para informação e nada abaixo de 14. Sem texto essencial com alpha < 0,8. Validar numa janela real em 1280×720. |
| Baixa | Telas com tamanho fixo em pixels (ofertas de 1 240×660-680, baú de 1 040×600) ocupam 78-77% da largura. Em ultrawide (aspect `expand`), ficam pequenas no centro. | `HUD.BlessingOffer.cs:229`; `HUD.ArtifactChest.cs:19-22` | Aceitável. No guia, definir larguras máximas e mínimas, não fixas. |

---

## Padrões transversais

**T1 — Paleta sem papéis definidos.**
- O vermelho saturado é usado para vida, perigo, prompts, raridade Stirps, botão destrutivo, títulos 3D e seleção (`Modulate (1,0.35,0.32)` no slot do baú e nas abas do compêndio).
- O dourado (`0.82,0.64,0.30`) aparece só no título do menu e no foco.
- Proposta para o guia:

| Papel | Cor |
|---|---|
| Perigo | vermelho-alaranjado vivo (telegraphs, alertas) |
| Vida | carmesim escuro |
| Barreira | azul |
| Fúria | laranja |
| Seleção, foco e títulos | dourado |
| Raridades | paleta própria, sempre com rótulo ou forma |

**T2 — Três gerações de overlay convivem.**
- **(a)** `ColorRect` cru com `Label` de 17 px: ficha, fonte, baú, oferta de artefatos, bancada, game over e pausa.
- **(b)** `ColorRect` + `AddSimpleFrame` + `ChoiceCard`: oferta de bênção e altar.
- **(c)** `PanelContainer` do tema: compêndio.
- Falta um componente "Janela" (painel + cabeçalho dourado + corpo + rodapé de ações/dicas) usado por todas.

**T3 — Tipografia sem escala.**
- Quase tudo usa os 17 px do tema. Os overrides são pontuais: 13, 22, 24, 27, 32, 34 e 54.
- Os títulos de tela variam entre 17 px (a maioria) e 27 px (bênção e altar).
- Definir no guia: display 54 / título de tela 28 / título de card 22 / corpo 17 / secundário 15 / selo 13.

**T4 — Estado comunicado por `Modulate`.**
- Seleção do slot do baú, aba do compêndio, estados dos nós da árvore e o `Modulate` geral dos rótulos.
- Problemas: depende só da cor, tinge o texto junto e brigaria com o foco.
- Trocar por StyleBox por estado: normal, hover, foco, selecionado, desabilitado e bloqueado.

**T5 — Tooltips como único canal de informação.**
- Afeta a barra de bênçãos, a fúria, a ficha, a árvore e os `[hint]`.
- Regra proposta: tooltip só **repete** algo acessível de outra forma. Toda tela com itens tem um painel de detalhes que segue o foco.

**T6 — Textos fixos e sem acento.**
- Só o menu principal e algumas chaves do HUD (`hud.*`) usam `Localization`.
- O resto (HUD, overlays, `IntermissionChamber`, prompts 3D) é PT fixo, muitas vezes sem acento, e há enums crus em inglês ("Rare", "Weapon", "Stirps").
- A troca de idioma tem efeito só no menu.

**T7 — Ações destrutivas sem confirmação.**
- Excluir slot, desistir da run e voltar ao menu.
- Falta um componente "Confirmar" com foco padrão em Cancelar.

**T8 — Ferramentas de dev no fluxo do jogador.**
- "Parametros de gameplay" e o Dicionário editável (com caminhos de arquivo) estão no release.
- O cheat de moedas, "Reiniciar teste" e o FPS já estão corretamente atrás de `OS.IsDebugBuild()`. Aplicar a mesma regra ao tuning e ao editor do compêndio.

**T9 — Fechar/voltar sem posição fixa.**
- O botão fica no topo direito (fonte), embaixo e centralizado (baú, compêndio), embaixo à direita (altar) ou não existe (ficha, oferta de artefatos).
- Padronizar um rodapé com "[B] Voltar".

**T10 — Prompts de interação heterogêneos.**
- Quatro formatos de texto, sem glifo de controle, em vermelho e pequenos.
- Padronizar formato, cor e tamanho, idealmente como prompt 2D do HUD.

## Não mexer (funciona, deve ser preservado)

- **Identidade do menu principal:** fundo preto com gradiente radial carmesim, título dourado de 54 px com sombra carmesim, subtítulo em caixa alta e divisor. É a referência de tom para as demais telas (`MainMenu.cs:35-58,71-88`).
- **Card de escolha da bênção e do altar:** `UIThemeFactory.ChoiceCard` com borda de raridade, ícone de 92 px, título de 22 px e botão de ação à direita. A prévia "atual → próximo" do altar também fica. Estender, não substituir (`HUD.BlessingOffer.cs:313-361`; `IntermissionChamber.cs:152-190`).
- **Descrição de bênção com números calculados** (`FormatBloodBlessingDescription`) e o realce de palavras-chave de status por cor no texto.
- **Barreira integrada à barra de vida:** azul atrás do vermelho, sobre um máximo combinado (`HUD.StatusBars.cs:432-452`).
- **Fluxo de pausa:**
  - `CloseTopmostOverlay` fecha a camada do topo com `Esc`/B;
  - `RefreshModalPause` recalcula a pausa;
  - a oferta obrigatória de bênção não fecha com `Esc` e mostra um aviso (`HUD.Pause.cs:436-475`).
- **Ciclo do d-pad entre as escolhas de bênção**, com wrap-around (`HUD.cs:299-306`).
- **Fallback de foco:** `ControllerUiSupport.EnsureMappings` e `FocusFirst` em toda abertura de overlay (manter como fallback).
- **Ferramentas de teste fora do release:** `OS.IsDebugBuild()` no FPS, no cheat de moedas e em "Reiniciar teste".
- **Indicadores de status nos inimigos:**
  - ícones de status distintos por forma;
  - "!" de alerta com `NoDepthTest`;
  - telegraphs no chão sem serem enterrados.
- **Animação "Sanguis Purgatus":** a absorção das essências em espiral, com o pulso de chegada, é um bom momento de recompensa. Ajustar só a cor do título (hoje vermelho de perigo) para carmesim ou dourado (`HUD.BlessingOffer.cs:19-98,252-253`).
- **Nomes de métodos chamados por `CallGroup("hud", …)`:** `AppendGameLog`, `NotifyEnemyDefeated`, `NotifyWaveIncoming`, `NotifyRoomCleared`, `BindBoss`, `OpenBloodFountain`, `OpenArtifactChest`, `OpenWeaponBench`, `OpenArtifactOffer`, `OpenBloodBlessingOffer`, `OpenMandatoryBloodBlessingOffer`, `RefreshBlessingDisplays`, `NotifySkillMessage`, `NotifyArtifactCollected`, `SyncBloodCoins`, `NotifyBloodCoinsRewarded`, `SyncEnemyProgress` etc. Qualquer refatoração visual deve manter as assinaturas (lista completa no inventário T2).

## Fora do escopo desta auditoria (encaminhamentos)

- **`ui-hud` (bugs de comportamento com efeito visual):**
  - limite de 100 nos sliders de tuning que vão até 250 (`HUD.Pause.cs:351`);
  - slots do baú escondidos (`HUD.ArtifactChest.cs:56,66`);
  - número de dano fora do tema (`HUD.StatusBars.cs:414`);
  - cursor visível no controle;
  - restauração de foco.
- **`game-designer`/conteúdo:**
  - acentos em nomes de dados ("Calaboucos", textos de arma);
  - decidir se a oferta de artefatos é obrigatória como a de bênção;
  - o que o jogador vê no Codex.
- **Licenças:** todos os ícones usados pela UI (`images/bloodBoons/*`, `images/effects/*`, `images/generic.png`, `icon.png`) e o 9-slice candidato `images/inventory/*` estão listados em `CREDITS.md` como "origem ainda não registrada". Antes de distribuir ou de adotar `images/inventory/` como moldura, é preciso registrar autoria e licença.

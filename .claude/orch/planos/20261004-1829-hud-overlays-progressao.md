<!-- orch:plano · id: 20261004-1829-hud-overlays-progressao · status: concluido · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
# HUD e menus — Fase 3c: overlays de progressão

**Demanda original:** revisar, ajustar e melhorar a HUD inteira do jogo, de todos os menus e etc..
**Grupo:** 20261004-1829-hud-auditoria · **Depende dos planos:** 20261004-1829-hud-fundacao

## Objetivo

Ficha do personagem, Fonte de Sangue (árvore de skills), ofertas de bênção, baú e oferta de artefatos, bancada de armas e overlay do altar da Câmara de Hemomancia refeitos segundo o guia, com cards de escolha consistentes e navegação por controle.

## Suposições e decisões

- Segue `docs/design/ui-guia.md` e usa os componentes da fase 2; não edita `UIThemeFactory`/`UiComponents`.
- Ofertas de bênção, de artefato e do altar usam o mesmo componente de card (regra já existente no projeto).
- Textos continuam onde estão; a fase 4 move tudo para recursos por idioma (`data/localization/<idioma>/`).
- Regras de jogo não mudam (custos, ranks, exclusividade de bênçãos, reroll pago); só apresentação e usabilidade — **exceto** a decisão D1 do usuário: a oferta de artefato passa a ser obrigatória (T4).
- Pode rodar em paralelo com `hud-ingame` e `hud-menus-sistema`. Limite de 4 tarefas simultâneas no total.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Refazer ficha do personagem | `ui-hud` | — | `scripts/ui/HUD.CharacterSheet.cs` | concluida |
| T2 | Refazer Fonte de Sangue e árvore de skills | `ui-hud` | — | `scripts/ui/HUD.BloodFountain.cs` | concluida |
| T3 | Refazer oferta de bênçãos | `designer-ux-arte` | — | `scripts/ui/HUD.BlessingOffer.cs` | concluida |
| T4 | Refazer baú, oferta de artefatos e bancada de armas | `ui-hud` | — | `scripts/ui/HUD.ArtifactChest.cs`, `scripts/ui/HUD.ArtifactOffer.cs`, `scripts/ui/HUD.WeaponBench.cs` | concluida |
| T5 | Refazer overlay do altar da Câmara de Hemomancia | `designer-ux-arte` | — | `scripts/systems/IntermissionChamber.cs`, `scripts/ui/UiPreview.cs` (preview `altar`) | concluida |
| T8 | Refazer rótulos 3D/prompts e bloquear `interact` com UI aberta | `designer-ux-arte` | — | `scripts/systems/ArtifactChest.cs`, `scripts/systems/BloodFountain.cs`, `scripts/systems/WeaponBench.cs`, `scripts/systems/ScenePortal.cs`, `scripts/items/RoomArtifact.cs` | concluida |
| T9 | Corrigir prompts 3D, títulos e altar | `designer-ux-arte` | T6, T7 | `scripts/systems/InteractPrompt.cs`, `scripts/systems/IntermissionChamber.cs`, `scripts/systems/RunManager.cs` (só rótulo de sala), `scripts/systems/RoomGenerator.cs` (só rótulo), `scenes/game/CrowsNest.tscn` (textos de Label3D) | concluida |
| T11 | Nomes PT dos efeitos de artefato e flag `IsImplemented` de arma | `conteudo-run` | T6, T7 | `scripts/systems/artifacts/ArtifactEffectRegistry.cs`, `scripts/systems/weapons/*.cs`, `scripts/ui/HUD.WeaponBench.cs` | concluida |
| T10 | Corrigir input, Fonte, ofertas e cards | `ui-hud` | T6, T7, menus-sistema T8 | `scripts/ui/HUD.cs`, `HUD.BlessingOffer.cs`, `HUD.ArtifactOffer.cs`, `HUD.BloodFountain.cs`, `HUD.CharacterSheet.cs`, `HUD.ArtifactChest.cs`, `HUD.Pause.cs`, `HUD.StatusBars.cs`, `UiComponents.cs` (ChoiceCard) | concluida |
| T12 | Reverificar correções (captura + revisão) | `orch:revisor` | T9, T10, T11 | — (leitura) | concluida |
| T6 | Verificar visualmente os overlays de progressão | `designer-ux-arte` | T1, T2, T3, T4, T5, T8 | — (leitura) | concluida |
| T7 | Revisar código dos overlays de progressão | `orch:revisor` | T1, T2, T3, T4, T5, T8 | — (leitura) | concluida |

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T3 ∥ T4 ∥ T5 ∥ T8   (até 4 simultâneas no total do grupo)
Onda 2: T6 ∥ T7                       (após T1–T5, T8)
Onda 3: T9 ∥ T11 ∥ T10                (T10 também após menus-sistema T8 — HUD.cs/HUD.Pause.cs/UiComponents)
Onda 4: T12
```

Caminho crítico: T2 → T6 (árvore de skills é a maior tela).

## Detalhe das tarefas

### T1 — Refazer ficha do personagem
- **Executor:** `ui-hud`
- **Faz:** colunas (atributos, skills, bênçãos, artefatos) no padrão do guia, rolagem por controle, hierarquia de títulos/valores.
- **Pronto quando:** captura `--ui-preview=character_sheet` com dados de exemplo legível; build limpo.
- **Resultado:** `HUD.CharacterSheet.cs` reescrito seguindo o guia §8.13 (abas em vez de 4 colunas): `Window(Full)` com moedas, abas Atributos/Bênçãos/Artefatos/Habilidades (L1/R1, Q/E, esquerda/direita numa linha), lista focável + `DetailPanel(Right)` 520 px; bênçãos com ícone, raridade, linhagem, nível, efeitos efetivos e flavor (**P0 resolvido: acessíveis por controle**); artefatos em Equipados/Desta run/Coleção (Mítico marfim; corrigido valor sem `BossDropPowerMultiplier`); atributos com efeito derivado; habilidades por árvore; `UiModalStack.Push` com pausa (resolve ficha atrás de overlays), foco devolvido ao fechar; preview com dados fictícios sem tocar save/diário (`--ui-tab=`). Verificação: build 0/0; capturas `o1_*` (kbm/xbox/ps/en); navegação simulada por script de input (baixo, esquerda/direita, abas, cancel, reabrir) ok; smoke TestArena/CrowsNest limpo. Achado fora do escopo: no Godot 4 eventos de teclado/controle só chegam ao controle focado — `OnCompendiumGuiInput` na raiz do Dicionário não troca aba com L1/R1 nem rola detalhe com foco numa linha (confirmado). Pendências: comentário do `UiPreview.cs`; código redundante da ficha em HUD.Pause.cs/HUD.cs; textos de atributo derivados das fórmulas.

### T2 — Refazer Fonte de Sangue e árvore de skills
- **Executor:** `ui-hud`
- **Faz:** nós e ligações legíveis (comprado/disponível/bloqueado/planejado), popup de descrição no padrão de tooltip, zoom/pan e navegação por controle entre nós, status de moedas claro; cheat de moedas segue só em debug.
- **Pronto quando:** captura `--ui-preview=blood_fountain` ok; compra continua funcionando; build limpo.
- **Resultado:** `HUD.BloodFountain.cs` reescrito: `Window` Full com moedas no cabeçalho e status de 1 linha; árvores Bestialis/Humanitas lado a lado num canvas com recorte, layout só pelos dados; nós `Button` com `SkillNodeStyle` por estado (comprado, máximo, disponível, sem moedas, bloqueado com `LockGlyph`, exclusivo riscado, planejado tracejado "EM BREVE"), selo de rank e custo com `CoinGlyph`; ligações ortogonais desenhadas (Gold se comprado); `DetailPanel` inferior seguindo foco/hover com requisitos (adquirida/falta), ação com glifo e motivo do bloqueio; vizinhos de foco explícitos entre árvores, pan segue o foco; zoom 40–150% (`ui_zoom_*`, roda) e pan (RS, arrastar); cheat em `DevSection` só debug; `UiModalStack.Push` sem pausa, reabrir não rouba foco. Compra com a mesma lógica/ordem. Preview simula compra sem gravar (resolve risco do Anexo B) e aceita `--ui-zoom=`. `OpenBloodFountain` movido de HUD.Notifications.cs para HUD.BloodFountain.cs (mesma assinatura). Verificação: build 0/0; capturas `o2_*` (teclado, popup bloqueado, xbox, zoom 40%); smoke CrowsNest sem SCRIPT ERROR. Pendências: em 100% só ~2,5 de 6 linhas visíveis (avaliar painel à direita/zoom padrão em T6); `_skillDescriptionPopup`/`IsVisible(_bloodFountainOverlay)` legados em HUD.Pause.cs/HUD.cs; acesso a membros `internal` do `UiDetailPanel` (fundação poderia expor `ActionColor`/`IconOverride`); estados planejado/exclusivo sem dado real para captura; validar com controle e janelas reais.

### T3 — Refazer oferta de bênçãos
- **Executor:** `designer-ux-arte`
- **Faz:** cards com família, raridade (cor), nível, ataque afetado e efeitos; reroll claro; foco por controle; mantém o VFX de absorção (sanguis trail) e a regra de oferta obrigatória.
- **Pronto quando:** captura `--ui-preview=blessing_offer` ok; seleção e reroll funcionam; build limpo.
- **Resultado:** `HUD.BlessingOffer.cs` refeito: `Window(Large)` "Hereditas Sanguinis" mandatory no `UiModalStack` (pausa pela pilha; Esc/B/pausa só avisam "Escolha uma Bênção…" e refocam); `ChoiceCard` com ícone, raridade por `UiPalette.Rarity`, meta "Linhagem · ataque afetado · Nova (N1)"/"N→N+1", números em negrito dourado (só na oferta), aviso "Substitui: X"; glossário dos termos acima do rodapé (substitui `[hint]` só-mouse); reroll "Espelho do Destino" no rodapé com `ui_alt`, (N)/(0) desabilitado; foco no 1º card, cima/baixo/esq/dir; trava contra escolha dupla; mesma ordem de chamadas na escolha; absorção mantida com guarda de reentrada e `RefreshPause`; cópia de `BlessingRarityColor` removida; preview `blessing_offer_replace`. Verificação: build ok; capturas `o3_*` (kbm/xbox/ps/en/replace); smoke RunStartBlessingRoom/TestArena sem SCRIPT ERROR. Pendências: ramos legados de `_bloodBlessingOverlay` em HUD.Pause.cs/HUD.cs redundantes; flash dourado de 0,15 s (§3.4) omitido; textos PT (fase 4); glossário cita teclas de mouse/teclado; regex não pega "ataque especial" singular; validar jogando (absorção, reroll real, controle).

### T4 — Refazer baú, oferta de artefatos e bancada de armas
- **Executor:** `ui-hud`
- **Faz:** baú com slots e coleção (botões de equipar da limpeza anterior) no padrão do guia; oferta de artefatos com o mesmo card das bênçãos (3–4 opções), **obrigatória** (D1: Esc/B não fecham, sem "Decidir depois", reabrir não re-sorteia — as opções sorteadas ficam guardadas até a escolha; modal não cancelável na `UiModalStack`); bancada de armas com arma atual e placeholders marcados.
- **Pronto quando:** capturas `--ui-preview=artifact_chest|artifact_offer|weapon_bench` ok; equipar/escolher funcionam; build limpo.
- **Resultado:** Oferta de artefato: `Window(Large)` com `ChoiceCard` (monograma, raridade, efeitos), foco no 1º card; **obrigatória (D1)** via `UiModalStack.Push(pauses:true, mandatory:true, onCancel: aviso)` — Esc/B/Start só avisam, pausa não abre por cima; opções guardadas por `source.GetInstanceId()` (reabrir não re-sorteia; limpo na escolha; fontes liberadas podadas); reroll pago em `ui_alt`; escolha na mesma ordem de antes com trava anti-duplo. Baú: slots equipados (SelectButton, badge) + coleção rolável com badges + `DetailPanel`; fluxo slot→coleção→slot por controle; corrigido bug (slots invisíveis e equipar sempre no slot 1); `Push(pauses:false)`. Bancada: cards 880 com "EQUIPADA" e "EM BREVE" (equipar desabilitado — mudança de regra pelo guia §8.18; detecção heurística). `OpenArtifactChest` movido de HUD.Notifications.cs (mesma assinatura). Previews com dados em memória (`artifact_offer_4` novo) sem tocar no diário. Verificação: build ok; smoke TestArena/CrowsNest sem SCRIPT ERROR; capturas `o4_*`. Pendências: flag `IsImplemented` em `WeaponDefinition` seria melhor que a heurística; ficha (C) abre sob a oferta (resolve se T1 usar `Push`); código morto em HUD.Pause.cs/HUD.cs; efeitos sem nome PT em `ArtifactEffectRegistry.FriendlyName`; mesmo artefato pode ir em 2 slots (regra antiga); glifos voltam a teclado no CrowsNest em captura; validar fluxo sala→oferta→portal jogando.

### T5 — Refazer overlay do altar da Câmara de Hemomancia
- **Executor:** `designer-ux-arte`
- **Faz:** torna `OpenUpgradeOverlay` público (para o `--ui-preview`); overlay do altar usando o card comum (remove a cópia de `BlessingRarityColor` e do layout de cards), custo de −5% de vida máxima explícito, cura da fonte com valor real (não o "35%" fixo).
- **Pronto quando:** captura na `IntermissionChamber` com o altar aberto (preview ou interação simulada) ok; build limpo.
- **Resultado:** `OpenUpgradeOverlay` público; altar = `Window(Large)` + `ChoiceCard` (mesmo formato da oferta de bênção: ícone, raridade, meta "Linhagem · N1 → N2", efeito atual → próximo, custo "−5% vida máx." em Warn calculado de `BlessingSacrificeFraction`); `UiModalStack.Push(pauses:true, onCancel: Close)` — continua dando para sair sem escolher; removidos `BlessingRarityColor`, layout manual, `_Input` paralelo e `Paused` diretos. Fonte com cura real (`FountainHealPreview` = regra de `UseFountain`), "Vida cheia" quando cheia. Título/status da Câmara em duas linhas (Gold + Secondary), escondido com modal aberto; mensagens viram toast (`NotifySkillMessage`). Interação via `InteractPrompt.ConsumeInteract` e rótulos com o helper da T8. Preview `--ui-preview=altar` (host IntermissionChamber em `UiPreview.cs`). Regras de jogo iguais. Verificação: build ok; smoke IntermissionChamber limpo; capturas `o5_kbm`, `o5_xbox`, `o5_room`. Pendências: prompts 3D não capturados (só com jogador perto); título da fonte sob a moldura do HUD no spawn; moldura da Câmara 800×76 fixa em `HUD.StatusBars.cs:246-248` larga demais (ideal: título no `_roomLabel` do HUD); preview adiciona bênçãos e marca diário (debug); texto de efeito booleano "ativo → ativo" e `FriendlyName` sem acento.

### T8 — Refazer rótulos 3D/prompts e bloquear `interact` com UI aberta
- **Executor:** `designer-ux-arte`
- **Faz:** aplica a seção 8.8 do guia aos `Label3D` de título/prompt dos interativos (fonte, baú, bancada, portais, artefato de sala) com um helper comum; e faz a leitura de `interact` por polling ignorar o input quando `IsGameplayInputBlocked()` — hoje baú/fonte/bancada não pausam e apertar ✕/A dentro do overlay pode reabri-lo.
- **Pronto quando:** prompts legíveis e consistentes nas capturas do hub; `interact` não reabre overlays; build limpo.
- **Resultado:** helper novo `scripts/systems/InteractPrompt.cs` (+uid): prompt "[glifo] Verbo objeto" (TextPrimary 36, contorno, billboard, sem depth test, glifo por dispositivo atualizado em `UiInput.DeviceChanged`, × no PS por falta de ✕ na fonte), remove prefixos antigos das chaves existentes; títulos irmãos em Gold 48 a ≥2,2 m sem depth test; `ConsumeInteract(node)` ignora `interact` se UI bloqueava (ou árvore pausada) neste frame ou no anterior (estado amostrado em `SceneTree.ProcessFrame`). Aplicado em BloodFountain, ArtifactChest, WeaponBench, ScenePortal (prompt a 3,2 m em portais de sala) e RoomArtifact (título adiado). Orquestrador aplicou `ConsumeInteract` também em `RunStartBlessingRoom.cs:32`; T5 avisada para usar no IntermissionChamber. Verificação: build 0/0; smokes CrowsNest/TestArena/IntermissionChamber sem SCRIPT ERROR; capturas `o8_*` (posição do player alterada temporariamente e restaurada, `git diff` limpo no .tscn). Pendências: títulos sem depth test aparecem através de paredes; título perto da câmera fica grande; `CoffinLabel` e títulos de sala (`RunManager.CreateRoomLabel`, `RoomGenerator.cs:670`) ainda vermelhos; "Bau"/"Caixao" sem acento nos .tscn; prompt 2D no HUD não feito; validar fechar com A/✕ sem reabrir jogando.

### T9 — Corrigir prompts 3D, títulos e altar
- **Executor:** `designer-ux-arte`
- **Faz:** T6 itens 1, 2, 7, 8, 9; alocação por frame do `InteractPrompt` (T7 item 10); "Aprimorar" do rodapé do altar só com card focado (T7 item 6).
- **Resultado:** (1) prompts 3D atualizam o glifo: handler de `DeviceChanged` inscrito na criação + `Refresh` em `TreeEntered`/`VisibilityChanged` — hub mostra [A]/[×]; (2) altar: efeitos booleanos viram "Nome: mantido (parâmetros)", sem seta, e aviso "não altera valores numéricos" quando nada escala (parâmetros não escalam com nível em `RunState.ApplyBlessingEffect`); (3) títulos/prompts com `FixedSize` (PixelSize 0,0011), títulos só até 9 m (histerese) e escondidos sob a moldura do topo do HUD; (4) `AltarTitle`/`RoomTitle` (`RunManager`) e pontos de interesse (`RoomGenerator`) em Gold via `StyleTitle`; (5) "Baú"/"Caixão" acentuados no `CrowsNest.tscn`; (6) HUD/jogador em cache, `StringName` estáticos; (7) "Aprimorar" do rodapé só escolhe com card focado. Orquestrador completou o item 5 estilizando o `CoffinLabel` em `CrowsNestCrypt._Ready` (≥2,2 m). Verificação: build 0/0; smoke 300 quadros (3 cenas) limpo; capturas `o9_*`. Pendências: **decisão de design** — bênçãos só booleanas (`orlok_infinite_pestilent_blood`, `stirps_pestilent_blood`, `stirps_crimson_vow`, `stirps_crimson_pestilence_pool`) podem ser oferecidas no altar e cobrar 5% sem ganho; `TrainingHallLabel` com estilo próprio; pontos de interesse sem acento ("Saida", "Portao Final") — fase 4; títulos sem depth test aparecem sobre inimigos; validar jogando (histerese, troca de dispositivo ao vivo, item 7).

### T11 — Nomes PT dos efeitos de artefato e flag `IsImplemented` de arma
- **Executor:** `conteudo-run`
- **Faz:** T6 item 5 (nomes PT e unidades em `ArtifactEffectRegistry.FriendlyName`); T7 item 9 (`WeaponDefinition.IsImplemented`, usado pela bancada no lugar da heurística).
- **Resultado:** `ArtifactEffectRegistry.Names` cobre os 96 tipos de efeito de artefatos e bênçãos (0 caem no `SplitPascalCase`; tabela em scratchpad `o11_effect_names.txt`), nomes antigos acentuados; helpers novos `HasFriendlyName`, `FormatValue(type, op, value)` (%, s, %/s, sinal só em Add/Scale, booleano ativo/inativo, vírgula decimal) e `FormatEffect`. `WeaponDefinition.IsImplemented` (placeholders = false); bancada usa a flag e a regra fica em `AllowEquipPlaceholderWeapons = false` (aguarda confirmação do usuário). Verificação: build 0/0; validadores 20/0/0 e 63/0/0 (os avisos "sem referência C#" sumiram porque o registro agora cita todos os tipos — novo baseline); capturas `o11_*`; smoke limpo. Pendências: UI ainda usa formatadores locais ("+5,25" sem "s", "+0,2" em vez de "+20%") — T10 deve trocar por `FormatEffect`; compêndio (`HasFriendlyEffectNames`, `HUD.Compendium.cs:1133`) passará a listar efeitos de bênção — trocar por `HasFriendlyName`; "especiais" × "elite" a padronizar (fase 4).

### T10 — Corrigir input, Fonte, ofertas e cards
- **Executor:** `ui-hud`
- **Faz:** T7 altos 1 e 2 (cima/baixo legado em `HUD.cs:88-95`; reroll `ui_alt` roteado pelo topo da pilha), T7 baixos 5, 6 (bênção), 7, 8, 10 (Fonte); T6 itens 3, 4, 6, 10–16.
- **Resultado:** (1) bloco legado cima/baixo removido; escolha/reroll só com a oferta no topo da pilha; (2) reroll por `ui_alt`/Y via `RegisterModalShortcuts` nas duas ofertas (+ reserva na ficha); (3) Fonte: `DetailPanel` à direita (400), grade compacta, nome em 1 linha, zoom inicial "caber na altura" 85–100% — 6 linhas visíveis; (4) `ChoiceCard` mantém a cor da raridade no foco, anel externo + barra Gold, linha Stirps sem deslocar; (5) glossário só do card focado, 2 linhas fixas, texto neutro ao dispositivo; (6) bênção 1080 de largura; (7) efeitos via `ArtifactEffectRegistry.FormatEffect` na oferta, baú e ficha; Compêndio usa `HasFriendlyName`; (8) oferta não abre sobre o game over; (9) rodapé "Escolher" só com card focado; (10) reroll de artefato sorteia antes de consumir, "(0)" desabilitado; (11) ramos legados removidos de `CloseTopmostOverlay`/`IsGameplayInputBlocked`/`IsLegacyPauseRequested`; (12) `StringName` estáticos na Fonte; (13) moldura da Câmara mín. 360×76; (14) polimentos (dica Mouse oculta, EM BREVE único, selos com fundo, margem para anel de foco). Orquestrador aplicou `Width = 1080` no card do altar. Verificação: build 0/0; validadores 63/0/0 e 20/0/0; smoke 300 quadros em 4 cenas limpo; injeção de input (ficha por cima não escolhe, `ui_alt`/Y reroll com foco no card, rodapé sem foco) ok; capturas `o10_*`. Pendências: reroll real com skill e consumo só por leitura; zoom manual da Fonte não persiste; card focado com dois anéis (checar gosto); formatador local do Compêndio ainda imprime "+0,2"; título 3D "Fonte Restauradora" não aparece na captura da Câmara (efeito do corte de 9 m/moldura da T9?).

### T12 — Reverificar correções
- **Executor:** `orch:revisor`
- **Faz:** revisão dos diffs de T9–T11, build, validadores, smoke e capturas dos itens altos/médios.
- **Resultado:** 0 altos. Os 4 altos de T6/T7 resolvidos (confirmados por injeção de input e capturas); regras de jogo iguais ao baseline linha a linha (reroll de artefato agora sorteia antes de consumir — corrige fechamento indevido); D1 mantida; todas as telas na pilha modal; `InteractPrompt` sem vazamento; `CoffinLabel` ok; 16 `CallGroup` ok. "Fonte Restauradora" escondida na Câmara é a regra da moldura (esperado). Build 0/0; validadores 63/0/0 e 20/0/0; smoke 300 quadros nas 5 cenas limpo. Médio (pré-existente, não regressão): absorção "Sanguis Purgatus" fora da pilha/`IsGameplayInputBlocked` — Start durante a absorção abre a pausa e a oferta entra por cima; dá para andar/atacar. Baixos: "Escolher" do rodapé da oferta de artefato sem ação; zona de exclusão fixa ±420×104 do `InteractPrompt` vale mesmo sem moldura (hub); lista do altar sem `PadChoiceCardScroll` (anel cortado); reroll de bênção consome antes de sortear (antigo); sorteio do reroll de artefato marca o diário antes de consumir.

### T6 — Verificar visualmente os overlays de progressão
- **Executor:** `designer-ux-arte`
- **Faz:** capturas em 3 resoluções, consistência dos cards entre bênção/artefato/altar, foco visível; lista ajustes.
- **Pronto quando:** relatório com capturas e ajustes; severidade alta volta para T1–T5.
- **Resultado:** 30 capturas de overlay (3 dispositivos) + prompts 3D no hub e na Câmara (posição do player trocada temporariamente e restaurada, `git diff` limpo); 720p/1080p por cálculo. **Altos:** (1) prompts 3D do hub presos em "[E]" com controle (`InteractPrompt.CreatePrompt` não atualiza com o dispositivo; os da Câmara funcionam); (2) altar mostra "ativo → ativo" em efeitos booleanos. **Médios:** (3) Fonte mostra ~2,5 de 6 linhas a 100% e é ilegível a 40% — recomenda `DetailPanel` à direita (~400), nome do nó em 1 linha, zoom inicial "caber na altura" 85–100%; (4) foco num card Comum parece Mítico (borda clareada vira marfim); (5) efeitos de artefato com ID em inglês (`FriendlyName`); (6) glossário da oferta cita "botão esquerdo" e empurra o 3º card; (7) títulos 3D perto da câmera enormes/cortados (sugere `FixedSize`); (8) títulos de sala ainda vermelhos (`RunManager.cs:351,374`, `RoomGenerator.cs:670`); (9) "Bau"/"Caixao" sem acento, `CoffinLabel` sem estilo. Baixos: moldura da Câmara 800×76; largura de cards diferente (bênção/altar ~1192 vs artefato 1080); linha Stirps desalinhada; títulos quebrando no DetailPanel; keycap "Mouse" no rodapé da ficha; "EM BREVE" duplicado e selos sem fundo no baú; formato de efeitos diferente entre bênção e artefato; acentos de dados (fase 4). Capturas `v6_*`.

### T7 — Revisar código dos overlays de progressão
- **Executor:** `orch:revisor`
- **Faz:** revisa diffs de T1–T5 (regras de jogo intactas, `CallGroup`, pausa, foco), roda build, validadores e smoke.
- **Pronto quando:** sem achados de severidade alta.
- **Resultado:** build 0/0, validadores no baseline, smoke 300 quadros nas 5 cenas limpo; regras de jogo intactas linha a linha; 16 `CallGroup` ok; D1 ok; Push/Pop balanceados. Confirmado por teste (projeto mínimo): no Godot 4.7 eventos de teclado/controle não sobem para o `GuiInput` do pai. **Altos:** (1) `HUD.cs:88-95` intercepta cima/baixo da oferta de bênção mesmo com a ficha por cima → A escolhe a bênção escondida (confirmado); (2) reroll "Espelho do Destino" por `ui_alt` não funciona com teclado/controle em `HUD.ArtifactOffer.cs:58` e `HUD.BlessingOffer.cs:275` (dicas do rodapé sem foco) — regressão. **Médios:** (3) ficha abre por baixo do altar (`ChamberInterface` mesmo layer do HUD, adicionado depois); (4) Dicionário (menus-sistema T8). **Baixos:** oferta de bênção sobre o game over se morrer durante a absorção; rodapé "Aprimorar"/"Escolher" sem card focado escolhe o primeiro (cobra sacrifício); reroll de artefato consome antes de sortear e dica some vs. "(0)" na bênção; ramo legado em `HUD.Pause.cs:505` esconde a oferta de artefato (contra D1, inalcançável); save antigo com arma placeholder equipada (recomenda `IsImplemented`); alocações por frame em `HUD.BloodFountain.cs:1010` e `InteractPrompt.cs:72`; previews marcam o diário em memória.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-04 19:20 | Decisão D1 do usuário: oferta de artefato obrigatória (T4) |
| 2026-10-05 15:35 | T12 concluída por orch:revisor (0 altos, 1 médio pré-existente, 5 baixos). Plano concluído |
| 2026-10-05 15:10 | T10 concluída por ui-hud; orquestrador aplicou largura 1080 ao card do altar. T12 disparada |
| 2026-10-05 14:50 | T9 concluída por designer-ux-arte; orquestrador estilizou o `CoffinLabel` (`CrowsNestCrypt.cs`) |
| 2026-10-05 14:35 | T10 disparada (menus-sistema T8 concluída) |
| 2026-10-05 14:20 | T11 concluída por conteudo-run |
| 2026-10-05 14:05 | T6 concluída por designer-ux-arte (retomada; 2 altos, 7 médios, 8 baixos). Replanejado: T9 (designer-ux-arte), T11 (conteudo-run) disparadas; T10 (ui-hud) aguarda menus-sistema T8; T12 reverificação |
| 2026-10-05 13:50 | T7 concluída por orch:revisor (retomada; 2 altos, 2 médios, 7 baixos). Correções aguardam T6 e menus-sistema T8 (conflito em HUD.cs/HUD.Pause.cs); menus T8 avisada para bloquear a ficha com a pilha aberta |
| 2026-10-05 13:00 | T1 concluída por ui-hud. T6 e T7 disparadas |
| 2026-10-05 12:45 | T5 concluída por designer-ux-arte |
| 2026-10-05 12:25 | T8 concluída por designer-ux-arte; orquestrador aplicou `ConsumeInteract` em `RunStartBlessingRoom.cs` |
| 2026-10-05 12:10 | T4 concluída por ui-hud. T5 disparada |
| 2026-10-05 12:00 | T3 concluída por designer-ux-arte. T1 disparada |
| 2026-10-05 11:35 | T2 concluída por ui-hud. T8 disparada |
| 2026-10-05 11:20 | T3 disparada |
| 2026-10-05 11:05 | T4 disparada (RunState liberado por menus-sistema T6) |
| 2026-10-05 10:10 | Execução iniciada em paralelo com hud-menus-sistema (3 tarefas dele rodando → 1 vaga). T2 disparada (caminho crítico). Ordem seguinte: T4 só após hud-menus-sistema T6 (RunState em edição), demais conforme vagas |
| 2026-10-04 19:05 | Ajustado com o guia: nova T8 (rótulos 3D/prompts e `interact` bloqueado); T5 torna `OpenUpgradeOverlay` público |

## Resultado final

Ficha (abas, bênçãos acessíveis por controle), Fonte de Sangue (estados de nó, ligações, detalhe à direita, zoom/pan por controle, 6 linhas visíveis), ofertas de bênção e artefato e altar com o mesmo `ChoiceCard` (raridade incl. Mítico marfim), oferta de artefato **obrigatória** (D1, opções guardadas por fonte), baú e bancada refeitos pelo guia, rótulos 3D com glifo por dispositivo e `interact` bloqueado com UI aberta, efeitos com nome PT e formato único, atalhos roteados pelo topo da pilha modal.

**Arquivos:** `scripts/ui/{HUD,HUD.CharacterSheet,HUD.BloodFountain,HUD.BlessingOffer,HUD.ArtifactOffer,HUD.ArtifactChest,HUD.WeaponBench,HUD.Notifications,HUD.Pause,HUD.StatusBars,UiComponents (ChoiceCard),UiPreview}.cs`, `scripts/systems/{InteractPrompt (novo),IntermissionChamber,BloodFountain,ArtifactChest,WeaponBench,ScenePortal,RunStartBlessingRoom,CrowsNestCrypt,RunManager,RoomGenerator}.cs`, `scripts/items/RoomArtifact.cs`, `scripts/systems/artifacts/ArtifactEffectRegistry.cs`, `scripts/systems/weapons/{WeaponDefinition,WeaponCatalog}.cs`, `scenes/game/CrowsNest.tscn` (acentos).

**Decisões pendentes do usuário:** bancada bloqueia armas sem moveset (`AllowEquipPlaceholderWeapons`); bênçãos só booleanas no altar cobram 5% sem ganho.

**Pendências técnicas:** absorção fora da pilha modal (pré-existente); 5 baixos de T12; textos novos com fallback PT (fase 4); validar jogando com controle real (reroll com a skill, troca de dispositivo ao vivo, histerese de 9 m, 720p/1080p reais).

<!-- orch:plano · id: 20261004-1829-hud-menus-sistema · status: concluido · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
# HUD e menus — Fase 3b: menus de sistema

**Demanda original:** revisar, ajustar e melhorar a HUD inteira do jogo, de todos os menus e etc..
**Grupo:** 20261004-1829-hud-auditoria · **Depende dos planos:** 20261004-1829-hud-fundacao

## Objetivo

Menu principal (inclusive slots, opções e créditos), pausa, opções, ajustes de tuning, game over e compêndio refeitos segundo o guia, com navegação completa por controle.

## Suposições e decisões

- Segue `docs/design/ui-guia.md` e usa os componentes da fase 2; não edita `UIThemeFactory`/`UiComponents`.
- Textos continuam onde estão; a fase 4 move tudo para recursos por idioma (`data/localization/<idioma>/`).
- Ferramentas de debug (ajustes de tuning, "Reiniciar teste") seguem visíveis só em build de debug; ganham separação visual clara do menu do jogador.
- O fluxo de pausa (`SetModalPause`, `RefreshModalPause`, `CloseTopmostOverlay` em `HUD.Pause.cs`) não muda de comportamento — só de aparência.
- Pode rodar em paralelo com `hud-ingame` e `hud-overlays-progressao`.
- Decisão D2 do usuário: o compêndio vira **diário de descobertas** no release (entradas não descobertas bloqueadas, "???"); em debug mostra tudo, com IDs e editores. O registro de descobertas é estado novo salvo por slot (T6/T7); o compêndio (T3) consome a API de T6.
- Decisão D4: o log de eventos só existe em debug (implementado em `hud-ingame` T2).

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Refazer menu principal: slots, opções e créditos | `designer-ux-arte` | — | `scripts/ui/MainMenu.cs` | concluida |
| T2 | Refazer pausa, opções, ajustes e game over | `ui-hud` | — | `scripts/ui/HUD.Pause.cs` | concluida |
| T3 | Refazer compêndio como diário de descobertas | `ui-hud` | T6 | `scripts/ui/HUD.Compendium.cs` | concluida |
| T6 | Criar registro de descobertas salvo por slot | `conteudo-run` | — | `scripts/systems/CodexProgress.cs` (novo), `scripts/systems/CharacterSheet.cs`, `scripts/systems/SaveGameManager.cs`, `scripts/systems/RunState.cs` | concluida |
| T7 | Registrar descoberta de inimigos e bosses | `inimigos-e-bosses` | T6 | `scripts/systems/RunManager.cs`, `scripts/enemies/EnemyBasic.cs`, `scripts/player/PlayerCombat.cs` (3 linhas de hook de ataque) | concluida |
| T4 | Verificar visualmente os menus de sistema | `designer-ux-arte` | T1, T2, T3 | — (leitura) | concluida |
| T8 | Corrigir achados de T4/T5 | `ui-hud` | T4, T5 | `scripts/ui/HUD.Pause.cs`, `scripts/ui/HUD.Compendium.cs`, `scripts/ui/HUD.cs`, `scripts/ui/MainMenu.cs`, `scripts/ui/UiComponents.cs` (Confirm), `scripts/ui/UiPreview.cs`, `scripts/ui/GameCursor.cs`, `scripts/systems/SaveGameManager.cs`, `scripts/systems/CodexProgress.cs` | concluida |
| T5 | Revisar código dos menus de sistema | `orch:revisor` | T1, T2, T3, T6, T7 | — (leitura) | concluida |

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T6
Onda 2: T3 ∥ T7       (após T6)
Onda 3: T4 ∥ T5       (após T1, T2, T3; T5 também após T6, T7)
Onda 4: T8            (após T4, T5)
```

Caminho crítico: T6 → T3 → T4.

## Detalhe das tarefas

### T1 — Refazer menu principal: slots, opções e créditos
- **Executor:** `designer-ux-arte`
- **Faz:** slots com resumo legível (moedas, data) e ação de excluir com confirmação; opções com rótulos e controles alinhados; créditos rolando bem com controle; foco e `ui_cancel` consistentes.
- **Pronto quando:** capturas `--ui-preview=menu_slots|menu_options|menu_credits` batem com o guia; excluir slot pede confirmação; build limpo.
- **Resultado:** `MainMenu.cs` refeito: identidade preservada; main (Jogar/Opções/Créditos/Sair); slots de 72 px com data, `CoinGlyph`+moedas, ranks e artefatos via `GetSlotInfo`, "Novo jogo" em Gold, Excluir (Destructive) com `UiComponents.Confirm` empilhado no `UiModalStack` (foco em Cancelar), foco inicial no último slot jogado; opções = `OptionsPanel(true)` reconstruído ao trocar idioma; créditos com `ScrollList` focável, d-pad e analógico direito; `ui_cancel` volta à página anterior (main não faz nada); HintBar por dispositivo + versão; preview novo `menu_confirm_delete`. Textos novos via `TextOr(chave, PT)`. Verificação: build 0/0; smoke MainMenu sem SCRIPT ERROR; capturas em scratchpad `t1_*`. Pendências: anel de foco de `ScrollContainer` não aparece no Godot 4.7 (contornado com moldura local; levar para `MakeScrollFocusable`); `--ui-device=ps` não aplicou na captura (GameCursor volta para teclado?); resumo do slot em PT nos outros idiomas (fase 4); validar com controle real e janelas 720p/1080p.

### T2 — Refazer pausa, opções, ajustes e game over
- **Executor:** `ui-hud`
- **Faz:** menu de pausa com hierarquia clara (continuar, opções, compêndio, desistir da run, menu principal), desistir com confirmação, opções iguais às do menu principal, ajustes de tuning como seção de debug, game over com causa da morte e ação clara.
- **Pronto quando:** capturas `--ui-preview=pause|options|game_over` ok; fluxo de pausa sem regressão (abrir/fechar com Esc/B, pausa modal); build limpo.
- **Resultado:** `HUD.Pause.cs` reescrito: pausa em `Window` média (Continuar/Opções/Compêndio; DevSection com Ajustes e Reiniciar teste só em debug; Desistir da run e Menu principal destrutivos com `Confirm` empilhado no `UiModalStack`, foco em Cancelar); opções = `OptionsPanel(false)` (mesmo do menu); tuning em DevSection com lista de `LabeledSlider` (corrigido bug do clamp em 100; sync com `SetValueNoSignal`; salvar com `ui_alt`); game over 640 px com causa/último golpe/resumo da run e botões Ninho do Corvo (foco) / Menu principal, empurrado como `mandatory, pausesAudio:false` após `UiModalStack.Clear()` (fica acima dos demais overlays). Pausa continua camada legada (de propósito, para não ficar sobre compêndio/ficha). HUD.cs: 22+ campos mortos removidos, previews de pausa movidos para `OpenPausePreviewScreen`. Verificação: build 0/0; capturas `t2_*` (inclui PS ok); smoke TestArena/CrowsNest sem SCRIPT ERROR. Pendências: janela média cresce para ~700 px em opções/tuning (números da fundação); título "PAUSADO" (chave, fase 4); título da sala/log aparecem sob o scrim da pausa (`HUD._Process`); borda inferior do `DevSection` cortada (fundação); "Compêndio" vs "Dicionário"; textos de confirmação a revisar; dois scrims escurecem demais; moedas do game over são o total do slot (RunState não rastreia ganho da run); validar com controle real.

### T3 — Refazer compêndio
- **Executor:** `ui-hud`
- **Faz:** abas, listas e fichas no padrão do guia; no release mostra só entradas descobertas (as demais como "???" bloqueadas) usando a API de T6; em `OS.IsDebugBuild()` mostra tudo, com IDs e os editores de valores numa seção DEV separada; navegação por controle entre abas e itens.
- **Pronto quando:** captura `--ui-preview=compendium` de cada aba legível; build limpo.
- **Resultado:** `HUD.Compendium.cs` reescrito como "Dicionário": `Window` Full com `Tabs` (Artefatos, Bênçãos, Habilidades, Ataques, Inimigos, Armas — nova), contador "Descobertos x/y" (`IsDiscoveredRaw`), lista focável + `DetailPanel` 560 px; `UiModalStack.Push(pauses:true)`/`Pop` (foco volta à pausa). Release: "???" com cadeado e "Ainda não descoberto" + dica; debug: tudo visível com selo "DEV: tudo visível" e seção DEV no detalhe (ID, estado no save, ícone, editores — chaves antigas preservadas, aplica com 0,2 s; "Redefinir valores" com confirmação). Inimigos: 1 entrada por arquétipo (variantes de raridade no detalhe) + 1 por chefe do `BossCatalog`. Preview `--ui-codex-locked[=sample]` simula release sem gravar. Verificação: build ok; smoke TestArena sem SCRIPT ERROR; capturas `t3_*`/`t3_v2_*` (debug, xbox, bloqueado). Pendências: chaves `attack.light_N/special/dash.*` não têm efeito (PlayerCombat lê `weapon.<id>.*`; editores dessas adicionados na aba Armas) — decisão; padrões de vida/dano de inimigo corrigidos para 110/22; falta `BossCatalog.All`; comentário do `UiPreview.cs` sem `Weapons`/`--ui-codex-locked`; `HUD.Pause.cs` ainda esconde o compêndio pelo caminho antigo (redundante); nomes de arquétipos inventados ("Lâmina Sombria", "Demônio Atirador", "Orc Brutamontes") a validar; textos PT literais (fase 4).

### T6 — Criar registro de descobertas salvo por slot
- **Executor:** `conteudo-run`
- **Faz:** `CodexProgress` com categorias (artefato, bênção, skill, inimigo, boss, ataque), `MarkDiscovered(categoria, id)` e `IsDiscovered`; persistência no `GameSaveData` (campo novo, saves antigos carregam com lista vazia, sem quebrar), reset junto com o progresso permanente; registro automático quando o jogador recebe/vê ofertas de bênção e artefato, compra skill e usa ataques do moveset (hooks em `RunState`/`CharacterSheet`). Em debug, `IsDiscovered` retorna sempre verdadeiro.
- **Pronto quando:** descobertas persistem entre sessões no slot; save antigo carrega; build limpo.
- **Resultado:** `scripts/systems/CodexProgress.cs` (+uid). Categorias `CodexCategory` (string persistida): artifact, blessing, skill, enemy (`melee` e `melee:rare`), boss (`BossCatalog` Id), attack (`CodexAttackId`: light_1..3, special, dash, fury), weapon. API: `MarkDiscovered/IsDiscovered` (debug sempre true) / `IsDiscoveredRaw` / `Count` / `TotalCount` / `GetDiscovered` / `MarkEnemy` / `MarkBoss` / `MarkAttack` / evento `Discovered` / `Reset` / `ToSaveData` / `LoadSaveData`. `GameSaveData.Discoveries` (save antigo → vazio); `SlotInfo.DiscoveryCount`; CharacterSheet grava/carrega/reseta e na carga marca skills, artefatos e arma já possuídos. Hooks: `AddSkill`, `AddArtifact`, `RunState.AddBloodBlessing/AddTemporaryArtifact/StartRun`, e ofertas em `BloodBlessingCatalog.RollOffers` e `ArtifactCatalog.RollChoices` (catálogos editados porque o sorteio é chamado pelo HUD). Verificação: build 0/0; validadores no baseline; smoke TestArena sem SCRIPT ERROR; teste .NET de persistência no scratchpad `codextest/` (save antigo, null, repetição sem save, round-trip). Pendências: marcação de ataques em `PlayerCombat.cs` (BeginAttack ~221, BeginQuickAttack ~324, ActivateFury ~856) → passada para T7; visto = obtido (sem distinção); troca de arma na bancada só registra no próximo StartRun.

### T7 — Registrar descoberta de inimigos e bosses
- **Executor:** `inimigos-e-bosses`
- **Faz:** chama `CodexProgress.MarkDiscovered` quando um arquétipo/raridade de inimigo aparece pela primeira vez e quando um boss é enfrentado (em `RunManager`/`EnemyBasic`), sem custo por frame.
- **Pronto quando:** inimigos e bosses entram no diário ao serem encontrados; build e smoke limpos.
- **Resultado:** hooks: `RoomGenerator.SpawnNextWave` (ramo não-boss, `MarkEnemy(archetype, rarity)` por inimigo instanciado; Legendary comuns do mapa 2+ marcados corretamente); `RunManager.ConfigureBossRoom:242` `MarkBoss(BossCatalog.Get(mapIndex).Id)` antes do `BindBoss`; `PlayerCombat.cs` 223 (`BeginAttack`: Special ou `Light(_lightComboIndex)`, índice conferido), 326 (Dash), 859 (Fury). `EnemyBasic.cs` e `BossController.cs` intactos. Verificação: build 0/0; smoke 400 quadros sem SCRIPT ERROR; run de 3000 quadros com print temporário (removido) mostrou o total subindo só para variantes novas. Boss e ataques só por leitura de código — validar jogando.

### T4 — Verificar visualmente os menus de sistema
- **Executor:** `designer-ux-arte`
- **Faz:** capturas em 3 resoluções; confere foco visível e contra o guia; lista ajustes.
- **Pronto quando:** relatório com capturas e ajustes; severidade alta volta para T1–T3.
- **Resultado:** 52 capturas (menu, pausa, Dicionário × kbm/xbox/ps × pt-BR/en/es) + janela real 720p; 1080p por cálculo (a resolução salva sobrepõe `--resolution`). 0 altos. Glifos xbox/ps corretos — o "volta a teclado" é artefato do preview (mouse do sistema sobre a janela → `GameCursor`), não bug. Médios: (1) HUD de combate visível sob o scrim da pausa e no game over (§8.11); (2) dois scrims na confirmação e rodapé da pausa visível com ação errada; (3) `Confirm` sem rodapé de dicas; (4) "Compêndio" na pausa × "Dicionário" na janela; (5) detalhe do Dicionário segue o hover do mouse parado em vez do foco; (6) textos internos em inglês no detalhe do Dicionário (efeitos `Blood Pool Area`, raridades "Common ×1"). Baixos: largura da pausa varia (640→~710); borda do DevSection cortada na pausa; "PAUSADO" em caixa alta; textos PT em en/es e nomes sem acento (fase 4); opções da pausa sem Voltar; rótulo do tuning quebra; game over com botões de larguras diferentes; "EM BREVE" sem fundo, "???" mais apagado que secundário, monogramas com caixa mista; seletor de idioma do menu ignora `--ui-lang`; colunas do menu 648×640; Excluir desabilitado em slot vazio (ocultar); preview da pausa sem run ativa. Capturas `v4_*`.

### T8 — Corrigir achados de T4/T5
- **Executor:** `ui-hud`
- **Faz:** médios de T4 (1–6) e baixos de T5 (1–7) e de T4 que forem baratos; bug confirmado em hud-overlays T1: `OnCompendiumGuiInput` na raiz não recebe L1/R1 nem RS com foco numa linha. Edição de `UiComponents.Confirm` (fundação) autorizada.
- **Pronto quando:** médios resolvidos e conferidos em captura; build e smoke limpos.
- **Resultado:** médios: HUD de combate agrupada numa camada `CombatHud` escondida com pausa/game over/Dicionário; Confirm empilhado com scrim 0,4 e rodapé da camada de baixo transparente; Confirm com HintBar (aceitar = "Escolher", porque o foco inicial é Cancelar); botão "Dicionário" na pausa; hover do Dicionário só com movimento real do mouse (150 ms) e foco = destaque único; detalhe de bênção com formatadores da oferta/ficha e raridades localizadas. Bug de atalhos: roteamento genérico `HUD.RegisterModalShortcuts(layer, handler)` + `RouteModalShortcut` no início de `_UnhandledInput` pelo `UiModalStack.Top` (Dicionário registrado; tuning `ui_alt` pela pausa). Baixos: `SlotInfo.Corrupted` ("Save ilegível", confirmação, cópia `.unreadable.json`, PushError uma vez), `FlushPendingSave` antes do menu, `CloseCompendium`/`FlushCompendiumApply`, ficha só alterna no topo ou sem modal, dicas de bloqueio alinhadas à regra, `CodexAttackId.Light` sem alocação, "Pausa", Voltar nas opções, game over 280 px, badges com fundo, `UiPreview.DeviceLocked`, preview da pausa com run ativa. Verificação: build 0/0; validadores 63/0/0 e 20/0/0; smoke 300 quadros em 4 cenas limpo; injeção de input (L1/R1, RB/LB, RS, `ui_alt`/Y) ok com tuning restaurado (`cmp` idêntico); capturas `m8_*`. Pendências: validar com controle/mouse reais; painel do Confirm alpha 0,97 deixa vazar texto; chaves novas só com fallback PT (fase 4); conferir a Câmara com o novo `Root/CombatHud`.

### T5 — Revisar código dos menus de sistema
- **Executor:** `orch:revisor`
- **Faz:** revisa diffs de T1–T3 (pausa, foco, debug-only, `CallGroup`), roda build e smoke.
- **Pronto quando:** sem achados de severidade alta.
- **Resultado:** 0 altos. Build 0/0; validadores no baseline; smoke 300 quadros nas 5 cenas sem SCRIPT ERROR. Conferidos: fluxo de pausa/confirmações/game over/troca de cena; compêndio↔pausa pela pilha; debug-only (DevSection, tuning, `--ui-codex-locked`, `DebugUnlockAll`); 16 nomes de `CallGroup`; saves antigos; padrões de tuning batem com RunManager/PlayerCombat; handlers desinscritos. **Médio (corrigido pelo orquestrador):** `SaveGameManager.GetSlotInfo` sem proteção para `SkillRanks: null` travaria o menu principal. Baixos: (1) save corrompido aparece como "Novo jogo" e é sobrescrito sem aviso, `PushError` repetido; (2) `ReturnToMainMenu` não faz `FlushPendingSave` antes de trocar de cena (slot mostra dados velhos); (3) `ResumeGame`/`HideOverlaysForGameOver` escondem o compêndio sem `FlushCompendiumApply` (debug); (4) tecla da ficha abre por baixo do compêndio/pausa e rouba foco (`HUD.cs:113-117`); (5) dicas de bloqueio do Dicionário dizem "escolha" mas a descoberta ocorre ao ser oferecida; skills só entram ao comprar mas a Fonte mostra nomes; (6) `CodexAttackId.Light` aloca string por ataque; (7) ramo legado do compêndio em `CloseTopmostOverlay`.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-05 14:35 | T8 concluída por ui-hud. Plano concluído |
| 2026-10-05 13:40 | T4 concluída por designer-ux-arte (retomada após limite de API; 0 altos, 6 médios). Replanejado: T8 (ui-hud) corrige achados de T4/T5; disparada |
| 2026-10-05 12:35 | T5 concluída por orch:revisor (0 altos, 1 médio — corrigido pelo orquestrador em `SaveGameManager.cs:72` —, 7 baixos). Correções dos baixos agrupadas para depois de T4 |
| 2026-10-05 12:25 | T4 (verificação visual) disparada |
| 2026-10-05 11:50 | T3 concluída por ui-hud. T5 (revisão) disparada; T4 (visual) aguarda vaga |
| 2026-10-05 11:20 | T7 concluída por inimigos-e-bosses. Vaga usada por hud-overlays-progressao T3 |
| 2026-10-05 11:05 | T2 concluída por ui-hud. Vaga usada por hud-overlays-progressao T4 |
| 2026-10-05 10:50 | T1 concluída por designer-ux-arte. T7 disparada |
| 2026-10-05 10:40 | T6 concluída por conteudo-run. T3 disparada (1 vaga no limite de 4); T7 recebe também os 3 hooks de ataque em `PlayerCombat.cs` |
| 2026-10-05 10:00 | Execução iniciada (commit 19a412b, branch feat/hud-reforma). Orquestrador adicionou `SaveGameManager.GetSlotInfo` + record `SlotInfo` para T1 ler o slot sem conflitar com T6. T1, T2, T6 disparadas |
| 2026-10-04 19:20 | Decisão D2 do usuário: compêndio vira diário de descobertas; novas T6 (conteudo-run) e T7 (inimigos-e-bosses); T3 depende de T6 |

## Resultado final

Menu principal (slots com resumo, excluir com confirmação, save ilegível tratado, opções compartilhadas, créditos roláveis), pausa (hierarquia, confirmações empilhadas, ferramentas DEV separadas, tuning corrigido), game over (causa e resumo, acima de tudo, sem pausar música) e Dicionário como **diário de descobertas** (D2: release bloqueia "???", debug mostra tudo com IDs e editores) refeitos pelo guia, com navegação por controle e atalhos roteados pelo topo da pilha modal. Registro `CodexProgress` salvo por slot, com marcação de artefatos, bênçãos, skills, arma, inimigos (arquétipo e raridade), chefes e ataques.

**Arquivos:** `scripts/ui/{MainMenu,HUD,HUD.Pause,HUD.Compendium,UiComponents (Confirm),UiPreview,GameCursor}.cs`, `scripts/systems/{CodexProgress (novo),CharacterSheet,SaveGameManager,RunState,RoomGenerator,RunManager}.cs`, `scripts/systems/{artifacts/ArtifactCatalog,blessings/BloodBlessingCatalog}.cs`, `scripts/player/PlayerCombat.cs`.

**Pendências:** decisões do usuário — editores de tuning `attack.light_N/special/dash.*` sem efeito (remover ou religar), nomes de arquétipos de inimigo inventados; textos novos com fallback PT (fase 4); painel do Confirm translúcido; validar com controle real, janela 1080p real, sala de chefe (diário) e combo/fúria.

<!-- orch:plano · id: 20261004-1829-hud-menus-sistema · status: planejado · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
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
| T1 | Refazer menu principal: slots, opções e créditos | `designer-ux-arte` | — | `scripts/ui/MainMenu.cs` | pendente |
| T2 | Refazer pausa, opções, ajustes e game over | `ui-hud` | — | `scripts/ui/HUD.Pause.cs` | pendente |
| T3 | Refazer compêndio como diário de descobertas | `ui-hud` | T6 | `scripts/ui/HUD.Compendium.cs` | pendente |
| T6 | Criar registro de descobertas salvo por slot | `conteudo-run` | — | `scripts/systems/CodexProgress.cs` (novo), `scripts/systems/CharacterSheet.cs`, `scripts/systems/SaveGameManager.cs`, `scripts/systems/RunState.cs` | pendente |
| T7 | Registrar descoberta de inimigos e bosses | `inimigos-e-bosses` | T6 | `scripts/systems/RunManager.cs`, `scripts/enemies/EnemyBasic.cs` | pendente |
| T4 | Verificar visualmente os menus de sistema | `designer-ux-arte` | T1, T2, T3 | — (leitura) | pendente |
| T5 | Revisar código dos menus de sistema | `orch:revisor` | T1, T2, T3, T6, T7 | — (leitura) | pendente |

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T6
Onda 2: T3 ∥ T7       (após T6)
Onda 3: T4 ∥ T5       (após T1, T2, T3; T5 também após T6, T7)
```

Caminho crítico: T6 → T3 → T4.

## Detalhe das tarefas

### T1 — Refazer menu principal: slots, opções e créditos
- **Executor:** `designer-ux-arte`
- **Faz:** slots com resumo legível (moedas, data) e ação de excluir com confirmação; opções com rótulos e controles alinhados; créditos rolando bem com controle; foco e `ui_cancel` consistentes.
- **Pronto quando:** capturas `--ui-preview=menu_slots|menu_options|menu_credits` batem com o guia; excluir slot pede confirmação; build limpo.
- **Resultado:** —

### T2 — Refazer pausa, opções, ajustes e game over
- **Executor:** `ui-hud`
- **Faz:** menu de pausa com hierarquia clara (continuar, opções, compêndio, desistir da run, menu principal), desistir com confirmação, opções iguais às do menu principal, ajustes de tuning como seção de debug, game over com causa da morte e ação clara.
- **Pronto quando:** capturas `--ui-preview=pause|options|game_over` ok; fluxo de pausa sem regressão (abrir/fechar com Esc/B, pausa modal); build limpo.
- **Resultado:** —

### T3 — Refazer compêndio
- **Executor:** `ui-hud`
- **Faz:** abas, listas e fichas no padrão do guia; no release mostra só entradas descobertas (as demais como "???" bloqueadas) usando a API de T6; em `OS.IsDebugBuild()` mostra tudo, com IDs e os editores de valores numa seção DEV separada; navegação por controle entre abas e itens.
- **Pronto quando:** captura `--ui-preview=compendium` de cada aba legível; build limpo.
- **Resultado:** —

### T6 — Criar registro de descobertas salvo por slot
- **Executor:** `conteudo-run`
- **Faz:** `CodexProgress` com categorias (artefato, bênção, skill, inimigo, boss, ataque), `MarkDiscovered(categoria, id)` e `IsDiscovered`; persistência no `GameSaveData` (campo novo, saves antigos carregam com lista vazia, sem quebrar), reset junto com o progresso permanente; registro automático quando o jogador recebe/vê ofertas de bênção e artefato, compra skill e usa ataques do moveset (hooks em `RunState`/`CharacterSheet`). Em debug, `IsDiscovered` retorna sempre verdadeiro.
- **Pronto quando:** descobertas persistem entre sessões no slot; save antigo carrega; build limpo.
- **Resultado:** —

### T7 — Registrar descoberta de inimigos e bosses
- **Executor:** `inimigos-e-bosses`
- **Faz:** chama `CodexProgress.MarkDiscovered` quando um arquétipo/raridade de inimigo aparece pela primeira vez e quando um boss é enfrentado (em `RunManager`/`EnemyBasic`), sem custo por frame.
- **Pronto quando:** inimigos e bosses entram no diário ao serem encontrados; build e smoke limpos.
- **Resultado:** —

### T4 — Verificar visualmente os menus de sistema
- **Executor:** `designer-ux-arte`
- **Faz:** capturas em 3 resoluções; confere foco visível e contra o guia; lista ajustes.
- **Pronto quando:** relatório com capturas e ajustes; severidade alta volta para T1–T3.
- **Resultado:** —

### T5 — Revisar código dos menus de sistema
- **Executor:** `orch:revisor`
- **Faz:** revisa diffs de T1–T3 (pausa, foco, debug-only, `CallGroup`), roda build e smoke.
- **Pronto quando:** sem achados de severidade alta.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-04 19:20 | Decisão D2 do usuário: compêndio vira diário de descobertas; novas T6 (conteudo-run) e T7 (inimigos-e-bosses); T3 depende de T6 |

## Resultado final

—

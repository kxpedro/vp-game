<!-- orch:plano · id: 20261004-1829-hud-overlays-progressao · status: planejado · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
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
| T1 | Refazer ficha do personagem | `ui-hud` | — | `scripts/ui/HUD.CharacterSheet.cs` | pendente |
| T2 | Refazer Fonte de Sangue e árvore de skills | `ui-hud` | — | `scripts/ui/HUD.BloodFountain.cs` | pendente |
| T3 | Refazer oferta de bênçãos | `designer-ux-arte` | — | `scripts/ui/HUD.BlessingOffer.cs` | pendente |
| T4 | Refazer baú, oferta de artefatos e bancada de armas | `ui-hud` | — | `scripts/ui/HUD.ArtifactChest.cs`, `scripts/ui/HUD.ArtifactOffer.cs`, `scripts/ui/HUD.WeaponBench.cs` | pendente |
| T5 | Refazer overlay do altar da Câmara de Hemomancia | `designer-ux-arte` | — | `scripts/systems/IntermissionChamber.cs` | pendente |
| T8 | Refazer rótulos 3D/prompts e bloquear `interact` com UI aberta | `designer-ux-arte` | — | `scripts/systems/ArtifactChest.cs`, `scripts/systems/BloodFountain.cs`, `scripts/systems/WeaponBench.cs`, `scripts/systems/ScenePortal.cs`, `scripts/items/RoomArtifact.cs` | pendente |
| T6 | Verificar visualmente os overlays de progressão | `designer-ux-arte` | T1, T2, T3, T4, T5, T8 | — (leitura) | pendente |
| T7 | Revisar código dos overlays de progressão | `orch:revisor` | T1, T2, T3, T4, T5, T8 | — (leitura) | pendente |

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T3 ∥ T4 ∥ T5 ∥ T8   (até 4 simultâneas no total do grupo)
Onda 2: T6 ∥ T7                       (após T1–T5, T8)
```

Caminho crítico: T2 → T6 (árvore de skills é a maior tela).

## Detalhe das tarefas

### T1 — Refazer ficha do personagem
- **Executor:** `ui-hud`
- **Faz:** colunas (atributos, skills, bênçãos, artefatos) no padrão do guia, rolagem por controle, hierarquia de títulos/valores.
- **Pronto quando:** captura `--ui-preview=character_sheet` com dados de exemplo legível; build limpo.
- **Resultado:** —

### T2 — Refazer Fonte de Sangue e árvore de skills
- **Executor:** `ui-hud`
- **Faz:** nós e ligações legíveis (comprado/disponível/bloqueado/planejado), popup de descrição no padrão de tooltip, zoom/pan e navegação por controle entre nós, status de moedas claro; cheat de moedas segue só em debug.
- **Pronto quando:** captura `--ui-preview=blood_fountain` ok; compra continua funcionando; build limpo.
- **Resultado:** —

### T3 — Refazer oferta de bênçãos
- **Executor:** `designer-ux-arte`
- **Faz:** cards com família, raridade (cor), nível, ataque afetado e efeitos; reroll claro; foco por controle; mantém o VFX de absorção (sanguis trail) e a regra de oferta obrigatória.
- **Pronto quando:** captura `--ui-preview=blessing_offer` ok; seleção e reroll funcionam; build limpo.
- **Resultado:** —

### T4 — Refazer baú, oferta de artefatos e bancada de armas
- **Executor:** `ui-hud`
- **Faz:** baú com slots e coleção (botões de equipar da limpeza anterior) no padrão do guia; oferta de artefatos com o mesmo card das bênçãos (3–4 opções), **obrigatória** (D1: Esc/B não fecham, sem "Decidir depois", reabrir não re-sorteia — as opções sorteadas ficam guardadas até a escolha; modal não cancelável na `UiModalStack`); bancada de armas com arma atual e placeholders marcados.
- **Pronto quando:** capturas `--ui-preview=artifact_chest|artifact_offer|weapon_bench` ok; equipar/escolher funcionam; build limpo.
- **Resultado:** —

### T5 — Refazer overlay do altar da Câmara de Hemomancia
- **Executor:** `designer-ux-arte`
- **Faz:** torna `OpenUpgradeOverlay` público (para o `--ui-preview`); overlay do altar usando o card comum (remove a cópia de `BlessingRarityColor` e do layout de cards), custo de −5% de vida máxima explícito, cura da fonte com valor real (não o "35%" fixo).
- **Pronto quando:** captura na `IntermissionChamber` com o altar aberto (preview ou interação simulada) ok; build limpo.
- **Resultado:** —

### T8 — Refazer rótulos 3D/prompts e bloquear `interact` com UI aberta
- **Executor:** `designer-ux-arte`
- **Faz:** aplica a seção 8.8 do guia aos `Label3D` de título/prompt dos interativos (fonte, baú, bancada, portais, artefato de sala) com um helper comum; e faz a leitura de `interact` por polling ignorar o input quando `IsGameplayInputBlocked()` — hoje baú/fonte/bancada não pausam e apertar ✕/A dentro do overlay pode reabri-lo.
- **Pronto quando:** prompts legíveis e consistentes nas capturas do hub; `interact` não reabre overlays; build limpo.
- **Resultado:** —

### T6 — Verificar visualmente os overlays de progressão
- **Executor:** `designer-ux-arte`
- **Faz:** capturas em 3 resoluções, consistência dos cards entre bênção/artefato/altar, foco visível; lista ajustes.
- **Pronto quando:** relatório com capturas e ajustes; severidade alta volta para T1–T5.
- **Resultado:** —

### T7 — Revisar código dos overlays de progressão
- **Executor:** `orch:revisor`
- **Faz:** revisa diffs de T1–T5 (regras de jogo intactas, `CallGroup`, pausa, foco), roda build, validadores e smoke.
- **Pronto quando:** sem achados de severidade alta.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-04 19:20 | Decisão D1 do usuário: oferta de artefato obrigatória (T4) |
| 2026-10-04 19:05 | Ajustado com o guia: nova T8 (rótulos 3D/prompts e `interact` bloqueado); T5 torna `OpenUpgradeOverlay` público |

## Resultado final

—

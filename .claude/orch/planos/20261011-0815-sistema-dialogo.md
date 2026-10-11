<!-- orch:plano · id: 20261011-0815-sistema-dialogo · status: planejado · criado: 2026-10-11 08:15 · commit-inicial: bd370e4 · versao-orch: 0.5.0 -->
# Mecânica de diálogo no estilo Hades

**Demanda original:** Pedro (2026-10-11), texto colado: (1) analisar videos, imagens da gameplay do Hades e Sworn como referencia; verificar se o jogo e parecido mecanicamente, identificar e destacar as principais diferencas e se e possivel implementar; analisar toda e qualquer mecanica. (2) melhorar visualmente as animacoes de ataque do jogador e dos inimigos, deixar mais obvio quando um inimigo for atacar, usando Hades e Sworn como referencia. (3) desenvolver mecanica de dialogo, usando Hades como referencia. (4) focar um pouco nas configuracoes graficas do jogo, dar uma revisada.
**Grupo:** 20261011-0815-referencia-hades-sworn · **Depende dos planos:** 20261011-0815-referencia-hades-sworn

## Objetivo

Sistema de diálogo reutilizável: conversas com NPC no hub e falas curtas na run, caixa com retrato, nome e texto progressivo, escolhas opcionais, condições (primeira vez, boss derrotado, número da run), falas já vistas persistidas no save, tudo localizado nos 3 idiomas e jogável por teclado e controle. Entrega com 1 NPC de exemplo.

## Suposições e decisões

- Dados em `data/dialogue/*.json` (só IDs, condições e referências); textos em `data/localization/<idioma>/dialogue.json` (arquivo novo, T2 confirma o formato e o validador é ajustado se preciso).
- Conteúdo mínimo: 1 NPC no hub (definido pelo game-designer com `docs/design/personagens-futuros.md`) e 2–3 falas de run. Não é o roteiro completo.
- Save: apenas um campo opcional novo (falas vistas); sem renomear IDs e sem excluir saves.
- T1 e T2 não esperam o comparativo; o plano só depende dele para o conteúdo/prioridades de T5. T4 edita `ui.json` e `HUD*.cs`, por isso roda antes do plano de gráficos.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Pesquisar o sistema de diálogo do Hades (estrutura, gatilhos, prioridade, repetição, UI) | `orch:pesquisador` | — | — (leitura) | pendente |
| T2 | Projetar arquitetura, formato de dados e fluxo | `orch:planejador` | T1 | — (leitura) | pendente |
| T3 | Implementar o núcleo (catálogo, gerenciador, condições, persistência de falas vistas) | `conteudo-run` | T2 | `scripts/systems/dialogue/`, `data/dialogue/`, `scripts/systems/SaveGameManager.cs`, `scripts/systems/CharacterSheet.cs` | pendente |
| T4 | Criar a UI do diálogo (overlay, retrato, escolhas, controle) e integrar ao input | `ui-hud` | T3 | `scripts/ui/HUD.Dialogue.cs`, `scripts/ui/HUD.cs`, `scripts/ui/UiPreview.cs`, `data/localization/*/ui.json` | pendente |
| T5 | Escrever o conteúdo inicial e a localização (NPC do hub e falas de run) | `game-designer` | T3 | `data/dialogue/`, `data/localization/*/dialogue.json`, `docs/design/dialogos.md` | pendente |
| T6 | Revisar o sistema (save, input travando o jogo, localização, regressões) | `orch:revisor` | T4, T5 | — (leitura) | pendente |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1
Onda 2: T2
Onda 3: T3
Onda 4: T4 ∥ T5
Onda 5: T6
```

Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Pesquisar o sistema de diálogo do Hades (estrutura, gatilhos, prioridade, repetição, UI)
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Resumo com fontes: tipos de conversa, prioridade e condições, caixa de diálogo, voz, interrupções.
- **Resultado:** —

### T2 — Projetar arquitetura, formato de dados e fluxo
- **Executor:** `orch:planejador`
- **Pronto quando:** Spec com classes, JSON de exemplo, condições suportadas, integração com RunState/SaveGameManager/ScenePortal/input e plano de verificação.
- **Resultado:** —

### T3 — Implementar o núcleo (catálogo, gerenciador, condições, persistência de falas vistas)
- **Executor:** `conteudo-run`
- **Pronto quando:** Carrega o JSON com try/catch e PushError; avalia condições; API para abrir conversa; falas vistas gravadas no save; `dotnet build VP0.1.sln` com 0 erros/0 avisos; `.cs.uid` dos arquivos novos.
- **Resultado:** —

### T4 — Criar a UI do diálogo (overlay, retrato, escolhas, controle) e integrar ao input
- **Executor:** `ui-hud`
- **Pronto quando:** Overlay com texto progressivo, avançar/pular, escolhas com foco por controle, pausa o jogo, reage a `LanguageChanged`; tela no `--ui-batch` nos 3 idiomas; `dotnet build VP0.1.sln` com 0 erros/0 avisos; `py tools/validate_localization.py` sem erro.
- **Resultado:** —

### T5 — Escrever o conteúdo inicial e a localização (NPC do hub e falas de run)
- **Executor:** `game-designer`
- **Pronto quando:** Falas em pt-BR, en e es no tom dark fantasy e coerentes com a lore (Virgil, Armand, linhagens); paridade de chaves nos 3 idiomas; guia de escrita.
- **Resultado:** —

### T6 — Revisar o sistema (save, input travando o jogo, localização, regressões)
- **Executor:** `orch:revisor`
- **Pronto quando:** Sem achados altos; diálogo não trava jogo nem input ao fechar; saves antigos carregam.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 08:15 | Plano criado |

## Resultado final

—

<!-- orch:plano · id: 20261011-0815-referencia-hades-sworn · status: em-execucao · criado: 2026-10-11 08:15 · commit-inicial: bd370e4 · versao-orch: 0.5.0 -->
# Referência Hades e Sworn: comparativo mecânico e viabilidade

**Demanda original:** Pedro (2026-10-11), texto colado: (1) analisar videos, imagens da gameplay do Hades e Sworn como referencia; verificar se o jogo e parecido mecanicamente, identificar e destacar as principais diferencas e se e possivel implementar; analisar toda e qualquer mecanica. (2) melhorar visualmente as animacoes de ataque do jogador e dos inimigos, deixar mais obvio quando um inimigo for atacar, usando Hades e Sworn como referencia. (3) desenvolver mecanica de dialogo, usando Hades como referencia. (4) focar um pouco nas configuracoes graficas do jogo, dar uma revisada.
**Grupo:** 20261011-0815-referencia-hades-sworn · **Depende dos planos:** —

## Objetivo

Documento de comparação entre Vampire's Prey, Hades e Sworn cobrindo todas as mecânicas (combate, movimento, salas, recompensas, build, meta-progressão, narrativa, UI, game feel), com diferenças destacadas, viabilidade e prioridade de cada uma. Alimenta os planos de combate visual e diálogo.

## Suposições e decisões

- Os agentes não assistem vídeo: a pesquisa usa wikis, guias, análises e descrições de gameplay da web (WebSearch/WebFetch). Se Pedro tiver vídeos ou prints próprios, colocar prints em `docs/referencias/` (imagens são lidas; vídeo não).
- Saída única: `docs/design/comparativo-hades-sworn.md`, em pt-BR, com tabela mecânica × Hades × Sworn × VP × viabilidade (alta/média/baixa) × esforço. Nenhum código muda neste plano.
- Regra do projeto: o Hades é a referência primária de decisão de jogo; o Sworn entra principalmente em combate, telegraph e ritmo.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Levantar as mecânicas do Hades (combate, dash, salas, recompensas, Espelho, armas, bênçãos, diálogo, UI) | `orch:pesquisador` | — | — (leitura) | concluida |
| T2 | Levantar as mecânicas do Sworn (combate, esquiva, telegraph, classes, progressão, coop, salas) | `orch:pesquisador` | — | — (leitura) | concluida |
| T3 | Mapear as mecânicas atuais do Vampire's Prey a partir do código e dos docs | `orch:pesquisador` | — | — (leitura) | concluida |
| T4 | Redigir o comparativo, as diferenças principais e a viabilidade | `game-designer` | T1, T2, T3 | `docs/design/comparativo-hades-sworn.md` | concluida |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T3
Onda 2: T4
```

Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Levantar as mecânicas do Hades (combate, dash, salas, recompensas, Espelho, armas, bênçãos, diálogo, UI)
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Lista estruturada com fontes (links) cobrindo todas as categorias, com números de referência (i-frames do dash, cooldowns, duração de telegraph, tamanho de sala).
- **Resultado:** Hades levantado com fontes (Fextralife, RPG Site, Kotaku, Gamepressure, GDC): combate, 6 armas x 4 aspectos, boons (9 deuses, 4 raridades), Espelho, Pacto de Punição, God Mode, portas com símbolo. Sem frame data nem telegraph/hit-stop/diálogo-prioridade (não verificado).

### T2 — Levantar as mecânicas do Sworn (combate, esquiva, telegraph, classes, progressão, coop, salas)
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Idem T1 para o Sworn, destacando o que difere do Hades.
- **Resultado:** Sworn (Windwalk, coop 1-4, Camelot) levantado: 4 classes x 4 armas, spells com cooldown, bênçãos por slot (leve/pesado/spell/dash/ultimate/companion), portas com recompensa visível, árvores do Merlin, corrupção e 5 dificuldades. Sem números de dash/telegraph/hit-stop (não verificado). Premissa corrigida: não é jogo de vampiros.

### T3 — Mapear as mecânicas atuais do Vampire's Prey a partir do código e dos docs
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Inventário por categoria com arquivo/linha: player, dash, armas, skills, inimigos, bosses, salas, recompensas, loja, hub, UI, câmera, feedback de hit, áudio, narrativa; lista do que não existe.
- **Resultado:** Inventário do VP feito com arquivo/linha: dash infinito sem cargas, 1 arma, 3 arquétipos, wind-up 0,85 s com caixa no chão (ranged só '!'), bosses com 3 fases, 4 waves x 5, portas com ícone provisório, sem diálogo/NPCs/cast/deflect/Heat. Dúvidas: i-frames ~0,65 s do dash, dano de raridade possivelmente sobrescrito, vida dos bosses baixa.

### T4 — Redigir o comparativo, as diferenças principais e a viabilidade
- **Executor:** `game-designer`
- **Pronto quando:** Tabela completa, top diferenças destacadas, viabilidade/esforço/risco por item, priorização em 3 níveis (ganho rápido, médio, grande) e recomendação do que entra nos próximos planos. O que não foi verificado fica marcado.
- **Resultado:** Criado docs/design/comparativo-hades-sworn.md: tabela de 18 categorias, 12 diferenças destacadas com viabilidade/esforço, 8 achados a confirmar em jogo, priorização em 3 níveis ligada aos planos combate-leitura-visual e sistema-dialogo.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 08:15 | Plano criado |
| 2026-10-11 08:30 | Plano em-execucao |
| 2026-10-11 08:30 | T1 em-andamento |
| 2026-10-11 08:30 | T2 em-andamento |
| 2026-10-11 08:30 | T3 em-andamento |
| 2026-10-11 08:40 | T2 concluida |
| 2026-10-11 08:45 | T1 concluida |
| 2026-10-11 08:55 | T3 concluida |
| 2026-10-11 08:55 | T4 em-andamento |
| 2026-10-11 09:10 | T4 concluida |
| 2026-10-11 09:10 | Plano concluido |

## Resultado final

Comparativo entregue (docs/design/comparativo-hades-sworn.md). Sem código alterado. Pesquisa só por texto web; vários números (frame data, telegraph, hit-stop, prioridade de diálogo) ficaram não verificados. 8 achados do inventário a confirmar em jogo.

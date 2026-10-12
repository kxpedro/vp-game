<!-- orch:plano · id: 20261011-0815-combate-leitura-visual · status: em-execucao · criado: 2026-10-11 08:15 · commit-inicial: bd370e4 · versao-orch: 0.5.0 -->
# Animações de ataque e telegraph legíveis (jogador e inimigos)

**Demanda original:** Pedro (2026-10-11), texto colado: (1) analisar videos, imagens da gameplay do Hades e Sworn como referencia; verificar se o jogo e parecido mecanicamente, identificar e destacar as principais diferencas e se e possivel implementar; analisar toda e qualquer mecanica. (2) melhorar visualmente as animacoes de ataque do jogador e dos inimigos, deixar mais obvio quando um inimigo for atacar, usando Hades e Sworn como referencia. (3) desenvolver mecanica de dialogo, usando Hades como referencia. (4) focar um pouco nas configuracoes graficas do jogo, dar uma revisada.
**Grupo:** 20261011-0815-referencia-hades-sworn · **Depende dos planos:** 20261011-0815-referencia-hades-sworn

## Objetivo

Os ataques do jogador ganham anticipation, trail, hit-stop e feedback mais fortes; inimigos e bosses avisam claramente cada ataque (indicador no chão, cor por tipo, tempo de aviso, silhueta) no estilo Hades/Sworn, sem mudar dano nem balanceamento.

## Suposições e decisões

- Não altera dano, vida nem velocidade: só apresentação e tempo de aviso (wind-up). Qualquer aumento de wind-up fica registrado para o game-designer rever.
- T1 (auditoria do estado atual) roda já, em paralelo à pesquisa; T2 e T3 esperam o comparativo (plano de referência, T4) para usar as referências.
- Leitura de combate e cores por arquétipo seguem `docs/design/ui-guia.md`; texto novo só em `data/localization`.
- Verificação visual só por `--ui-batch` ou smoke headless, em um único processo do Godot.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Auditar telegraphs e animações atuais de jogador, inimigos e bosses | `orch:pesquisador` | — | — (leitura) | concluida |
| T2 | Melhorar animações e feedback de ataque do jogador | `designer-ux-arte` | T1 + plano referência | `scripts/player/PlayerAnimator.cs`, `scripts/player/PlayerWeaponVisual.cs`, `scripts/player/PlayerCombat.cs`, `scripts/systems/CombatFeedback.cs`, `scripts/systems/weapons/WeaponVisualCatalog.cs` | concluida |
| T3 | Tornar óbvio o ataque dos inimigos e bosses (telegraph) | `inimigos-e-bosses` | T1 + plano referência | `scripts/enemies/`, `scripts/bosses/`, `scripts/combat/` | concluida |
| T4 | Revisar o conjunto (regressões, performance, contraste/daltonismo) | `orch:revisor` | T2, T3 | — (leitura) | concluida |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1
Onda 2: T2 ∥ T3
Onda 3: T4
```

Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Auditar telegraphs e animações atuais de jogador, inimigos e bosses
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Relatório do que existe hoje (EnemyBasic, BossController, EnemyProjectile, PlayerAnimator, PlayerWeaponVisual, CombatFeedback), tempos de wind-up por arquétipo e lacunas de leitura.
- **Resultado:** Auditoria feita: melee/tank com caixa de alfa 0,22 sem flash/som; ranged sem indicador no chão; bosses com círculos visíveis mas avisos de 0,34-0,48 s e sem som; animação de ataque única e dessincronizada; jogador sem anticipation, Roll/RecieveHit sem uso, indicador de ataque no mesmo vermelho do perigo, sem hit-stop/shake/som ao ser atingido.

### T2 — Melhorar animações e feedback de ataque do jogador
- **Executor:** `designer-ux-arte`
- **Pronto quando:** Anticipation/follow-through, arco ou trail do golpe, hit-stop curto e screen shake discretos, por arma; `dotnet build VP0.1.sln` com 0 erros/0 avisos; smoke headless da TestArena sem erro.
- **Resultado:** Jogador: feedback ao receber dano (hit-stop 45 ms, shake, flash, RecieveHit), anticipation com speed ramp por golpe sem mudar tempos do catálogo, Roll na esquiva, trail da espada, indicador de ataque ciano/branco-dourado com preview, hit-stop ponderado por golpe. Build 0/0; pontos de golpe das animações são palpite a conferir em jogo.

### T3 — Tornar óbvio o ataque dos inimigos e bosses (telegraph)
- **Executor:** `inimigos-e-bosses`
- **Pronto quando:** Indicador de área/direção no chão com preenchimento progressivo, flash/cor de aviso, distinção melee/ranged/tank/boss e aviso mínimo; `dotnet build VP0.1.sln` com 0 erros/0 avisos; smoke headless da TestArena sem erro.
- **Resultado:** Ranged com linha magenta e mira que trava 0,25 s (projétil sai pela direção travada); telegraph melee/tank com borda fixa e preenchimento da base; flash, ping do '!' e sons de aviso (reaproveitando áudio existente); bosses com contorno fixo, chevrons, impacto, aviso de teleporte e pausa de 0,5 s na troca de fase; pisos de aviso centralizados no BossController. Build 0/0.

### T4 — Revisar o conjunto (regressões, performance, contraste/daltonismo)
- **Executor:** `orch:revisor`
- **Pronto quando:** Sem achados altos; sem alocação por frame desnecessária nos efeitos; telegraphs legíveis sem depender só de cor.
- **Resultado:** Revisão sem achados altos; corrigidos M1 (animação de ataque cortada), M2 (esquiva perfeita na carga do boss), M3 (troca de fase cancela o golpe), B1, B2, B6 e um erro de add_child do trail achado no smoke. Build 0/0; smoke TestArena e CrowsNest sem erro; localização ok.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 08:15 | Plano criado |
| 2026-10-11 09:20 | Plano em-execucao |
| 2026-10-11 09:20 | T1 em-andamento |
| 2026-10-11 09:35 | T1 concluida |
| 2026-10-11 09:35 | T2 em-andamento |
| 2026-10-11 09:35 | T3 em-andamento |
| 2026-10-11 09:55 | T2 concluida |
| 2026-10-11 10:10 | T3 concluida |
| 2026-10-11 10:10 | T4 em-andamento |
| 2026-10-11 10:40 | T4 concluida |
| 2026-10-11 10:40 | Plano concluido |

## Resultado final

Jogador ganhou feedback ao ser atingido, anticipation, Roll, trail e indicador ciano; inimigos ganharam telegraph legível (ranged com linha e mira travada, melee/tank com preenchimento), flash e sons de aviso; bosses ganharam avisos mais legíveis, pisos de aviso, aviso de teleporte e pausa na troca de fase. Nada foi visto em jogo: pontos de golpe das animações, trail, cores e sons (reaproveitados) precisam de teste manual. Pendências baixas: B3, B4, B5.

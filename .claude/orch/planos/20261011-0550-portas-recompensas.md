<!-- orch:plano · id: 20261011-0550-portas-recompensas · status: planejado · criado: 2026-10-11 05:50 · commit-inicial: 3c2e257 · versao-orch: 0.5.0 -->
# Portas com recompensa visível (fase 2): ícone da recompensa da próxima sala na porta, como no Hades

**Demanda original:** Pedro (2026-10-11), texto colado: fazer 4 personagens jogaveis que vao sendo liberados com o tempo, talvez possam ser inicialmente bosses que depois de derrotados sao desbloqueados para o jogador; cada personagem vai ter 4 variantes de armas, elaborar depois. Power ups por sala, ja tem isso, mas tem que mostrar pro jogador qual vai ser antes dele entrar na proxima, mostrando o icone na porta de acesso (igual hades). Novas recompensas de sala: devil's fruit (fruta do diabo), uma fruta formada por sangue, adiciona 15 pontos de vida ao total do jogador; blood coins (moeda de sangue), ja implementado, hoje serve para comprar aprimoramentos no hub, mas vamos remover isso, agora so vai funcionar para comprar upgrades dentro de uma run; cursed blood (sangue amaldicoado), novo recurso para comprar aprimoramentos no hub, adquirido principalmente ao derrotar bosses e inimigos especiais. Adendos na conversa: atributos dos personagens invisiveis ao jogador mas computados; artefatos de boss passam a ser vendidos na loja e saem da recompensa dos bosses; os outros personagens desbloqueaveis NAO serao implementados agora, apenas um placeholder no hub.
**Grupo:** 20261011-0550-economia-recompensas · **Depende dos planos:** 20261011-0550-economia-recompensas

## Objetivo

Cada sala limpa abre 1 ou 2 portas (quantidade e tipos por RNG); cada porta exibe o ícone do tipo de recompensa da sala seguinte (Bênção de Sangue, Fruta do Diabo ou Moedas de Sangue) e a recompensa da sala é a da porta que o jogador escolheu.

## Suposições e decisões

- **D7 (Pedro, 2026-10-11) Portas:** 1 a 2 portas por sala, com a quantidade e os tipos de recompensa por RNG, no estilo Hades/Sworn.
- **D8 Tipos de porta:** Bênção de Sangue (a oferta atual de 3 opções), Fruta do Diabo e Moedas de Sangue. Sangue Amaldiçoado não é tipo de porta (vem de boss e de Lendário).
- **A8** A recompensa da sala é a do ícone da porta cruzada, entregue ao limpar a sala em que o jogador entrou (modelo do Hades). Hoje a bênção sempre vem ao limpar a sala.
- **A9** Salas de boss mantêm a recompensa fixa (D5) e uma única porta. A saída da sala inicial e a da Câmara também rolam as portas da primeira sala de cada mapa.
- **A10** Ícones em geometria provisória + rótulo curto (sem asset externo, sem tela nova de UI).
- **A11** Valores (peso por tipo, chance de 1 vs 2 portas, quantidade de moedas) saem da T1 deste plano e da `economia-v2.md` do plano 1.
- Convenções do projeto (perfil): `dotnet build VP0.1.sln` deve terminar 0 erros/0 avisos ao fim de CADA tarefa; textos novos em `data/localization/<idioma>/*.json` nos 3 idiomas (`py -I tools/validate_localization.py`); IDs persistidos não mudam; `.cs` novo precisa de `.cs.uid`; indentação do arquivo editado; NUNCA abrir o Godot uma vez por tela: telas só pelo modo `--ui-batch` do UiPreview, um único processo.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Projetar as portas e a tabela de recompensas | `game-designer` | — | `docs/design/portas-recompensas.md` (novo) | pendente |
| T2 | Modelo do plano de recompensas por sala | `conteudo-run` | T1 | `scripts/systems/rewards/RoomRewardCatalog.cs` (+ .cs.uid, novo), `scripts/systems/RunState.cs`, `data/localization/*/content.json` | pendente |
| T3 | Fluxo de salas com 1–2 portas | `inimigos-e-bosses` | T2 | `scripts/systems/RunManager.cs`, `scripts/systems/ScenePortal.cs`, `scripts/systems/RoomGenerator.cs`, `scripts/systems/RunStartBlessingRoom.cs`, `scripts/systems/IntermissionChamber.cs`, `data/localization/*/world.json` | pendente |
| T4 | Ícones de recompensa nas portas | `designer-ux-arte` | T3 | `scripts/systems/DoorRewardIcon.cs` (+ .cs.uid, novo), `scripts/systems/ScenePortal.cs` (só ganchos), `data/localization/*/ui.json` (se precisar) | pendente |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1
Onda 2: T2   (após T1)
Onda 3: T3   (após T2)
Onda 4: T4   (após T3)
```

Caminho crítico: T1 → T2 → T3 → T4. Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Projetar as portas e a tabela de recompensas
- **Executor:** `game-designer`
- **Faz:** Pesos por tipo e por mapa/sala, chance de 1 vs 2 portas, regras (duas portas iguais? garantia mínima de bênçãos?), quantidade de Moedas como recompensa, interação com a economia da fase 1, descrição visual de cada ícone. Só documento.
- **Pronto quando:** Documento com tabelas e regras, sem número sem justificativa.
- **Resultado:** —

### T2 — Modelo do plano de recompensas por sala
- **Executor:** `conteudo-run`
- **Faz:** `RoomRewardType` e definições; `RoomRewardCatalog.Roll(...)` devolve 1–2 opções para a próxima sala; `RunState` guarda as opções e a escolhida, expõe o consumo ao limpar a sala e reseta por run; nome e descrição de cada tipo nos 3 idiomas.
- **Pronto quando:** Build 0/0; rolagem reprodutível por seed verificada em preview/smoke; validadores sem erro.
- **Resultado:** —

### T3 — Fluxo de salas com 1–2 portas
- **Executor:** `inimigos-e-bosses`
- **Faz:** Ao limpar a sala abre 1–2 portais (o segundo criado por código a partir do `ExitPortal`, sem editar .tscn à mão); cada `ScenePortal` carrega o tipo e expõe a propriedade; entrar registra a escolha; `CompleteRoom` entrega a recompensa do tipo (oferta de bênção, Fruta do Diabo ou Moedas) em vez da bênção fixa; start room, Câmara e sala de boss adaptadas (A9); textos de mundo.
- **Pronto quando:** Sala limpa abre portas com tipo; a recompensa entregue bate com a porta; build e smokes limpos.
- **Resultado:** —

### T4 — Ícones de recompensa nas portas
- **Executor:** `designer-ux-arte`
- **Faz:** Ícone 3D legível acima de cada porta (silhueta e cor por tipo, forma além da cor, geometria provisória), rótulo curto localizado ao aproximar, leitura contra o piso escuro e nas duas posições de porta.
- **Pronto quando:** Capturas (uma rodada de `--ui-batch`/preview) mostram os 3 tipos distinguíveis com 1 e 2 portas e o rótulo nos 3 idiomas.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 05:50 | Plano criado |

## Resultado final

—

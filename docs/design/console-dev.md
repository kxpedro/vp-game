# Console de desenvolvedor

Console de texto dentro do jogo, só em build de debug (`OS.IsDebugBuild()`). Complementa o painel F1: o painel serve para explorar e ajustar parâmetros, e o console para ações rápidas e repetíveis ("dar a bênção X", "pôr uma Bigorna no chão", "matar tudo").

## Uso

- **Abrir/fechar:** tecla abaixo do Esc (`'` no ABNT2, `` ` `` no US; ação `dev_console`, por tecla física) ou **F2**. Esc também fecha. Com Shift/Ctrl/Alt a tecla não abre o console, para que Shift+`'` continue digitando aspas.
- O console pausa o jogo enquanto está aberto (`UiModalStack`) e fica por cima de tudo, inclusive do painel F1.
- **Enter** executa, **Tab** completa (com mais de um candidato, completa o prefixo comum e lista as opções), **↑/↓** percorrem o histórico e **PgUp/PgDn** rolam a saída.
- A `/` inicial é opcional. Aspas agrupam um argumento com espaços (`give boon "sede predatória"`). Use `;` para encadear comandos (`god on; add 1000 coins; spawn boon anvil`).
- Nomes e aliases são comparados sem acento e sem caixa, e `_`/`-` contam como espaço. Os comandos aceitam aliases em inglês e em português (`dar`, `listar`, `matar`, `bigorna`, `bencao`...).
- A saída e o histórico são estáticos e sobrevivem à troca de cena.

## Comandos

| Comando | O que faz | Exemplos |
|---|---|---|
| `help [comando]` | lista os comandos ou detalha um deles (uso, sinônimos, exemplos) | `help give boon` |
| `clear` | limpa a saída | |
| `status` | mapa/sala, vida, recursos, bênçãos, artefatos e mods da run | |
| `list boons [linhagem\|owned\|texto]` | bênçãos (id, nome, linhagem, se já estão na run) | `list boons orlok`, `list boons owned` |
| `list families` / `rarities` / `artifacts [texto]` / `mods [arma]` / `weapons` / `enemies` / `params [texto] [modified]` / `commands` | outras listas | `list params fruit` |
| `give <boon\|artifact\|mod\|fruit> <nome ou id> [raridade]` | entrega direto na run. Sem o tipo, procura o nome nos três catálogos | `give boon predatory_thirst epic`, `give artifact ledger`, `give fruit 3`, `give orlok_rotten_bite` |
| `spawn <itens...>` | põe itens no chão perto do jogador ou cria inimigos; aceita vários. Um número antes do tipo é a quantidade; depois de `coins`, é o valor | `spawn boon anvil`, `spawn boon orlok`, `spawn 3 fruit`, `spawn coins 500`, `spawn enemy tank rare`, `spawn 5 enemy` |
| `offer <boon [linhagem]\|anvil>` | abre a oferta na hora, sem item nem porta | `offer boon karnstein` |
| `add <n> <health\|coins\|cursed\|fury\|fruit>` | soma (negativo subtrai), em qualquer ordem | `add 50 health`, `add coins 1000` |
| `set <health\|coins\|cursed\|fury> <valor>` | define o valor | `set health 1` |
| `heal`, `god [on\|off]`, `stamina [on\|off]` | cura total, modo deus, stamina infinita | |
| `kill [all\|mobs\|boss\|player]` | mata os inimigos vivos (as waves futuras continuam), só o chefe ou o jogador | `kill all mobs` |
| `room complete` / `room reroll` | encerra a sala como limpa / sorteia de novo as portas desta sala | |
| `jump <mapa> [sala\|boss]`, `jump boss` | pula de sala (recarrega a arena) | `jump 2 boss` |
| `rise` | volta ao hub saindo do caixão, como ao renascer (encerra a run em andamento; só no 2D) | `rise` |
| `doors <tipo> [tipo]`, `doors <1\|2>`, `doors off` | força as próximas portas (mesmo override do F1) | `doors anvil fruit` |
| `time <escala>` | escala de tempo de 0.1 a 4 | `time 0.25` |
| `tune <chave> [valor\|reset]` | lê ou altera um parâmetro do `DevParameterRegistry` (o mesmo do F1, gravado em `user://dev_tuning.json`) | `tune room.waves.count 1` |
| `promote` | promove os parâmetros alterados neste PC para `data/tuning/tuning_overrides.json` (o mesmo botão "Promover para o jogo" do F1) | `promote` |

### Regras que valem a pena saber

- **Itens do `spawn`** são avulsos: não entram na contagem de itens da sala, então não travam nem destravam as portas. Ao pegar, cada um paga a si mesmo: a Bênção abre a oferta com o plano sorteado no spawn (com linhagem, se pedida), a Bigorna abre a oferta da Bigorna (sem mod livre, paga as Moedas de fallback) e as Moedas entram no saldo. A Fruta é a `DevilsFruit` normal.
- **Inimigos do `spawn`** só nascem na arena, já com o tuning da sala (`RunManager.ApplyCommonEnemyTuning`). Com a sala ainda em combate, eles entram no total da sala (`RunManager._devExtraEnemies`), e as portas só abrem depois deles. O Diário não é marcado.
- **Ofertas fora do fluxo** (`offer`, itens do `spawn`, "Bênção agora" do F1) chamam `RunManager.UnlockExitAfterReward` ao fechar. Esse método agora não faz nada antes de a sala ser paga (`_roomRewardsPaid`), então abrir uma oferta no meio do combate não destrava mais a saída. Antes destravava.
- `give boon` usa a raridade Comum se nenhuma for dada; Stirps tem poder fixo. Os pré-requisitos não são exigidos, só avisados.
- Recompensas de run dadas fora de uma run ativa (no hub, por exemplo) valem até o próximo `StartRun`, que zera a run. O console avisa.

## Como adicionar um comando

1. Escolha (ou crie) um `scripts/systems/dev/DevCommands.<Area>.cs` (`static partial class DevCommands`) e registre no `Register<Area>()` dele. Uma área nova também entra em `DevCommands.RegisterAll`.
2. Comando de topo: `DevConsole.Register(new DevCommand("nome", "resumo", Run) { Aliases = ..., Usage = ..., Examples = ..., Complete = ... })`.
3. Subcomando: `pai.Add(new DevCommand(...))`. O parser desce na árvore enquanto o próximo argumento casar com um filho. Para pendurar uma lista nova em `list`, use `ListRoot.Add(...)`.
4. No `Run`, use o `DevCommandContext`: `Arg(i)`, `Rest(i)`, `Count`, os atalhos `Player`, `PlayerHealth`, `PlayerCombat`, `RunManager`, `RoomGenerator`, `Hud`, e a saída com `Print`, `Ok`, `Warn`, `Error` e `UsageError`. Para abrir uma tela por cima, ponha `CloseConsole = true` e chame a tela com `Callable.From(...).CallDeferred()`.
5. `NeedsArena = true` recusa o comando fora da arena. `Complete` devolve os candidatos para o argumento atual (`c.Args` traz os argumentos já digitados); o filtro por prefixo é automático.
6. Busca por nome: `DevText.Find(itens, busca, id, nome)` (id exato → nome exato → prefixo do id → todas as palavras) e `PickOne` para pedir desambiguação.

Nada disso existe em release: o HUD só monta o console com `OS.IsDebugBuild()`, e as APIs `Dev*` de `RunManager`/`RoomGenerator`/`RunState` não fazem nada fora de debug.

## Pré-visualização

`--ui-preview=dev_console` (também no `--ui-batch`) abre o console depois de rodar `help` e `list boons orlok`.

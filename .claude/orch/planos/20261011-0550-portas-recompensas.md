<!-- orch:plano · id: 20261011-0550-portas-recompensas · status: concluido · criado: 2026-10-11 05:50 · commit-inicial: 3c2e257 · versao-orch: 0.5.0 -->
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
| T1 | Projetar as portas e a tabela de recompensas | `game-designer` | — | `docs/design/portas-recompensas.md` (novo) | concluida |
| T2 | Modelo do plano de recompensas por sala | `conteudo-run` | T1 | `scripts/systems/rewards/RoomRewardCatalog.cs` (+ .cs.uid, novo), `scripts/systems/RunState.cs`, `data/localization/*/content.json` | concluida |
| T3 | Fluxo de salas com 1–2 portas | `inimigos-e-bosses` | T2 | `scripts/systems/RunManager.cs`, `scripts/systems/ScenePortal.cs`, `scripts/systems/RoomGenerator.cs`, `scripts/systems/RunStartBlessingRoom.cs`, `scripts/systems/IntermissionChamber.cs`, `data/localization/*/world.json` | concluida |
| T4 | Ícones de recompensa nas portas | `designer-ux-arte` | T3 | `scripts/systems/DoorRewardIcon.cs` (+ .cs.uid, novo), `scripts/systems/ScenePortal.cs` (só ganchos), `data/localization/*/ui.json` (se precisar), `scripts/ui/HUD.Notifications.cs` (so o aviso de sala limpa) | concluida |

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
- **Resultado:** docs/design/portas-recompensas.md criado: conjunto de portas por saida de sala (24 por run), 1 ou 2 portas (base 30/35/40% por mapa), pesos Bencao/Fruta/Moedas por mapa, pity de Bencao, moedas de porta 150/130/110, fruta de porta ~15% das CPs com teto ajustado de 4 para 3. Duas opcoes de progressao para o usuario decidir: A (recomendada: 22 ofertas de Bencao por run) ou B (25 ofertas, com Fruta e Moedas so na segunda porta). Simulacao Monte Carlo em arquivos temporarios (fora do repo). Nada de codigo alterado.

### T2 — Modelo do plano de recompensas por sala
- **Executor:** `conteudo-run`
- **Faz:** `RoomRewardType` e definições; `RoomRewardCatalog.Roll(...)` devolve 1–2 opções para a próxima sala; `RunState` guarda as opções e a escolhida, expõe o consumo ao limpar a sala e reseta por run; nome e descrição de cada tipo nos 3 idiomas.
- **Pronto quando:** Build 0/0; rolagem reprodutível por seed verificada em preview/smoke; validadores sem erro.
- **Resultado:** RoomRewardCatalog (static, rolagem pura da secao 7.4 na Opcao B: Bencao em toda CP, Fruta/Moedas na 2a porta, pity e teto 3 de frutas) e RunState (conjunto por sala guardado na run, escolha pendente, contadores, semente da run; zera em StartRun/FailRun/campanha). Textos reward.type.* nos 3 idiomas. Build 0/0; localizacao 0/0. Simulacao Python de 1000 runs: 25 ofertas de Bencao, 10,5 CPs com 2 portas, teto de frutas atingido em 19%. Pendencias: teto 3 a confirmar; secoes 9.3 e 11 do documento ainda com numeros da Opcao A; reward.door.* e hud.room.choose_door ficam para T3/T4; reproducao por semente no Godot nao verificada.

### T3 — Fluxo de salas com 1–2 portas
- **Executor:** `inimigos-e-bosses`
- **Faz:** Ao limpar a sala abre 1–2 portais (o segundo criado por código a partir do `ExitPortal`, sem editar .tscn à mão); cada `ScenePortal` carrega o tipo e expõe a propriedade; entrar registra a escolha; `CompleteRoom` entrega a recompensa do tipo (oferta de bênção, Fruta do Diabo ou Moedas) em vez da bênção fixa; start room, Câmara e sala de boss adaptadas (A9); textos de mundo.
- **Pronto quando:** Sala limpa abre portas com tipo; a recompensa entregue bate com a porta; build e smokes limpos.
- **Resultado:** Portas de recompensa criadas em codigo a partir do ExitPortal (ScenePortal.BuildRewardDoors, 1 ou 2 portas, moldura a 60% com 2 portas), escolha grava RunState.ChooseRoomRewardDoor antes da troca de cena, outra porta fecha em 0,3 s; RunManager.CompleteRoom consome a pendencia e entrega (Bencao abre oferta, Fruta spawna e trava portas, Moedas creditam); Sala Inicial e Camara montam conjunto de origem 0; boss e mapa 4 sem conjunto. DevilsFruit ganhou sinal Collected. 9 chaves em world.json. Build 0/0; validador 0 erros, 1 aviso (hud.room.choose_door sem uso). Godot nao aberto: fluxo conferido so por leitura. Pendencias de teste em jogo: leitura das duas portas, fruta entre as portas, Camara sem invadir paredes, prompt de porta.

### T4 — Ícones de recompensa nas portas
- **Executor:** `designer-ux-arte`
- **Faz:** Ícone 3D legível acima de cada porta (silhueta e cor por tipo, forma além da cor, geometria provisória), rótulo curto localizado ao aproximar, leitura contra o piso escuro e nas duas posições de porta.
- **Pronto quando:** Capturas (uma rodada de `--ui-batch`/preview) mostram os 3 tipos distinguíveis com 1 e 2 portas e o rótulo nos 3 idiomas.
- **Resultado:** DoorRewardIcon (NOVO): icones provisorios por tipo (losango dourado, esfera vermelha com haste emissiva so na fruta, pilha de discos bronze) + rotulo curto localizado; ScenePortal.AddRewardIcon so cria o ponto de encaixe; HUD.Notifications mostra hud.room.choose_door quando a sala tem portas (NotifyPortalOpened mantido). Build 0/0; validador 0/0 (aviso de choose_door resolvido). Sem checagem visual (Godot nao aberto). DoorRewardIcon.cs.uid nao gerado (o Godot gera ao abrir o projeto). Pendencias: toast hud.toast.portal_open e log 'O portal de saida foi aberto' ainda dizem portal aberto com portas de recompensa; decisao do usuario.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 05:50 | Plano criado |
| 2026-10-11 16:33 | Execucao iniciada pelo orquestrador (orch 0.5.0) |
| 2026-10-11 16:33 | T1 iniciada por game-designer |
| 2026-10-11 16:46 | T1 concluída por game-designer |
| 2026-10-11 16:46 | Decisao pendente do usuario antes do T2: opcao A (22 ofertas de Bencao por run) ou B (25 ofertas). Tambem: teto de frutas de porta 4 -> 3 altera economia-v2 (sec. 5.2 e 9.3), que precisa de atualizacao. |
| 2026-10-11 16:49 | Decisao do usuario (Opcao B): toda sala tem Bencao em uma das portas (25 ofertas por run, como hoje); Fruta e Moedas aparecem so na segunda porta. O teto de frutas de porta (4 -> 3, economy.fruit.run_cap_from_doors) segue a proposta do T1 e fica a confirmar. |
| 2026-10-11 16:49 | T2 iniciada por conteudo-run |
| 2026-10-11 16:53 | T2 concluída por conteudo-run |
| 2026-10-11 16:53 | Pendencia para o usuario: teto de frutas de porta em 3 (ainda nao confirmado). Documento precisa de atualizacao nas secoes 9.3 e 11 para a Opcao B. |
| 2026-10-11 16:53 | T3 iniciada por inimigos-e-bosses |
| 2026-10-11 17:04 | T3 concluída por inimigos-e-bosses |
| 2026-10-11 17:04 | T4 escopo ampliado: HUD.Notifications.cs (so o aviso de sala limpa passa a usar hud.room.choose_door quando ha portas; hoje mostra Portal aberto), para resolver o aviso do validador. |
| 2026-10-11 17:04 | T4 iniciada por designer-ux-arte |
| 2026-10-11 17:07 | T4 concluída por designer-ux-arte |
| 2026-10-11 17:07 | Plano concluido: 4/4 tarefas (T1 a T4), com checagem visual pendente de jogo |
| 2026-10-11 17:20 | Teste em jogo (usuario): portas aparecem no meio da sala, empilhadas/mal orientadas, e o rotulo Bencao nao informa qual bencao vem. Pedido: portas na parede do mapa (nao no meio da sala). Correcao em andamento na branch feat/portas-recompensas. Texto da Bencao aguardando decisao do usuario. |
| 2026-10-11 17:21 | Pedido do usuario (teste em jogo): porta de Bencao com (a) as 3 bencoes pre-sorteadas na montagem da porta, guardadas na run e usadas pela oferta (a oferta mostra exatamente essas 3), (b) nomes no prompt ao aproximar e (c) icone de cada bencao na porta (BloodBlessingDefinition.IconPath, ja existente em images/bloodBoons). Tarefa de follow-up, depois da correcao de posicao (conflito em ScenePortal e RunManager). |
| 2026-10-11 17:27 | Correcao de posicao das portas (paredes, voltadas para a sala, sem o visual antigo de Saida) feita; build 0/0; nao testada em jogo ainda. Sala N segue com saida no centro (pendente). |
| 2026-10-11 17:27 | DECISAO DO USUARIO (teste em jogo): a recompensa da sala atual fica NO CHAO ao limpar a sala; a porta exibe a recompensa da PROXIMA sala (1 ou 2 portas). Interagir com a recompensa no chao (estilo Hades): Bencao abre a oferta das 3 pre-sorteadas; Moedas e Fruta tambem exigem interacao. Pendente: bonus de sala limpa (12/14/16 moedas) segue automatico (nao e item no chao). Tarefas: (A) pickups no chao com interacao e entrega ao limpar a sala; (B) pre-sorteio das 3 bencoes na porta, prompt com nomes e icones (pedido anterior). |
| 2026-10-11 17:29 | Decisao do usuario confirmada: (1) bonus de sala limpa tambem vira item no chao; (2) sala N com saida na parede. Sala N feita (RoomGenerator, build 0/0); bonus de sala em andamento no agente de recompensas no chao. |
| 2026-10-11 17:32 | Recompensas no chao implementadas (bencao, moedas, fruta e bonus de sala limpa viram pickups com interacao; portas e saida travadas ate pegar tudo). Build 0/0 e localizacao 0/0. Ajuste em andamento: fruta do boss tambem trava a saida do boss. Pendente: pre-sorteio das 3 bencoes na porta com nomes e icones (tarefa B). Nada testado em jogo ainda. |
| 2026-10-11 17:38 | Implementacao concluida (nao commitada): recompensas no chao com interacao (bencao, moedas, fruta, bonus de sala; portas e saida travadas ate pegar tudo); fruta do boss trava a saida; saida na parede (inclusive sala N); pre-sorteio das 3 bencoes na porta com nomes e icones, oferta usa as mesmas. Build 0/0; validadores 0/0. Aguardando teste em jogo do usuario. Pendencias: icones de familia repetidos (Dracul mostra 3 iguais), docs/design/portas-recompensas.md desatualizado. |

## Resultado final

Plano concluido. Entregue: docs/design/portas-recompensas.md (Opcao B escolhida pelo usuario); RoomRewardCatalog e estado da run por sala (conjunto, escolha pendente, pity, teto de frutas de porta em 3); portas de recompensa criadas a partir da saida (1 ou 2 por sala, escolha paga na sala seguinte, Fruta trava as portas ate ser coletada); icones provisorios com rotulo e aviso de escolha. Build 0 erros/0 avisos; validadores 0/0. Pendencias: teste em jogo (legibilidade e colisao das duas portas, Camara sem invadir paredes, fluxo completo), DoorRewardIcon.cs.uid a gerar, decisoes do usuario sobre o teto de frutas (3 ou 4) e o toast 'portal aberto'; docs/design/portas-recompensas.md (secoes 9.3 e 11) ainda com numeros da Opcao A; docs/gameplay_systems.md ainda sem as portas. Nada commitado ainda.

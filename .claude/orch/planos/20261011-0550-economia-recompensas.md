<!-- orch:plano · id: 20261011-0550-economia-recompensas · status: concluido · criado: 2026-10-11 05:50 · commit-inicial: 3c2e257 · versao-orch: 0.5.0 -->
# Economia e recompensas (fase 1): Moedas de Sangue só na run, Sangue Amaldiçoado, Fruta do Diabo e loja de artefatos

**Demanda original:** Pedro (2026-10-11), texto colado: fazer 4 personagens jogaveis que vao sendo liberados com o tempo, talvez possam ser inicialmente bosses que depois de derrotados sao desbloqueados para o jogador; cada personagem vai ter 4 variantes de armas, elaborar depois. Power ups por sala, ja tem isso, mas tem que mostrar pro jogador qual vai ser antes dele entrar na proxima, mostrando o icone na porta de acesso (igual hades). Novas recompensas de sala: devil's fruit (fruta do diabo), uma fruta formada por sangue, adiciona 15 pontos de vida ao total do jogador; blood coins (moeda de sangue), ja implementado, hoje serve para comprar aprimoramentos no hub, mas vamos remover isso, agora so vai funcionar para comprar upgrades dentro de uma run; cursed blood (sangue amaldicoado), novo recurso para comprar aprimoramentos no hub, adquirido principalmente ao derrotar bosses e inimigos especiais. Adendos na conversa: atributos dos personagens invisiveis ao jogador mas computados; artefatos de boss passam a ser vendidos na loja e saem da recompensa dos bosses; os outros personagens desbloqueaveis NAO serao implementados agora, apenas um placeholder no hub.
**Grupo:** 20261011-0550-economia-recompensas · **Depende dos planos:** —

## Objetivo

Moedas de Sangue passam a existir só dentro da run e são gastas numa nova Loja na Câmara de Hemomancia; Sangue Amaldiçoado vira o recurso permanente que compra a árvore de habilidades no hub; a Fruta do Diabo existe como recompensa; os artefatos saem dos bosses e do hub e passam a ser vendidos na loja; o save vira versão 2 e os saves antigos são excluídos.

## Suposições e decisões

- **D1 (Pedro, 2026-10-11) Moedas de Sangue:** recurso só da run. O saldo zera ao fim da run e nunca vai para o save. Continuam caindo de cada inimigo e também virão como recompensa de sala (plano `portas-recompensas`). Gastas na Loja da Câmara de Hemomancia (entre mapas).
- **D2 Sangue Amaldiçoado:** recurso permanente (save). Dropado por bosses e por inimigos **Lendários** (quando coletado vai direto ao save e sobrevive à morte). Gasto no hub, na Fonte de Sangue (árvore Bestialis/Humanitas), no lugar das Moedas.
- **D3 Saves:** zerar o saldo e **excluir os saves antigos** (decisão explícita do usuário, irreversível). `GameSaveData.Version` sobe para 2; slot com versão menor que 2 é excluído ao ser lido.
- **D4 Fruta do Diabo:** +15 de vida máxima e cura 15, só na run, acumulativa (cada fruta soma). Aparece como recompensa de sala (plano `portas-recompensas`) e é garantida ao derrotar um boss.
- **D5 Recompensa do boss:** Sangue Amaldiçoado + Fruta do Diabo garantida; sem artefato; o portal abre direto.
- **D6 Artefatos:** vendidos na Loja da Câmara com Moedas de Sangue; valem só na run; **sem limite** de quantidade; estoque sorteado a cada visita (3 a 4, peso e preço por raridade). Coleção permanente, slots equipados e Baú de Artefatos do hub são removidos.
- **D12 (Pedro, 2026-10-11) Skills sem artefato de boss:** 'Olho do Colecionador' (humanitas.collector_eye) vira desconto de -5/-10/-15% no preco dos artefatos da loja por rank; 'Destino Reescrito' (humanitas.rewritten_fate) perde so a parte de artefato e mantem o resto.
- **D13 Duplicatas:** a loja NAO oferece artefato que o jogador ja tem na run (estoque exclui os possuidos).
- **D14 Poder dos artefatos:** mantem o multiplicador atual x1,75 (BossDropPowerMultiplier) tambem para os da loja (escolha do usuario; o design sugeria x1,25, risco de power creep com ~7 artefatos por run, a validar jogando).
- **D15 Livro-Caixa do Sangue Nao Pago:** o efeito DamagePerCurrencyHeld passa a usar o saldo de Moedas da run, com teto de +100% de dano (escolha do usuario).
- **A1** (corrigida na execucao) A Loja e um NOVO ponto interativo na Camara de Hemomancia, criado em codigo ao lado da fonte (que so cura 35%) e do altar de sacrificio, sem substitui-los. A arvore de habilidades fica so no hub (CrowsNest).
- **A2** Itens extras na loja além de artefatos (cura, reroll de bênção) são opcionais: a T1 pode propô-los, mas só entram com aprovação do usuário. Padrão: só artefatos.
- **A3** Nenhum número (preço, quantidade, taxa de drop, escala de custo da árvore) é inventado nas tarefas de implementação: tudo sai da T1 e fica em tuning/JSON.
- **A4** Cada tarefa mantém o build verde: a T2 e a T4 deixam atalhos `[Obsolete]` (ex.: `CharacterSheet.BloodCoins` apontando para o saldo permanente) que a T7 remove.
- **A5** O saldo de moedas da run mora em `RunState` (volátil). Nada de moeda de run em `CharacterSheet`/save.
- **A6** HUD da run mostra "Moedas" (saldo da run); o hub mostra "Sangue Amaldiçoado".
- **A7** Sem framework de testes: verificação = build + validadores (localização, bênçãos, artefatos) + smokes headless + `--ui-batch` (um processo).
- Convenções do projeto (perfil): `dotnet build VP0.1.sln` deve terminar 0 erros/0 avisos ao fim de CADA tarefa; textos novos em `data/localization/<idioma>/*.json` nos 3 idiomas (`py -I tools/validate_localization.py`); IDs persistidos não mudam; `.cs` novo precisa de `.cs.uid`; indentação do arquivo editado; NUNCA abrir o Godot uma vez por tela: telas só pelo modo `--ui-batch` do UiPreview, um único processo.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Projetar a nova economia | `game-designer` | — | `docs/design/economia-v2.md` (novo) | concluida |
| T2 | Modelo de moedas, save v2 e exclusão de saves antigos | `conteudo-run` | — | `scripts/systems/SaveGameManager.cs`, `scripts/systems/CharacterSheet.cs`, `scripts/systems/RunState.cs`, `scripts/player/PlayerSkillController.cs`, `scripts/systems/RunManager.cs` (só as linhas de moeda) | concluida |
| T3 | Fruta do Diabo | `conteudo-run` | T1, T2 | `scripts/items/DevilsFruit.cs` (+ .cs.uid, novo), `scripts/systems/RunState.cs`, `data/localization/*/content.json` | concluida |
| T4 | Artefatos só na loja: estoque e remoção do ciclo de coleção | `conteudo-run` | T3 | `scripts/systems/artifacts/ArtifactCatalog.cs`, `data/artifacts/artifacts.json` (só se precisar de preço), `scripts/systems/CharacterSheet.cs`, `scripts/systems/RunState.cs`, `scripts/systems/ArtifactChest.cs`, `scenes/game/CrowsNest.tscn` (só remover o Baú), `data/localization/*/content.json`, `.codex/skills/vp-create-artifact/**`, `data/skills/skills.json`, `scripts/player/PlayerSkillController.cs`, `scripts/systems/skills/*.cs`, `scripts/player/PlayerCombat.cs` (so o teto do Livro-Caixa), `scripts/systems/SaveGameManager.cs` (campos de colecao de artefatos) | concluida |
| T5 | Recompensas do boss e Sangue Amaldiçoado nos drops | `inimigos-e-bosses` | T3, T6 | `scripts/systems/RunManager.cs`, `scripts/enemies/EnemyBasic.cs`, `scripts/systems/RoomGenerator.cs`, `scripts/items/RoomArtifact.cs`, `data/localization/*/world.json` | concluida |
| T6 | Loja da Câmara de Hemomancia | `ui-hud` | T1, T4 | `scripts/systems/IntermissionChamber.cs`, `scripts/ui/HUD.Shop.cs` (+ .cs.uid, novo), `scripts/ui/HUD.cs`, `scripts/ui/HUD.StatusBars.cs`, `scripts/ui/HUD.Notifications.cs`, `scripts/ui/UiPreview.cs`, `data/localization/*/ui.json` | concluida |
| T7 | Hub: árvore com Sangue Amaldiçoado e remoção do Baú na interface | `ui-hud` | T2, T4, T6, T8 | `scripts/ui/HUD.BloodFountain.cs`, `scripts/ui/HUD.ArtifactChest.cs` (remover), `scripts/ui/HUD.CharacterSheet.cs`, `scripts/ui/HUD.Compendium.cs`, `scripts/ui/MainMenu.cs`, `scripts/ui/UiPreview.cs`, `scripts/ui/HUD.cs`, `data/localization/*/ui.json`, `scripts/ui/HUD.Pause.cs`, `scripts/items/RoomArtifact.cs` (remover), `scripts/ui/HUD.ArtifactOffer.cs` (remover), `scripts/ui/HUD.StatusBars.cs`, `scripts/ui/UiComponents.cs` (so tokens), `scripts/systems/CrowsNestCrypt.cs` (decoracao do antigo Bau), `scripts/systems/CharacterSheet.cs` e `SaveGameManager.cs` (so remover atalhos), `scripts/player/PlayerSkillController.cs` e `scripts/systems/artifacts/ArtifactCatalog.cs` (so atalhos), `scripts/ui/HUD.BlessingOffer.cs`, `data/localization/*/world.json` (so chaves orfas) | concluida |
| T8 | Reajustar os custos da árvore de habilidades | `conteudo-run` | T1, T2 | `data/skills/skills.json` | concluida |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1 ∥ T2
Onda 2: T3 ∥ T8   (após T1, T2)
Onda 3: T4   (após T3)
Onda 4: T6   (após T1, T4)
Onda 5: T5 ∥ T7   (após T2, T3, T4, T6, T8)
```

Caminho crítico: T1 → T3 → T4 → T6 → T5. Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Projetar a nova economia
- **Executor:** `game-designer`
- **Faz:** Define valores e regras: Moedas por inimigo e por sala, Sangue Amaldiçoado por boss e por Lendário (por mapa), escala de custo das 18 skills da árvore (hoje 300–2500 Moedas) no novo recurso, Fruta do Diabo, tamanho do estoque e preço por raridade dos 20 artefatos (Common a Mythic) na loja, e se há itens extras (A2). Simula uma run completa de 4 mapas: quanto Sangue Amaldiçoado por run e quantas runs para comprar a árvore inteira. Só documento; não edita código.
- **Pronto quando:** `docs/design/economia-v2.md` com tabelas e fórmulas, sem números sem justificativa, pronto para os implementadores.
- **Resultado:** docs/design/economia-v2.md criado (10 secoes, todos os numeros como proposta a validar jogando). Moedas: sorteio 3-7 x arquetipo x raridade x fator de mapa (1,0/0,6/0,5), bonus de sala 12/14/16, boss 100/130/160; ~3.740 Moedas por run. Sangue Amaldicoado: boss 40/60/80/120, Lendario 6 (mapa 2) e 8 (mapa 3); run completa ~364. Arvore: 16 skills (nao 18), total 3.196 Sangue (~25 runs de jogador medio). Fruta: 3 garantidas por boss + ~2 por salas (~+75 de vida). Loja: 3-4 itens, precos Common 250 a Mythic 2.400. Pendentes para o usuario: D-A (collector_eye e rewritten_fate sem funcao), D-B (duplicatas), D-C (poder x1,75 vs x1,25), ledger_of_unpaid_blood com saldo da run.

### T2 — Modelo de moedas, save v2 e exclusão de saves antigos
- **Executor:** `conteudo-run`
- **Faz:** Cria `CursedBlood` permanente em `CharacterSheet`/`GameSaveData` (Version = 2) e `RunState` com o saldo de moedas da run (volátil; zera em `StartRun`, `FailRun` e `CompleteBoss`). Remove `BloodCoins` do save. Ao ler um slot com Version < 2 (ou sem versão), exclui o arquivo: antes de excluir, lista os slots existentes e registra no resultado o que foi excluído; erro de leitura continua tratado como slot corrompido (não exclui). `SlotInfo` passa a expor `CursedBlood`. `BloodCoinMultiplier` da skill passa a multiplicar as moedas da run. Atalhos `[Obsolete]` para o resto compilar (A4).
- **Pronto quando:** Build 0/0; save novo grava Version 2 e CursedBlood; save v1 é excluído; moedas da run não entram no save (verificado lendo o JSON gravado).
- **Resultado:** CursedBlood permanente e RunState.RunBloodCoins (volátil; zera em StartRun, FailRun e fim da campanha, não entre mapas); save v2 (CurrentSaveVersion=2) com exclusão de slot válido de Version<2 (nunca de corrompido); atalhos [Obsolete] no CharacterSheet; RunManager usa o saldo da run. Build 0 erros e 21 avisos CS0618 esperados (atalhos), a T7 remove. Save real encontrado: só save_slot_1 (Version 1, 4972 moedas), será excluído na primeira leitura pelo jogo (decisão do usuário). Orquestrador corrigiu PlayerCombat (dano por moeda usa RunBloodCoins).

### T3 — Fruta do Diabo
- **Executor:** `conteudo-run`
- **Faz:** Coletável (Area3D) com visual de fruta de sangue em geometria provisória; efeito +15 de vida máxima e cura 15 via `RunState`, acumulativo, zerado ao fim da run; `DevilsFruit.Spawn(parent, position)` reutilizável (boss e portas); nome e descrição nos 3 idiomas.
- **Pronto quando:** Coletar soma 15 de vida máxima na run (verificado em preview/smoke), textos nos 3 idiomas, validadores 0 erros.
- **Resultado:** DevilsFruit (Area3D, scripts/items/DevilsFruit.cs, geometria provisoria, Spawn(parent,pos) estatico) e RunState.AddDevilsFruit: +15 de vida maxima acumulativo e vida atual sobe junto (cura 15 sem acionar penalidade de cura), bonus separado do bonus de artefatos (RecalculateArtifactBonuses nao o apaga), zera em StartRun/FailRun/fim da campanha, persiste entre salas; textos reward.devils_fruit.name/.desc/.toast nos 3 idiomas. Build 0 erros (18 avisos CS0618 esperados), localizacao 908 chaves 0/0. Nao testado em cena; sem .cs.uid (gerado na proxima abertura do Godot); ninguem chama Spawn ainda (T5 e portas).

### T4 — Artefatos só na loja: estoque e remoção do ciclo de coleção
- **Executor:** `conteudo-run`
- **Faz:** `ArtifactCatalog.RollShopStock(rng, count)` (peso e preço por raridade, da T1); remove a coleção permanente e os 3 slots equipados (`CommitArtifacts`, `ApplyEquippedArtifacts`, campos do save), mantendo a compra como `RunState.AddTemporaryArtifact` sem limite; remove o Baú do hub (sistema e nó da cena, mínimo necessário no .tscn); ajusta a skill `vp-create-artifact` e o validador ao novo modelo. Atalhos `[Obsolete]` para o HUD compilar até a T7. Tambem (D12-D15): 'RollShopStock' exclui artefatos ja possuidos; multiplicador de poder da loja x1,75; 'humanitas.collector_eye' passa a conceder desconto de loja (-5/-10/-15% por rank) e 'humanitas.rewritten_fate' perde so a parte de artefato (ajusta skills.json, PlayerSkillController e as descricoes em content.json); teto de +100% no efeito DamagePerCurrencyHeld (PlayerCombat, 2 pontos), que ja le RunState.RunBloodCoins.
- **Pronto quando:** Build 0/0; validadores 0/0; nenhuma referência ao Baú/equipar fora do HUD (HUD é a T7).
- **Resultado:** ArtifactCatalog.RollShopStock/GetShopPrice/GetShopDiscount (estoque 3-4 sem repetidos, exclui possuidos, 1o slot Common/Uncommon, precos 250..2.400 e pesos por visita via economy.shop.*; visita = CurrentMapIndex-1); RunState.TryBuyShopArtifact/HasRunArtifact (sem limite; artefatos da run persistem entre mapas); colecao permanente, slots equipados e Bau do hub removidos (ArtifactChest.cs e 4 nos do CrowsNest.tscn); collector_eye vira desconto de loja (5/10/15%), rewritten_fate sem a parte de artefato; teto +100% no Livro-Caixa; poder x1,75 mantido; skill/guia vp-create-artifact atualizados. Build 0 erros; validadores 0/0 (1 aviso: prompt.artifact_chest orfao em ui.json). Atalhos [Obsolete] listados para a T7; sobra decoracao ArtifactChestVisual em CrowsNestCrypt.cs:71 (decidir no plano hub-personagens).

### T5 — Recompensas do boss e Sangue Amaldiçoado nos drops
- **Executor:** `inimigos-e-bosses`
- **Faz:** Boss e inimigos Lendários dropam Sangue Amaldiçoado (valores da T1; vai direto ao save com autosave); as moedas por inimigo vão para o saldo da run; a sala do boss deixa de criar o altar de artefato (`SpawnArtifact`) e passa a soltar uma Fruta do Diabo garantida + Sangue Amaldiçoado e abrir o portal direto; notificações pelos métodos da T6; textos de mundo em `world.json`.
- **Pronto quando:** Derrotar boss = +Sangue Amaldiçoado no save, fruta no chão e portal aberto; Lendário comum dropa; build e smokes limpos.
- **Resultado:** EnemyBasic.RollDefeatRewards: moedas pela formula do doc (sorteio inclusivo, x arquetipo x raridade x fator do mapa x skill, por inimigo) e Sangue Amaldicoado dos Lendarios comuns por mapa (0/6/8/8); boss paga so os valores do boss (moedas 100/130/160/0, Sangue 40/60/80/120). RunManager: QueueEnemyDefeat(coins, cursedBlood, pos) com mesmo nome; Sangue vai ao save com autosave e NotifyCursedBloodRewarded; sala do boss sem altar (SpawnArtifact removido): fruta garantida via DevilsFruit.Spawn (nenhuma no mapa 4) e portal aberto direto; sala comum soma bonus de sala limpa 12/14/16; CompleteRoom idempotente. Build 0 erros; localizacao 0 erros e 2 avisos (prompt.artifact_chest e room.artifact_altar.title orfaos, para a T7). Nao testado em jogo. Pendencias: HUD.Compendium ainda tem editores enemy.*_blood_coins sem efeito; HUD.BlessingOffer:308 ainda 25 moedas (doc manda 75).

### T6 — Loja da Câmara de Hemomancia
- **Executor:** `ui-hud`
- **Faz:** Confere o que a fonte da Câmara faz hoje e a troca pela Loja: overlay com 3–4 artefatos sorteados (ícone, nome localizado, descrição, preço em Moedas), comprar aplica o artefato na run; saldo insuficiente tratado; foco por controle, `UiModalStack` e reação a `LanguageChanged`. HUD da run mostra as Moedas como saldo da run; cria `NotifyCursedBloodRewarded` e ajusta `NotifyBloodCoinsRewarded` (a T5 chama). Tela `shop` no UiPreview e no `--ui-batch`. Textos nos 3 idiomas.
- **Pronto quando:** Loja abre, compra e recusa sem moedas; preview `shop` nos 3 idiomas; validadores e build sem erro.
- **Resultado:** Loja de Artefatos: novo balcao interativo em codigo na Camara (IntermissionChamber.BuildShopStand, sem substituir fonte/altar) e overlay HUD.Shop.cs (estoque sorteado uma vez por visita, preco ao vivo com desconto, Comprar/Comprado/Faltam N, saldo no cabecalho, foco e pausa pela UiModalStack); HUD mostra o saldo da run com rotulo Moedas; NotifyCursedBloodRewarded(amount,total) com estilo violeta; preview shop e entrada no --ui-batch; 19 chaves de ui.json nos 3 idiomas. Build 0 erros, localizacao 0 erros. Capturas da loja nos 3 idiomas sem estouro/chave crua, mas o 4o cartao fica cortado e o cartao sem saldo nao recebe foco: ajuste pedido ao mesmo agente (reenvio). Balcao 3D na Camara e fluxo com controle nao conferidos em jogo; 'Illegal instruction' ao sair do Godot a investigar.

### T7 — Hub: árvore com Sangue Amaldiçoado e remoção do Baú na interface
- **Executor:** `ui-hud`
- **Faz:** Custos da Fonte de Sangue em Sangue Amaldiçoado (nome, ícone e cor distintos das Moedas) e saldo no cabeçalho; remove `HUD.ArtifactChest` e o preview `artifact_chest`; a aba Artefatos da Ficha mostra os da run; o slot do menu principal mostra o Sangue Amaldiçoado; remove os atalhos `[Obsolete]` das T2 e T4; textos nos 3 idiomas.
- **Pronto quando:** Build sem atalhos obsoletos; previews das telas alteradas nos 3 idiomas; nenhuma referência a moeda permanente no código.
- **Resultado:** Fonte de Sangue cobra e mostra Sangue Amaldicoado (losango e numero em UiPalette.CursedBlood, UiComponents.CursedBloodGlyph); removidos HUD.ArtifactChest, HUD.ArtifactOffer e RoomArtifact (com .cs.uid), previews e reroll de artefato, decoracao do antigo Bau; Ficha/Artefatos mostra os artefatos da run (origem Comprado na Loja); Compendio com ShopPowerMultiplier e editores economy.*; menu de slots mostra Sangue Amaldicoado e sem contagem de artefatos; game over mostra Moedas da run; indicador da HUD: Sangue Amaldicoado no hub (cena CrowsNest) e Moedas nas demais; bencao esgotada paga economy.coins.blessing_fallback (75) no saldo da run; TODOS os atalhos [Obsolete] removidos; 39 chaves orfas removidas (889 chaves por idioma). Editou RunState.ApplyArtifactEffect (fora da lista) so para trocar o enum que sumiu por strings. Build 0 erros/0 avisos; validadores 0/0; grep limpo; 48 capturas em um processo, principais lidas sem chave crua nem acento quebrado. Nao visto: indicador do hub (overlay cobre) e aviso de bencao esgotada; foco por gamepad nao testado.

### T8 — Reajustar os custos da árvore de habilidades
- **Executor:** `conteudo-run`
- **Faz:** Aplica a escala de custo da T1 às 18 skills, mantendo todos os IDs (`bestialis.*`, `humanitas.*`).
- **Pronto quando:** Custos novos aplicados, IDs intactos, JSON válido, build e validadores sem erro.
- **Resultado:** skills.json: custo base das 16 skills trocado pela tabela da economia-v2 (Bestialis 1.544, Humanitas 1.652, total 3.196 Sangue pela formula base x (1+0,5 x rank)); currency trocado para cursed_blood (nenhum codigo le o campo); IDs e maxRank intactos; JSON valido, validador de localizacao 0/0. Riscos: HUD.BloodFountain ainda paga em Moedas ate a T7; CompendiumTuning '<id>.cost' pode sobrescrever o custo novo se existir override salvo.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 05:50 | Plano criado |
| 2026-10-11 05:57 | Execução iniciada pelo orquestrador (orch 0.5.0) |
| 2026-10-11 05:57 | T1 iniciada por game-designer |
| 2026-10-11 05:57 | T2 iniciada por conteudo-run |
| 2026-10-11 05:58 | Esclarecimento na T2: o saldo de Moedas da run zera em StartRun, FailRun e no fim da campanha, mas NAO entre mapas (a Loja da Camara fica entre mapas). T2 NAO abre o Godot; a exclusao de saves v1 acontece pela logica do jogo ao ler o slot (decisao do usuario). |
| 2026-10-11 06:00 | T2 concluída por conteudo-run |
| 2026-10-11 06:00 | T7 passa a escrever tambem em scripts/ui/HUD.Pause.cs (resumo de game over deve mostrar o saldo da run). Orquestrador corrigiu PlayerCombat (linhas 271 e 590) para o efeito de dano por moeda usar RunState.RunBloodCoins; HUD.StatusBars (T6) e HUD.CharacterSheet:774 (T7) tambem precisam migrar para o saldo da run. |
| 2026-10-11 06:06 | T1 concluída por game-designer |
| 2026-10-11 06:06 | Correcao de contagem: a arvore tem 16 skills (8 bestialis + 8 humanitas), nao 18. T8 usa a tabela por ID da secao 4.2 de economia-v2.md. T6 deve usar CurrentMapIndex-1 como indice da visita a Camara (CompleteBoss ja incrementou o mapa). |
| 2026-10-11 06:06 | T3 iniciada por conteudo-run |
| 2026-10-11 06:06 | T8 iniciada por conteudo-run |
| 2026-10-11 06:08 | T8 concluída por conteudo-run |
| 2026-10-11 06:08 | Decisoes do usuario apos a T1: D12 (collector_eye vira desconto de loja; rewritten_fate perde so a parte de artefato), D13 (loja nao repete artefato possuido), D14 (mantem x1,75), D15 (Livro-Caixa com teto de +100%). T4 amplia o escopo e a lista de arquivos. |
| 2026-10-11 06:09 | T3 concluída por conteudo-run |
| 2026-10-11 06:09 | T4 iniciada por conteudo-run |
| 2026-10-11 06:15 | T4 concluída por conteudo-run |
| 2026-10-11 06:15 | Correcao da suposicao A1: a fonte da Camara NAO abre a arvore; ela cura 35% da vida (uma vez) e o altar de sacrificio troca bencao por vida. A Loja sera um NOVO ponto interativo criado em codigo na Camara (sem editar .tscn a mao), sem substituir a fonte nem o altar. A arvore de habilidades continua so no hub. |
| 2026-10-11 06:15 | T6 iniciada por ui-hud |
| 2026-10-11 06:23 | T6 concluída por ui-hud |
| 2026-10-11 06:23 | Reenvio de ajuste a T6 (mesmo agente): todos os cartoes da loja visiveis sem rolagem em 1600x900, cartao sem saldo focavel com mensagem ao acionar, cor do Sangue Amaldicoado em UiPalette. T5 inicia em paralelo (nao toca HUD.Shop/IntermissionChamber). T7 inicia depois do ajuste (compartilha HUD.cs/UiPreview.cs) e passa a remover tambem RoomArtifact.cs junto com HUD.ArtifactOffer.cs, e a tratar o indicador de moedas no hub. |
| 2026-10-11 06:23 | T5 iniciada por inimigos-e-bosses |
| 2026-10-11 06:26 | Pedido do usuario: interromper a execucao depois de terminar T5 e T6. A T7 NAO sera iniciada; o plano sera encerrado como parcial ao fim de T5 e do ajuste da T6. |
| 2026-10-11 06:26 | T5 concluída por inimigos-e-bosses |
| 2026-10-11 06:27 | Ajuste da T6 concluido: 4 cartoes sem rolagem em 1600x900, cartao sem saldo e Comprado focaveis, UiPalette.CursedBlood. Lote de captura de um unico processo, saida 0. |
| 2026-10-11 06:27 | Plano encerrado como parcial: T7 nao iniciada por pedido do usuario |
| 2026-10-11 06:29 | Execucao retomada a pedido do usuario: somente a T7 (as demais tarefas ja estavam concluidas) |
| 2026-10-11 06:29 | T7 iniciada por ui-hud |
| 2026-10-11 06:39 | T7 concluída por ui-hud |
| 2026-10-11 06:39 | Plano concluido: T7 finalizada, 8/8 tarefas |

## Resultado final

Plano concluido (8/8). Entregue: economia-v2.md; Moedas de Sangue so na run e Sangue Amaldicoado permanente (save v2 que exclui saves antigos); Fruta do Diabo; artefatos so na Loja da Camara (balcao novo, estoque sorteado, sem duplicatas, desconto da skill, x1,75, Livro-Caixa com teto de +100%); Bau do hub removido; custos da arvore reescalados (3.196 Sangue no total); drops de Sangue de boss e Lendarios; boss sem altar com fruta garantida (exceto mapa 4) e portal direto; interface migrada, atalhos temporarios removidos. Verificacao: build 0 erros e 0 avisos, validadores 0 erros e 0 avisos, grep limpo, capturas da loja e das telas do hub nos 3 idiomas. NADA foi testado jogando (so leitura de codigo e capturas estaticas). Pendencias: indicador de Sangue Amaldicoado no hub e aviso de bencao esgotada sem checagem visual; foco por gamepad nao testado; balcao 3D da Camara e posicao da fruta do boss so jogando; DevilsFruit.cs e HUD.Shop.cs sem .cs.uid (gerado ao abrir o editor); docs/gameplay_systems.md e SOURCE.md do pacote de dungeon ainda citam o Bau; ArtifactCatalog.RollChoices sem chamadores; todos os numeros de balanceamento sao proposta a validar jogando (riscos no cap. 10 de economia-v2.md); 'Illegal instruction' ao sair do Godot ocorreu uma vez e nao reproduziu; nada commitado. Save antigo (slot 1, 4972 moedas) foi excluido pela regra do save v2; copia de seguranca temporaria na pasta da sessao.

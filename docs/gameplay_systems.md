# Sistemas de gameplay e interface

Registro consolidado da grande atualização de julho de 2026. Os valores descritos representam a implementação atual e podem ser balanceados pelos catálogos e propriedades exportadas.

## Controles multiplataforma

As ações de gameplay ficam em `project.godot`. `ControllerUiSupport` complementa em runtime as ações padrão de interface para aceitar qualquer dispositivo compatível com o mapeamento de gamepad do Godot.

| Ação | Teclado/mouse | DualSense | Xbox |
| --- | --- | --- | --- |
| Movimento e direção do ataque | `WASD` / cursor | Analógico esquerdo | Analógico esquerdo |
| Dash | `Espaço` | `X` | `A` |
| Ataque básico | Clique esquerdo | `Quadrado` | `X` |
| Ataque especial | `Q` | `Triângulo` | `Y` |
| Fúria Bestial | `F` | `R1` | `RB` |
| Interação | `E` | `X` | `A` |
| Ficha | `C` | `Criar` | `View` |
| Pausa | `Esc` | `Opções` | `Menu` |

No controle, movimento e mira compartilham o analógico esquerdo. O ataque rápido é produzido por dash + ataque básico e possui buffer para aceitar os inputs em sequência rápida.

Na interface, d-pad e analógico navegam, `X`/`A` confirma e `Círculo`/`B` volta. `Esc` fecha a camada superior antes de alternar a pausa. Ofertas de Bênção de Sangue são a exceção: não podem ser fechadas por nenhum botão e exigem uma escolha antes da continuação da run.

## Pausa e segurança de estado

- abrir a ficha com `C` pausa a árvore do jogo;
- projéteis inimigos têm processamento pausável e não avançam durante menus;
- overlays que recebem input pausado usam `ProcessModeEnum.Always`;
- fechar um overlay restaura o estado correto da pausa;
- “Desistir da run” força a morte e reutiliza o fluxo normal de retorno ao Ninho do Corvo.

## Espada Grande

`WeaponDefinition` concentra os dados da arma: ataques do combo, dano, delay de impacto, recovery, stagger, alcance, largura, forma, tolerância da hitbox, empurrão, multiplicadores posicionais, lunge e recarga especial. `AttackControlProfile` acrescenta uma matriz por golpe com buffer, cancelamento por esquiva/especial, mobilidade durante startup/recovery, assistência de mira e redirecionamento no impacto.

A arma funcional preserva o ID interno `longsword`, mas é apresentada como **Espada Grande**. IDs antigos `greatsword` e `dual_blades` migram para ela. A antiga Espada Grande placeholder e Espadas Duplas foram removidas; Revolver Duplo e Chicote continuam reservados para expansão. Os três IDs já possuem modelos registrados em `WeaponVisualCatalog`.

`PlayerWeaponVisual` escuta `WeaponLoadout.EquippedWeaponChanged`, cria sockets nos ossos `Fist.R` e `Fist.L` e troca o modelo carregado junto com o equipamento. Os modelos mantêm o pivô original do ponto de empunhadura; a Espada Grande fica presa pelo cabo e acompanha as animações do personagem. O modelo equipado permanece oculto em idle, caminhada e corrida, tornando-se visível apenas durante um ataque.

`PlayerAnimator` sincroniza a velocidade do clipe com a duração efetiva de startup + recovery informada por `PlayerCombat`. Cortes, ataque rápido e especial deixam de tocar com uma duração independente da janela de combate, e golpes em linha usam uma animação própria de estocada.

### Ataque básico

- combo de três golpes, reiniciado após 1 segundo sem sequência;
- cada golpe possui buffer, cancelamentos, restrição de movimento e assistência de mira próprios;
- bônus de velocidade de ataque altera somente hit delay e recovery deste ataque;
- terceiro golpe com empurrão 20% maior que o padrão do combo;
- a estocada final usa recovery de 0,12 segundo e pode ser cancelada mais cedo;
- detecção combina forma do ataque, raio físico do alvo e tolerância de borda;
- efeitos exclusivos do básico não são reutilizados pelo ataque rápido.

### Ataque rápido

- ativado por dash + ataque básico, com buffer de input;
- usa dano, hitbox, stagger e empurrão próprios da arma;
- não recebe a velocidade global do ataque básico;
- bênçãos diretas compartilham um grupo exclusivo e substituem a equipada desse grupo.

### Ataque especial

- impacto após 0,12 segundo e recovery de 0,30 segundo;
- cooldown independente de 1 segundo;
- velocidade de ataque básico não altera conjuração ou cooldown;
- recebe apenas os efeitos de bênção destinados ao especial;
- bênçãos diretas do especial são exclusivas entre si e a escolha nova substitui a equipada.

### Stagger, empurrão e impacto

- o jogador recebe stagger ao sofrer dano;
- ataques do jogador voltaram a aplicar empurrão;
- inimigos comuns podem ser interrompidos repetidamente quando pressionados contra paredes;
- bosses e inimigos especiais protegidos possuem armadura de stagger;
- tanks mantêm resistência adicional ao empurrão;
- colisões reais contra paredes e objetos sólidos podem gerar dano de impacto.

## Fúria Bestial e feedback de hit

- requer uma bênção com `UnlockBestialFury`;
- não acumula pontos e não exibe sua barra enquanto essa bênção não estiver equipada;
- acumula até 100 pontos e consome 10 pontos por segundo por padrão, resultando em 10 segundos de duração-base;
- `FuryDuration` e efeitos permanentes de duração continuam alterando a taxa efetiva de consumo;
- ao chegar a zero, causa em área 15% do dano básico atual;
- bênçãos diretas de Fúria usam `bestial_fury_mode` e substituem a equipada anteriormente;
- o pós-processamento dessatura o mundo e preserva vermelhos associados a sangue.

Hits criam partículas de sangue; flash, hit-stop, números, câmera e formas de alcance foram alinhados à hitbox efetiva. Ataques pelas costas e impactos contra parede possuem cores, partículas, marcadores e sons próprios. Críticos usam amarelo-ouro e fonte três vezes maior que o número comum.

O áudio diferencia os dois cortes laterais, estocada, rodopio, queda especial e impacto no chão. Acertos selecionam variações carnais por tipo de golpe; bloqueios, parede e esquiva usam conjuntos separados, com pequena variação de pitch.

## Inimigos

Os inimigos básicos usam modelos animados do pacote Ultimate Monsters:

- melee: Ninja;
- ranged: Demon;
- tank: Orc;
- bosses, por índice de mapa: `invisible_woman` (Guardiã Invisível, modelo Ghost Skull), `labyrinth_keeper` (Guardião do Labirinto, Orc Skull), `silver_dragonkin` (Dragonkin Prateado, Dragon Evolved) e `armand` (Lorde Armand, Blue Demon); `CampaignCatalog.BossId` coincide com o `Id` do `BossCatalog` no mesmo índice;
- escala é normalizada em relação à altura do jogador;
- idle, locomoção, ataque e reação a hit usam animações do asset, com root motion removido para manter a física autoritativa.

O dano aplicado pelos inimigos recebeu redução de 10%. Spawns usam 5 unidades por wave, quatro waves por sala, aviso de 3 segundos e intervalo-base de 9 segundos. As posições procuram preservar distância mínima entre inimigos.

Encostar no corpo de um inimigo causa, a cada cooldown de contato, 5% do dano de ataque atual daquele inimigo. O boneco de treinamento mantém esse dano desabilitado.

### Ranged

- participação reduzida em 20% sobre a distribuição anterior;
- pode recuar e tenta recuperar a distância preferencial;
- dispara flechas emissivas maiores e mais lentas;
- o projétil não expira por distância nem desaparece quando seu atirador morre; permanece até atingir jogador, parede ou obstáculo;
- projéteis pausam junto do jogo;
- a caixa que antecipava a hitbox foi removida, mantendo apenas o aviso de preparação.

### Tank

- mais vida e resistência a empurrão;
- windup maior, aviso `!!`, hitbox ampliada e dano superior.

### Bosses

- reposicionamentos de `kinetic_shift` e `mirror_dash` são projetados para uma célula navegável com margem de duas células das paredes;
- investidas continuam usando colisão física e telegraph linear;
- bosses recebem somente 5% do empurrão e do dano de impacto contra parede;
- após a morte, o boss paga Moedas de Sangue e Sangue Amaldiçoado fixos por mapa e solta uma Fruta do Diabo (exceto no mapa final); não há Altar de Artefatos nem drop de artefato;
- a saída libera quando a Fruta é pega; ver [Economia e recompensas](#economia-e-recompensas).

## Bênçãos de Sangue

Existem 63 definições ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps. `giant_slayer` e `bathory_scarlet_steps` permanecem preservadas como comentários JSONC; `orlok_contagious_demise` não está no pool atual. Dracul, Bathory, Orlok e Karnstein possuem opções diretamente ligadas a ataque básico, rápido e especial. Essas bênçãos têm peso superior; a seleção da família Stirps usa peso 0,45 contra 1,0 das linhagens comuns.

Raridade e nível são independentes. A raridade é sorteada uma vez e determina `PowerMultiplier`; toda bênção começa em N1 e pode chegar a N10. Cada nível adicional soma 25% ao efeito já escalado pela raridade. Consumidores de gameplay usam `EffectivePowerMultiplier`. Chance é limitada a 100% na agregação, nas descrições e nas prévias.

As descrições das ofertas substituem o valor-base pelo valor efetivo da raridade, sem exibir uma segunda linha redundante de “Efeito”. Termos de ataque básico, rápido, especial e Fúria Bestial são destacados e explicados por hover/foco.

Nome, descrição e flavor não ficam em `blood_blessings.json` (só IDs e números), e sim em `data/localization/<idioma>/content.json`, nas chaves `blessing.<id>.name`, `.desc` e `.flavor`. A descrição é um modelo: `{n}` é o valor do efeito `n` e `[kw=<id>]termo[/kw]` marca uma palavra-chave (`BloodBlessingText`). O nome do termo e sua explicação ficam em `blessing.keyword.<id>.term` e `.hint`; o tema de cada família, em `blessing.family.<id>.theme`. Ver [Localização](#localização).

### Exclusividade por ataque

- bênçãos diretas do ataque básico compartilham seu grupo exclusivo;
- bênçãos diretas do rápido compartilham `quick_attack_status`;
- bênçãos diretas do especial compartilham `special_attack_status`;
- bênçãos diretas de Fúria compartilham `bestial_fury_mode`;
- uma nova escolha do mesmo grupo substitui a equipada;
- oferta e HUD informam qual bênção será trocada;
- efeitos sem exclusividade continuam acumuláveis.

O validador exige raridade-base Common e o grupo exclusivo correto para bênçãos ligadas diretamente ao moveset. O catálogo aceita comentários `//` e `/* ... */`, permitindo retirar uma bênção do pool sem apagar sua definição.

### Status e efeitos defensivos

- Sangramento e Pestilência acumulam stacks; cada stack aumenta em 5% o dano recebido;
- o dano periódico de Sangramento também é multiplicado pelo total de stacks;
- cinco stacks de Pestilência formam um Hospedeiro;
- os números de stack têm o tamanho visual do ícone, usam a cor do efeito e ignoram o depth test;
- Encanto dura 5 segundos base e recebe multiplicadores externos de duração;
- Laço de Sangue desenha uma linha rosa entre os envolvidos;
- Fascínio faz alvos imunes a Encanto sofrerem 25% mais dano;
- a explosão de Briga de Amantes usa feedback rosa.

### Sinergia Orlok + Bathory

`stirps_crimson_pestilence_pool` faz a explosão de um cadáver pestilento criar uma poça com o mesmo raio por 5 segundos. Inimigos dentro dela recebem Sangramento por uma duração-base de 3 segundos.

As poças usam uma malha irregular de baixo brilho, com uma camada interna sutil, e ocorrências próximas são mescladas para evitar sobreposição visual excessiva. A explosão de Pestilência segue a mesma linguagem: mancha orgânica no chão, veios radiais e esporos projetados, sem alterar o raio usado pela lógica de propagação.

### Bênção dos Ratos

- somente a morte de inimigos sob Pestilência dispara a invocação;
- cada rato dura 8 segundos e usa um pequeno cilindro verde-limão;
- `CharacterBody3D`, colisão cilíndrica e força de separação impedem a sobreposição física entre ratos;
- movimento e intervalo de ataque são capturados do jogador ao invocar;
- dano é 50% do ataque básico do jogador;
- capacidade simultânea-base: Common 1, Uncommon 2, Rare 3, Epic 4 e Legendary 5;
- cada nível acima de N1 acrescenta mais um rato à capacidade.

### Correções de runtime

- cura excedente convertida em barreira usa o overheal reportado por `Health`, corrigindo Reserva Imperial e equivalentes;
- a barreira espera 8 segundos depois de ser obtida ou parcialmente consumida, então perde 0,25 ponto por segundo; é zerada na troca de sala e aparece como segmento azul junto da vida;
- Sangue Espesso recalcula a vida máxima preservando os pontos que já faltavam, atualiza imediatamente o sinal da HUD e exibe na descrição o ganho efetivo após os multiplicadores ativos;
- a trilha de roubo de vida entra na árvore antes de receber `GlobalPosition`, evitando `!is_inside_tree()`;
- artefatos não vêm mais de bosses: são vendidos na Loja da Câmara de Hemomancia (ver [Economia e recompensas](#economia-e-recompensas)).

Cada inimigo comum que morre deixa uma poça de sangue em pixel art (`BloodPool`, folha gerada por `tools/pixel_art/blood_pool/`), que escorre do corpo e fica no chão com um brilho em loop. Ao pegar a recompensa da sala (a da porta escolhida; sem ela, o primeiro item pego), o Virgil suga todo o sangue: as poças secam enquanto um filete contínuo de sangue sobe de cada uma e escorre em espiral até ele, com respingos na ponta e um pulso de condensação. Se a recompensa é uma Bênção, isso é o "Sanguis Purgatus" antes da oferta obrigatória; nas outras, a sucção é mais curta e não pausa o jogo. Sala de boss não tem poças.

`bathory_immortal_beauty` converte dano em Ferida Recuperável após 2 segundos sem novo golpe e restaura durante 5 segundos. Pela escala atual, recupera 20%/40%/70%/110%/140% em Common/Uncommon/Rare/Epic/Legendary; Epic e Legendary ainda são pontos explícitos de rebalanceamento.

## HUD e ficha

- HUD redistribuída em um estilo minimalista, usando apenas painel e divisor discretos do Kenney UI Pack; dicas fixas do topo removidas;
- cursor global substituído por ponteiro e manopla temáticos do Kenney Cursor Pack;
- minimapa removido da HUD e atalho `Tab` desativado;
- faixa compacta com até sete bênçãos e um `+` abaixo do último slot quando houver mais;
- ícones exibem `N2` a `N10`; N1 fica implícito, e a ficha mostra nível e raridade separadamente;
- ficha em `C` com coluna rolável para todas as bênçãos e descrições por hover/foco;
- passivas permanentes da Fonte de Sangue visíveis com descrição;
- ficha pausa o jogo; a aba Atributos só aparece em build de debug (em release os atributos continuam computados, mas ficam ocultos: `CharacterSheetTabOffset` em `HUD.CharacterSheet.cs`);
- o contador de recursos mostra as Moedas de Sangue da run e, no Ninho do Corvo, o Sangue Amaldiçoado;
- Fonte de Sangue usa scroll e layout responsivo, sem botão fechar sobre o texto;
- HUD permanece disponível na Câmara de Hemomancia;
- vida e Fúria ficam ocultas no Ninho do Corvo;
- a barra de Fúria também fica oculta durante a run até existir uma bênção que desbloqueie Fúria Bestial;
- barreira é representada em azul como extensão do total de vida, sem label separado;
- o título da sala mostra o nome do mapa e, abaixo, “Sala x/y · n/m”; ao limpar a sala a linha vira “Sala limpa” e, quando o portal de saída abre de fato (depois de pegar os itens do chão da sala), vira “Portal aberto” com um toast (`NotifyPortalOpened`);
- o aviso de wave mostra a contagem regressiva num rótulo próprio, e o “+N” de moedas aparece ao lado da moldura do contador;
- dano recebido em golpes seguidos (menos de 0,35 s de intervalo) abre os números em leque, no máximo três ao mesmo tempo.

## Localização

Os textos exibidos ficam em `data/localization/<idioma>/` (`pt-BR`, `en`, `es`), em JSON plano `"chave": "texto"`. O antigo `data/localization/game_texts.json` foi removido.

| Arquivo | Conteúdo |
| --- | --- |
| `ui.json` | interface: menus, HUD, opções, prompts (inclui quase todas as `portal.*`; `portal.hemomancy_chamber` está em `world.json`) |
| `content.json` | bênçãos, artefatos, skills, armas e nomes de efeitos |
| `world.json` | bosses e golpes, mapas, inimigos e mensagens de combate |

- `Localization` (autoload) carrega todos os `.json` da pasta do idioma. A resolução é idioma atual → `pt-BR` → a própria chave; em debug, cada chave ausente gera um aviso uma única vez.
- API: `Localization.Text(chave)`, `Localization.TextFormat(chave, args)`, `Localization.Has(chave)` e o sinal `LanguageChanged`. Se uma tradução tiver o formato malformado, o jogo tenta o texto `pt-BR` e, por fim, mostra o texto cru.
- A cultura da sessão segue o idioma do jogo: `en` usa ponto decimal; `pt-BR` e `es`, vírgula.
- Overlays, o HUD e os rótulos 3D (prompts de fonte, baú, bancada, portais, artefatos no chão) assinam `LanguageChanged` e se atualizam ao trocar o idioma.
- Chaves de conteúdo: `blessing.<id>.name/.desc/.flavor`, `artifact.<id>.name/.desc/.flavor`, `skill.<id>.name/.desc/.flavor/.icon`, `weapon.<arma>.name/.desc` (e os nomes dos golpes, como `weapon.longsword.basic_1.name`, `.quick.name` e `.special.name`), `artifact.effect.<Tipo>` (nome do efeito), `blessing.family.<id>.theme` e `blessing.keyword.<id>.term/.hint`. Os catálogos JSON guardam só IDs e números.
- Chaves de mundo: `boss.<id>.name`, `boss.<id>.move.<golpe>.name`, `map.<id>.name/.rule`, `enemy.rarity.<id>.name`, `enemy.archetype.<id>.name` e `enemy.legendary.<id>.name`.
- `PromptText` de `ScenePortal` (inclusive nos `.tscn`), `BloodFountain` e `WeaponBench` guarda uma chave (por exemplo `portal.start_run`); se a chave existir, a UI mostra o texto traduzido, senão mostra o valor literal.
- Cada artefato tem `saveName` em `artifacts.json`: identificador legado gravado nos saves, que nunca deve ser alterado. Registros antigos do save são resolvidos para o ID atual em `CharacterSheet.WithResolvedId`.

Validação: `py tools/validate_localization.py` confere JSON válido, paridade de chaves entre os idiomas, placeholders iguais e se as chaves literais do C# existem. `validate_blessings.py` e `validate_artifacts.py` conferem as chaves de `content.json` dos seus catálogos. Skills, inimigos e bosses só têm a checagem de paridade. Termos de referência: `docs/design/glossario-localizacao.json`.

Para adicionar um texto, crie a chave nos três idiomas. Um idioma novo exige alterar `Localization.Languages` e `NormalizeLanguage`, o seletor de `UiComponents.OptionsPanel` e a lista `LANGUAGES` dos três validadores. Textos só de debug (log de eventos, seção DEV, linhas “Prévia:” dos previews) podem ficar literais.

Em build de debug, `scripts/ui/UiPreview.cs` abre telas de UI com dados fictícios (`--ui-preview=<tela>`, `--ui-lang=`, `--ui-device=`, `--ui-tab=`) e tem o modo em lote `--ui-batch`, que percorre telas × idiomas num único processo e grava PNGs; ver o cabeçalho do arquivo e o Anexo A de `docs/design/ui-guia.md`.

## Economia e recompensas

Valores vêm de `CompendiumTuning` (chaves `economy.*` e `reward.*`); os números abaixo são os padrões do código. Design em `docs/design/economia-v2.md` e `docs/design/portas-recompensas.md`.

### Moedas de Sangue × Sangue Amaldiçoado

- **Moedas de Sangue** (`RunState.RunBloodCoins`): só na run, nunca no save. Pagas por inimigos (`RunManager.QueueEnemyDefeat` → `AddBloodCoins`), pelo bônus de sala limpa (`economy.coins.room_clear_bonus.*`, 12/14/16/0), por boss (`economy.coins.boss.*`, 100/130/160/0) e por portas de Moedas. Gastas na Loja (`RunState.TrySpendRunBloodCoins`). Zeram em `StartRun`, `FailRun` e em `CompleteBoss` do mapa final; não zeram entre mapas.
- **Sangue Amaldiçoado** (`CharacterSheet.CursedBlood`): permanente, serializado no slot. Pago pelo boss (`economy.cursed_blood.boss.*`, 40/60/80/120) e por inimigo Lendário comum (`economy.cursed_blood.legendary.*`, 0/6/8/8, com chance `economy.cursed_blood.legendary_drop_chance`, padrão 1,0). Gasto só na Fonte de Sangue, para comprar skills (`CharacterSheet.SpendCursedBlood`). Sobrevive à morte da run.

### Fruta do Diabo

`DevilsFruit` (Area3D, `scripts/items/DevilsFruit.cs`) é coletada com `interact`, soma `economy.fruit.max_health` (padrão 15) de vida máxima só na run (`RunState.AddDevilsFruit`) e cura `economy.fruit.heal` (padrão 15; se for maior que o ganho de vida máxima, cura a diferença). Emite `Collected`, que o `RunManager` usa para liberar a saída. Zera em `StartRun`, `FailRun` e no fim da campanha.

O ganho é exato: `RunState.DevilsFruitMaxHealthTotal` é somado em `PlayerController.ApplyRuntimeTuning` depois dos multiplicadores de vida máxima (`MaxHealthMultiplier`, `SacrificedMaxHealthMultiplier`), fora de `MaxHealthBonus`; cada Fruta dá +15.

### Portas com recompensa

- `RoomRewardCatalog` (classe pura) sorteia o conjunto de portas de cada saída: tipos `Blessing`, `DevilsFruit` e `Coins`, 1 ou 2 portas (chance de duas: base 0,30/0,35/0,40 por mapa + 0,05 a cada 2 salas, teto 0,55), sem repetir tipo na mesma saída. Com o padrão `reward.blessing.max_sets_without_blessing` = 0, toda saída tem uma porta de Bênção; Fruta e Moedas só aparecem na segunda porta. Moedas de porta: 150/130/110 por mapa. Teto de Frutas de porta por run: 3.
- `RunState` guarda o conjunto sorteado (`GetOrRollRoomRewardSet`, sobrevive a recarga de cena), a porta escolhida (`ChooseRoomRewardDoor`, `PendingRoomReward`) e as três bênçãos pré-sorteadas da porta de Bênção. Reinicia com a run e uma `RunSeed` nova.
- `ScenePortal.BuildRewardDoors` monta uma porta por recompensa a partir do portal-modelo da sala; `DoorRewardIcon` desenha o medalhão provisório e o rótulo, e o prompt informa o tipo (`reward.door.*`).
- A oferta de uma porta de Bênção é pré-sorteada sem marcar o diário; as bênçãos só são marcadas como descobertas quando a oferta abre, depois de `BloodBlessingCatalog.ReviewPlannedOffers` trocar as opções que deixaram de ser elegíveis. A troca de cena do `ScenePortal` usa um timer que não corre com a árvore pausada, então não ocorre com a pausa aberta. `RoomGenerator.PlaceExitOnFarWall` tenta a parede de saída em 3 níveis de rigor (3 células com parede atrás; só a central; qualquer borda).
- Ao limpar a sala, `RunManager.CompleteRoom` constrói as portas **travadas** e põe no chão o bônus de sala limpa e a recompensa da porta escolhida na sala anterior: `RewardPickup` para Bênção e Moedas, `DevilsFruit` para Fruta. Cada item é pego por `interact`; a Bênção abre a oferta (`OpenBloodBlessingOffer`) e a saída só abre (`UnlockExitAfterReward`) quando não restar item no chão.
- A saída da última sala comum (para o boss) e a do boss não têm conjunto de portas: saída única, liberada pelo mesmo mecanismo.

### Loja da Câmara

Ver [Câmara de Hemomancia](#câmara-de-hemomancia). Artefatos só existem na Loja; O sorteio do antigo altar do boss (`ArtifactCatalog.RollChoices`) foi removido.

### Save v2

`GameSaveData.Version` = 2 (`CurrentSaveVersion`). `SaveGameManager.ReadSlot` só trata como v1 um JSON com `Version` presente, inteiro e `>= 1` e `< 2`; esse arquivo é renomeado para `save_slot_N.v1.json` (`ArchiveLegacySlot`), sem migração, e o slot começa vazio. JSON válido sem `Version` é carregado normalmente; JSON inválido segue o fluxo de slot corrompido (backup).

## Salas especiais

### Sala bônus inicial

`RunStartBlessingRoom.tscn` aparece antes do primeiro mapa. A fonte central abre uma oferta obrigatória aleatória e o portão libera após a escolha. Recarregar a cena não concede outra bênção se a run já possui uma.

### Sala de treinamento

O Ninho do Corvo possui área de treinamento e boneco central. `TrainingDummy` recebe dano, stagger, estados e empurrão, mas restaura a vida para testes contínuos.

### Placeholders de personagens (hub)

`CrowsNestCrypt.CreateCharacterPlaceholders` põe três `CharacterPlaceholder` na sala leste do hub: sarcófagos em geometria provisória, com título "???/Bloqueado" (`scene.crows_nest.character_locked`) e o aviso "Em breve" (`prompt.character_soon`) ao chegar perto. Não têm interação nem escolha de personagem; os personagens jogáveis futuros não estão implementados (ver `docs/design/personagens-futuros.md`).

### Câmara de Hemomancia

`IntermissionChamber.tscn` aparece depois de bosses não finais.

- Fonte: recupera 35% da vida máxima, uma vez por passagem;
- Altar: reduz em 5% a vida máxima total da run e eleva uma bênção em um nível, preservando a raridade;
- a oferta do altar sorteia até três bênçãos elegíveis e reutiliza o layout de cards da escolha de bênçãos; se houver menos de três, mostra somente as disponíveis;
- a redução persiste entre salas e é cumulativa (`100 → 95 → 90,25`), sendo removida ao terminar a run;
- cada card mostra ícone, raridade, `N atual → próximo N` e compara os valores efetivos;
- nível máximo: N10; cada aprimoramento acrescenta 25% do valor-base escalado pela raridade;
- cada serviço (fonte e altar) pode ser usado uma vez;
- Loja de Artefatos: balcão no centro da Câmara que abre `HUD.Shop.cs` (`OpenShop`). O estoque é sorteado uma vez por visita (`ArtifactCatalog.RollShopStock`): 3 artefatos, com 50% de chance de um quarto, sem repetir no estoque nem oferecer os que a run já tem, e o primeiro slot sempre Common ou Uncommon; os pesos de raridade mudam conforme a visita. Preço-base por raridade `economy.shop.price.*` (250/420/700/1.100/1.700/2.400), com desconto da skill `humanitas.collector_eye` (0,05 por rank, máx. 0,9). Comprar (`RunState.TryBuyShopArtifact`) gasta Moedas de Sangue da run e adiciona o artefato à run, com poder multiplicado por `economy.shop.power_multiplier` (padrão 1,75). Fechar e reabrir mostra o mesmo estoque;
- HUD e ficha continuam disponíveis.

## Arquivos centrais

- `project.godot`;
- `scripts/ui/ControllerUiSupport.cs`;
- `scripts/ui/HUD.cs`;
- `scripts/player/PlayerCombat.cs`;
- `scripts/player/PlayerController.cs`;
- `scripts/player/PlayerWeaponVisual.cs`;
- `scripts/player/BloodBlessingRuntime.cs`;
- `scripts/enemies/EnemyBasic.cs`;
- `scripts/enemies/EnemyProjectile.cs`;
- `scripts/player/SummonedRat.cs`;
- `scripts/systems/weapons/WeaponDefinition.cs`;
- `scripts/systems/weapons/WeaponCatalog.cs`;
- `scripts/systems/weapons/WeaponVisualCatalog.cs`;
- `scripts/systems/AudioManager.cs`;
- `scripts/systems/Localization.cs`;
- `scripts/ui/UiPreview.cs`;
- `tools/validate_localization.py`;
- `scripts/systems/RunState.cs`;
- `scripts/systems/RunManager.cs`;
- `scripts/systems/RoomGenerator.cs`;
- `scripts/systems/RunStartBlessingRoom.cs`;
- `scripts/systems/IntermissionChamber.cs`;
- `scripts/systems/TrainingDummy.cs`;
- `data/blessings/blood_blessings.json`;
- `data/localization/<idioma>/{ui,content,world}.json`.

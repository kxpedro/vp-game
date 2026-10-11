# Personagens futuros (elenco desbloqueável)

Status: especificação de intenção. **Nada deste documento está implementado.** Hoje existe apenas Virgil e, no hub, três marcadores reservados (placeholders) sem função. Origem: decisões D9 a D11 e suposições A12 a A14 do plano `20261011-0550-hub-personagens`.

Este documento não define números de balanceamento. Onde um valor seria necessário, ele está listado em "Perguntas em aberto".

## 1. Decisões registradas

- **D9 - Elenco.** Virgil mais 3 personagens desbloqueáveis, obtidos por side quests ligadas aos bosses dos 3 mapas (ver seção 3). Os 3 não serão implementados agora.
- **D10 - Identidade.** Cada personagem terá 4 variantes de arma (a elaborar), um poder único e atributos base próprios, invisíveis ao jogador mas computados. A progressão permanente é compartilhada por slot.
- **D11 - O que muda agora.** Só os placeholders no hub e a ocultação da aba Atributos da Ficha em release.
- **A12.** Placeholder é um marcador físico no hub, com rótulo de bloqueado e prompt "Em breve", sem interação funcional.
- **A13.** A aba Atributos segue visível apenas em build de debug. Os cálculos de `CharacterAttribute` não mudam.
- **A14.** Nenhuma mudança de save agora. Campos de personagem entram num plano futuro, de forma aditiva.

## 2. Premissa e fantasia

Virgil é o caçador transformado em vampiro que foge da maldição de Armand. Os três desbloqueáveis também são presos ou amaldiçoados por Armand, mas cada um quer algo diferente da queda dele. O jogador não "compra" um personagem: ele o liberta numa side quest ligada ao boss do mapa.

Tom: dark fantasy, flavor curto. Os personagens são aliados incômodos no Ninho do Corvo: dividem o hub, mas querem finais que se excluem.

## 3. Elenco e ligação com a campanha

A campanha tem 3 mapas com boss próprio e uma luta direta contra Armand (`armands_throne`, 0 salas), conforme `CampaignCatalog.cs`. Cada desbloqueável liga-se a um dos 3 bosses de mapa, que continuam sendo bosses da campanha (não viram jogáveis).

| Mapa | Boss (id) | Personagem (nomes definidos por Pedro) | Quer | Final se derrotar Armand |
|---|---|---|---|---|
| - | - | **Virgil** | Cura e vingança | Cura da maldição (final canônico) |
| 1, `armands_dungeons` | Guardiã Invisível (`invisible_woman`) | **Selina**, a Libertadora | Destruir o castelo e libertar todos | O castelo desmorona; todos escapam, ao custo de quem estava dentro |
| 2, `blood_garden` | Guardião do Labirinto (`labyrinth_keeper`) | **Magdalena**, a Penitente | O fim da maldição, inclusive a dela | A maldição acaba para todos, mas ninguém volta a ser humano: é o fim do sangue |
| 3, `upper_castle` | Dragonkin Prateado (`silver_dragonkin`) | **Dominicus**, o Herdeiro | O trono, discordando do pai | Assume o castelo; ambíguo: governa "melhor", mas o ciclo continua |

Ordem dos bosses (decisão de Pedro): Guardiã Invisível, Guardião do Labirinto e Dragonkin Prateado, depois Armand. Dominicus é o terceiro a ser liberado e faz o gancho direto para a luta final: o filho de Armand surge logo antes do pai. Os mapas mantêm a ordem e as salas; só o boss de cada um muda (a adequação de tema de cada mapa ao boss novo está em aberto).

Eixo narrativo: vingança, poder, liberdade e fim. Nenhum final é "o certo".

### 3.1 Selina, a Libertadora (mapa 1)
- Feiticeira lunar, com um gato como familiar. Quer destruir o castelo.
- Ligação com o boss: a Guardiã Invisível é sua irmã ou mestra, com o poder corrompido por Armand. Lua e gato aparecem nas duas, como marca de parentesco.
- Direção visual: cabelo azul, chifres em forma de lua crescente, preto e dourado.
- Direção de jogo (hipótese): magia lunar à distância, gato como familiar.

### 3.2 Magdalena, a Penitente (mapa 2)
- Inquisidora com cruz, colar-selo e manopla. Pertencia à mesma ordem do Guardião do Labirinto.
- Ligação com o boss: o Guardião virou carcereiro de Armand; ela ficou presa a uma penitência.
- Side quest: provação que só se vence aceitando a dor.
- Direção visual: cabelo escuro, capa e armadura pretas, cruz em osso, manopla; paleta preto, osso e dourado.
- Direção de jogo (hipótese): corrente e manopla, com custo de vida em troca de dano.

### 3.3 Dominicus, o Herdeiro (mapa 3)
- Meio humano, meio vampiro, filho de Armand; não concorda com o que o pai faz, mas quer o trono. Preso pelo pai; é o último a ser libertado e o gancho para a luta final.
- Ligação com o boss: o Dragonkin Prateado é seu carcereiro ou antigo protetor.
- Side quest: provação de sangue para libertá-lo.
- Direção visual: elegância de Alucard, silhueta de dhampir (Vampire Hunter D) e magia de sangue com garras. Cabelo claro, sangue vermelho.
- Direção de jogo (hipótese): corpo a corpo com garras e sangue, poder de avanço.

### 3.4 Desbloqueio (side quest)
1. **Pista:** porta lateral com selo diferente, rara e fora da rota principal, que só abre com uma pista ou item (de NPC do hub ou de inimigo especial).
2. **Provação:** sala lateral com duelo ou mini-boss curto, com mecânica própria; a história se revela por fala e ambiente.
3. **Despertar:** o personagem acorda num dos 3 sarcófagos do hub (os placeholders atuais) e passa a ser jogável.

### 3.5 Finais e escolha final
- 4 finais, um por personagem (inclusive Virgil).
- No portão antes de Armand, o jogador escolhe quem entra. O personagem escolhido luta contra Armand.
- Para escolher um desbloqueável, o jogador precisa ter completado o arco dele (falas que avançam ao jogar com ele, ao longo de várias runs). Virgil sempre está disponível.
- O epílogo mostra o destino dos outros conforme as relações construídas.

### 3.6 Diálogo
No Ninho do Corvo, falas curtas escolhidas por condições (quem foi jogado, o que aconteceu na run, qual boss matou o jogador), seguindo a referência de Hades. Cada personagem tem falas com Virgil e com os outros dois; as tensões entre eles (Dominicus e Selina, por exemplo) sustentam o drama.

### 3.7 Referências visuais (fora do repositório)
As imagens de referência de Pedro incluem personagens de terceiros (Alucard, Vampire Hunter D, Lady Maria). Servem como inspiração e não devem ser copiadas nem entrar em `assets/` ou `references/`. Lady Maria foi descartada por ser muito reconhecível. O cavaleiro necromante de olhos verdes ficou como possível visual do Guardião do Labirinto antes de virar carcereiro.

## 4. Poder único por personagem

Cada personagem tem um poder próprio que muda a forma de jogar, sem substituir bênçãos, artefatos e skills (a camada comum). As direções abaixo são hipóteses de 3.1 a 3.3, não regras.

| Personagem | Direção temática do poder único |
|---|---|
| Virgil | já existe: Fúria Bestial (`F`) e a progressão Bestialis/Humanitas |
| Dominicus | pressão e avanço, ligados ao sangue |
| Selina | controle de espaço e magia à distância, com o familiar |
| Magdalena | correntes, marcas e custo de vida em troca de dano |

Regras herdadas do projeto:
- o poder único não pode quebrar as regras de bênçãos (únicas por run, modificando um ataque, com `exclusiveGroup`) nem a de que só `UnlockBestialFury` libera o `F`;
- todo perigo ou efeito forte precisa de leitura clara (telegraph), ao menos visualmente;
- cuidado com degenerescência (cura excessiva, combos infinitos), a verificar no balanceamento.

## 5. Atributos base ocultos

Os cinco atributos de `CharacterAttribute` (Força, Destreza, Vitalidade, Resolução e Hemomancia/`Bloodcraft`) continuam existindo e sendo computados por `CharacterSheet.GetAttribute`, que hoje parte de um valor inicial único para todos. O jogador não os vê em release (aba oculta, A13).

Intenção futura:
- cada personagem tem uma distribuição base própria dos cinco atributos, definida em dados, que só aparece como diferença de sensação de jogo (vida, dano, velocidade, etc.);
- o jogador não vê números nem pontos a distribuir; a identidade vem do poder único, das armas e da distribuição oculta;
- a distribuição entra no cálculo existente, em vez de um sistema paralelo;
- a aba Atributos continua como ferramenta de debug para conferir o resultado.

Risco: com valores ocultos, o balanceamento depende de testes de TTK e sobrevivência por personagem, não de leitura direta do jogador.

## 6. Armas: 4 variantes por personagem (a elaborar)

Estado do código: `scripts/systems/weapons/WeaponCatalog.cs` é um array C# sem JSON e hoje tem uma única arma implementada (`longsword`, com o mapeamento legado de `greatsword` e `dual_blades`). O visual vive em `WeaponVisualCatalog.cs`.

Intenção: cada personagem terá 4 variantes de arma, dando 16 no total contando Virgil. A elaboração é trabalho futuro. O que já se decide:
- as armas de Virgil existentes devem caber dentro dessas 4 variantes sem renomear IDs persistidos;
- cada variante precisa de moveset próprio (ataque básico, especial, etc.) compatível com os `exclusiveGroup` das bênçãos, que são por tipo de ataque;
- as variantes devem ser tematicamente coerentes com o personagem e com o poder único;
- ainda não há decisão sobre se a arma é exclusiva do personagem ou compartilhada entre eles.

## 7. Progressão compartilhada por slot

A progressão permanente (skills Bestialis/Humanitas, artefatos comprados, Sangue Amaldiçoado, códice) pertence ao slot de save e vale para qualquer personagem. Trocar de personagem não zera nada. Consequências:
- desbloquear um personagem é ganho de variedade, não de poder bruto;
- as skills e artefatos precisam funcionar bem com todos os personagens, o que pesa na revisão de conteúdo existente;
- o que é por personagem (e vira dado novo no save): desbloqueado, arma equipada, e possivelmente favoritos.

## 8. Sangue Amaldiçoado como moeda do hub

Sangue Amaldiçoado é o recurso permanente do hub (`CharacterSheet.CursedBlood`), obtido principalmente ao derrotar bosses e inimigos especiais, gasto em aprimoramentos permanentes. Moedas de Sangue ficam restritas à run.

Pergunta de design: o desbloqueio de personagem custa Sangue Amaldiçoado, é só a vitória sobre o boss, ou ambos? A hipótese preferida é desbloqueio pela vitória (a narrativa de "conquista") e Sangue Amaldiçoado para as armas e melhorias do personagem, mas não está decidido.

## 9. Perguntas em aberto

1. Armand pode virar personagem jogável pós-campanha?
2. Como a pista da porta lateral é obtida em cada mapa, e quão rara ela é?
3. Poder único: ativo (botão) ou passivo? Qual a regra exata, custo e recarga? Como convive com Fúria Bestial e com as skills?
4. Como os temas das linhagens (Dracul, Bathory, Karnstein, Orlok) se relacionam com Dominicus, Selina e Magdalena?
5. As 4 armas de cada personagem: quais, como se desbloqueiam (Sangue Amaldiçoado, drop, conquista), e se são exclusivas.
6. Distribuição dos atributos ocultos por personagem e como o balanceamento será verificado.
7. A primeira oferta de bênção precisa ter `basic_attack`: isso continua valendo para qualquer moveset novo?
8. Como a dificuldade e o tempo-alvo dos bosses (`TargetDurationSeconds` x `ExpectedPlayerDps`) se ajustam a DPS esperados diferentes por personagem?
9. As falas e o arco de cada personagem, em pt-BR, en e es; quantas runs o arco de cada um exige.
10. Tela de seleção: onde fica (hub, antes da sala de bênção inicial) e como mostra bloqueado/desbloqueado.
11. O que acontece com os 3 placeholders do hub quando o personagem é desbloqueado.
12. Compêndio/códice: separar estatísticas por personagem ou agregar.

## 10. Esboço do plano futuro

Etapas sugeridas, a detalhar quando o plano for aberto:

1. **Modelo de personagem.** Dado (definição estática: id estável, nome/flavor por chave de localização, distribuição de atributos base, poder único, lista de armas) e leitura pelo código que hoje assume um único protagonista. Reaproveitar `CharacterSheet` sem duplicá-lo.
2. **Desbloqueio.** Evento ao derrotar o boss associado, persistido por slot, com notificação no HUD. Ligação com o `CampaignCatalog` e `BossCatalog` (ids de boss estáveis).
3. **Seleção.** Fluxo no hub (interação nos marcadores que hoje são placeholders), tela de escolha respeitando navegação por controle e localização nos 3 idiomas.
4. **Armas.** Elaborar as 4 variantes de cada personagem; expandir `WeaponCatalog` e `WeaponVisualCatalog`; manter o mapeamento de IDs legados.
5. **Visuais.** Modelo, animações e arma de cada personagem; hoje a geometria é provisória. Assets novos exigem `SOURCE.md` e entrada em `CREDITS.md`.
6. **Poder único.** Implementação por personagem, seguindo as regras de leitura e as salvaguardas contra degenerescência.
7. **Save aditivo.** Novos campos (personagens desbloqueados, personagem ativo, arma por personagem) opcionais, com valor padrão equivalente a "somente Virgil". Nunca renomear ou remover IDs já persistidos, e `GameSaveData.Version` hoje não migra nada: o carregamento precisa tolerar campos ausentes.
8. **Balanceamento e verificação.** Testes de TTK, sobrevivência e sensação por personagem, validadores de conteúdo e de localização, smoke headless.

## 11. Divergências e observações

- O `GDD.txt` (visão de 2022) não prevê um elenco jogável; este documento é nova direção, e o GDD deve ser atualizado se a ideia se confirmar.
- O código só tem uma arma implementada, então "4 variantes por personagem" é todo conteúdo novo.
- A aba Atributos oculta não remove os atributos do cálculo; a mudança é só de exposição.

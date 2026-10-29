# Todos os diálogos e seus gatilhos (estado atual do jogo)

Gerado dos arquivos `data/dialogue/*.json` e `data/localization/pt-BR/dialogue.json`. Texto em pt-BR. Serve como retrato do que existe hoje, **antes** do roteiro novo de abertura (ver `narrativa.md`).

## Como as falas são escolhidas

1. O jogo filtra as conversas pela **categoria** do momento (falar com o NPC, voltar ao hub, bark de run, encontro de boss etc.) e pelos **requisitos** (todos precisam ser verdadeiros).
2. Entre as elegíveis, ordena por **classe** (essencial > específica > evergreen), depois **não vista primeiro**, depois **prioridade** numérica, depois a mais antiga, depois sorteio.
3. Falas repetíveis só voltam depois de esgotar as não vistas e de passar o **cooldown** (em nº de conversas do mesmo grupo). Algumas têm **chance** de tocar.
4. Uma conversa por visita ao hub por NPC. Escolhas do jogador gravam **flags** que liberam falas futuras.


## Odalric, o Cronista (NPC do hub) (19 conversas)


### Hub: ao falar com o NPC

**`first_hub_arrival`**  
Gatilho: nenhum (sempre elegível)  
essencial / prioridade 90, toca uma vez, abre sozinha  
Grava flags: `met_odalric`

- **Odalric:** Então é você, o caçador que fugiu de Armand. Bem-vindo ao Ninho do Corvo.
- **Virgil:** Fugir é uma palavra generosa. Ainda sinto a maldição no sangue.
- **Odalric:** Todos sentem. A Fonte de Sangue, ali adiante, troca o Ícor que você traz por força. Use-a.
- **Odalric:** Eu fico aqui, com meu livro. Alguém precisa escrever como isso termina.
- *Escolha:* “Quem é você?” → `who_reply` (flag `asked_odalric_name`)
  - **Odalric:** Odalric, o Cronista. Servi na corte de Armand. Agora anoto cada tentativa sua neste livro.
  - **Odalric:** Um pacto de tinta me prendeu a este ninho. Só me solta no último capítulo.
- *Escolha:* “Não preciso de guia.” → `curt_reply` (flag `was_curt_with_odalric`)
  - **Odalric:** Ótimo. Guias falam demais. Eu só escrevo.

**`odalric_after_third_boss`**  
Gatilho: boss `silver_dragonkin` já derrotado; já viu `odalric_after_second_boss`  
específica / prioridade 64, toca uma vez

- **Odalric:** Um dragão de prata, e você ainda de pé. Passei a escrever seu nome com letra maior.
- **Odalric:** Só resta o trono. Armand sabe que você vem, e Armand detesta não ser a surpresa.
- **Odalric:** Os leitos selados deixaram passar um fio de luz. Não direi o que vi. Ainda não.

**`odalric_after_second_boss`**  
Gatilho: boss `labyrinth_keeper` já derrotado; já viu `odalric_after_first_boss`  
específica / prioridade 62, toca uma vez

- **Odalric:** O Guardião do Labirinto caiu. Anotei, mas o labirinto nunca foi o ponto. Importava o que ele cercava.
- **Odalric:** Do leste, junto aos leitos selados, agora ouço um ritmo contra a pedra. Talvez seja só o vento. Talvez.
- **Odalric:** Continue, Virgil. O livro ainda tem páginas.

**`odalric_after_first_boss`**  
Gatilho: boss `invisible_woman` já derrotado  
específica / prioridade 60, toca uma vez

- **Odalric:** Virgil. A Guardiã Invisível caiu. Anotei como "derrotada", não "morta". Há diferença.
- **Odalric:** Os arquivos ligam uma lua e um gato àquele posto. Nunca soube por quê.
- **Odalric:** Os leitos selados, a leste, andam inquietos desde então. Talvez seja só o vento.

**`odalric_after_victory`**  
Gatilho: já viu `odalric_return_victory`; campanhas vencidas 1..  
específica / prioridade 35, toca uma vez

- **Odalric:** Armand caiu, e o pacto de tinta continua me prendendo a este ninho.
- **Odalric:** Ou escrevi errado o último capítulo, ou ele não era o último.
- **Odalric:** De todo modo, restam páginas em branco. Guardo-as para você.

**`odalric_many_runs`**  
Gatilho: runs iniciadas 10..  
específica / prioridade 20, toca uma vez

- **Odalric:** {0} tentativas registradas. Meu tinteiro quase secou.
- **Odalric:** Insista. Prefiro uma história longa a uma bonita.

**`odalric_name_close`**  
Gatilho: flag `asked_odalric_name`; runs iniciadas 3..  
específica / prioridade 15, toca uma vez

- **Odalric:** Você perguntou meu nome no primeiro dia. Na corte me chamavam "o arquivo".
- **Odalric:** Ninguém perguntava. Um arquivo não tem nome, tem prateleira.
- **Odalric:** Obrigado, Virgil. Fica anotado em letra miúda, para não estragar minha reputação.

**`odalric_name_curt`**  
Gatilho: flag `was_curt_with_odalric`; runs iniciadas 3..  
específica / prioridade 15, toca uma vez

- **Odalric:** No primeiro dia você dispensou guias. Anotei, e respeitei.
- **Odalric:** Mas cronista sem leitor escreve mal. Siga sem guia; só não finja que não me ouve.

**`odalric_lineages_a`**  
Gatilho: já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 4)

- **Odalric:** Dracul manda. Toma a vida do outro e a faz sua, golpe a golpe.
- **Odalric:** Bathory prefere poças de sangue e sacrifício. Cura quem paga o preço.
- **Odalric:** Eu, por ofício, prefiro a que sobrevive para ser escrita.

**`odalric_lineages_b`**  
Gatilho: já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 4)

- **Odalric:** Karnstein seduz. Agilidade e encanto: o inimigo luta por você, ou nem luta.
- **Odalric:** Orlok adoece. Pestilência, controle, e um risco que sempre cobra juros.
- **Odalric:** As Stirps misturam duas dessas linhagens. Raras, e quase nunca gentis.

**`odalric_armand_rumor`**  
Gatilho: já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 4)

- **Odalric:** Dizem que Armand já foi um homem. A maldição, porém, é mais velha que ele.
- **Odalric:** Os arquivos que eu guardava queimaram na noite em que ele se coroou.
- **Odalric:** Restam boatos. Anoto todos. Peneiro depois.

**`odalric_bench_book`**  
Gatilho: arma `longsword`  
evergreen / prioridade 0, repetível (cooldown 4)

- **Odalric:** Aquele livro na bancada trata de armas. A sua espada tem capítulo próprio.
- **Odalric:** Curto, mas sangrento. Do jeito que eu gosto.


### Hub: retorno após morrer

**`odalric_death_by_boss1`**  
Gatilho: última run: died; morreu no boss `invisible_woman`  
específica / prioridade 70, toca uma vez

- **Odalric:** Caiu diante da Guardiã Invisível. Os golpes dela avisam antes de cair. Olhe o chão.
- **Odalric:** Anotei como "cedo demais". Há tempo para rever o capítulo.

**`odalric_death_by_armand`**  
Gatilho: última run: died; morreu no boss `armand`; mortes 3..  
específica / prioridade 70, toca uma vez

- **Odalric:** Armand outra vez. Servi à mesa dele por anos: ele só se cansa de quem para de tentar.
- **Odalric:** Conte o que viu lá em cima. Eu anoto, você cuida das feridas. Dividimos o serviço.

**`odalric_first_death`**  
Gatilho: mortes 1..1; última run: died  
específica / prioridade 50, toca uma vez

- **Odalric:** Primeira vez? Anotei. O sangue não esquece, caçador. O livro também não.
- **Odalric:** Mas você voltou. Já é mais do que muitos capítulos meus conseguiram.

**`odalric_many_deaths`**  
Gatilho: mortes 5..; última run: died  
específica / prioridade 30, toca uma vez

- **Odalric:** {1} mortes no livro. Passei a numerá-las.
- **Odalric:** Não é crítica. É estatística.

**`odalric_death_again`**  
Gatilho: última run: died  
evergreen / prioridade 0, repetível (cooldown 3)

- **Odalric:** Mais uma linha no livro. A caligrafia já nem finge surpresa.
- **Odalric:** Gaste o que trouxe na Fonte de Sangue e volte. É o que você faz de melhor.


### Hub: retorno após vitória/abandono

**`odalric_return_victory`**  
Gatilho: última run: victory  
específica / prioridade 40, toca uma vez

- **Odalric:** Chegou ao fim do castelo. Meu último capítulo, ou o seu primeiro.
- **Odalric:** Venha. Quero ouvir tudo, e depois fingir que sempre soube.

**`odalric_return_abandoned`**  
Gatilho: última run: abandoned  
específica / prioridade 10, toca uma vez

- **Odalric:** Desistiu no meio? Anotei como retirada. Quem recua vive para escrever.


## Falas de run (Virgil, Guardiã e bosses) (37 conversas)


### Run: fala solta (bark)

**`bark_room_enter_1`**  
Gatilho: em run  
evergreen / prioridade 0, repetível (cooldown 3), chance 35%, gatilho `room_enter`

- **Virgil:** Pedra fria. Cheiro de ferro. Nada de novo.

**`bark_room_enter_garden`**  
Gatilho: mapa `blood_garden`  
evergreen / prioridade 0, repetível (cooldown 3), chance 50%, gatilho `room_enter`

- **Virgil:** Rosas regadas com sangue. Alguém caprichou.

**`bark_room_enter_castle`**  
Gatilho: mapa `upper_castle`  
evergreen / prioridade 0, repetível (cooldown 3), chance 50%, gatilho `room_enter`

- **Virgil:** Corredores largos demais para tão pouca vida.

**`bark_room_cleared_1`**  
Gatilho: em run  
evergreen / prioridade 0, repetível (cooldown 3), chance 30%, gatilho `room_cleared`

- **Virgil:** Mais um passo.

**`bark_low_health_1`**  
Gatilho: em run  
evergreen / prioridade 0, repetível (cooldown 3), chance 60%, gatilho `low_health`

- **Virgil:** Ainda não. Não aqui.

**`bark_low_health_2`**  
Gatilho: em run; já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 3), chance 50%, gatilho `low_health`

- **Odalric:** Aguente, caçador. Não terminei o capítulo.

**`bark_room_enter_dungeons_1`**  
Gatilho: mapa `armands_dungeons`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Celas abertas. Quem estava aqui não saiu por vontade própria.

**`bark_room_enter_dungeons_2`**  
Gatilho: mapa `armands_dungeons`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Correntes ainda mornas. Alguém foi levado há pouco.

**`bark_room_enter_garden_2`**  
Gatilho: mapa `blood_garden`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Neblina demais. Algo respira dentro dela.

**`bark_room_enter_garden_3`**  
Gatilho: mapa `blood_garden`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Os espinhos se mexem quando desvio o olhar.

**`bark_room_enter_castle_2`**  
Gatilho: mapa `upper_castle`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Tapetes sem pó. Alguém ainda cuida deste lugar.

**`bark_room_enter_castle_3`**  
Gatilho: mapa `upper_castle`  
evergreen / prioridade 0, repetível (cooldown 4), chance 50%, gatilho `room_enter`

- **Virgil:** Cada degrau me leva mais perto dele.

**`bark_room_cleared_dungeons`**  
Gatilho: mapa `armands_dungeons`  
evergreen / prioridade 0, repetível (cooldown 4), chance 40%, gatilho `room_cleared`

- **Virgil:** Só as correntes balançando. Por enquanto.

**`bark_room_cleared_garden`**  
Gatilho: mapa `blood_garden`  
evergreen / prioridade 0, repetível (cooldown 4), chance 40%, gatilho `room_cleared`

- **Virgil:** As rosas se fecham, devagar. Como quem espera.

**`bark_room_cleared_castle`**  
Gatilho: mapa `upper_castle`  
evergreen / prioridade 0, repetível (cooldown 4), chance 40%, gatilho `room_cleared`

- **Virgil:** Silêncio comprado a ferro. Caro, mas é silêncio.

**`bark_room_enter_odalric_dungeons`**  
Gatilho: mapa `armands_dungeons`; já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 4), chance 35%, gatilho `room_enter`

- **Odalric:** Os calabouços. No meu livro: "hospitalidade, ver também castigo".

**`bark_room_enter_odalric_castle`**  
Gatilho: mapa `upper_castle`; já viu `first_hub_arrival`  
evergreen / prioridade 0, repetível (cooldown 4), chance 35%, gatilho `room_enter`

- **Odalric:** A ala superior. Só a vi num relatório. Descreva-me depois.

**`bark_boss_phase_invisible_woman_1`**  
Gatilho: na sala do boss `invisible_woman`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Ela acelerou. Os golpes ainda avisam antes de cair.

**`bark_boss_phase_invisible_woman_2`**  
Gatilho: na sala do boss `invisible_woman`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Mais perto do fim. Ela também sabe.

**`bark_boss_phase_labyrinth_keeper_1`**  
Gatilho: na sala do boss `labyrinth_keeper`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** As paredes mudaram de lugar. De novo.

**`bark_boss_phase_labyrinth_keeper_2`**  
Gatilho: na sala do boss `labyrinth_keeper`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Ele fecha o cerco. Leio as marcas no chão.

**`bark_boss_phase_silver_dragonkin_1`**  
Gatilho: na sala do boss `silver_dragonkin`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Prata e fúria. Ele só está começando a se irritar.

**`bark_boss_phase_silver_dragonkin_2`**  
Gatilho: na sala do boss `silver_dragonkin`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Respire, Virgil. Cauda, garras, saliva. Um de cada vez.

**`bark_boss_phase_armand_1`**  
Gatilho: na sala do boss `armand`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Lorde Armand:** É o melhor que você tem, caçador?

**`bark_boss_phase_armand_2`**  
Gatilho: na sala do boss `armand`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Lorde Armand:** Meus súditos aplaudem. Aplaudem qualquer vencedor.

**`bark_boss_phase_armand_3`**  
Gatilho: na sala do boss `armand`  
evergreen / prioridade 0, repetível (cooldown 3), chance 85%, gatilho `boss_phase`

- **Virgil:** Ele ainda sorri. Isso me incomoda mais que os golpes.


### Run: encontro com o boss (antes da luta)

**`boss_pre_invisible_woman`**  
Gatilho: na sala do boss `invisible_woman`  
específica / prioridade 50, toca uma vez

- **Virgil:** Ar parado, velas acesas e ninguém à vista. Apareça.
- **Guardiã Invisível:** Eu nunca saí daqui, caçador. Você é que não sabia olhar.
- **Virgil:** Armand deixou uma guarda na porta. Que original.
- **Guardiã Invisível:** Armand deixou uma prisioneira. Hoje ela pode descontar em alguém.
- **Virgil:** Então desconte. Eu tenho pressa.
- **Guardiã Invisível:** Olhe o chão, cria da noite. É a única cortesia que faço.

**`boss_pre_labyrinth_keeper`**  
Gatilho: na sala do boss `labyrinth_keeper`  
específica / prioridade 50, toca uma vez

- **Virgil:** As paredes não ficam quietas. Isto não é um corredor, é uma boca.
- **Odalric:** O Guardião do Labirinto troca o caminho e cobra cada distração. Leia as marcas no chão: aqui tudo avisa antes de ferir.

**`boss_pre_silver_dragonkin`**  
Gatilho: na sala do boss `silver_dragonkin`  
específica / prioridade 50, toca uma vez

- **Virgil:** Calor. Cheiro de metal derretido. Armand guarda a escada com um dragão.
- **Odalric:** Garras, cauda, saliva, um réquiem inteiro. Muito repertório, e cada peça avisa antes de cair.

**`boss_pre_armand`**  
Gatilho: na sala do boss `armand`  
específica / prioridade 50, toca uma vez

- **Lorde Armand:** Virgil. Meus calabouços, meu jardim, meu castelo inteiro. E você ainda diz que fugiu.
- **Virgil:** Não vim por você. Vim pelo que você deixou no meu sangue.
- **Lorde Armand:** É a mesma coisa. A maldição é só o meu nome, dito por dentro.
- **Virgil:** Então eu o calo.
- **Lorde Armand:** Tente. Meus súditos adoram um espetáculo.

**`boss_pre_invisible_woman_again_1`**  
Gatilho: na sala do boss `invisible_woman`; já viu `boss_pre_invisible_woman`  
específica / prioridade 40, repetível (cooldown 0)

- **Guardiã Invisível:** De volta. O sangue de Armand não deixa você descansar, não é?
- **Virgil:** Nem a você. Vamos acabar com isso.

**`boss_pre_invisible_woman_again_2`**  
Gatilho: na sala do boss `invisible_woman`; já viu `boss_pre_invisible_woman`  
específica / prioridade 40, repetível (cooldown 0)

- **Guardiã Invisível:** Eu vi você chegar desde a primeira porta. Ainda não aprendeu a olhar.
- **Virgil:** Aprendi a ler o chão. Basta.

**`boss_pre_generic`**  
Gatilho: sala de boss  
evergreen / prioridade 0, repetível (cooldown 0)

- **Virgil:** {3} me espera. Hora de fechar este andar.


### Run: depois de derrotar o boss

**`boss_post_invisible_woman`**  
Gatilho: na sala do boss `invisible_woman`  
específica / prioridade 50, toca uma vez

- **Virgil:** Nenhum corpo. Só o ar voltando ao lugar.
- **Virgil:** Derrotada. Não sei se isso é o mesmo que acabada.

**`boss_post_labyrinth_keeper`**  
Gatilho: na sala do boss `labyrinth_keeper`  
específica / prioridade 50, toca uma vez

- **Virgil:** As paredes pararam. Até um labirinto sabe quando perdeu.
- **Odalric:** O Guardião caiu. Resta saber o que ele guardava de verdade.

**`boss_post_silver_dragonkin`**  
Gatilho: na sala do boss `silver_dragonkin`  
específica / prioridade 50, toca uma vez

- **Virgil:** Prata fria no chão. Ele não pediu para guardar esta escada.
- **Odalric:** Resta uma sala, Virgil. Depois dela, o capítulo que temo e desejo escrever.

**`boss_post_armand`**  
Gatilho: na sala do boss `armand`  
específica / prioridade 50, toca uma vez

- **Lorde Armand:** Um homem. Eu fui um homem, antes de virar nome.
- **Virgil:** Descanse. Ou o que restar de você.
- **Virgil:** Acabou. Então por que o sangue ainda canta?

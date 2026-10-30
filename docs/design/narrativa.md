# Narrativa e fluxo de abertura (save novo)

Status: **especificação de intenção, nada implementado.** Origem: roteiro de abertura esboçado por Pedro (fluxograma de 5 blocos) e conversa de design. Itens marcados com **[EM ABERTO]** ainda serão definidos pelo Pedro; o que não está marcado foi decidido ou aprovado por ele.

Documentos relacionados: [`dialogos.md`](dialogos.md) e [`dialogos-arquitetura.md`](dialogos-arquitetura.md) (sistema de diálogos e falas já existentes, incluindo Odalric e Armand), [`personagens-futuros.md`](personagens-futuros.md) (elenco jogável desbloqueável).

## 1. Princípios

- A história deve ter **profundidade**: nada raso. O roteiro completo (vários NPCs, camadas de revelação) será escrito depois; este documento fixa só a abertura e o esqueleto da narrativa profunda.
- A história é revelada **aos poucos, a cada run**, no estilo do Hades: a progressão de enredo anda junto com a progressão de runs.
- Tom: dark fantasy sóbrio (ver `dialogos.md`). Frases curtas, sem exposição longa.

## 2. Fluxo do save novo

Roda **somente quando o jogador cria um save novo** (slot vazio). Em saves existentes, o jogo segue o fluxo normal (MainMenu → hub).

```
Cutscene 1 -> Tutorial -> Sala do trono (luta) -> Cutscene 2 -> Hub (caixão) -> Loop de runs
```

### 2.1 Cutscene 1: apresentação
- Apresenta o personagem (Virgil) e o vilão (Armand) e seu castelo.
- Deve poder ser **pulada** (ver seção 4).

### 2.2 Tutorial
- Gameplay de introdução, em um mapa na **entrada do castelo de Armand**.
- Usa os **mesmos golpes e controles do loop**, para que o hub e as runs não precisem reensinar nada.
- **[EM ABERTO]** O que o tutorial ensina e em que ordem (movimento, ataque, esquiva, habilidades), e se há inimigos reais ou só bonecos.

### 2.3 Sala do trono: luta contra Armand (impossível de vencer)
- Ao passar pela entrada, Virgil entra **direto na sala do trono**, já diante de Armand.
- É uma luta curta, jogável, **projetada para ser perdida**: o jogador sente o poder do vilão e ganha um objetivo.
- **[EM ABERTO]** Como garantir a derrota: Armand invulnerável, dano que escala até matar, ou vida do Virgil que nunca basta. Se há alguma "janela" de acerto simbólica.
- Observação técnica: `armands_throne` já existe no `CampaignCatalog` como a luta final (0 salas). Decidir se a sala do trono do tutorial reaproveita essa cena ou é uma cena própria.

### 2.4 Cutscene 2: morte e maldição
- Mini cutscene curta: Armand **mata e amaldiçoa** Virgil.
- É a transição entre a sala do trono e o hub.
- Deve poder ser **pulada**.

### 2.5 Hub: despertar e primeiro NPC
- Virgil **acorda no hub, saindo do caixão** (já existe a animação `caixao-virgil`).
- Um NPC apresenta a Virgil o novo lugar e explica:
  - Virgil está **preso ao castelo** até desfazer a maldição;
  - como quebrar a maldição (o objetivo que justifica as runs).
- **Spoiler (não revelado ao jogador no começo):** o NPC é do "mal" e quer **tomar o lugar de Armand**. Ele ajuda Virgil porque lhe convém.
- **[EM ABERTO]** Quem é esse NPC. Hoje o único NPC do hub é **Odalric, o Cronista**, arquivista da corte de Armand; ele é candidato natural, mas não está decidido. Também em aberto: se o NPC do spoiler é o mesmo que apresenta o hub ou outro.
- **[EM ABERTO]** Como isso se encaixa com o texto atual, em que Virgil é "caçador transformado em vampiro que foge da maldição de Armand" (`dialogos.md`, `personagens-futuros.md`): o roteiro novo faz a maldição nascer na morte no trono. Revisar esses textos quando o roteiro for implementado.

### 2.6 Loop de runs
- Início do loop normal de gameplay (hub → runs → hub), sem fim definido para o jogador (o diagrama marca como ∞).
- A morte em run leva de volta ao hub, ancorada na mesma maldição: **a maldição explica por que Virgil renasce no caixão**.

## 3. Pistas sutis do NPC

Para a revelação ser merecida, o NPC deve deixar pistas pequenas desde a primeira conversa, sem entregar a traição:
- falas ambíguas (por exemplo, interesse demais no destino de Armand);
- algo estranho no visual ou na postura;
- perguntas que só alguém com plano próprio faria.

**[EM ABERTO]** Lista de pistas, em que run cada uma aparece e quando a revelação acontece.

## 4. Regras das cutscenes

- Tocam **só em save novo**.
- Têm **botão de pular** (acessível por teclado e controle).
- **[EM ABERTO]** Se o tutorial e a luta do trono também podem ser pulados, ou só as cutscenes.
- **[EM ABERTO]** Estilo das cutscenes (imagens estáticas com texto, animação em sprite, cena em engine) e se há voz ou só texto.

## 5. Abertura gradual do hub

Nas primeiras runs, o hub não entrega tudo de uma vez: o NPC **libera os sistemas aos poucos**, conforme o jogador completa runs, para evitar sobrecarga.

**[EM ABERTO]** Quais sistemas (Fonte de Sangue, Bigorna, Loja de Artefatos, skills, compêndio etc.), em que ordem e com que gatilho (nº de runs, boss derrotado, morte).

## 6. Narrativa profunda futura (a escrever)

Esta seção é um espaço reservado para o roteiro completo, ainda sem conteúdo definido.

- **Vários NPCs** no hub, cada um com história, motivações e arco próprios. Pedro quer "pelo menos vários", não um só.
- **História revelada por run**: cada run abre novas falas, e o enredo avança junto com a progressão (modelo Hades). Isso se apoia no sistema de diálogos existente (`dialogos-arquitetura.md`: requisitos, categorias, "seen", derrotas por boss).
- **Elenco jogável futuro** (Selina, Magdalena, Dominicus) já tem esboço em `personagens-futuros.md`; falta integrá-lo ao novo roteiro de abertura.
- **[EM ABERTO]** Todo o resto: elenco de NPCs, arcos, finais, motivação de Armand e a origem da maldição.

## 7. Referência: como o Hades organiza diálogos e história

Fontes públicas achadas: palestra da GDC 2021 "Breathing Life into Greek Myth: The Dialogue of Hades" (Darren Korb e Greg Kasavin, só no [GDC Vault](https://gdcvault.com/play/1026975/Breathing-Life-into-Greek-Myth), não consegui ler o conteúdo), entrevistas com Kasavin ([ScreenHub](https://www.screenhub.com.au/news-article/features/digital/jini-maxwell/hades-greg-kasavin-breaks-down-supergiants-unique-approach-to-narrative-262459), [Game Developer](https://www.gamedeveloper.com/design/roguelikes-and-narrative-design-with-i-hades-i-creative-director-greg-kasavin)). Elas explicam a filosofia, mas não o formato dos dados.

Confirmado nas fontes:
- A narrativa persiste e avança a cada morte; os personagens têm consciência do ciclo, então morrer vira parte da história.
- Foco em poucos personagens com personalidade clara, em vez de um enredo largo.
- Reatividade a pequenos comportamentos do jogador (arma escolhida, recursos que tem, onde morreu).
- Toda a fala é dublada, e o roteiro é escrito pensando na produção de áudio.

Sobre os **deuses do Hades**, que é o análogo direto das vozes do sangue (relatos de jogadores, não fonte oficial: [discussão na Steam](https://steamcommunity.com/app/1145360/discussions/0/3005549744941536780)):
- Cada deus fala **uma vez por run**, na primeira vez que o jogador o encontra naquela run. É a mesma regra que o Pedro definiu.
- Há **pools separados** por tipo de encontro: o encontro normal em câmara, a loja e as bênçãos duplas (duo) têm cada um o seu.
- Cumprir um objetivo de história **coloca a conversa no pool de não ouvidas**, mas ela pode não tocar logo: conversas anteriores do pool tocam primeiro. Ou seja, o pool é uma **fila em ordem**, não um sorteio.

Sobre o ritmo de história, a [revisão de narrativa da GDC 2021](https://media.gdcvault.com/GDC+2021/Scott+Parker+GDC+Narrative+Review+2021.pdf) (de Scott Parker, não da Supergiant) cita que as histórias dos personagens vizinhos costumam abrir depois de **5 a 10 runs**, e que a volta de Perséfone exige 10 runs de sucesso. Serve como ordem de grandeza para as fatias de história, não como regra.

Do meu conhecimento da comunidade de mods do Hades (**não verificado nesta pesquisa**): cada fala é uma entrada em tabelas Lua, com requisitos de estado de jogo (runs, bosses derrotados, falas já vistas, itens), prioridade para decidir qual toca primeiro, flag de tocar uma vez e cooldown nas repetíveis. O jogo avalia o que está elegível e escolhe uma por interação.

## 8. O jogo já tem esse sistema

Já existe um sistema de diálogos com a mesma ideia, documentado em [`dialogos-arquitetura.md`](dialogos-arquitetura.md) e com dados em `data/dialogue/*.json`:
- **Resource/JSON:** conversas com `requirements` (lista fechada de condições: `runs_started`, `deaths`, `boss_defeated`, `seen`, `flag`, `last_run_outcome`, `died_at_boss`, `weapon`, `map` etc.), `category`, `priority`, `play_once`, `cooldown_visits`, `chance` e `sets_flags`. Textos ficam em `data/localization/<idioma>/dialogue.json`.
- **Seleção:** filtra por requisitos, ordena por classe (essencial > específica > evergreen), "não vista primeiro", prioridade e sorteio; uma conversa por NPC por visita ao hub.
- **Persistência:** progresso (vistas, flags, contadores) no save.

Para o roteiro novo, o que **falta** (a decidir com o Pedro):
- Condições de **estágio da história**: um contador/flag de "ato" ou "capítulo" e condições para o fluxo de save novo (por exemplo `intro_done`, `tutorial_done`).
- **Vários NPCs**: hoje só Odalric; falta o elenco e uma forma de organizar conversas por NPC em arquivos separados.
- **Cutscenes** como conteúdo de dados (hoje o sistema cobre conversa de NPC, boss e barks, não cenas).
- **Gatilhos do spoiler**: flags para pistas e revelação do NPC.
- **Ferramenta de autoria**: validador (`tools/validate_dialogue.py` está previsto) e, mais adiante, um jeito de ver a "árvore" de quais falas liberam quais.

## 9. Vozes do sangue: diálogo ao pegar uma Herança

Ideia do Pedro: o castelo é tão antigo que todos os grandes vampiros passaram por ele, ou é a **origem de todos os vampiros**. Ele guarda a **memória em sangue** de cada um. Quando Virgil interage com uma Bênção/Herança no chão, aparece um diálogo com a **personalidade daquele sangue**.

### 9.1 As cinco famílias e a referência de cada uma

Stirps é um conceito de gameplay, não uma linhagem: o jogador pode achar uma herança que junta duas famílias. O Pedro quer revisar esse conceito no jogo depois; por ora vale a regra acima.

Os nomes das famílias vêm de `data/blessings/blood_blessings.json` (`families`). As referências abaixo são minha leitura dos nomes e dos temas; **[EM ABERTO] confirmar com o Pedro**.

| Família | Referência provável | Tema mecânico atual | Personalidade sugerida (rascunho) |
|---|---|---|---|
| Dracul (15 bênçãos) | Drácula / Vlad Dracul | Domínio, roubo de vida, poder permanente | Soberano, cortês e cruel; fala como quem governa |
| Báthory (15) | Elizabeth Báthory | Poças de sangue, regeneração, sacrifício | Vaidosa, sensual e cruel; obcecada por juventude e beleza |
| Orlok (14) | Conde Orlok (Nosferatu) | Pestilência, contágio, cadáveres | Sussurrante, faminto, ligado a peste e ratos |
| Karnstein (15) | Carmilla (Karnstein) | Encanto, aliados, mobilidade | Sedutora e possessiva; fala de desejo e devoção |
| Stirps (4) | **Não é família.** Conceito de herança híbrida de duas famílias | Despertada por combinação de famílias de bênçãos | Sem voz própria: falam as **duas famílias envolvidas**, uma com a outra |

Os nomes de bênçãos também carregam a mesma pista (`dracul_first_vampire_blood`, `bathory_bath_of_immortality`, `orlok_rats_blessing`, `karnstein_lovers_quarrel`), o que ajuda a escrever cada voz.

### 9.2 Como encaixa no sistema de diálogos

Proposta, não implementada:
- **Nova categoria** (por exemplo `blessing_pickup`) para o gatilho "interagiu com Bênção no chão" (`RewardPickup`). A fala é curta, ao estilo dos barks, para não travar o ritmo da run.
- **Sequência exata (decidido):** (1) Virgil interage com a Herança no chão; (2) abre o **painel de diálogo** entre Virgil e a personalidade do sangue; (3) ao concluir, o painel fecha; (4) abre o **painel de escolha das 3 bênçãos**; (5) o jogador escolhe; (6) toca a **fala de conclusão**, sempre, curta e sem interromper a gameplay.
- **Fala de conclusão (decidido):** toca **sempre**, é bem simples e dita pela personalidade da família da bênção escolhida (por exemplo "Ainda bem que me escolheu" ou um "entendido"). Vale uma ou duas variações por família na primeira versão.
- **Quando toca (decidido):** ao interagir com a Herança no chão, o diálogo toca **antes** de o jogador escolher a bênção. Quando ele conclui, o jogador escolhe. Depois da escolha toca uma **fala de conclusão** curta, que **não interrompe a gameplay** (estilo bark, legenda não modal).
- **Sem escolhas (decidido):** é só diálogo narrativo; o jogador assiste e não escolhe opções. Na prática, `choices` fica vazio nessas conversas.
- **Stirps:** a conversa tem **as duas famílias envolvidas** como falantes (e o **Virgil também participa**: são três falantes na mesma conversa, decidido), em diálogo entre si (por exemplo Dracul e Báthory, conforme a híbrida). Precisa saber quais duas famílias compõem cada Stirps (hoje o JSON só marca `family: Stirps`; **[EM ABERTO]** de onde vem o par, p. ex. um campo novo `families` na bênção).
- **Falante por família**: entradas novas em `speakers.json` (`dracul`, `bathory`, `orlok`, `karnstein`), usando a cor da própria família (`color` do JSON) no retrato e no nome.
- **Condições novas**: `blessing_family` (família da bênção pega) e `blessing_picked` (id específico), além das já existentes (`seen`, `flag`, `runs_started`, `map`).
- **Camadas de fala**, da mais geral à mais específica: (a) fala da família, (b) fala da bênção específica (essencial/específica), (c) fala de **primeira vez** com a família, que vira a apresentação daquele sangue.
- **Uso da narrativa profunda**: as vozes podem revelar aos poucos a história do castelo (por exemplo: quem foi cada vampiro, o que Armand fez com a memória deles), usando `seen` e `runs_started` como nas falas do Odalric.
- **Frequência e pool (decidido):** o diálogo toca **só na primeira vez** que o jogador interage com uma Herança daquela família **naquela run**: um diálogo por herança por run. Cada conversa tem um **pool** por instância (família, ou par de famílias nas Stirps). Quando uma conversa é lida, é marcada como **lida (done)** e **nunca mais se repete**; na próxima run entra a **próxima** do pool. Assim o pool demora a esgotar. Os pools são separados por **instância** (cada família e cada par de Stirps) e por **momento da história** (ver abaixo). Isso é parecido com o `play_once` e o "não vista primeiro" já existentes no sistema, só que com o limite de uma por família por run.
- **Pool esgotado (decidido):** silêncio. Sem diálogo antes da escolha; a fala de conclusão continua tocando.
- **Ordem do pool (sugestão a partir do Hades):** o pool é uma **fila**: se várias conversas estão liberadas, tocam na ordem em que foram escritas/liberadas, uma por run, em vez de sortear. **[EM ABERTO]** fila ou sorteio.
- **Momento da história (a definir):** cada pool é fatiado por estágio (por exemplo antes/depois da luta contra Armand no tutorial, bosses derrotados, número de runs), usando as mesmas condições do sistema atual (`boss_defeated`, `runs_started`, `seen`) e as novas de estágio. **[EM ABERTO]** quais estágios e quantas conversas por fatia.

### 9.3 Pontos em aberto

- **Stirps:** o par de famílias de cada híbrida e se cada par tem pool próprio (hoje são 4 bênçãos Stirps).

- Se as vozes são só texto ou também têm retrato e som.
- Se o Odalric comenta (por exemplo, no hub, sobre as vozes que Virgil ouviu).
- Ligação com o spoiler: o NPC do mal pode ter interesse nessas memórias (poder de tomar o lugar de Armand).
- Quantas falas por família para a primeira versão.

## 10. Atos, tom e finais por personagem

### 10.1 Tom (decidido)
História **sombria**, com uma pitada de **humor negro**: sarcasmo e ironia à vontade. Não seguimos o desfecho do Hades (a família reunida e os deuses ajudando): aqui ninguém é salvo de graça. A premissa é a de um personagem amaldiçoado que quer se livrar da maldição, e a jornada pode carregar **metáforas** disso (a maldição como dívida, fome, herança que não se escolhe).

### 10.2 Atos (decidido que o pool se separa por atos; o fim de cada ato está em aberto)
A [revisão de narrativa da GDC 2021](https://media.gdcvault.com/GDC+2021/Scott+Parker+GDC+Narrative+Review+2021.pdf) descreve o Hades em atos (1 Encontro, 2 Retorno, 3 Reencontro, depois a conclusão como epílogo). Usamos a ideia de **atos como fatias do pool de diálogos**, mas com conteúdo próprio.

Rascunho de atos (**sugestão, não decidido**), só para dar nome ao que as fatias significam:
1. **Cativeiro:** Virgil descobre que está preso e aprende o hub; o NPC se apresenta como aliado.
2. **Dívida:** os bosses caem, a memória dos vampiros no sangue começa a falar e as pistas do NPC se acumulam.
3. **Herdeiros:** a cobiça pelo trono aparece (NPC, aliados, Armand) e cada personagem mostra o que quer.
4. **Epílogo:** o final do personagem escolhido.

**[EM ABERTO]** O que encerra cada ato (boss derrotado, nº de runs, flag de história) e quantos atos há. Ordem de grandeza do Hades: 5 a 10 runs para abrir histórias vizinhas.

### 10.3 Finais por personagem
Leitura que fiz da sua mensagem: **4 finais, um para cada personagem jogável** (Virgil + 3), cada um com um objetivo diferente. Isso casa com a tabela de `personagens-futuros.md` (cura, destruir o castelo, fim da maldição, assumir o trono). Se a ideia era 4 finais **por personagem** (16 no total), avise: muda bastante o escopo.

Regras já definidas:
- Cada personagem interage de forma diferente com as Heranças, os NPCs, os bosses e Armand.
- O jogador precisa **escolher ativamente** qual final perseguir.
- Cada rota exige **várias runs consecutivas** para ser concluída.
- Ao concluir, a história **trava naquele personagem** e o final é definitivo para ele.

**[EM ABERTO]** Como o jogador escolhe a rota. Duas opções levantadas pelo Pedro: (a) o personagem que ele mais jogou; (b) uma side quest que, ao ser concluída, trava a história naquele personagem.

### 10.4 Minha opinião (sugestões para decidir)

1. **Escolha ativa, não "o mais jogado".** O mais jogado é invisível para o jogador e pode surpreender. Prefiro a opção (b), como um **juramento** feito no hub, com aviso claro de que é definitivo. Combina com o tom sombrio: o jogador assume o pacto.
2. **Travar não precisa ser punição.** Depois do final, o personagem fecha a história, mas o jogo segue jogável (runs livres, ou "novo ciclo" com outro personagem). Senão o jogador sente que "acabou" cedo.
3. **Camadas para não multiplicar o texto.** Escrever 4 histórias completas é caro. Sugiro: um **tronco comum** (atos 1 e 2, o mesmo para todos, com o spoiler do NPC) e **camadas por personagem** nos atos 3 e 4. O sistema de diálogos ganharia uma condição `character` para que a mesma situação (herança, boss, NPC) tenha falas diferentes por personagem, só onde importa.
4. **Cada final responde à mesma pergunta**: "o que fazer com uma maldição herdada?" (curar, destruir, acabar com tudo, assumir). Isso dá coesão temática entre rotas e liga com as vozes do sangue como memória.
5. **O NPC do spoiler cruza todos os finais.** A traição dele pode mudar de forma em cada rota (aliado, rival, vítima), o que dá rejogabilidade sem escrever mais um personagem.
6. **Escopo por etapas.** Primeiro a rota do Virgil completa (abertura, atos, final), usando os personagens futuros como esboço. As outras três entram depois, reaproveitando o tronco comum.

### 10.5 Perguntas para o Pedro
- 4 finais no total ou 4 por personagem?
- Juramento no hub (b) ou personagem mais jogado (a)?
- O jogo continua depois do final de um personagem?
- Quantas runs consecutivas por rota, e o que acontece se o jogador falha no meio?
- Os personagens se encontram e se atrapalham no mesmo hub (como em `personagens-futuros.md`), ou cada rota é isolada?

## 11. Implementação (não planejada)

Fica para depois da aprovação do roteiro. Pontos já identificados:
- detectar "save novo" (slot vazio) e encaminhar o `StartSlot` ao fluxo de abertura em vez de direto ao hub;
- persistir no save o progresso da abertura e o das revelações do NPC (mudança aditiva; ver os cuidados de versão do save no `perfil.md`), reaproveitando as flags do `DialogueProgress`;
- cenas de tutorial e sala do trono, e as duas cutscenes;
- textos novos em `data/localization/<idioma>/*.json`, nos três idiomas.

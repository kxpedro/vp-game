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

## 7. Implementação (não planejada)

Fica para depois da aprovação do roteiro. Pontos já identificados:
- detectar "save novo" (slot vazio) e encaminhar o `StartSlot` ao fluxo de abertura em vez de direto ao hub;
- persistir no save o progresso da abertura e o das revelações do NPC (mudança aditiva; ver os cuidados de versão do save no `perfil.md`);
- cenas de tutorial e sala do trono, e as duas cutscenes;
- textos novos em `data/localization/<idioma>/*.json`, nos três idiomas.

# Contexto atual do projeto

Snapshot técnico de Vampire's Prey em 2026-07-27. Este arquivo é o ponto de entrada para futuras sessões de desenvolvimento.

## Visão do jogo

Vampire's Prey é um roguelite de ação 3D isométrico. Virgil atravessa salas do castelo, combate criaturas, absorve sangue, escolhe Bênçãos de Sangue e acumula progressão permanente no Ninho do Corvo.

O objetivo atual é consolidar uma primeira versão jogável com combate responsivo, boa leitura visual, suporte consistente a controle e sistemas expansíveis por catálogo.

## Loop principal

1. Escolher idioma e slot no menu principal.
2. Preparar a run no Ninho do Corvo e, se desejado, testar o moveset na sala de treinamento.
3. Entrar na sala bônus inicial e escolher uma bênção aleatória na fonte central.
4. Sobreviver a quatro waves de 5 inimigos em cada sala comum.
5. Absorver o sangue da sala e escolher uma Bênção de Sangue.
6. Avançar somente após confirmar a recompensa.
7. Derrotar o boss, interagir com o Altar de Artefatos e escolher um artefato passivo.
8. Entre mapas, usar a Câmara de Hemomancia para curar ou aprimorar uma bênção mediante redução permanente da vida máxima da run.
9. Retornar ao hub após vitória final, morte ou desistência, preservando apenas a progressão permanente aplicável.

## Campanha e salas

- `CampaignCatalog` define 6, 8, 10 e 0 salas comuns nos quatro mapas.
- O quarto mapa é reservado ao confronto final.
- A sala bônus de bênção aparece apenas antes do primeiro mapa da run.
- A Câmara de Hemomancia aparece após bosses não finais.
- Salas comuns usam quatro regiões compactas de 9 × 9 células, com deslocamentos intermediários, rotação e espelhamento.
- A arena de boss usa 21 × 21 células e restringe destinos de teleporte/dash a posições navegáveis.
- Spawns respeitam margem das paredes e distância mínima de 2,35 unidades entre posições planejadas.
- Cada sala comum agenda quatro waves de 5 inimigos, totalizando 20.
- Ranged corresponde a aproximadamente 26,67% dos sorteios de arquétipo, redução de 20% sobre a distribuição anterior.
- O portão de saída permanece bloqueado durante o combate e até a escolha da recompensa.
- Os nomes das alas não são exibidos sobre os mapas gerados.

Detalhes: [`room_generation.md`](room_generation.md).

## Jogador, arma e combate

Scripts centrais:

- `scripts/player/PlayerController.cs`;
- `scripts/player/PlayerCombat.cs`;
- `scripts/player/PlayerAnimator.cs`;
- `scripts/player/PlayerSkillController.cs`;
- `scripts/player/BloodBlessingRuntime.cs`;
- `scripts/combat/Health.cs`;
- `scripts/systems/weapons/WeaponCatalog.cs`;
- `scripts/systems/weapons/WeaponDefinition.cs`.

Estado atual:

- base de 100 HP, com bônus legítimos aplicados posteriormente;
- HP absoluto preservado entre salas e mapas da mesma run;
- personagem usa `Knight_Male.gltf`, com idle, caminhada, corrida, esquiva e ataque;
- Espada Grande é a arma funcional; IDs antigos de Espada Grande e Espadas Duplas migram para `longsword`;
- o visual da arma é preso aos ossos das mãos, muda por evento ao equipar Espada Grande, Revolver Duplo ou Chicote e só aparece durante ataques;
- a velocidade dos clipes de ataque é sincronizada com startup + recovery; a estocada final usa uma animação distinta;
- dano, tempos, alcance, forma da hitbox, tolerância de borda, empurrão, stagger e cooldown especial pertencem à definição da arma;
- cada golpe possui perfil de buffer, cancelamentos, mobilidade, assistência de mira e redirecionamento;
- combo básico de três golpes reiniciado após 1 segundo sem sequência;
- terceiro golpe básico com 20% mais empurrão que os golpes comuns do combo;
- ataque rápido com buffer da combinação dash + ataque e sem herdar efeitos exclusivos do ataque básico;
- especial com impacto rápido e recarga de 1 segundo;
- velocidade de ataque afeta apenas o ataque básico;
- dano recebido aplica stagger ao jogador;
- inimigos comuns podem ser mantidos sob pressão de stagger, enquanto bosses e inimigos especiais protegidos possuem armadura de stagger;
- hit-stop, câmera, sangue e números de dano reforçam o contato dos golpes;
- Fúria Bestial aplica pós-processamento em preto e branco, preservando os vermelhos associados a sangue;
- Fúria só acumula e aparece na HUD quando uma bênção com `UnlockBestialFury` estiver equipada;
- Fúria cheia possui 100 pontos, consome 10 por segundo por padrão e explode com 15% do dano básico ao terminar;
- críticos usam amarelo-ouro e números três vezes maiores;
- barreira absorve dano antes da vida, espera 8 segundos, perde 0,25 ponto por segundo e é zerada ao trocar de sala.

## Controles e pausa

`project.godot` contém os bindings de gameplay e `scripts/ui/ControllerUiSupport.cs` completa a navegação de interface.

- teclado/mouse e controles genéricos seguem as ações do Godot;
- analógico esquerdo movimenta e orienta o ataque;
- `X` no DualSense / `A` no Xbox confirma, interage e executa o dash;
- `Círculo` / `B` volta ou fecha a camada de UI atual;
- d-pad e analógico navegam por botões, ofertas de bênção e listas;
- `Esc` sempre fecha a camada superior antes de abrir ou fechar a pausa;
- abrir a ficha do personagem com `C` pausa a árvore;
- projéteis usam processamento pausável e não continuam ativos durante menus;
- “Desistir da run” força o fluxo normal de morte e retorno ao Ninho do Corvo.

## Bênçãos de Sangue

O catálogo `data/blessings/blood_blessings.json` contém 63 definições ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps. O loader e o validador aceitam JSONC para preservar entradas comentadas fora do pool.

- ofertas usam Common, Uncommon, Rare, Epic ou Legendary; Stirps possui regra própria;
- valores numéricos escalam pela qualidade sorteada;
- raridade permanece fixa; níveis N1–N10 são aprimorados separadamente e acrescentam 25% por nível adicional;
- chances são limitadas a 100% no runtime e na interface;
- há ao menos uma bênção de cada família para ataque básico, rápido e especial;
- bênçãos diretamente ligadas aos três tipos de ataque têm peso elevado;
- bênçãos Stirps têm peso de família reduzido para 45% do peso das linhagens comuns;
- grupos exclusivos de ataque básico, ataque rápido, ataque especial e Fúria Bestial substituem a bênção equipada do mesmo grupo;
- efeitos de ataque básico não são automaticamente repassados ao ataque rápido;
- Dracul aplica roubo de vida;
- Bathory aplica Sangramento e cria sinergias com poças de sangue;
- Orlok aplica Pestilência e pode invocar ratos ao derrotar hospedeiros pestilentos;
- Karnstein aplica Encanto, fazendo inimigos atacarem aliados;
- a Stirps de Orlok + Bathory cria uma poça de sangue no raio da explosão pestilenta e aplica Sangramento aos inimigos dentro dela;
- poças próximas são mescladas e usam uma superfície orgânica irregular; explosões pestilentas acrescentam veios e esporos sem alterar a área lógica;
- Sanguis Purgatis conduz gotas em espiral até o jogador, com rastro de partículas e pulso final antes da oferta;
- cura excedente convertida em barreira, inclusive Reserva Imperial, passa pela API de overheal de `Health`;
- a trilha visual de roubo de vida valida a árvore antes de alterar transformações globais.

Sangramento e Pestilência possuem stacks visíveis; cada stack aumenta em 5% o dano recebido, e Sangramento também escala seu dano periódico. Encanto dura 5 segundos base, recebe multiplicadores externos, e Laço de Sangue desenha uma linha rosa entre alvos vinculados. A explosão de Briga de Amantes é rosa.

Ferida Recuperável espera 2 segundos sem novo dano e restaura gradualmente durante 5 segundos. A escala atual é 20%/40%/70%/110%/140% nas cinco raridades e permanece marcada para rebalanceamento.

A Bênção dos Ratos exige a morte de um inimigo sob Pestilência. Cada rato dura 8 segundos, usa um pequeno cilindro físico, separa-se dos demais, captura a velocidade de movimento e o intervalo de ataque do jogador e causa 50% do dano básico. A capacidade simultânea-base é Common 1, Uncommon 2, Rare 3, Epic 4 e Legendary 5, com mais um por nível acima de N1.

## Inimigos e bosses

- melee usa o modelo Ninja;
- ranged usa o modelo Demon;
- tank usa o modelo Orc;
- bosses usam Dragon Evolved, Ghost Skull, Orc Skull e Blue Demon;
- os modelos reproduzem idle, locomoção, ataque e reação a dano;
- inimigos aparecem mais separados e o empurrão dos ataques do jogador está ativo;
- ranged pode recuar ou buscar distância tática;
- projéteis ranged são flechas emissivas maiores e mais lentas, permanecendo até colisão com jogador ou cenário; o retângulo de hitbox antecipada foi removido;
- tank carrega o golpe por mais tempo e ataca com área e dano maiores;
- dano geral dos inimigos foi reduzido em 10% na etapa de aplicação do dano;
- contato corporal causa 5% do dano de ataque atual do inimigo;
- o comportamento visual incorreto que fazia o primeiro boss crescer e ficar vermelho foi removido;
- bosses e raridades altas mantêm resistências próprias de stagger e empurrão;
- bosses recebem apenas 5% do empurrão e do impacto contra parede;
- reposicionamentos do segundo boss são limitados à arena para evitar dashes fora do mapa.

## Recompensas, progressão e save

- três slots são gerenciados por `SaveGameManager`;
- atributos, moedas, skills, artefatos permanentes e loadouts são persistidos;
- estado temporário da run fica em `RunState`;
- Bênçãos de Sangue são temporárias;
- artefatos são exclusivamente passivos e são oferecidos em um altar interativo após bosses;
- árvores Bestialis e Humanitas usam ranks máximos 5/3/1, pré-requisitos e custos crescentes;
- passivas compradas na Fonte de Sangue aparecem na ficha do personagem com tooltip;
- a Fonte de Sangue usa layout responsivo, lista rolável e botão de fechamento fora das descrições.

## HUD e interface

A maior parte da HUD é criada por código em `scripts/ui/HUD.cs`.

- vida, fúria condicional, arma, skills, moedas e estado do boss;
- painéis e divisores discretos do Kenney UI Pack e cursores globais do Kenney Cursor Pack;
- progresso da sala, aviso de wave e log de combate; o minimapa e o atalho `Tab` foram removidos;
- dicas permanentes de botões no topo foram removidas;
- até sete bênçãos aparecem na faixa compacta; havendo mais, um `+` ocupa a linha inferior do último slot;
- bênçãos aprimoradas mostram `N2` a `N10` ao lado do ícone;
- a ficha do personagem pausa o jogo e abre uma coluna rolável com todas as bênçãos e descrições por hover/foco;
- habilidades passivas permanentes também aparecem na ficha;
- overlays recebem foco inicial para d-pad/analógico e respeitam confirmar/voltar;
- o HUD permanece disponível na Câmara de Hemomancia;
- a vida e a Fúria não aparecem no Ninho do Corvo;
- a barreira aparece como extensão azul da barra de vida;
- a Fonte de Sangue permanece rolável e responsiva;
- o Altar do Sacrifício sorteia até três bênçãos elegíveis e as apresenta no mesmo layout de cards da oferta de bênçãos.

Na Câmara de Hemomancia, o Altar do Sacrifício reduz em 5% a vida máxima total da run para elevar uma bênção em um nível sem mudar sua raridade. A redução é cumulativa, persiste entre salas e é removida quando a run termina.

## Build e validação

- solução: `VP0.1.sln`;
- projeto: `VP0.1.csproj`;
- build local: `dotnet build VP0.1.sln`;
- execução rápida: `jogar.bat`;
- export Windows: `exportar_windows.bat`;
- templates necessários: Godot `4.7.stable.mono`.

Validadores locais:

- `.codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py`;
- `.codex/skills/vp-create-artifact/scripts/validate_artifacts.py`.

Detalhes: [`build_and_export.md`](build_and_export.md).

## Arquivos para abrir primeiro

- `README.md`;
- `docs/gameplay_systems.md`;
- `docs/room_generation.md`;
- `scripts/systems/RunState.cs`;
- `scripts/systems/RunManager.cs`;
- `scripts/systems/RoomGenerator.cs`;
- `scripts/player/PlayerCombat.cs`;
- `scripts/player/BloodBlessingRuntime.cs`;
- `scripts/enemies/EnemyBasic.cs`;
- `scripts/ui/HUD.cs`.

## Pontos ainda em evolução

- conteúdo e identidade visual definitivos dos personagens e bosses;
- movesets de Revolver Duplo e Chicote;
- tradução integral das telas antigas para inglês e espanhol;
- balanceamento de raridades, waves, dano e economia;
- substituição dos placeholders geométricos e revisão de licenças antes da distribuição pública.

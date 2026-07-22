# Contexto atual do projeto

Snapshot técnico de Vampire's Prey em 2026-07-22. Este arquivo é o ponto de entrada para futuras sessões de desenvolvimento.

## Visão do jogo

Vampire's Prey é um roguelite de ação 3D isométrico. Virgil atravessa salas do castelo, combate criaturas, absorve sangue, escolhe Bênçãos de Sangue e acumula progressão permanente no Ninho do Corvo.

O objetivo atual é consolidar uma primeira versão jogável com combate responsivo, boa leitura visual, suporte consistente a controle e sistemas expansíveis por catálogo.

## Loop principal

1. Escolher idioma e slot no menu principal.
2. Preparar a run no Ninho do Corvo e, se desejado, testar o moveset na sala de treinamento.
3. Entrar na sala bônus inicial e escolher uma bênção aleatória na fonte central.
4. Sobreviver a três waves de 5 inimigos em cada sala comum.
5. Absorver o sangue da sala e escolher uma Bênção de Sangue.
6. Avançar somente após confirmar a recompensa.
7. Derrotar o boss e escolher um artefato passivo.
8. Entre mapas, usar a Câmara de Hemomancia para curar ou aprimorar uma bênção mediante sacrifício de vida.
9. Retornar ao hub após vitória final, morte ou desistência, preservando apenas a progressão permanente aplicável.

## Campanha e salas

- `CampaignCatalog` define 6, 8, 10 e 0 salas comuns nos quatro mapas.
- O quarto mapa é reservado ao confronto final.
- A sala bônus de bênção aparece apenas antes do primeiro mapa da run.
- A Câmara de Hemomancia aparece após bosses não finais.
- Salas comuns usam cinco famílias de layout, rotação, espelhamento e reparo de conectividade.
- Existe 35% de chance de uma sala lateral adicional.
- Spawns respeitam margem das paredes e distância mínima de 2,35 unidades entre posições planejadas.
- Cada sala comum agenda três waves de 5 inimigos, totalizando 15.
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
- personagem provisório representado por um cilindro azul;
- Espada Grande é a arma funcional; IDs antigos de Espada Grande e Espadas Duplas migram para `longsword`;
- dano, tempos, alcance, forma da hitbox, tolerância de borda, empurrão, stagger e cooldown especial pertencem à definição da arma;
- combo básico de três golpes reiniciado após 1 segundo sem sequência;
- terceiro golpe básico com 20% mais empurrão que os golpes comuns do combo;
- ataque rápido com buffer da combinação dash + ataque e sem herdar efeitos exclusivos do ataque básico;
- especial com impacto rápido e recarga de 1 segundo;
- velocidade de ataque afeta apenas o ataque básico;
- dano recebido aplica stagger ao jogador;
- inimigos comuns podem ser mantidos sob pressão de stagger, enquanto bosses e inimigos especiais protegidos possuem armadura de stagger;
- hit-stop, câmera, sangue e números de dano reforçam o contato dos golpes;
- Fúria Bestial aplica pós-processamento em preto e branco, preservando os vermelhos associados a sangue.

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

O catálogo `data/blessings/blood_blessings.json` contém 66 definições distribuídas entre Dracul, Bathory, Orlok, Karnstein e combinações Stirps.

- ofertas usam Common, Uncommon, Rare, Epic ou Legendary; Stirps possui regra própria;
- valores numéricos escalam pela qualidade sorteada;
- há ao menos uma bênção de cada família para ataque básico, rápido e especial;
- bênçãos diretamente ligadas aos três tipos de ataque têm peso elevado;
- bênçãos Stirps têm peso de família reduzido para 45% do peso das linhagens comuns;
- grupos exclusivos de ataque básico e ataque rápido substituem a bênção equipada do mesmo grupo;
- efeitos de ataque básico não são automaticamente repassados ao ataque rápido;
- Dracul aplica roubo de vida;
- Bathory aplica Sangramento e cria sinergias com poças de sangue;
- Orlok aplica Pestilência e pode invocar ratos ao derrotar hospedeiros pestilentos;
- Karnstein aplica Encanto, fazendo inimigos atacarem aliados;
- a Stirps de Orlok + Bathory cria uma poça de sangue no raio da explosão pestilenta e aplica Sangramento aos inimigos dentro dela;
- cura excedente convertida em barreira, inclusive Reserva Imperial, passa pela API de overheal de `Health`;
- a trilha visual de roubo de vida valida a árvore antes de alterar transformações globais.

A Bênção dos Ratos exige a morte de um inimigo sob Pestilência. Cada rato dura 8 segundos, usa um círculo verde-limão, captura a velocidade de movimento e o intervalo de ataque do jogador e causa 50% do dano básico. A capacidade simultânea é Common 1, Uncommon 2, Rare 3, Epic 4 e Legendary 5; aprimoramentos no altar atualizam essa capacidade.

## Inimigos e bosses

- melee usa cilindro ciano;
- ranged usa prisma triangular rosa-choque;
- tank usa cubo amarelo;
- os três arquétipos possuem brilho na cor correspondente e não recebem mais o vermelho genérico;
- inimigos aparecem mais separados e o empurrão dos ataques do jogador está ativo;
- ranged pode recuar ou buscar distância tática;
- projéteis ranged são flechas emissivas maiores, mais lentas e de alcance prolongado; o retângulo de hitbox antecipada foi removido;
- tank carrega o golpe por mais tempo e ataca com área e dano maiores;
- dano geral dos inimigos foi reduzido em 10% na etapa de aplicação do dano;
- o comportamento visual incorreto que fazia o primeiro boss crescer e ficar vermelho foi removido;
- bosses e raridades altas mantêm resistências próprias de stagger e empurrão.

## Recompensas, progressão e save

- três slots são gerenciados por `SaveGameManager`;
- atributos, moedas, skills, artefatos permanentes e loadouts são persistidos;
- estado temporário da run fica em `RunState`;
- Bênçãos de Sangue são temporárias;
- artefatos são exclusivamente passivos e voltaram a ser oferecidos após bosses;
- árvores Bestialis e Humanitas usam ranks máximos 5/3/1, pré-requisitos e custos crescentes;
- passivas compradas na Fonte de Sangue aparecem na ficha do personagem com tooltip;
- a Fonte de Sangue usa layout responsivo, lista rolável e botão de fechamento fora das descrições.

## HUD e interface

A maior parte da HUD é criada por código em `scripts/ui/HUD.cs`.

- vida, stamina, fúria, arma, skills, moedas e estado do boss;
- progresso da sala, aviso de wave, minimapa e log de combate;
- dicas permanentes de botões no topo foram removidas;
- até sete bênçãos aparecem na faixa compacta; havendo mais, um `+` ocupa a linha inferior do último slot;
- a ficha do personagem pausa o jogo e abre uma coluna rolável com todas as bênçãos e descrições por hover/foco;
- habilidades passivas permanentes também aparecem na ficha;
- overlays recebem foco inicial para d-pad/analógico e respeitam confirmar/voltar;
- o HUD permanece disponível na Câmara de Hemomancia;
- interfaces de Fonte de Sangue e Altar do Sacrifício são roláveis e responsivas.

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

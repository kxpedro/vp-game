# Contexto atual do projeto

Snapshot técnico de Vampire's Prey em 2026-07-14. Este arquivo é o ponto de entrada para futuras sessões de desenvolvimento.

## Visão do jogo

Vampire's Prey é um roguelite de ação 3D isométrico. Virgil atravessa salas do castelo, combate criaturas, absorve sangue, escolhe Bênçãos de Sangue e acumula progressão permanente no Ninho do Corvo.

O objetivo de curto prazo é consolidar uma primeira versão jogável com combate responsivo, runs compreensíveis e sistemas expansíveis por catálogo.

## Loop principal

1. Escolher idioma e slot no menu principal.
2. Preparar a run no Ninho do Corvo.
3. Entrar em uma sala procedural.
4. Sobreviver a três waves temporizadas.
5. Absorver o sangue da sala e escolher uma Bênção de Sangue.
6. Avançar somente após confirmar a recompensa.
7. Derrotar o boss do mapa e seguir mantendo a vida atual.
8. Retornar ao hub após vitória final ou morte, preservando apenas a progressão permanente aplicável.

## Campanha e salas

- `CampaignCatalog` define quatro mapas com 3, 4, 5 e 0 salas comuns.
- O quarto mapa é reservado ao confronto final.
- Salas comuns usam cinco famílias de layout, rotação, espelhamento e reparo de conectividade.
- Corredores procedurais extras foram reduzidos: há somente 35% de chance de uma sala lateral adicional.
- Spawns respeitam margem das paredes e usam células mais internas da sala.
- Cada sala comum agenda três waves de 8 inimigos, totalizando 24.
- A primeira wave é anunciada imediatamente; as seguintes usam intervalo total de 15 segundos.
- Cada wave exibe marcadores durante cinco segundos e instancia modelos ao longo de frames para evitar hitch de spawn.
- Waves novas podem entrar enquanto inimigos anteriores ainda estão vivos.

Detalhes: [`room_generation.md`](room_generation.md).

## Jogador e combate

Scripts centrais:

- `scripts/player/PlayerController.cs`
- `scripts/player/PlayerCombat.cs`
- `scripts/player/PlayerAnimator.cs`
- `scripts/player/PlayerSkillController.cs`
- `scripts/player/BloodBlessingRuntime.cs`
- `scripts/combat/Health.cs`

Estado atual:

- base de 100 HP, com bônus legítimos aplicados posteriormente;
- HP absoluto preservado entre salas e entre mapas da mesma run;
- movimentação isométrica, corrida padrão e caminhada com `Caps Lock`;
- esquiva sem custo de stamina, com cooldown;
- combo leve de três golpes, ataque pesado e ataque rápido após esquiva;
- impacto sincronizado com animação, hit-stop, stagger, câmera e números de dano;
- velocidade de animação acompanha modificadores de movimento, ataque e dash;
- longsword é o moveset funcional; outras armas estão registradas para expansão.

O asset e os nomes de animação de Virgil estão em [`character_asset.md`](character_asset.md).

## Bênçãos de Sangue

O catálogo fica em `data/blessings/blood_blessings.json` e contém 54 definições distribuídas entre Dracul, Bathory, Orlok e Karnstein.

- ofertas possuem Common, Uncommon, Rare, Epic ou Legendary;
- valores numéricos escalam pela qualidade sorteada;
- bênçãos que modificam o mesmo slot de ataque usam `exclusiveGroup` e substituem a equipada;
- a HUD mostra visualmente a substituição antes da escolha;
- cada família possui uma bênção de ataque básico com peso elevado;
- Dracul aplica roubo de vida;
- Bathory aplica Sangramento e cria sinergias com poças de sangue;
- Orlok aplica Pestilência, que se espalha para pelo menos três alvos quando o hospedeiro morre;
- Karnstein aplica Encanto, fazendo o inimigo atacar aliados ou patrulhar quando não existe alvo;
- Sangramento expira após dois segundos sem novo hit do jogador;
- Pestilência expira após três segundos sem novo hit;
- os estados possuem ícones sobre os inimigos e descrições destacadas na UI;
- roubo de vida possui trilha visual entre inimigo e jogador.

Arquivos centrais:

- `scripts/systems/blessings/BloodBlessingCatalog.cs`
- `scripts/systems/blessings/BloodBlessingDefinition.cs`
- `scripts/systems/RunState.cs`
- `scripts/player/BloodBlessingRuntime.cs`
- `scripts/enemies/EnemyBasic.cs`
- `scripts/ui/HUD.cs`

## Inimigos e bosses

- arquétipos melee, ranged e tank;
- raridades Normal, Magic, Rare e Legendary;
- raridade cresce com mapa e progresso da sala; Legendary não aparece no primeiro mapa;
- raridade aumenta vida, dano e recompensa;
- inimigos Legendary funcionam como mini-bosses, recebem nome e barra maior;
- barras aéreas comuns mostram apenas vida, raridade e efeitos, sem números ou nome de arquétipo;
- bosses não exibem barra sobre a cabeça: usam exclusivamente a barra superior do HUD;
- poções de vida não são mais dropadas por inimigos;
- modelos são pré-carregados e distribuídos por frames durante o spawn.

`BossController` e `BossCatalog` fornecem fases e movesets. Existem duas definições visuais/mecânicas distintas nesta etapa:

- Dragonkin Prateado: orçamento aproximado de 5.000 HP;
- Guardiã Invisível: 14.000 HP, mais rápida, maior e mais agressiva.

Os mapas posteriores ainda reutilizam a última definição disponível enquanto os bosses definitivos não forem cadastrados.

## Recompensas, progressão e save

- três slots são gerenciados por `SaveGameManager`;
- atributos, moedas, skills, artefatos permanentes e loadouts são persistidos;
- estado temporário da run fica em `RunState`;
- Bênçãos de Sangue são temporárias e exclusivas da run;
- artefatos podem ser passivos ou ativos em `Q`/`R`;
- skill trees Bestialis e Humanitas usam pré-requisitos e até quatro slots ativos;
- recompensas de sala mantêm o portal bloqueado até a escolha;
- a absorção `Sanguis Purgatus` segue o jogador e não bloqueia movimento;
- `Hereditas Sanguinis` apresenta a recompensa de Bênção de Sangue.

## HUD e interface

A maior parte da HUD é criada por código em `scripts/ui/HUD.cs`.

- vida, stamina, fúria, arma, skills e moedas;
- progresso da sala, aviso de wave e log de combate;
- minimapa com névoa e modo expandido em `Tab`;
- ficha do personagem com ícones e tooltips das bênçãos;
- interfaces de fonte de sangue, artefatos, compêndio e pausa;
- barra exclusiva para boss;
- números de dano recebido continuam registrados após a morte;
- tema centralizado em `UIThemeFactory`.

## Localização e configurações globais

- `Localization` é um autoload anterior a `GlobalSettings`;
- textos migrados usam `data/localization/game_texts.json`;
- idioma padrão: `pt-BR`;
- inglês (`en`) e espanhol (`es`) já possuem estrutura e textos iniciais;
- o idioma é escolhido no menu e salvo globalmente;
- resolução, fullscreen, idioma e volumes ficam em `user://global_settings.json`;
- os três saves de progressão são independentes dessas configurações.

A migração de textos ainda deve continuar sempre que uma UI antiga for alterada: textos novos voltados ao jogador não devem ser inseridos diretamente no código.

## Áudio

`AudioManager` é persistente e troca música pelo contexto: menu principal, Ninho do Corvo, salas comuns, bosses e game over. Efeitos próprios acompanham recompensa, morte e vitória contra boss.

## Build e exportação

- solução obrigatória: `VP0.1.sln`;
- projeto: `VP0.1.csproj`;
- build local: `dotnet build VP0.1.sln`;
- execução rápida: `jogar.bat`;
- export Windows: `exportar_windows.bat`;
- templates necessários: Godot `4.7.stable.mono`;
- o export self-contained gera o `.exe` e `data_VP0.1_windows_x86_64`, que devem ser distribuídos juntos;
- o script restaura runtimes `win-x64` e rejeita logs que contenham `ERROR`.

Detalhes: [`build_and_export.md`](build_and_export.md).

## Skills locais do projeto

Existem três skills versionadas em `.codex/skills/`:

- `vp-create-blood-blessing`;
- `vp-create-artifact`;
- `vp-create-player-skill`.

As duas primeiras incluem validadores Python. Elas documentam o fluxo completo entre catálogo, runtime e HUD e devem ser usadas ao expandir esses sistemas.

## Arquivos para abrir primeiro

- `README.md`
- `docs/initialization.md`
- `docs/room_generation.md`
- `docs/build_and_export.md`
- `scripts/systems/RunState.cs`
- `scripts/systems/RoomGenerator.cs`
- `scripts/player/PlayerCombat.cs`
- `scripts/player/BloodBlessingRuntime.cs`
- `scripts/enemies/EnemyBasic.cs`
- `scripts/ui/HUD.cs`

## Pontos ainda em evolução

- conteúdo e identidade visual definitivos dos bosses 3 e 4;
- tradução integral de telas antigas para inglês e espanhol;
- implementação de efeitos conceituais de catálogos que ainda não têm consumidor;
- balanceamento de raridades, waves, dano e economia;
- substituição de placeholders e revisão de licenças dos assets antes de distribuição pública.

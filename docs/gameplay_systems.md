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

Na interface, d-pad e analógico navegam, `X`/`A` confirma e `Círculo`/`B` volta. `Esc` fecha a camada superior antes de alternar a pausa. Esse fluxo vale para menu principal, pausa, ficha, ofertas de bênção, Fonte de Sangue, baú, altar e compêndio.

## Pausa e segurança de estado

- abrir a ficha com `C` pausa a árvore do jogo;
- projéteis inimigos têm processamento pausável e não avançam durante menus;
- overlays que recebem input pausado usam `ProcessModeEnum.Always`;
- fechar um overlay restaura o estado correto da pausa;
- “Desistir da run” força a morte e reutiliza o fluxo normal de retorno ao Ninho do Corvo.

## Espada Grande

`WeaponDefinition` concentra os dados da arma: ataques do combo, dano, delay de impacto, recovery, stagger, alcance, largura, forma, tolerância da hitbox, empurrão, multiplicadores posicionais, lunge e recarga especial.

A arma funcional preserva o ID interno `longsword`, mas é apresentada como **Espada Grande**. IDs antigos `greatsword` e `dual_blades` migram para ela. A antiga Espada Grande placeholder e Espadas Duplas foram removidas; Revolver Duplo e Chicote continuam reservados para expansão.

### Ataque básico

- combo de três golpes, reiniciado após 1 segundo sem sequência;
- bônus de velocidade de ataque altera somente hit delay e recovery deste ataque;
- terceiro golpe com empurrão 20% maior que o padrão do combo;
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
- acumula até 100 pontos e consome 10 pontos por segundo por padrão, resultando em 10 segundos de duração-base;
- `FuryDuration` e efeitos permanentes de duração continuam alterando a taxa efetiva de consumo;
- ao chegar a zero, causa em área 15% do dano básico atual;
- bênçãos diretas de Fúria usam `bestial_fury_mode` e substituem a equipada anteriormente;
- o pós-processamento dessatura o mundo e preserva vermelhos associados a sangue.

Hits criam partículas de sangue; flash, hit-stop, números, câmera e formas de alcance foram alinhados à hitbox efetiva. Críticos usam amarelo-ouro e fonte três vezes maior que o número comum.

## Inimigos

Os inimigos básicos não usam mais modelos importados. A representação geométrica atual prioriza leitura:

- melee: cilindro ciano;
- ranged: prisma triangular rosa-choque;
- tank: cubo amarelo;
- halo e emissão repetem a cor do arquétipo;
- o antigo tingimento vermelho genérico foi removido.

O dano aplicado pelos inimigos recebeu redução de 10%. Spawns usam 5 unidades por wave e procuram preservar distância mínima entre posições.

Encostar no corpo de um inimigo causa, a cada cooldown de contato, 5% do dano de ataque atual daquele inimigo. O boneco de treinamento mantém esse dano desabilitado.

### Ranged

- participação reduzida em 20% sobre a distribuição anterior;
- pode recuar e tenta recuperar a distância preferencial;
- dispara flechas emissivas maiores, mais lentas e de curso prolongado;
- projéteis pausam junto do jogo;
- a caixa que antecipava a hitbox foi removida, mantendo apenas o aviso de preparação.

### Tank

- mais vida e resistência a empurrão;
- windup maior, aviso `!!`, hitbox ampliada e dano superior.

### Bosses

- reposicionamentos de `kinetic_shift` e `mirror_dash` são projetados para uma célula navegável com margem de duas células das paredes;
- investidas continuam usando colisão física e telegraph linear;
- após a morte, surge um Altar de Artefatos na arena;
- a oferta só abre quando o jogador entra na área e interage; o portão permanece bloqueado até a escolha.

## Bênçãos de Sangue

Existem 63 definições ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps. `giant_slayer` e `bathory_scarlet_steps` permanecem preservadas como comentários JSONC; `orlok_contagious_demise` não está no pool atual. Dracul, Bathory, Orlok e Karnstein possuem opções diretamente ligadas a ataque básico, rápido e especial. Essas bênçãos têm peso superior; a seleção da família Stirps usa peso 0,45 contra 1,0 das linhagens comuns.

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

### Bênção dos Ratos

- somente a morte de inimigos sob Pestilência dispara a invocação;
- cada rato dura 8 segundos e usa um pequeno círculo verde-limão;
- movimento e intervalo de ataque são capturados do jogador ao invocar;
- dano é 50% do ataque básico do jogador;
- capacidade simultânea: Common 1, Uncommon 2, Rare 3, Epic 4 e Legendary 5;
- aprimorar a raridade no altar atualiza a capacidade imediatamente.

### Correções de runtime

- cura excedente convertida em barreira usa o overheal reportado por `Health`, corrigindo Reserva Imperial e equivalentes;
- a barreira perde 1 ponto por segundo, é zerada na troca de sala e aparece como segmento azul junto da vida;
- a trilha de roubo de vida entra na árvore antes de receber `GlobalPosition`, evitando `!is_inside_tree()`;
- artefatos voltaram a ser oferecidos após bosses por meio do altar interativo.

`bathory_immortal_beauty` converte dano em Ferida Recuperável após 2 segundos sem novo golpe e restaura durante 5 segundos. Pela escala atual, recupera 20%/40%/70%/110%/140% em Common/Uncommon/Rare/Epic/Legendary; Epic e Legendary ainda são pontos explícitos de rebalanceamento.

## HUD e ficha

- HUD redistribuída para reduzir sobreposição; dicas fixas do topo removidas;
- faixa compacta com até sete bênçãos e um `+` abaixo do último slot quando houver mais;
- ficha em `C` com coluna rolável para todas as bênçãos e descrições por hover/foco;
- passivas permanentes da Fonte de Sangue visíveis com descrição;
- ficha pausa o jogo;
- Fonte de Sangue usa scroll e layout responsivo, sem botão fechar sobre o texto;
- HUD permanece disponível na Câmara de Hemomancia;
- vida e Fúria ficam ocultas no Ninho do Corvo;
- barreira é representada em azul como extensão do total de vida, sem label separado.

## Salas especiais

### Sala bônus inicial

`RunStartBlessingRoom.tscn` aparece antes do primeiro mapa. A fonte central abre uma oferta obrigatória aleatória e o portão libera após a escolha. Recarregar a cena não concede outra bênção se a run já possui uma.

### Sala de treinamento

O Ninho do Corvo possui área de treinamento e boneco central. `TrainingDummy` recebe dano, stagger, estados e empurrão, mas restaura a vida para testes contínuos.

### Câmara de Hemomancia

`IntermissionChamber.tscn` aparece depois de bosses não finais.

- Fonte: recupera 35% da vida máxima, uma vez por passagem;
- Altar: reduz em 5% a vida máxima total da run e eleva uma bênção em uma raridade;
- a redução persiste entre salas e é cumulativa (`100 → 95 → 90,25`), sendo removida ao terminar a run;
- lista rolável mostra qualidade atual, próxima qualidade e comparação dos efeitos;
- cada serviço pode ser usado uma vez;
- HUD e ficha continuam disponíveis.

## Arquivos centrais

- `project.godot`;
- `scripts/ui/ControllerUiSupport.cs`;
- `scripts/ui/HUD.cs`;
- `scripts/player/PlayerCombat.cs`;
- `scripts/player/PlayerController.cs`;
- `scripts/player/BloodBlessingRuntime.cs`;
- `scripts/enemies/EnemyBasic.cs`;
- `scripts/enemies/EnemyProjectile.cs`;
- `scripts/player/SummonedRat.cs`;
- `scripts/systems/weapons/WeaponDefinition.cs`;
- `scripts/systems/weapons/WeaponCatalog.cs`;
- `scripts/systems/RunState.cs`;
- `scripts/systems/RunManager.cs`;
- `scripts/systems/RoomGenerator.cs`;
- `scripts/systems/RunStartBlessingRoom.cs`;
- `scripts/systems/IntermissionChamber.cs`;
- `scripts/systems/TrainingDummy.cs`;
- `data/blessings/blood_blessings.json`.

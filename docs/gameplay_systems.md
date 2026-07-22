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
- recebe apenas os efeitos de bênção destinados ao especial.

### Stagger, empurrão e impacto

- o jogador recebe stagger ao sofrer dano;
- ataques do jogador voltaram a aplicar empurrão;
- inimigos comuns podem ser interrompidos repetidamente quando pressionados contra paredes;
- bosses e inimigos especiais protegidos possuem armadura de stagger;
- tanks mantêm resistência adicional ao empurrão;
- colisões reais contra paredes e objetos sólidos podem gerar dano de impacto.

## Fúria Bestial e feedback de hit

Quando ativa, a Fúria Bestial aplica pós-processamento dessaturado ao mundo. Vermelhos associados a sangue permanecem visíveis. Hits criam partículas de sangue; flash, hit-stop, números de dano, câmera e formas de alcance foram ajustados em conjunto para aproximar o feedback visual da hitbox efetiva.

## Inimigos

Os inimigos básicos não usam mais modelos importados. A representação geométrica atual prioriza leitura:

- melee: cilindro ciano;
- ranged: prisma triangular rosa-choque;
- tank: cubo amarelo;
- halo e emissão repetem a cor do arquétipo;
- o antigo tingimento vermelho genérico foi removido.

O dano aplicado pelos inimigos recebeu redução de 10%. Spawns usam 5 unidades por wave e procuram preservar distância mínima entre posições.

### Ranged

- participação reduzida em 20% sobre a distribuição anterior;
- pode recuar e tenta recuperar a distância preferencial;
- dispara flechas emissivas maiores, mais lentas e de curso prolongado;
- projéteis pausam junto do jogo;
- a caixa que antecipava a hitbox foi removida, mantendo apenas o aviso de preparação.

### Tank

- mais vida e resistência a empurrão;
- windup maior, aviso `!!`, hitbox ampliada e dano superior.

## Bênçãos de Sangue

Existem 66 definições no catálogo atual. Dracul, Bathory, Orlok e Karnstein possuem ao menos uma bênção diretamente ligada a cada tipo de ataque: básico, rápido e especial. Essas bênçãos têm peso superior; a seleção da família Stirps usa peso 0,45 contra 1,0 das linhagens comuns.

### Exclusividade por ataque

- bênçãos diretas do ataque básico compartilham seu grupo exclusivo;
- bênçãos diretas do rápido compartilham `quick_attack_status`;
- uma nova escolha do mesmo grupo substitui a equipada;
- oferta e HUD informam qual bênção será trocada;
- efeitos sem exclusividade continuam acumuláveis.

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
- a trilha de roubo de vida entra na árvore antes de receber `GlobalPosition`, evitando `!is_inside_tree()`;
- artefatos voltaram a ser oferecidos após bosses.

## HUD e ficha

- HUD redistribuída para reduzir sobreposição; dicas fixas do topo removidas;
- faixa compacta com até sete bênçãos e um `+` abaixo do último slot quando houver mais;
- ficha em `C` com coluna rolável para todas as bênçãos e descrições por hover/foco;
- passivas permanentes da Fonte de Sangue visíveis com descrição;
- ficha pausa o jogo;
- Fonte de Sangue usa scroll e layout responsivo, sem botão fechar sobre o texto;
- HUD permanece disponível na Câmara de Hemomancia.

## Salas especiais

### Sala bônus inicial

`RunStartBlessingRoom.tscn` aparece antes do primeiro mapa. A fonte central abre uma oferta obrigatória aleatória e o portão libera após a escolha. Recarregar a cena não concede outra bênção se a run já possui uma.

### Sala de treinamento

O Ninho do Corvo possui área de treinamento e boneco central. `TrainingDummy` recebe dano, stagger, estados e empurrão, mas restaura a vida para testes contínuos.

### Câmara de Hemomancia

`IntermissionChamber.tscn` aparece depois de bosses não finais.

- Fonte: recupera 35% da vida máxima, uma vez por passagem;
- Altar: consome 20% da vida máxima, preservando ao menos 1 HP, e eleva uma bênção em uma raridade;
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

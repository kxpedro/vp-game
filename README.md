# Vampire's Prey

Vampire's Prey é um roguelite de ação 3D com câmera isométrica, ambientação dark fantasy e progressão entre partidas. O jogador controla Virgil, um caçador transformado em vampiro, atravessa salas procedurais, enfrenta criaturas e combina poderes das linhagens Dracul, Bathory, Orlok e Karnstein para escapar da maldição de Armand.

> Projeto em desenvolvimento. A versão atual prioriza legibilidade, responsividade do combate e validação dos sistemas; parte dos visuais ainda usa geometria provisória.

## Estado atual

- suporte a teclado/mouse e controles compatíveis com o mapeamento padrão do Godot, incluindo DualSense e controles Xbox;
- navegação completa de menus por direcional ou analógico, confirmação por `X`/`A` e retorno por `Círculo`/`B`;
- Espada Grande como arma funcional e visível nas mãos do personagem, com combo básico de três golpes, ataque rápido, ataque especial e Fúria Bestial;
- controles de ataque definidos por golpe: buffer, cancelamentos, recuperação, mobilidade, assistência de mira e redirecionamento no impacto;
- atributos de combate definidos pela arma equipada: dano, tempos, alcance, hitbox, empurrão, stagger e recargas;
- 63 Bênçãos de Sangue ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps;
- no mínimo quatro bênçãos diretas para cada tipo de ataque — básico, rápido e especial — com peso maior nas ofertas;
- quatro waves de 5 inimigos por sala, com intervalo reduzido, spawns espaçados e arquétipos melee, ranged e tank;
- campanha com 6, 8 e 10 salas comuns nos três primeiros mapas, além do confronto final;
- primeira sala bônus da run, Câmara de Hemomancia entre mapas e sala de treinamento no Ninho do Corvo;
- recompensas de Bênção de Sangue após salas comuns e Altar de Artefatos interativo após bosses;
- HUD responsiva sem minimapa, com ficha, bênçãos, níveis de aprimoramento, passivas, pausa, compêndio e barreira azul integrada à vida;
- personagem, inimigos, bosses, armas e partes dos cenários usam assets 3D externos com origem e licença registradas;
- áudio de combate diferencia cortes, estocada, rodopio, batida no chão, impacto corporal, ataque pelas costas, parede e esquiva;
- três slots de save, progressão permanente Bestialis/Humanitas e configurações globais;
- localização centralizada com `pt-BR` como idioma padrão;
- exportação Windows self-contained para Godot .NET.

As mudanças de combate, interface, inimigos, bênçãos e fluxo de campanha estão detalhadas em [`docs/gameplay_systems.md`](docs/gameplay_systems.md).

## Tecnologias

- Godot Engine 4.7 Stable Mono;
- .NET 8;
- C#;
- Jolt Physics;
- catálogos JSON para habilidades, bênçãos, artefatos e localização.

## Como executar

1. Instale o Godot 4.7 com suporte a .NET e o SDK do .NET 8.
2. Clone o repositório:

   ```bash
   git clone https://github.com/kxpedro/vp-game.git
   cd vp-game
   ```

3. Abra `project.godot` no editor e aguarde a importação dos assets.
4. Execute o projeto com `F6`/`F5`.

No Windows, `jogar.bat` inicia o projeto pelo Godot sem abrir o editor.

### Validação

```bash
dotnet build VP0.1.sln
py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py
py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py
godot --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn
```

## Controles

| Ação | Teclado e mouse | DualSense | Xbox |
| --- | --- | --- | --- |
| Movimentar e mirar | `W`, `A`, `S`, `D` e direção do cursor | Analógico esquerdo | Analógico esquerdo |
| Caminhar | `Caps Lock` | — | — |
| Esquivar / dash | `Espaço` | `X` | `A` |
| Ataque básico | Botão esquerdo | `Quadrado` | `X` |
| Ataque rápido | Dash + ataque básico | `X` + `Quadrado` | `A` + `X` |
| Ataque especial | `Q` | `Triângulo` | `Y` |
| Fúria Bestial | `F` | `R1` | `RB` |
| Interagir / confirmar | `E` / `Enter` | `X` | `A` |
| Voltar / fechar camada atual | `Esc` | `Círculo` | `B` |
| Ficha do personagem | `C` | `Criar` | `View` |
| Pausar | `Esc` | `Opções` | `Menu` |

O ataque no controle é orientado pelo analógico esquerdo. Ao abrir a ficha com `C`, o jogo pausa; `Esc`, `Círculo` ou `B` fecham primeiro a camada de interface que estiver no topo. O menu de pausa também permite desistir da run, aplicando o mesmo fluxo de morte e retorno ao Ninho do Corvo.

## Fluxo da campanha

1. Escolher idioma e um dos três slots no menu principal.
2. Preparar a run no Ninho do Corvo, onde ficam a Fonte de Sangue, o Baú de Artefatos, a bancada de armas e a sala de treinamento.
3. Na primeira run room, interagir com a fonte central e escolher a bênção inicial obrigatória. Essa sala aparece apenas antes do primeiro mapa.
4. Vencer as salas comuns e escolher uma Bênção de Sangue antes de atravessar o portão de saída.
5. Derrotar o boss, interagir com o Altar de Artefatos na arena e escolher uma recompensa passiva.
6. Visitar a Câmara de Hemomancia para recuperar até 35% da vida e, opcionalmente, reduzir a vida máxima total da run em 5% para elevar em um nível uma bênção, preservando sua raridade.
7. Avançar pelo segundo mapa com 8 salas e pelo terceiro com 10; o quarto mapa contém o confronto final.

A vida atual é preservada entre salas e mapas. Ao morrer ou desistir, a run temporária termina e a progressão permanente do slot é mantida.

## Combate e progressão

A Espada Grande é a arma jogável atual. Saves antigos que referenciam `greatsword` ou `dual_blades` são migrados para o identificador funcional `longsword`. Revolver Duplo e Chicote permanecem como entradas de expansão sem moveset definitivo.

O combo básico reinicia após 1 segundo sem continuação, e o terceiro golpe empurra 20% além do empurrão padrão do combo. Cada golpe possui perfil próprio de buffer, janela de cancelamento, recuperação, mobilidade e assistência de mira; a estocada final libera o jogador mais cedo. O ataque rápido aceita buffer da combinação dash + ataque. O ataque especial sai rapidamente, mas possui 1 segundo de recarga. Modificadores de velocidade de ataque afetam somente o ataque básico, sem acelerar dash, ataque rápido, especial ou Fúria Bestial.

Com 100 pontos acumulados, a Fúria Bestial consome 10 pontos por segundo e dura 10 segundos antes de causar uma explosão final de 15% do dano básico em área. Efeitos externos podem alterar essa duração. A barreira absorve dano antes da vida, aguarda 8 segundos após obtenção ou consumo, perde 0,25 ponto por segundo e é zerada ao trocar de sala.

As habilidades permanentes ficam em `data/skills/skills.json` e são compradas na Fonte de Sangue. As Bênçãos de Sangue ficam em `data/blessings/blood_blessings.json` e duram apenas durante a run. Artefatos em `data/artifacts/artifacts.json` são exclusivamente passivos.

Raridade e nível de bênção são progressões independentes. A raridade sorteada determina a base; toda bênção começa no nível 1 e pode chegar ao nível 10. Cada nível adicional aumenta em 25% os valores escaláveis daquela raridade. Chances são limitadas a 100% tanto na lógica quanto na interface.

## Estrutura do projeto

```text
.codex/      skills locais e validadores dos catálogos
3d/          modelos e rigs 3D mantidos no projeto
assets/      pacotes externos com licença e origem registradas
audio/       músicas, ambientes e efeitos sonoros
data/        skills, bênçãos, artefatos e localização
docs/        documentação técnica
images/      interface, ícones e efeitos
references/  referências conceituais e artísticas
scenes/      cenas do Godot
scripts/     código C# de gameplay e sistemas
```

Cenas principais:

- `scenes/ui/MainMenu.tscn`;
- `scenes/game/CrowsNest.tscn`;
- `scenes/game/RunStartBlessingRoom.tscn`;
- `scenes/game/TestArena.tscn`;
- `scenes/game/IntermissionChamber.tscn`;
- `scenes/player/Player.tscn`.

## Exportação Windows

Instale pelo editor os templates correspondentes a `4.7.stable.mono` e execute:

```bat
exportar_windows.bat
```

A distribuição deve manter juntos:

```text
builds/VampiresPrey.exe
builds/data_VP0.1_windows_x86_64/
```

Mais detalhes em [`docs/build_and_export.md`](docs/build_and_export.md).

## Documentação

- [`docs/initialization.md`](docs/initialization.md): snapshot técnico para novas sessões;
- [`docs/gameplay_systems.md`](docs/gameplay_systems.md): controles, combate, HUD, inimigos, bênçãos e salas especiais;
- [`docs/room_generation.md`](docs/room_generation.md): campanha, geração procedural e waves;
- [`docs/character_asset.md`](docs/character_asset.md): estado do visual provisório e catálogo do asset arquivado;
- [`docs/build_and_export.md`](docs/build_and_export.md): build e exportação;
- [`CREDITS.md`](CREDITS.md): autoria e licenças dos assets externos;
- [`GDD.txt`](GDD.txt): documento de design original e adendo de implementação atual.

## Autoria

Projeto e conceito de Pedro Henrique Rech Sarmento.

# Vampire's Prey

Vampire's Prey é um roguelite de ação 3D com câmera isométrica, ambientação dark fantasy e progressão entre partidas. O jogador controla Virgil, um caçador de monstros transformado em vampiro, que precisa atravessar as salas do castelo, derrotar criaturas e reunir recursos para escapar da maldição de Armand.

> Projeto em desenvolvimento. A versão atual é um protótipo jogável e pode conter recursos incompletos ou sujeitos a alterações.

## Funcionalidades atuais

- movimentação isométrica, corrida, esquiva e câmera com zoom;
- combate com ataques leves e pesados;
- Bênçãos de Sangue oferecidas em três opções após cada sala comum;
- ofertas de bênçãos por família, sem repetir bênçãos já escolhidas;
- cinco qualidades possíveis para cada bênção, com poder escalado pelo JSON;
- artefatos raros e poderosos obtidos exclusivamente ao derrotar bosses;
- campanha configurável com quatro mapas, salas procedurais e chefes;
- inimigos com patrulha, detecção, perseguição e ataque;
- inimigos corpo a corpo, de longo alcance e tanks, com raridades e modificadores;
- moedas de sangue persistentes entre salas;
- árvores de habilidades Bestialis e Humanitas;
- artefatos temporários por partida e coleção permanente;
- bancada de armas expansível e arma equipada visível na HUD;
- HUD, pausa, ficha do personagem e painéis de progressão;
- menu de opções com resolução, tela cheia e volumes separados;
- minimapa com névoa de guerra, mapa expandido, FPS e feedback visual de dano;
- painel de eventos no canto inferior direito e indicadores de spawn inimigo;
- três waves de 10 inimigos por sala comum;
- ajustes de gameplay em tempo de execução.

## Tecnologias

- [Godot Engine](https://godotengine.org/) 4.7 com suporte a C#;
- .NET 8;
- física 3D com Jolt Physics.

## Como executar

1. Instale o Godot 4.7 na edição com suporte a .NET e o SDK do .NET 8.
2. Clone o repositório:

   ```bash
   git clone https://github.com/kxpedro/vp-game.git
   cd vp-game
   ```

3. Abra o arquivo `project.godot` no editor.
4. Aguarde a importação dos assets e a compilação dos scripts C#.
5. Execute o projeto com `F6`/`F5` pelo editor.

Para verificar os scripts pelo terminal:

```bash
dotnet build VP0.1.csproj
```

No Windows, `jogar.bat` inicia o jogo diretamente usando a instalação local do Godot,
sem abrir o editor. `exportar_windows.bat` gera `builds/VampiresPrey.exe` quando os
templates de exportação do Godot 4.7 estiverem instalados.

## Controles

| Ação | Tecla/botão |
| --- | --- |
| Movimentar | `W`, `A`, `S`, `D` |
| Correr | `Shift` |
| Esquivar | `Espaço` |
| Ataque leve | Botão esquerdo do mouse |
| Ataque pesado | Botão direito do mouse |
| Habilidades | `1`, `2`, `3`, `4` |
| Artefatos ativos | `Q`, `R` |
| Interagir | `E` |
| Ficha do personagem | `C` |
| Pausar | `Esc` |
| Expandir minimapa | `Tab` |

## Estrutura do projeto

```text
3d/          modelos 3D
audio/       músicas, ambientes e efeitos sonoros
data/        dados de artefatos e progressão
docs/        documentação técnica do protótipo
images/      elementos visuais e interface
references/  referências conceituais e artísticas
scenes/      cenas do Godot
scripts/     código C# de gameplay e sistemas
```

As principais cenas são `scenes/game/CrowsNest.tscn`, `scenes/game/TestArena.tscn` e `scenes/player/Player.tscn`. Mais detalhes sobre o estado do protótipo estão em [`docs/initialization.md`](docs/initialization.md), e o documento de design está em [`GDD.txt`](GDD.txt).

## Estado do desenvolvimento

O fluxo começa no menu inicial e permite escolher entre três slots de progressão. No Ninho do Corvo, a arma da run é escolhida. A campanha segue por três mapas procedurais com 3, 25 e 35 salas comuns, cada um encerrado por um chefe, e termina em um quarto mapa dedicado ao chefe final. Ao morrer, o jogador retorna ao Ninho mantendo as moedas de sangue adquiridas e perdendo os recursos temporários da run. Mapas, contagens, chefes e armas são definidos em catálogos expansíveis.

## Autoria

Projeto e conceito de Pedro Henrique Rech Sarmento.

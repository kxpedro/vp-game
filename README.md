# Vampire's Prey

Vampire's Prey é um roguelite de ação 3D com câmera isométrica, ambientação dark fantasy e progressão entre partidas. O jogador controla Virgil, um caçador de monstros transformado em vampiro, que precisa atravessar as salas do castelo, derrotar criaturas e reunir recursos para escapar da maldição de Armand.

> Projeto em desenvolvimento. A versão atual é um protótipo jogável e pode conter recursos incompletos ou sujeitos a alterações.

## Funcionalidades atuais

- movimentação isométrica, corrida, esquiva e câmera com zoom;
- combate com ataques leves e pesados;
- geração procedural de salas e progressão até o chefe;
- inimigos com patrulha, detecção, perseguição e ataque;
- moedas de sangue persistentes entre salas;
- árvores de habilidades Bestialis e Humanitas;
- artefatos temporários por partida e coleção permanente;
- HUD, pausa, ficha do personagem e painéis de progressão;
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

## Controles

| Ação | Tecla/botão |
| --- | --- |
| Movimentar | `W`, `A`, `S`, `D` |
| Correr | `Shift` |
| Esquivar | `Espaço` |
| Ataque leve | Botão esquerdo do mouse |
| Habilidades | `1`, `2`, `3`, `4` |
| Artefatos ativos | `Q`, `R` |
| Interagir | `E` |
| Ficha do personagem | `C` |
| Pausar | `Esc` |
| Zoom | Roda do mouse |

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

O fluxo atual começa no Ninho do Corvo, segue por cinco salas comuns e termina na sexta sala com um chefe. Ao concluir a partida, os artefatos temporários podem ser incorporados à progressão permanente. O foco atual é aprimorar sincronização de animações, movimentação, combate e balanceamento.

## Autoria

Projeto e conceito de Pedro Henrique Rech Sarmento.

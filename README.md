# Vampire's Prey

Vampire's Prey é um roguelite de ação 3D com câmera isométrica, ambientação dark fantasy e progressão entre partidas. O jogador controla Virgil, um caçador transformado em vampiro, atravessa salas procedurais, enfrenta criaturas e escolhe poderes de famílias vampíricas para escapar da maldição de Armand.

> Projeto em desenvolvimento. A branch atual representa a primeira versão jogável e ainda usa modelos e conteúdos provisórios.

## Funcionalidades atuais

- movimentação isométrica, corrida, caminhada, esquiva e câmera fixa com feedback de impacto;
- moveset com ataque básico, ataque rápido ao combinar esquiva e golpe, ataque especial e Fúria Bestial desbloqueável;
- quatro famílias de Bênçãos de Sangue: Dracul, Bathory, Orlok e Karnstein;
- bênçãos com cinco qualidades, pesos configuráveis e efeitos data-driven;
- bênçãos ligadas diretamente ao ataque básico, rápido, especial e à Fúria Bestial;
- primeira oferta de Bênçãos de Sangue sempre contendo uma opção que modifica o ataque básico;
- Sangramento com dano periódico, Pestilência que se espalha na morte e Encanto que faz inimigos atacarem entre si;
- poças de sangue, barreiras, roubo de vida e demais sinergias das famílias;
- três waves temporizadas de 8 inimigos por sala comum, com 2 segundos de preparação antes de cada inimigo atacar;
- inimigos melee, ranged e tank, com raridades progressivas, nomes lendários e recompensas escaladas;
- campanha configurável com quatro mapas, salas procedurais e arenas de chefe;
- bosses com controlador, fases, movesets, animações do rig e barra de vida exclusiva no HUD;
- Bênçãos de Sangue após salas comuns e artefatos exclusivamente passivos como recompensas especiais;
- saída da sala bloqueada até a escolha da recompensa;
- três slots de save para progressão permanente;
- vida atual preservada entre salas e mapas durante a run;
- árvores permanentes de habilidades passivas Bestialis e Humanitas, em cruz invertida e cruz em pé;
- habilidades da Fonte de Sangue com ranks máximos 5/3/1 conforme a força e custos crescentes por aprimoramento;
- Ninho do Corvo com Fonte de Sangue, Baú de Artefatos que reúne descobertas da run e do save, e bancada de armas;
- HUD com ficha do personagem, minimapa, log de combate, pausa e compêndio;
- áudio persistente por contexto e configurações globais de vídeo e volume;
- localização centralizada com `pt-BR` padrão e estrutura pronta para inglês e espanhol;
- exportação Windows self-contained para Godot .NET.

## Tecnologias

- Godot Engine 4.7 Stable Mono;
- .NET 8;
- C#;
- assets e configurações de gameplay carregados por catálogos.

## Como executar

1. Instale o Godot 4.7 na edição com suporte a .NET e o SDK do .NET 8.
2. Clone o repositório:

   ```bash
   git clone https://github.com/kxpedro/vp-game.git
   cd vp-game
   ```

3. Abra `project.godot` no editor e aguarde a importação dos assets.
4. Execute o projeto com `F6`/`F5`.

Para validar o código e os catálogos principais:

```bash
dotnet build VP0.1.sln
py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py
py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py
```

No Windows, `jogar.bat` inicia o projeto pelo Godot sem abrir o editor.

## Exportação Windows

Instale pelo editor os templates de exportação correspondentes a `4.7.stable.mono` e execute:

```bat
exportar_windows.bat
```

O script restaura os runtimes `win-x64`, compila a solução, executa o preset `Windows Desktop` e rejeita exports que contenham erros internos. A distribuição deve manter juntos:

```text
builds/VampiresPrey.exe
builds/data_VP0.1_windows_x86_64/
```

Mais detalhes em [`docs/build_and_export.md`](docs/build_and_export.md).

## Controles

| Ação | Tecla/botão |
| --- | --- |
| Movimentar | `W`, `A`, `S`, `D` |
| Caminhar | `Caps Lock` |
| Esquivar | `Espaço` |
| Ataque básico | Botão esquerdo do mouse |
| Ataque rápido | Esquiva + ataque básico |
| Ataque especial | `Q` |
| Fúria Bestial | `F` (requer bênção que a desbloqueie) |
| Interagir | `E` |
| Ficha do personagem | `C` |
| Expandir minimapa | `Tab` |
| Pausar | `Esc` |

## Estrutura do projeto

```text
.codex/      skills locais para manutenção dos catálogos
3d/          modelos e rigs 3D
audio/       músicas, ambientes e efeitos sonoros
data/        skills permanentes, bênçãos, artefatos e localização
docs/        documentação técnica
images/      interface, ícones e efeitos
references/  referências conceituais e artísticas
scenes/      cenas do Godot
scripts/     código C# de gameplay e sistemas
```

As cenas principais são `scenes/ui/MainMenu.tscn`, `scenes/game/CrowsNest.tscn`, `scenes/game/TestArena.tscn` e `scenes/player/Player.tscn`.

## Estrutura da campanha

O fluxo começa no menu, com escolha de idioma e de um dos três slots. O Ninho do Corvo funciona como hub permanente. A campanha é definida em `CampaignCatalog`; o primeiro mapa possui 6 salas comuns antes da arena do primeiro boss. Ao morrer, a run temporária é encerrada, salvo quando um revive disponível é consumido, e a progressão permanente do slot é preservada.

## Progressão e combate

As habilidades compradas com Moedas de Sangue ficam em `data/skills/skills.json`. Elas são passivas e permanentes: habilidades básicas chegam ao nível 5, medianas ao nível 3 e as mais fortes possuem um único nível. O custo aumenta a cada rank. Referências a heavy, thrash e power metal existem apenas como easter eggs nos metadados e não aparecem nas descrições.

As Bênçãos de Sangue ficam em `data/blessings/blood_blessings.json` e duram apenas durante a run. Elas modificam diretamente o moveset; a Fúria Bestial em `F` só fica disponível quando uma bênção aplica seu desbloqueio. Artefatos em `data/artifacts/artifacts.json` são exclusivamente passivos e não ocupam teclas nem slots ativos.

Ataques básicos causam 200% mais empurrão que a base, ataques especiais 150% e ataques rápidos 170%. Bosses mantêm a resistência original, enquanto inimigos tank recebem apenas metade do empurrão do ataque básico. Paredes, pilares e objetos sólidos podem provocar dano de impacto quando o inimigo realmente colide com eles.

Consulte também:

- [`docs/initialization.md`](docs/initialization.md): snapshot técnico e contexto para novas sessões;
- [`docs/room_generation.md`](docs/room_generation.md): geração procedural e waves;
- [`docs/character_asset.md`](docs/character_asset.md): animações importadas de Virgil;
- [`GDD.txt`](GDD.txt): documento de design.

## Autoria

Projeto e conceito de Pedro Henrique Rech Sarmento.

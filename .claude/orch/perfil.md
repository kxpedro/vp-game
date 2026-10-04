<!-- orch:perfil · versao-orch: 0.2.0 · analisado-em: 2026-10-04 · commit: 7d8926f+ (limpeza e atualizacao visual nao commitadas) · modo: analise · atualizado: 2026-10-04 -->
# Perfil do projeto — Vampire's Prey (VP0.1)

Roguelite de ação 3D isométrico, dark fantasy, de autoria solo (Pedro Sarmento). O jogador (Virgil) atravessa salas procedurais em 4 mapas, combinando Bênçãos de Sangue temporárias com progressão permanente (skills Bestialis/Humanitas, artefatos). Protótipo jogável em desenvolvimento ativo, com parte dos visuais ainda em geometria provisória. Documentação de referência: `README.md` e `docs/*.md` (prevalecem sobre `GDD.txt`, que é a visão de 2022 e tem um adendo desatualizado).

## Stack

| Item | Valor | Evidência |
|---|---|---|
| Linguagem | C# (sem `Nullable`, sem `ImplicitUsings`, sem namespaces) | `VP0.1.csproj` |
| Engine | Godot 4.7 Mono, Forward Plus, d3d12 no Windows, Jolt Physics | `project.godot:15,112-118` |
| SDK / runtime | `Godot.NET.Sdk/4.7.0`, `net8.0`; SDK local instalado: 10.0.401 | `VP0.1.csproj:1-4` |
| Pacotes | nenhum NuGet explícito | `VP0.1.csproj` |
| Dados | catálogos JSON/JSONC em `data/`; save JSON em `user://save_slot_{1..3}.json` | `SaveGameManager.cs:78` |
| Ferramentas | Python (`py`) para validadores dos catálogos | `.codex/skills/*/scripts/` |

## Comandos

| Ação | Comando | Status |
|---|---|---|
| Instalar dependências | não há (só restauração implícita do `dotnet build`) | — |
| Build | `dotnet build VP0.1.sln` (o `jogar.bat` usa `VP0.1.csproj`) | não verificado |
| Rodar | `jogar.bat` (build + `F:\GODOT\App\Godot.exe --path .`) ou F5 no editor | executável do Godot verificado |
| Rodar cena | `F:/GODOT/App/Godot.exe --path . res://scenes/game/<Cena>.tscn` | não verificado |
| Smoke headless | `F:/GODOT/App/Godot.exe --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn` (também `CrowsNest` e `IntermissionChamber` com 120) | não verificado |
| Validar bênçãos | `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` | verificado (63, 0 erros, 26 avisos) |
| Validar artefatos | `py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py` | verificado (20, 0 erros, 16 avisos) |
| Testes unitários | não encontrado (não há framework) | — |
| Lint / format | não encontrado (`.editorconfig` só define utf-8) | — |
| Export Windows | `exportar_windows.bat` (restore win-x64, build Release, `--export-release "Windows Desktop"`, saída em `builds/`) | não verificado |

O `godot` não está no PATH: use `F:/GODOT/App/Godot.exe`. Os avisos "no quoted C# reference" dos validadores são preexistentes; erros contam, avisos não. Não há CI.

## Estrutura

| Caminho | Responsabilidade |
|---|---|
| `scripts/ui/` (~4,1k linhas) | UI 100% por código: `HUD` em `partial class` (`HUD.cs` = campos e ciclo de vida; `HUD.<Area>.cs` = StatusBars, Notifications, WeaponBench, CharacterSheet, BloodFountain, ArtifactChest, ArtifactOffer, BlessingOffer, BlessingBar, Pause, Compendium), `MainMenu` (com página de Créditos), `UIThemeFactory`, `ControllerUiSupport`, `GameCursor` |
| `scripts/player/` (~3,2k) | `PlayerController` (raiz), `PlayerCombat`, `PlayerAnimator`, `PlayerWeaponVisual`, `PlayerSkillController`, `BloodBlessingRuntime`, `SummonedRat` |
| `scripts/enemies/`, `scripts/bosses/`, `scripts/combat/` (~2,4k) | `EnemyBasic` (IA, status, stagger, APIs de boss), tuning/visual catalogs, projétil, `BossCatalog` + `BossController`, `Health` |
| `scripts/systems/` (~2,3k + catálogos) | Run e salas (`RunState`, `RunManager`, `RoomGenerator`, `CampaignCatalog`, `ScenePortal`), hub e salas especiais, autoloads, `CharacterSheet` |
| `scripts/systems/{blessings,skills,artifacts,weapons}/` | catálogos estáticos de conteúdo |
| `data/` | `blessings/blood_blessings.json` (JSONC), `skills/skills.json`, `artifacts/artifacts.json`, `localization/game_texts.json` |
| `scenes/` | `ui/MainMenu`, `ui/HUD` (só um CanvasLayer), `game/{CrowsNest,RunStartBlessingRoom,TestArena,IntermissionChamber}`, `player/Player`, `enemies/EnemyBasic` |
| `.codex/skills/vp-create-*` | guias e validadores para criar bênção, artefato, skill, inimigo e boss: **leia o SKILL.md correspondente antes de criar conteúdo** |
| `assets/`, `audio/`, `images/` | binários; origem em `SOURCE.md` por pacote e `CREDITS.md` (música, alguns efeitos e imagens ainda sem origem registrada — ver `CREDITS.md`). `assets/materials/dungeon_stone.tres` = pedra compartilhada das salas fixas e do hub |

## Arquitetura

- **Entradas:** cena principal `scenes/ui/MainMenu.tscn`. Autoloads (`project.godot:20-25`): Localization, GlobalSettings, AudioManager, SaveGameManager, CombatFeedback e GameCursor.
- **Fluxo:** MainMenu (`StartSlot`) → CrowsNest (hub) → RunStartBlessingRoom → TestArena (repetida a cada sala) → IntermissionChamber entre mapas. As cenas mudam via `ScenePortal` (`TargetScenePath` + `RunPortalAction`, `ScenePortal.cs:100-110`), e o `RunManager` só reconfigura o portal de saída (`RunManager.cs:261-287`).
- **Estado:**
  - `RunState` é estático e volátil: a run não é salva.
  - `CharacterSheet` é estático e guarda a progressão permanente, serializada pelo `SaveGameManager`.
  - Os efeitos agregados são consultados por `RunState.GetEffectTotal(key)`.
- **Sala:**
  - `RoomGenerator` monta o layout e as waves: 4 × 5 inimigos (`RoomGenerator.cs:14-16`). A sala de boss tem um único inimigo Legendary.
  - `RunManager` contabiliza mortes e recompensas e chama o HUD.
  - O boss é um `EnemyBasic` com um `BossController` filho, escolhido por `BossCatalog.Get(mapIndex)`.
- **Comunicação:** grupos + `GetTree().CallGroup("hud", "Metodo", ...)`, com nomes de método em string (`RoomGenerator.cs:317`). Singletons usam `public static X Instance` atribuído em `_Ready`.
- **Referências para algo novo:**
  - Bênção: entrada em `data/blessings/blood_blessings.json`. Efeito novo em `BloodBlessingRuntime.cs`/`RunState`.
  - Artefato: `data/artifacts/artifacts.json` + `ArtifactEffectRegistry.cs`.
  - Skill: `data/skills/skills.json` (IDs `bestialis.*`/`humanitas.*`).
  - Inimigo: `EnemyBasic.ConfigureVariant` + `EnemyTuningCatalog` + `RoomGenerator.RollEnemyArchetype`.
  - Boss: `BossCatalog.cs` + case em `BossController.ExecuteMove` (linha 80).
  - Arma: `weapons/WeaponCatalog.cs` (array C#, sem JSON) + `WeaponVisualCatalog.cs`.
  - Catálogo JSON: `ArtifactCatalog.cs` (`EnsureLoaded`, try/catch, `PushError`).
  - Interativo: `BloodFountain.cs` / `WeaponBench.cs` (Area3D + `interact`).
  - Overlay de UI: métodos `Build*`/`Open*` no arquivo `HUD.<Area>.cs` correspondente (ou um novo `partial`), usando `UIThemeFactory`.

## Convenções

- **Nomes:**
  - Identificadores em inglês; classe = arquivo, em PascalCase.
  - Campos privados em `_camelCase`.
  - `[Export]` em propriedades (`{ get; set; } = valor`); sinais como `[Signal] delegate void XEventHandler` + `EmitSignal(SignalName.X)`.
  - Dados em `sealed record`; catálogos e tuning em `static class`.
  - IDs JSON em snake_case, únicos e estáveis.
- **Estilo:**
  - A indentação varia: TAB em `HUD.cs`/`PlayerController.cs`, 4 espaços em `Localization.cs`/`RoomGenerator.cs`/`Health.cs`. **Siga a do arquivo editado.**
  - Comentários e logs misturam português e inglês.
- **Dependências entre nós:** `GetNode<T>` para as obrigatórias, `GetNodeOrNull<T>` para as opcionais.
- **Erros:** `GD.PushError` para falha de carga (com try/catch em JSON e save) e `GD.PushWarning` + placeholder para asset ausente.
- **Textos:** chave em `game_texts.json` com `pt-BR`, `en` e `es`, lida via `Localization.Text`/`TextFormat`. Chave ausente aparece na tela crua. Muito texto legado está hardcoded em português (HUD, RunManager, BossCatalog); texto novo de UI deve usar chave.
- **Commits:** conventional commits (`feat:`, `fix:`, `docs:`, `chore:`), em português ou inglês. Branches `codex/*` com merge via PR.

## Testes

- **Framework:** não encontrado. `scripts/tests/` só tem o `CodexBossTelegraphSmoke.cs.uid` órfão, e `scenes/tests/` está vazia.
- **Validação usada:** `dotnet build` + os 2 validadores Python + smoke headless da `TestArena`. Para validador novo, imite `validate_artifacts.py` (stdlib, raiz em `parents[4]`, sai com 1 se houver erro).
- **Sem validador:** `skills.json`, `game_texts.json`, inimigos e bosses.

## Cuidados

- **Arquivos do Godot:**
  - Não editar `.godot/` (gerado).
  - `*.uid` e `*.import` são versionados: todo `.cs` novo precisa do `.cs.uid` (o Godot gera ao abrir o projeto).
  - Não mexer em `.tscn` à mão além do necessário.
- **Saves:**
  - Nunca renomear ou remover IDs persistidos (skills, artefatos, arma equipada) sem mapear o ID antigo para o novo. Exemplo: `WeaponCatalog.Get` mapeia `greatsword`/`dual_blades` → `longsword`.
  - `GameSaveData.Version` existe, mas não é usado para migrar.
  - `SaveGameManager.RequestAutosave()` agenda a gravação (agrupa chamadas em 1,5 s) e grava de forma atômica (`.tmp` + rename). Use `FlushPendingSave()` se precisar garantir a escrita imediata.
- **Assets novos:** exigem `SOURCE.md` e uma entrada em `CREDITS.md`. Atenção às licenças CC BY / OGA-BY, que exigem atribuição.
- **HUD:** dividido em `partial class` por área. Renomear métodos chamados via `CallGroup` quebra em runtime, sem erro de compilação.
- **Material arquivado:** `3d/` e `references/` foram movidos para `../vp-0.1-arquivo/` e ignorados no git (incluíam modelos de jogos comerciais). Não os traga de volta para o projeto.
- **Ferramentas de debug:** botão de cheat de moedas, "Reiniciar teste" e contador de FPS só aparecem com `OS.IsDebugBuild()`.
- **Bosses:** `CampaignCatalog.BossId` agora coincide com o `Id` do `BossCatalog` no mesmo índice. As chaves de tuning `enemy.blood_mage.*` (dano, velocidade, alcance base de chefes) continuam com esse nome por compatibilidade com valores salvos no compêndio.
- **Docs desatualizadas:** o adendo do `GDD.txt` diz 3 waves e jogador "cilindro azul"; o código e o README valem.

## Especialistas do projeto

| Agente | Quando usar |
|---|---|
| `conteudo-run` | Bênçãos, artefatos e skills: `data/{blessings,artifacts,skills}`, `scripts/systems/{blessings,artifacts,skills}`, `BloodBlessingRuntime`, `PlayerSkillController`, `RunState`, `CharacterSheet` |
| `inimigos-e-bosses` | `scripts/enemies`, `scripts/bosses`, `RoomGenerator`, `RunManager`, `CampaignCatalog`: IA, telegraph, waves, bosses |
| `ui-hud` | `scripts/ui` (`HUD.cs`), `scenes/ui`, `UIThemeFactory`, navegação por controle, `game_texts.json`/`Localization` |
| `game-designer` | Mecânicas, balanceamento, level design e narrativa, de ponta a ponta: docs, GDD, `data/*.json` e C# de tuning e regras |
| `designer-ux-arte` | Direção de arte, leitura de combate, layout e UX; implementa no código de apresentação (UI, feedback, visuais, cenas) |

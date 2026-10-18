<!-- orch:perfil · versao-orch: 0.2.0 · analisado-em: 2026-10-04 · commit: 7d8926f+ (limpeza e atualizacao visual nao commitadas) · modo: analise · atualizado: 2026-10-11 -->
# Perfil do projeto — Vampire's Prey (VP0.1)

Roguelite de ação 3D isométrico, dark fantasy, de autoria solo (Pedro Sarmento). O jogador (Virgil) atravessa salas procedurais em 4 mapas, combinando Bênçãos de Sangue temporárias com progressão permanente (skills Bestialis/Humanitas, artefatos). Protótipo jogável em desenvolvimento ativo, com parte dos visuais ainda em geometria provisória. Documentação de referência: `README.md` e `docs/*.md` (prevalecem sobre `GDD.txt`, que é a visão de 2022 e tem um adendo desatualizado).

## Stack

| Item | Valor | Evidência |
|---|---|---|
| Linguagem | C# (sem `Nullable`, sem `ImplicitUsings`, sem namespaces) | `VP0.1.csproj` |
| Engine | Godot 4.7 Mono, Forward Plus, d3d12 no Windows, Jolt Physics | `project.godot:15,112-118` |
| SDK / runtime | `Godot.NET.Sdk/4.7.0`, `net8.0`; SDK local instalado: 10.0.401 | `VP0.1.csproj:1-4` |
| Pacotes | nenhum NuGet explícito | `VP0.1.csproj` |
| Dados | catálogos JSON/JSONC em `data/` (só IDs e números); textos em `data/localization/<idioma>/*.json`; save JSON em `user://save_slot_{1..3}.json` | `SaveGameManager.cs:78`, `Localization.cs:15` |
| Ferramentas | Python (`py`) para validadores dos catálogos e da localização | `.codex/skills/*/scripts/`, `tools/validate_localization.py` |

## Comandos

| Ação | Comando | Status |
|---|---|---|
| Instalar dependências | não há (só restauração implícita do `dotnet build`) | — |
| Build | `dotnet build VP0.1.sln` (o `jogar.bat` usa `VP0.1.csproj`) | verificado (0 erros, 0 avisos) |
| Rodar | `jogar.bat` (build + `F:\GODOT\App\Godot.exe --path .`) ou F5 no editor | executável do Godot verificado |
| Rodar cena | `F:/GODOT/App/Godot.exe --path . res://scenes/game/<Cena>.tscn` | não verificado |
| Smoke headless | `F:/GODOT/App/Godot.exe --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn` (também `CrowsNest` e `IntermissionChamber` com 120) | `TestArena` e `RunStartBlessingRoom` verificados em headless; `CrowsNest`, `IntermissionChamber` e `MainMenu` verificados pelo `--ui-batch` (janela) |
| Validar bênçãos | `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` (inclui as chaves de `content.json`) | verificado (63, 0 erros, 0 avisos) |
| Validar artefatos | `py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py` (inclui as chaves de `content.json`) | verificado (20, 0 erros, 0 avisos) |
| Validar localização | `py tools/validate_localization.py` (`-v` lista chaves `TextOr` pendentes) | verificado (1081 chaves por idioma, 0 erros, 0 avisos) |
| Pré-visualizar telas (só debug) | `F:/GODOT/App/Godot.exe --path . res://scenes/game/<Cena>.tscn -- --ui-preview=<tela> [--ui-lang=] [--ui-device=] [--ui-tab=] [--ui-codex-locked]` | verificado em headless (`pause`, `en` e `es`) |
| Pré-visualizar em lote (só debug) | `F:/GODOT/App/Godot.exe --path . --resolution 1600x900 -- --ui-batch --ui-shots=<pasta> [--ui-langs=pt-BR,en,es] [--ui-screens=a,b]` | 54 telas × 3 idiomas = 162 capturas (`UiPreview.BatchScreens`, inclui `anvil_offer`, `anvil_offer_single`, `reward_doors`, `hub_cast`, `dialogue*`, `dev_panel`, `dev_panel_actions`) em 1 processo; contagem conferida no código e com o lote real de `pt-BR` (52 capturas, sem SCRIPT ERROR); não há preview dos itens no chão |
| Validar bigorna | `py tools/validate_weapon_mods.py` (`data/weapons/weapon_mods.json`: ids únicos snake_case, `weaponId` em `WeaponCatalog.cs`, tipos/campos/valores dos efeitos, `weaponmod.<id>.name/.desc/.flavor` nos 3 idiomas com placeholders iguais, chaves `weapon.mod.<id>.<campo>` registradas em `DevParameters.*.cs` com Default igual ao JSON) | verificado (5 mods, 0 erros, 0 avisos) |
| Validar tuning de dev | `py tools/validate_dev_tuning.py` (chaves lidas por `CompendiumTuning.Get*`/`G()` x chaves registradas em `DevParameters.*.cs`, duplicatas, Min <= Default <= Max, chaves sem leitura = aviso) | verificado (0 erros, 0 avisos) |
| Autoteste do registro (só debug) | `F:/GODOT/App/Godot.exe --headless --path . --quit-after 60 res://scenes/game/TestArena.tscn -- --dev-tuning-selftest` (imprime `DEV-TUNING-SELFTEST: OK` e o total de chaves; cuidado: apaga `user://dev_tuning.json`) | verificado (OK, 658 chaves) |
| Testes unitários | não encontrado (não há framework) | — |
| Lint / format | não encontrado (`.editorconfig` só define utf-8) | — |
| Export Windows | `exportar_windows.bat` (restore win-x64, build Release, `--export-release "Windows Desktop"`, saída em `builds/`) | não verificado |

O `godot` não está no PATH: use `F:/GODOT/App/Godot.exe`. Os validadores estão sem erros e sem avisos; erro conta (saída 1), aviso não. Não há CI.

O `--ui-batch` (`scripts/ui/UiPreview.cs`) percorre todas as telas × idiomas num único processo do Godot, recarregando a cena entre uma e outra, e grava PNGs em `--ui-shots=<pasta>` (padrão `user://ui_shots`): use-o em vez de abrir o jogo uma vez por tela. Os argumentos de usuário vêm depois de `--`, e tudo isso só funciona em build de debug.

## Estrutura

| Caminho | Responsabilidade |
|---|---|
| `scripts/ui/` (~4,1k linhas) | UI 100% por código: `HUD` em `partial class` (`HUD.cs` = campos e ciclo de vida; `HUD.<Area>.cs` = StatusBars, Notifications, WeaponBench, CharacterSheet, BloodFountain, Shop, BlessingOffer, BlessingBar, Pause, Compendium; o Baú e a Oferta de artefatos foram removidos), `MainMenu` (com página de Créditos), `UIThemeFactory`, `UiComponents`, `ControllerUiSupport`, `GameCursor`, `UiPreview` (pré-visualização de telas, só debug) |
| `scripts/player/` (~3,2k) | `PlayerController` (raiz), `PlayerCombat`, `PlayerAnimator`, `PlayerWeaponVisual`, `PlayerSkillController`, `BloodBlessingRuntime`, `SummonedRat` |
| `scripts/enemies/`, `scripts/bosses/`, `scripts/combat/` (~2,4k) | `EnemyBasic` (IA, status, stagger, APIs de boss), tuning/visual catalogs, projétil, `BossCatalog` + `BossController`, `Health` |
| `scripts/systems/` (~2,3k + catálogos) | Run e salas (`RunState`, `RunManager`, `RoomGenerator`, `CampaignCatalog`, `ScenePortal`), hub e salas especiais, autoloads, `CharacterSheet` |
| `scripts/systems/{blessings,skills,artifacts,weapons}/` | catálogos estáticos de conteúdo |
| `scripts/systems/rewards/`, `scripts/systems/{DoorRewardIcon,RewardPickup,CharacterPlaceholder}.cs`, `scripts/items/DevilsFruit.cs` | portas com recompensa (`RoomRewardCatalog`: modelo e sorteio puros; `DoorRewardIcon`: medalhão provisório), itens no chão (`RewardPickup` = Bênção/Moedas; `DevilsFruit` = Fruta), placeholders dos personagens futuros no hub (só marcador visual) |
| `data/` | `blessings/blood_blessings.json` (JSONC), `skills/skills.json`, `artifacts/artifacts.json` (só IDs e números, sem texto), `localization/<pt-BR,en,es>/{ui,content,world}.json` (textos; 1081 chaves por idioma) |
| `tools/` | `validate_localization.py` (paridade de chaves entre idiomas, placeholders, JSON válido, chaves literais do C#) |
| `docs/design/` | `ui-guia.md` (guia de UI e especificação das telas; Anexo A = previews, Anexo D = localização), `glossario-localizacao.json` (termos pt-BR/en/es) |
| `scenes/` | `ui/MainMenu`, `ui/HUD` (só um CanvasLayer), `game/{CrowsNest,RunStartBlessingRoom,TestArena,IntermissionChamber}`, `player/Player`, `enemies/EnemyBasic` |
| `.codex/skills/vp-create-*` | guias e validadores para criar bênção, artefato, skill, inimigo e boss: **leia o SKILL.md correspondente antes de criar conteúdo** |
| `assets/`, `audio/`, `images/` | binários; origem em `SOURCE.md` por pacote e `CREDITS.md` (música, alguns efeitos e imagens ainda sem origem registrada — ver `CREDITS.md`). `assets/materials/dungeon_stone.tres` = pedra compartilhada das salas fixas e do hub |

## Arquitetura

- **Entradas:** cena principal `scenes/ui/MainMenu.tscn`. Autoloads (`project.godot:20-25`): Localization, GlobalSettings, AudioManager, SaveGameManager, CombatFeedback e GameCursor.
- **Fluxo:** MainMenu (`StartSlot`) → CrowsNest (hub) → RunStartBlessingRoom → TestArena (repetida a cada sala) → IntermissionChamber entre mapas (fonte, altar de aprimoramento e Loja de Artefatos). As cenas mudam via `ScenePortal` (`TargetScenePath` + `RunPortalAction`, `ScenePortal.cs:100-110`), e o `RunManager` só reconfigura o portal de saída (`RunManager.cs:261-287`).
- **Estado:**
  - `RunState` é estático e volátil: a run não é salva.
  - `CharacterSheet` é estático e guarda a progressão permanente, serializada pelo `SaveGameManager`.
  - Os efeitos agregados são consultados por `RunState.GetEffectTotal(key)`.
  - Dois recursos: **Moedas de Sangue** (`RunState.RunBloodCoins`) só existem na run, não vão ao save e zeram em `StartRun`, `FailRun` e `CompleteBoss` do mapa final (não entre mapas); **Sangue Amaldiçoado** (`CharacterSheet.CursedBlood`) é permanente, pago por boss e inimigo Lendário (`EnemyBasic.RollDefeatRewards` → `RunManager.QueueEnemyDefeat`) e gasto só nas skills da Fonte de Sangue. Não confundir ao mexer em HUD, save ou `PlayerSkillController` (a Fúria multiplica as Moedas, não o Sangue Amaldiçoado).
  - Fruta do Diabo (`DevilsFruit`, `economy.fruit.max_health` padrão 15): só na run, em `RunState._devilsFruitMaxHealth`. `RunState.DevilsFruitMaxHealthTotal` é somado em `PlayerController.ApplyRuntimeTuning` depois dos multiplicadores (fora de `MaxHealthBonus`): +15 exatos.
  - Portas de recompensa: `RunState` guarda o conjunto sorteado por (mapa, sala de origem), o pity, o teto de Frutas e a recompensa pendente; zeram em `StartRun`/`FailRun`/fim da campanha, não ao trocar de sala nem em recarga de cena.
- **Sala:**
  - `RoomGenerator` monta o layout e as waves: 4 × 5 inimigos (`RoomGenerator.cs:14-16`). A sala de boss tem um único inimigo Legendary.
  - `RunManager` contabiliza mortes e recompensas e chama o HUD. Ao limpar a sala (`CompleteRoom`) ele constrói as portas de recompensa **travadas** (`ScenePortal.BuildRewardDoors`) e põe no chão o bônus de sala limpa e a recompensa da porta escolhida antes; as portas só abrem quando não resta item (`UnlockExitAfterReward`). Sala N (para o boss) e boss têm saída única. O boss solta uma Fruta (exceto no mapa final) e não há drop de artefato.
  - O boss é um `EnemyBasic` com um `BossController` filho, escolhido por `BossCatalog.Get(mapIndex)`.
- **Comunicação:** grupos + `GetTree().CallGroup("hud", "Metodo", ...)`, com nomes de método em string (`RoomGenerator.cs:317`). Singletons usam `public static X Instance` atribuído em `_Ready`.
- **Referências para algo novo:**
  - Bênção: entrada em `data/blessings/blood_blessings.json` (só IDs e números) + `blessing.<id>.name/.desc/.flavor` em `content.json` nos 3 idiomas. Efeito novo em `BloodBlessingRuntime.cs`/`RunState`.
  - Artefato (obtido só na Loja da Câmara, `HUD.Shop.cs` + `RunState.TryBuyShopArtifact`; `RollChoices` foi removido): `data/artifacts/artifacts.json` (com `saveName` único) + `artifact.<id>.name/.desc/.flavor` em `content.json` + `ArtifactEffectRegistry.cs` (e `artifact.effect.<Tipo>` se o efeito for novo).
  - Skill: `data/skills/skills.json` (IDs `bestialis.*`/`humanitas.*`) + `skill.<id>.name/.desc/.flavor/.icon` em `content.json`.
  - Inimigo: `EnemyBasic.ConfigureVariant` + `EnemyTuningCatalog` + `RoomGenerator.RollEnemyArchetype`.
  - Boss: `BossCatalog.cs` + case em `BossController.ExecuteMove` (linha 80).
  - Arma: `weapons/WeaponCatalog.cs` (array C#, sem JSON) + `WeaponVisualCatalog.cs`.
  - Power-up de arma (Bigorna): `data/weapons/weapon_mods.json` + `WeaponModCatalog` + `RunState` + `PlayerCombat.GetTunedAttack`; textos `weaponmod.<id>.*` em `content.json` (design em `docs/design/bigorna.md`; validar com `py tools/validate_weapon_mods.py`).
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
- **Textos:**
  - Todo texto exibido vem de `data/localization/<idioma>/{ui,content,world}.json` (JSON plano `"chave": "texto"`; idiomas `pt-BR`, `en`, `es`). `ui.json` = interface; `content.json` = bênçãos, artefatos, skills, armas e efeitos; `world.json` = bosses, mapas, inimigos e mensagens de combate.
  - Leitura via `Localization.Text`/`TextFormat`/`Has` (`Localization.cs`). Resolução: idioma atual → `pt-BR` → a própria chave (aviso em debug, uma vez por chave). Overlays e rótulos 3D reagem ao sinal `LanguageChanged`. A cultura da sessão segue o idioma (`en` com ponto decimal; `pt-BR` e `es` com vírgula).
  - Chave nova entra nos **três** idiomas (o validador cobra paridade e placeholders iguais) e, se for literal no C#, deve existir em `pt-BR`. Rode `py tools/validate_localization.py`.
  - `PromptText` dos interativos (`ScenePortal`, `BloodFountain`, `WeaponBench`, inclusive nos `.tscn`) guarda uma chave (`portal.*`, por exemplo), resolvida na exibição.
  - Só texto de debug (log de eventos, seção DEV, linhas "Prévia:" dos previews) pode ficar literal. Hoje o log de eventos do HUD (`AppendGameLog`) ainda tem frases literais em português.
  - Idioma novo: `Localization.Languages`/`NormalizeLanguage`, o seletor de `UiComponents.OptionsPanel` (três opções fixas) e a lista `LANGUAGES` dos três validadores Python.
- **Commits:** conventional commits (`feat:`, `fix:`, `docs:`, `chore:`), em português ou inglês. Branches `codex/*` com merge via PR.

## Testes

- **Framework:** não encontrado. `scripts/tests/` só tem o `CodexBossTelegraphSmoke.cs.uid` órfão, e `scenes/tests/` está vazia.
- **Validação usada:** `dotnet build` + os 5 validadores Python (bênçãos, artefatos, `tools/validate_localization.py`, `tools/validate_dev_tuning.py`, `tools/validate_weapon_mods.py`) + smoke headless da `TestArena`. Para validador novo, imite `validate_artifacts.py` (stdlib, raiz em `parents[4]`, sai com 1 se houver erro) ou `tools/validate_localization.py` (raiz em `parents[1]`).
- **Sem validador:** `skills.json`, inimigos e bosses. Para eles só vale a paridade de chaves de `validate_localization.py`; o conteúdo em si não é checado.
- **Conferência visual:** `--ui-preview`/`--ui-batch` (só debug, ver Comandos) geram capturas das telas por idioma; não são testes automáticos.

## Cuidados

- **Arquivos do Godot:**
  - Não editar `.godot/` (gerado).
  - `*.uid` e `*.import` são versionados: todo `.cs` novo precisa do `.cs.uid` (o Godot gera ao abrir o projeto).
  - Não mexer em `.tscn` à mão além do necessário.
- **Saves:**
  - Nunca renomear ou remover IDs persistidos (skills, artefatos, arma equipada) sem mapear o ID antigo para o novo. Exemplo: `WeaponCatalog.Get` mapeia `greatsword`/`dual_blades` → `longsword`.
  - O `saveName` de cada artefato em `artifacts.json` é o identificador legado gravado nos saves: nunca alterar. Registros antigos são resolvidos para o `Id` em `CharacterSheet.WithResolvedId`.
  - `GameSaveData.Version` = 2 (`CurrentSaveVersion`; v2 = Moedas só na run + campo `CursedBlood`). `SaveGameManager.ReadSlot` só trata como v1 JSON com `Version` presente, inteiro e `1 <= Version < 2`: **renomeia** para `save_slot_N.v1.json` (`ArchiveLegacySlot`), sem migração. JSON válido sem `Version` carrega normalmente; JSON inválido vai para o fluxo de backup de corrompido. Ao mudar o formato, suba a versão e decida antes entre migrar ou excluir.
  - `SaveGameManager.RequestAutosave()` agenda a gravação (agrupa chamadas em 1,5 s) e grava de forma atômica (`.tmp` + rename). Use `FlushPendingSave()` se precisar garantir a escrita imediata.
- **Assets novos:** exigem `SOURCE.md` e uma entrada em `CREDITS.md`. Atenção às licenças CC BY / OGA-BY, que exigem atribuição.
- **HUD:** dividido em `partial class` por área. Renomear métodos chamados via `CallGroup` quebra em runtime, sem erro de compilação.
- **Material arquivado:** `3d/` e `references/` foram movidos para `../vp-0.1-arquivo/` e ignorados no git (incluíam modelos de jogos comerciais). Não os traga de volta para o projeto.
- **Ferramentas de debug:** botão de cheat de moedas, "Reiniciar teste" e contador de FPS só aparecem com `OS.IsDebugBuild()`.
- **Painel F1 e tuning de dev (só debug):** `DevParameterRegistry` (`scripts/systems/DevParameterRegistry.cs` + `DevParameters.{Legacy,Player,Enemies,Economy}.cs`, ~658 chaves) é o registro central de parâmetros de gameplay (padrão, faixa, passo, modo de aplicação); o painel F1 (`scripts/ui/HUD.DevPanel.cs`) os edita. Os valores alterados vão para `CompendiumTuning` e persistem em `user://dev_tuning.json` (em release nada é lido nem gravado); os 11 ajustes rápidos `game.*` ficam no `GameTuning` (`user://game_tuning.cfg`). Chave nova lida por `CompendiumTuning.Get*`/`G()` precisa de `P(...)` em `DevParameters.*.cs`: rode `py tools/validate_dev_tuning.py`. Inventário: `docs/design/dev-tools-inventario.md`.
- **Forçar portas (só debug):** aba Ações do F1 > "Portas de recompensa" (`HUD.DevPanel.cs`) liga um override em `RunState` (`DevForceDoors`, `DevForcedDoorCount`, `DevForcedType`; `RoomRewardCatalog.DevRollForced`) que monta o conjunto de toda saída ainda não sorteada com os tipos escolhidos, ignorando pesos, pity e tetos (Fruta/Bigorna forçadas não contam nos tetos); zera em `StartRun`/`FailRun`/fim da campanha, não vai ao save. "Rerrolar portas desta sala" = `RunManager.DevRerollRewardDoors`; "Bênção agora"/"Bigorna agora" abrem as ofertas sem porta. Com o override desligado o sorteio e a sequência de RNG são os normais.
- **Hub e ficha:** os 3 `CharacterPlaceholder` (criados em `CrowsNestCrypt.CreateCharacterPlaceholders`) são só marcador "Em breve", sem interação; os personagens futuros não existem. A aba Atributos da ficha só aparece em debug (`CharacterSheetTabOffset`); em release os atributos continuam computados, só ocultos.
- **Bosses:** a ordem da campanha é `invisible_woman`, `labyrinth_keeper`, `silver_dragonkin`, `armand` (modelos: Ghost Skull, Orc Skull, Dragon Evolved, Blue Demon). `CampaignCatalog.BossId` coincide com o `Id` do `BossCatalog` no mesmo índice e `EnemyVisualCatalog.GetBoss` também indexa por mapa. As chaves de tuning `enemy.blood_mage.*` (dano, velocidade, alcance base de chefes) continuam com esse nome por compatibilidade com valores salvos no compêndio.
- **Docs desatualizadas:** o adendo do `GDD.txt` diz 3 waves e jogador "cilindro azul"; o código e o README valem.
- **Golpes e `Control`:** `PlayerCombat.GetTunedAttack` recria o `WeaponAttackDefinition` sem repassar o `Control` (perfis de buffer/aim assist do `WeaponCatalog`): na prática esses perfis não têm efeito hoje. Ao mexer no método, repasse `attack.Control` e revise o comportamento resultante.

## Especialistas do projeto

| Agente | Quando usar |
|---|---|
| `conteudo-run` | Bênçãos, artefatos e skills: `data/{blessings,artifacts,skills}`, `scripts/systems/{blessings,artifacts,skills}`, `BloodBlessingRuntime`, `PlayerSkillController`, `RunState`, `CharacterSheet` |
| `inimigos-e-bosses` | `scripts/enemies`, `scripts/bosses`, `RoomGenerator`, `RunManager`, `CampaignCatalog`: IA, telegraph, waves, bosses |
| `ui-hud` | `scripts/ui` (`HUD.cs`), `scenes/ui`, `UIThemeFactory`, navegação por controle, `data/localization/<idioma>/*.json`/`Localization`/`tools/validate_localization.py`, `UiPreview` |
| `game-designer` | Mecânicas, balanceamento, level design e narrativa, de ponta a ponta: docs, GDD, `data/*.json` e C# de tuning e regras |
| `designer-ux-arte` | Direção de arte, leitura de combate, layout e UX; implementa no código de apresentação (UI, feedback, visuais, cenas) |

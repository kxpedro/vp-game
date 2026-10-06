<!-- plano: 20261004-1829-hud-auditoria · tarefa: T2 · executor: ui-hud · gerado-em: 2026-10-04 · commit-base: 0c387de -->
# Inventário técnico da UI — Vampire's Prey

Levantamento da implementação de todas as telas e overlays, para orientar o guia de estilo (T3) e as fases de reforma. Este documento só descreve o código atual; não propõe arte. As linhas citadas são do commit `0c387de`.

**Arquivos lidos:**
- `scripts/ui/HUD.cs` e as partials `HUD.StatusBars.cs`, `HUD.Notifications.cs`, `HUD.Pause.cs`, `HUD.CharacterSheet.cs`, `HUD.BloodFountain.cs`, `HUD.ArtifactChest.cs`, `HUD.ArtifactOffer.cs`, `HUD.BlessingOffer.cs`, `HUD.BlessingBar.cs`, `HUD.WeaponBench.cs` e `HUD.Compendium.cs`.
- `scripts/ui/MainMenu.cs`, `UIThemeFactory.cs`, `ControllerUiSupport.cs` e `GameCursor.cs`.
- `scripts/systems/IntermissionChamber.cs` e `RunStartBlessingRoom.cs`.
- `scenes/ui/HUD.tscn` e as cenas de `scenes/game/*.tscn`.

**Legenda das contagens de texto:**
- **hardcoded:** número aproximado de strings visíveis ao jogador escritas em português no código, contado por leitura manual. Exclui nomes de nó, chaves e caminhos.
- **loc:** chamadas a `Localization.Text` ou `Localization.TextFormat`.
- Só o `MainMenu` escuta `LanguageChanged` (`MainMenu.cs:16`, reconstrói a tela inteira). O HUD não escuta: os textos localizados do HUD são lidos uma única vez, no `BuildLayout`.

---

## 0. Infraestrutura comum

| Campo | Valor |
|---|---|
| Cena | `scenes/ui/HUD.tscn` é só um `CanvasLayer` com `HUD.cs`. É instanciada em `CrowsNest.tscn:298`, `RunStartBlessingRoom.tscn:263`, `TestArena.tscn:218` e `IntermissionChamber.tscn:309`. |
| Ciclo de vida | `_Ready` (`HUD.cs:116`): `EnsureMappings`, `AddToGroup("hud")`, `ProcessMode = Always`, `BuildLayout()` e `CallDeferred(BindRuntimeNodes)`. |
| Raiz | `Control "Root"` em tela cheia com `Theme = UIThemeFactory.Create()` (`HUD.StatusBars.cs:11-18`). Tudo que é filho de `Root` herda o tema. **Exceção:** o número de dano recebido é adicionado direto no `CanvasLayer` (`StatusBars.cs:414`, `AddChild(label)`) e por isso fica sem o tema (sem outline). |
| Ordem de desenho (z) | É a ordem de criação em `BuildLayout` (`StatusBars.cs:245-254`): Pause → GameOver → RoomClearBanner → CharacterSheet → BloodFountain (com SkillDescriptionPopup dentro) → ArtifactChest → ArtifactOffer → BloodBlessing (e BloodAbsorptionOverlay) → WeaponBench → Compendium. O **GameOver fica abaixo** de Ficha, Fonte, Baú, Ofertas, Bancada e Compêndio. |
| `_Input` (`HUD.cs:125`) | Trata `ui_cancel` → `CloseTopmostOverlay()` (ignorado em game over). Na oferta de bênção, intercepta `ui_up`/`ui_down` para ciclar só os botões "Escolher" (`HUD.cs:135-142`). |
| `_UnhandledInput` (`HUD.cs:145`) | `pause` → fecha o topo ou abre a pausa. `character_sheet` → `ToggleCharacterSheet()`. Na Fonte, roda do mouse = zoom e arrasto com botão esquerdo = pan (`HUD.cs:162-188`). |
| `_Process` (`HUD.cs:191`) | Pulso da vinheta de vida crítica, visibilidade da moldura do título da sala e texto do FPS (este só em build de debug). |
| `IsGameplayInputBlocked()` (`HUD.cs:204`) | Público e chamado **diretamente**, não por CallGroup: `PlayerController.cs:96`, `PlayerCombat.cs:96` e `PlayerCombat.cs:429-430`. Retorna true se qualquer overlay do HUD estiver visível. Não inclui `_bloodAbsorptionOverlay` nem o overlay do altar da Câmara, que vive fora do HUD. |
| Pausa | `SetModalPause(bool)` (`Pause.cs:460`) define `GetTree().Paused` e chama `CallGroup("audio_manager","SetPaused")`. `RefreshModalPause()` (`Pause.cs:466`) recalcula a pausa a partir de: game over, Pausa, Ficha, Compêndio, Oferta de artefato e Oferta de bênção. Fonte, Baú e Bancada **não pausam** (bloqueiam o input só via `IsGameplayInputBlocked`). Vários overlays mexem em `GetTree().Paused` direto, sem passar pelo áudio (ver cada tela). |
| Tema (`UIThemeFactory.cs`) | Cria Theme: fonte padrão 17; cores `text` (0.92,0.88,0.80), `muted` e `gold` (0.82,0.64,0.30); outline 3 em Label; StyleBoxes de Button (normal, hover, pressed, focus, disabled), PanelContainer, LineEdit, OptionButton e HSlider. Também `HudPanel(alpha)`, `ChoiceCard(accent)` e `AddSimpleFrame(...)` (NinePatch `kenney_hud/simple_panel.png`). As cores ficam em variáveis locais de `Create()`, sem constantes públicas: quem precisa de dourado ou carmesim redeclara a cor inline. **Não há** estilos para ScrollContainer/ScrollBar, SpinBox, CheckButton (ícones), ProgressBar, TabBar, TooltipPanel/TooltipLabel nem RichTextLabel (normal). |
| Controle (`ControllerUiSupport.cs`) | `EnsureMappings()` adiciona botões 0/1/11-14 e eixos do analógico esquerdo a `ui_*`. `FocusFirst(root)` dá foco ao primeiro Control focável, visível e habilitado, em ordem de árvore. **Não há** helper de "restaurar foco" nem pilha de foco. |
| Cursor (`GameCursor.cs`) | Autoload. Define `Arrow`/`Help` = `cursor_pointer.png` e `PointingHand` = `cursor_gauntlet.png`. O HUD usa `CursorShape.PointingHand` nos cards de bênção e `Help` em tooltips da barra de bênçãos e da ficha. |

---

## 1. HUD em jogo (elementos sempre presentes)

### 1.1 Painel do jogador (vida, barreira e fúria)

| Campo | Valor |
|---|---|
| Construtor | `HUD.StatusBars.cs:19` (moldura `PlayerFrame` via `AddHudPanel`), `:23-88` (VBox `PlayerStatus`), `CreateBar` em `:258` e `ApplyHealthBarStyle` em `:284`. |
| Abre/fecha | Sempre visível, exceto no CrowsNest: `showCombatHud` testa `SceneFilePath.EndsWith("CrowsNest.tscn")` (`:86-88`). A barra de fúria só aparece com `RunState.GetEffectTotal("UnlockBestialFury.Flat") > 0` (`RefreshFuryVisibility`, `:485`). |
| Atualização | Sinais `Health.HealthChanged/Damaged/Died`, `BloodBlessingRuntime.BarrierChanged` e `PlayerCombat.FuryChanged`, ligados em `BindRuntimeNodes` (`:339`). `UpdateCombinedHealthDisplay` (`:432`) sobrepõe a barra de barreira (azul) à de vida. |
| Pausa / cancel / foco | Não se aplica. |
| CallGroup | Nenhum. |
| Estilo | `UIThemeFactory.HudPanel(0.9)` na moldura. Todo o resto é inline: 6 `StyleBoxFlat` em `ApplyHealthBarStyle` (`:286-336`), vida (0.86,0.02,0.04), barreira (0.12,0.52,0.95), fúria (0.94,0.37,0.04)/borda (1,0.72,0.2); header "VIRGIL" com fonte 13 e `Modulate` (0.82,0.72,0.62) (`:37-44`); rótulo de pontos com sombra inline (`:81-83`); cor azulada com barreira (`:450`). |
| Px fixos | Moldura (18,-142)-(430,-18); conteúdo (34,-128)-(414,-28); barras de 360×22 (`:60`, `:265`); label de pontos posicionado com `Position`/`Size` fixos (`:76`). |
| Texto | hardcoded ~6 ("VIRGIL", nomes das barras usados como tooltip "Vida/Barreira/Furia", 3 tooltips da fúria em `:478-482`); loc 0. |
| Estado | `Health`, `BloodBlessingRuntime`, `PlayerCombat`, `RunState.GetEffectTotal` e `CompendiumTuning("attack.fury.drain_per_second")`. |

### 1.2 Painel de recursos (moedas, arma e inimigos)

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:20` (moldura `ResourceFrame`) e `:129-163` (VBox `ResourcePanel` com 3 Labels). |
| Abre/fecha | Sempre visível, inclusive no hub. |
| Atualização | `OnBloodCoinsChanged` (`:492`), `SyncEnemyProgress` (`Notifications.cs:17`) e `EquipWeapon` (`WeaponBench.cs:62`). **Inconsistência:** `BuySkill` escreve `_coinsLabel.Text = $"Moedas de Sangue: ..."` hardcoded (`BloodFountain.cs:268`), passando por cima da chave `hud.blood_coins`. |
| CallGroup | `SyncBloodCoins`, `NotifyBloodCoinsRewarded`, `SyncEnemyProgress` e `NotifyEnemyDefeated` (ver §4). |
| Estilo | Moldura `HudPanel(0.78)`; labels com o tema puro. |
| Px fixos | Moldura (18,18)-(310,118); conteúdo (32,27)-(294,108). |
| Texto | loc 3 (`hud.blood_coins` ×2, `hud.enemies`); hardcoded 2 ("Arma: ..." em `:152` e `WeaponBench.cs:62`). |
| Estado | `CharacterSheet.BloodCoins`, `RunManager` (sinais) e `WeaponLoadout.EquippedWeapon`. |

### 1.3 Título da sala e divisor

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:21` (moldura `RoomFrame`, `HudPanel(0.72)`), `:165-179` (`RoomLabel`) e `:180-196` (`RoomDivider`, textura `simple_divider.png`). |
| Abre/fecha | `_Process` (`HUD.cs:194-199`) mostra a moldura quando o label tem texto ou quando `_roomFrameAlwaysVisible` (cena é `IntermissionChamber`, `StatusBars.cs:198`). |
| Atualização | `SyncRoomInformation` (`Notifications.cs:69`) e `OnRoomCleared` (`StatusBars.cs:523`, que troca o texto para "Sala concluida - portal vermelho liberado"). |
| Estilo | Fonte 17 e cor (0.86,0.78,0.68) inline (`:177-178`); divisor com `Modulate` inline. |
| Px fixos | Moldura ±280 × (14..58); label ±258 × (19..49); divisor ±190 × (52..56). |
| Texto | hardcoded 3 ("— BOSS", "— Sala x/y", "Sala concluida..."); loc 0. |
| Observação | Na Câmara de Hemomancia, o texto é escrito por **outro** Label, de `IntermissionChamber` (§3.10), com largura ±390 e altura 22..88. Ele não cabe na moldura ±280 × 14..58 do HUD. |

### 1.4 Barra do boss

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:213-225` (VBox `BossHealthPanel` com âncoras 0.22..0.78 × 0.075). |
| Abre/fecha | `BindBoss(Node, string)` (`Notifications.cs:51`), via `CallGroup` em `RunManager.cs:240`. Fecha por lambda em `health.Died`. |
| Estilo | Label com fonte 24 inline; 2 `StyleBoxFlat` inline: fill (0.52,0.015,0.025) e bg (`:223-224`). |
| Px fixos | Altura 78; barra com mínimo de 28 px de altura. |
| Texto | loc 1 (`hud.boss`, placeholder); o nome vem do `BossCatalog` (não localizado). |
| Risco | `UpdateBossHealth` reconstrói o texto com `Split('—')[0]` (`Notifications.cs:66`). Quebra se o nome do boss contiver "—". |

### 1.5 Barra de bênçãos (rodapé)

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:90-100` (HBox `BlessingHudList`, ±246 × (-66..-18)) e `RefreshBlessingHud` (`HUD.BlessingBar.cs:9`). |
| Atualização | `RefreshBlessingHud` destrói e recria todos os ícones (`:13`). Chamado por `SelectBloodBlessing` e `RefreshBlessingDisplays` (CallGroup em `IntermissionChamber.cs:281`). |
| Estilo | Fundo de cada ícone = `TextureRect` do `simple_panel.png` com `Modulate` inline (`:27-36`); badge "N2" com fonte 13 e sombra inline (`:65-69`); "+" com fonte 30 e `Modulate` carmesim (`:88-90`). |
| Px fixos | Ícone de 44×44 com inset de 4; badge (-24,-18); limite de 7 ícones (`:14`). |
| Texto | hardcoded 2 (tooltip "Mais N bênção(ões). Pressione C..." e "N{level}"); tooltip completo via `FullBlessingTooltip` (`BlessingOffer.cs:519`), também hardcoded. |
| Estado | `RunState.RunBlessings` e `BloodBlessingCatalog`. |
| Controle | Os tooltips só funcionam com mouse (`MouseFilter.Pass`, sem foco). |

### 1.6 Log de combate

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:119-127` (RichTextLabel `GameLog`, ancorado embaixo à direita). |
| Atualização | `AppendGameLog(string)` (`Notifications.cs:9`): fila de 5 entradas com hora do sistema; reescreve `Text` inteiro. |
| CallGroup | `AppendGameLog` (BossController, PlayerCombat, RoomGenerator e RunManager). |
| Estilo | `Modulate` alfa 0.55 inline; sem StyleBox. |
| Px fixos | (-382,-132)-(-24,-24) = 358×108. `FitContent = false` e `ScrollActive = false`: o excedente é cortado. |
| Texto | As mensagens chegam prontas dos chamadores, todas hardcoded em português (ver §4). |

### 1.7 Toast ("+1 inimigo derrotado" e mensagens)

| Campo | Valor |
|---|---|
| Construtor | `StatusBars.cs:227-243` (Label `EnemyDefeatedToast`, âncora 0.5 × 0.28, ±170). |
| Exibição | `ShowEnemyDefeatedToast` (`Notifications.cs:159`): `async void` com tween; mata o tween anterior e confere `_toastTween == tween && IsInstanceValid(this)` ao final. |
| Chamadores | `NotifyEnemyDefeated`, `NotifyWaveIncoming`, `NotifySkillMessage`, `NotifyArtifactCollected` e `SelectBloodBlessing`. |
| Estilo | Tema puro. Sem fundo, só texto com outline. |
| Px fixos | 340×36, com animação de `offset_top` -18→-30. |
| Texto | hardcoded ~5 formatos. |

### 1.8 Banner "sala limpa"

| Campo | Valor |
|---|---|
| Construtor | `BuildRoomClearBanner` (`Notifications.cs:109`): ColorRect (0.08,0,0,0.72) ±280 × ±36, com âncora em 0.18. |
| Exibição | `ShowRoomClearBanner` (`:140`), `async void` com tween de modulate e escala. Disparado por `OnRoomCleared` (`StatusBars.cs:517`), que é chamado tanto pelo sinal `RunManager.RoomCleared` quanto por `NotifyRoomCleared` (CallGroup). A duplicação é contida por `_roomClearAnnounced`. |
| Estilo | Cor inline; sem moldura nem `HudPanel`. `Scale` animada sem `PivotOffset`: escala a partir do canto superior esquerdo. |
| Texto | loc 1 (`hud.room_clear`). |

### 1.9 Efeitos de tela

| Elemento | Local | Observação |
|---|---|---|
| Flash de dano | `StatusBars.cs:102-108` e `:399-401` | ColorRect em tela cheia; tween de `color:a`. |
| Vinheta de vida crítica | `StatusBars.cs:109-117` e `:454-470` | Recalculada a cada frame em `_Process`; é um ColorRect chapado (não tem gradiente de vinheta). |
| Número de dano recebido | `StatusBars.cs:404-420` | `async void`; Label criado e destruído a cada hit; fonte 34 inline; filho do CanvasLayer, sem o tema. |
| Absorção "SANGUIS PURGATUS" | `BlessingOffer.cs:240-254` e `:19-98` | Overlay em tela cheia + label com fonte 32 e cor inline. A animação também move nós 3D (essências) e cria partículas e materiais 3D dentro do HUD (`AttachSanguisTrail`, `SpawnSanguisArrivalPulse`). |
| FPS | `StatusBars.cs:46-55` | Só em debug (`OS.IsDebugBuild()`). |
| Dica de controles | `StatusBars.cs:199-211` | `Visible = false` e nunca é ligada: **código morto**. |

---

## 2. Overlays do HUD

Convenções da coluna "Pausa": **Modal** = entra em `RefreshModalPause`; **direto** = mexe em `GetTree().Paused` sem passar pelo áudio; **não** = o jogo continua rodando e só o input do jogador é bloqueado.

### 2.1 Pausa (menu principal de pausa)

| Campo | Valor |
|---|---|
| Construtor | `BuildPauseOverlay` em `HUD.Pause.cs:66`. ColorRect em tela cheia (0,0,0,0.68) → VBox `PausePanel` com margens de 48/42 → título → `PauseMainMenu` (420×440). |
| Abre | `PauseGame()` (`:417`), pela ação `pause` em `_UnhandledInput` quando não há overlay a fechar. |
| Fecha | `ResumeGame()` (`:428`, também esconde o Compêndio). Por botão "Continuar", por `ui_cancel` (`CloseTopmostOverlay`, `:456`) ou por `pause`. |
| Pausa | Modal: `SetModalPause(true)` (`:425`) e `RefreshModalPause` ao fechar. |
| Foco | `ShowPauseMainMenu` + `FocusFirst(_pauseMainMenu)` (`:266`, `:424`). Ao fechar, não devolve foco (volta ao jogo). |
| Botões | Continuar, Parâmetros de gameplay, Opções, Desistir da run (só com `RunState.IsRunActive`), Dicionário, Reiniciar teste (só em debug) e Voltar ao menu principal. Criados por `AddPauseMenuButton` (`:246`, 360×52). |
| Estilo | Tema; "Desistir" com `font_color` (1,0.32,0.32) inline (`:120`). Sem painel nem moldura: os botões flutuam sobre o escurecimento. |
| Texto | loc 1 (`hud.paused`); hardcoded 7 (botões). |
| Ações | `AbandonRun` (`:391`, despausa e chama `ForceDeath`), `RestartCurrentScene` e `ReturnToMainMenu` (autosave + troca de cena). |

### 2.2 Pausa → Parâmetros de gameplay (tuning)

| Campo | Valor |
|---|---|
| Construtor | Dentro de `BuildPauseOverlay` (`Pause.cs:129-243`): VBox `GameplayParametersPage` com subtítulo, status, `ScrollContainer` (mínimo de 420 px de altura) e 11 sliders (`AddPauseTuningSlider`, `:303`). Rodapé com 5 botões de 220×44 (Continuar, Dicionário, Reiniciar [debug], Salvar parâmetros, Voltar). |
| Abre/fecha | `ShowPausePage(_pauseTuningPage)` (`:253`). Volta por `ShowPauseMainMenu` (botão Voltar ou `ui_cancel`, `:440-444`). |
| Foco | `FocusFirst(page)`: o primeiro focável é o primeiro HSlider. Ao voltar, `FocusFirst(_pauseMainMenu)` cai em "Continuar", não no botão que abriu a página. |
| Estilo | Tema puro; nenhum override. |
| Px fixos | Linha de 66 px; label de valor com 88 px; botões de 220×44 e 180×44. |
| Texto | loc 2 (`hud.continue`, `hud.back`); hardcoded ~17 (11 títulos de slider, subtítulo, status e 3 botões, mais as mensagens de status). |
| Estado | `GameTuning.*Percent` / `Set*Percent` / `Save()`. |
| **Bug** | `AddPauseTuningSlider` aceita `maxValue` 180/200/250 (`:177-178`, `:184`), mas o handler faz `Mathf.Clamp(..., 1, 100)` (`:351`): o valor acima de 100 nunca é aplicado e o label mostra 100 enquanto o slider está além. |

### 2.3 Pausa → Opções

| Campo | Valor |
|---|---|
| Construtor | `BuildOptionsPage` (`Pause.cs:269`): título "OPCOES", OptionButton de resolução (360×46), CheckButton de tela cheia (360×44), 2 volumes (`AddVolumeOption`, `:293`, 460×64) e Voltar. |
| Abre/fecha | `ShowPausePage(_optionsPage)`; volta por Voltar ou `ui_cancel`. |
| Foco | `FocusFirst` → OptionButton de resolução. |
| Texto | loc 0; hardcoded 6. **Duplica** a página de Opções do MainMenu (§3.1), que é localizada e tem seletor de idioma. A pausa **não tem** troca de idioma. |
| Estado | `GlobalSettings.Instance`. |

### 2.4 Game over

| Campo | Valor |
|---|---|
| Construtor | `BuildGameOverOverlay` (`Pause.cs:9`): ColorRect em tela cheia (0.02,0,0,0.82) → VBox ±180 × ±85 → título, subtítulo e botão "Continuar no Ninho do Corvo" (220×44). |
| Abre | `OnPlayerDied` (`:362`), pelo sinal `Health.Died`. Esconde a Oferta de artefato, a Oferta de bênção, o Compêndio e a Pausa; **não esconde** Ficha, Fonte, Baú nem Bancada, que ficam acima no z-order. |
| Fecha | `ContinueAfterGameOver` (`:382`), que troca para o CrowsNest. |
| Pausa | `_isGameOver = true` e `GetTree().Paused = true` direto (`:379`); o áudio recebe só `PlayGameOverMusic`. |
| Cancel | `ui_cancel`, `pause` e `character_sheet` ficam desativados com `_isGameOver`. |
| Foco | `FocusFirst(_gameOverOverlay)` (`:371`). |
| Estilo | Tema puro; título no tamanho padrão (17), sem destaque. |
| Px fixos | Painel de 360×170; título com mínimo de 360×42. |
| Texto | loc 1 (`hud.game_over`); hardcoded 4 (subtítulo padrão, subtítulo formatado, botão e linha de log). |

### 2.5 Ficha de personagem

| Campo | Valor |
|---|---|
| Construtor | `BuildCharacterSheetOverlay` (`HUD.CharacterSheet.cs:9`): ColorRect com âncoras 0.02..0.98 × 0.04..0.96 (0.02,0,0,0.86) → título → HBox com 4 colunas (Bênçãos em ScrollContainer; Atributos, Inventário e Habilidades+Artefatos sem scroll). |
| Abre/fecha | `ToggleCharacterSheet` (`:85`), pela ação `character_sheet` (C). Fecha também por `ui_cancel`/`pause` (`CloseTopmostOverlay`, `Pause.cs:452`). Não tem botão de fechar. |
| Pausa | Modal: `SetModalPause(true)` / `RefreshModalPause`. |
| Foco | `FocusFirst` → primeiro botão de skill (os `Button` com `Flat` de `AddCharacterSheetSkills`, `:224`). Se não houver skills, nada recebe foco. As colunas Atributos e Inventário são Labels sem foco e **não rolam**: o conteúdo longo vaza ou é cortado. |
| Atualização | `RefreshCharacterSheet` (`:101`) destrói e recria todos os filhos das 4 colunas. **É chamado mesmo com a ficha fechada** por `SelectBloodBlessing`, `CollectArtifact`, `NotifyArtifactCollected`, `BuySkill`, `EquipArtifactInSelectedSlot`, `UnequipSelectedArtifactSlot`, `RefreshBlessingDisplays` e a cada `ValueChanged` de SpinBox do Compêndio (`Compendium.cs:424`). Só `RefreshVisibleCoinOverlays` (`StatusBars.cs:499`) checa a visibilidade. |
| Estilo | Tema puro; sem painéis por coluna; cor de fundo inline. |
| Px fixos | Larguras mínimas das colunas: 230/220/270/270; ícone de 38×38; espaçador de 10 px. |
| Texto | loc 0; hardcoded ~30 (títulos de coluna e seção, 12 linhas de atributo, "- nenhuma/nenhum", "vazio"). Nomes de atributo como "Forca", "Determinacao" estão sem acento. |
| Estado | `CharacterSheet` (atributos, multiplicadores, skills, artefatos), `RunState` (bênçãos, artefatos, multiplicadores), `GameTuning`, `SkillCatalog` e `BloodBlessingCatalog`. |

### 2.6 Fonte de Sangue (árvore de skills)

| Campo | Valor |
|---|---|
| Construtor | `BuildBloodFountainOverlay` (`HUD.BloodFountain.cs:9`): ColorRect em tela cheia (0.02,0,0,0.88), título, status, botão de cheat (só em debug), `SkillTreeCanvas` com 2 tabuleiros (`BuildSkillTreeBoard`, `:110`), popup de descrição e botão Fechar no canto superior direito. |
| Abre | `OpenBloodFountain()` (`HUD.Notifications.cs:76`), via `CallGroup` em `scripts/systems/BloodFountain.cs:22`. |
| Fecha | `CloseBloodFountain` (`:485`, botão) ou `CloseTopmostOverlay` (`Pause.cs:453`). |
| Pausa | **Não** pausa. `CloseTopmostOverlay` chama `RefreshModalPause`; o botão Fechar não chama. |
| Foco | `FocusFirst(_bloodFountainOverlay)` → primeiro focável na ordem da árvore. Como o cheat e o Fechar foram movidos para o fim (`:106-107`), o foco cai no primeiro nó de skill. A navegação por d-pad entre nós posicionados à mão depende do vizinho geométrico automático do Godot, sem `FocusNeighbor*` explícito. Os detalhes da skill só abrem com o **clique direito** (`:165-172`): pelo controle não há como abrir a descrição. Zoom e pan só com mouse. |
| Estilo | Sem `UIThemeFactory`. Nós = `Button` de 72×72 tingidos por `Modulate`, com 7 cores inline (`:323-336`) para os estados (não implementada, máximo, aprimorável, conflito, bloqueada, comprável, sem moedas). Links = `ColorRect` (0.55,0.08,0.08,0.8) (`:202`). |
| Px fixos | Grade de 130×112 a partir de y=170 (`:146-147`); tabuleiro com 372 px de largura; posição dos tabuleiros calculada **uma vez** a partir de `GetViewport().GetVisibleRect()` no build (`:82-86`), sem recalcular no resize; pan limitado a ±700/±420 e zoom a 0.65..1.65. |
| Texto | loc 0; hardcoded ~25 (título, status de compra ×8, estados ×7, popup, cheat e Fechar). Os nomes de árvore "Bestialis"/"Humanitas" são nomes próprios. |
| Estado | `SkillCatalog` (grade, pré-requisitos, exclusividade), `CharacterSheet` (rank, moedas) e `CompendiumTuning` (`{id}.cost`, `{id}.max_rank`). |

### 2.7 Popup de descrição de skill

| Campo | Valor |
|---|---|
| Construtor | `BuildSkillDescriptionPopup` (`BloodFountain.cs:373`): ColorRect com âncoras 0.22..0.78 × 0.14..0.86 (0.015,0,0,0.94), filho da Fonte → ScrollContainer → Label → botão Fechar (152×44). |
| Abre/fecha | `ShowSkillDescription` (`:432`, clique direito no nó). Fecha pelo botão (lambda, `:428`) ou por `ui_cancel` (primeiro item de `CloseTopmostOverlay`, `Pause.cs:438`). |
| Foco | Não recebe foco ao abrir e não devolve foco ao nó ao fechar. |
| Estilo | Inline; texto é um Label único, sem hierarquia (nome, árvore e tier no mesmo tamanho). |
| Texto | hardcoded ~12 (rótulos "Nivel:", "Custo...", "Estado:", "Pre-requisitos:" e estados). |

### 2.8 Baú de artefatos (hub)

| Campo | Valor |
|---|---|
| Construtor | `BuildArtifactChestOverlay` (`HUD.ArtifactChest.cs:9`): ColorRect ±520 × ±300 (0.02,0,0,0.88), título, status, coluna de slots e título de slots (**ambos com `Visible = false`**, `:56`, `:66`), título da coleção, ScrollContainer (56,150)-(984,470) e Fechar (180×40). |
| Abre | `OpenArtifactChest()` (`Notifications.cs:99`), via `CallGroup` em `scripts/systems/ArtifactChest.cs:22`. |
| Fecha | `CloseArtifactChest` (`:248`, botão) ou `CloseTopmostOverlay` (`Pause.cs:454`). |
| Pausa | Não pausa. |
| Foco | `FocusFirst` → primeiro botão visível (um item equipável da coleção ou o Fechar). Equipar chama `RefreshArtifactChest` → `RefreshArtifactCollectionList`, que faz `QueueFree` em todos os itens, inclusive no botão focado: **o foco do controle se perde** após cada equipar. |
| Funcional | Como os botões de slot estão invisíveis, `_selectedArtifactSlot` fica sempre 0 e "Equipar no slot N" só equipa no slot 1. |
| Estilo | Inline (fundo); slot selecionado indicado por `Modulate` (1,0.35,0.32) (`:170`). Itens são `Button`/`Label` com texto multilinha, sem ícone. |
| Px fixos | Painel de 1040×600; itens de 880×82 e 880×96; lista com 900 px de largura mínima. |
| Texto | loc 0; hardcoded ~14. |
| Estado | `CharacterSheet` (slots, `FoundArtifacts`), `RunState.RunArtifacts` e `ArtifactCatalog`. |

### 2.9 Oferta de artefato (altar de sala)

| Campo | Valor |
|---|---|
| Construtor | `BuildArtifactOfferOverlay` (`HUD.ArtifactOffer.cs:21`): ColorRect ±620 × ±330 (0.015,0,0,0.93), título, status, botão "Trocar oferta" com offsets absolutos (980,68)-(1180,106) e VBox de escolhas (56,112)-(1184,570). |
| Abre | `OpenArtifactOffer(RoomArtifact)` (`:9`), via `CallGroup` em `scripts/items/RoomArtifact.cs:41`. |
| Fecha | `CollectArtifact` (`:151`) ao escolher, ou `CloseTopmostOverlay` (`Pause.cs:445`), que fecha **sem consumir** o `RoomArtifact`: dá para reabrir, e a oferta é re-sorteada (`BuildArtifactOfferChoices` roda a cada `Open`). |
| Pausa | Direta: `GetTree().Paused = true/false` (`:17`, `:159`, `:96`) sem `SetModalPause`, ou seja, sem pausar o áudio. Ao fechar por cancel, o `RefreshModalPause` corrige. |
| Foco | `FocusFirst` → "Trocar oferta" se estiver habilitado (vem antes da lista na árvore), senão a primeira escolha. |
| Estilo | **Não usa** `ChoiceCard`: cada opção é um `Button` de 1080×112 com texto multilinha (`AddArtifactOfferButton`, `:113`), sem ícone e sem cor de raridade. A Oferta de bênção usa card com ícone e borda de raridade (§2.10): as duas telas são inconsistentes entre si. |
| Px fixos | Painel de 1240×660; botão de reroll absoluto, que **sobrepõe** a faixa do status (58..92 em largura total). |
| Texto | loc 0; hardcoded ~8. |
| Estado | `ArtifactCatalog.RollChoices`, `RunState`, `CharacterSheet.GetEquippedArtifacts` e `PlayerSkillController` (reroll, chance de 4ª opção). |

### 2.10 Oferta de Bênção de Sangue ("Hereditas Sanguinis")

| Campo | Valor |
|---|---|
| Construtor | `BuildBloodBlessingOverlay` (`HUD.BlessingOffer.cs:223`): ColorRect ±620 × ±340 (0.025,0,0.008,0.95) + `AddSimpleFrame`, título (fonte 27), status, "Trocar oferta" absoluto (980,68)-(1180,106) e VBox (56,116)-(1184,640). Cards em `BuildBloodBlessingChoices` (`:286`). |
| Abre | `OpenBloodBlessingOffer()` (`:9`, via `CallGroup` em `RunManager.cs:147`), com animação de absorção antes. `OpenMandatoryBloodBlessingOffer()` (`:14`) é chamado **diretamente** por `RunStartBlessingRoom.cs:37-38` (`GetFirstNodeInGroup("hud") is HUD`), sem animação. |
| Fecha | Só escolhendo (`SelectBloodBlessing`, `:401`). `ui_cancel` **não fecha**: reescreve o status e devolve o foco (`Pause.cs:446-451`). Se o catálogo estiver esgotado, converte em 25 moedas e libera a saída (`:295-303`). |
| Pausa | Direta: `GetTree().Paused = true` (`:306`) e `false` (`:410`, `:300`); o áudio não é pausado. Na variante não obrigatória, há também `Paused = false` no início (`:28`). |
| Foco | `FocusBlessingChoice(0)` → `GrabFocus` no primeiro "Escolher". `_Input` captura `ui_up`/`ui_down` e cicla só entre os botões "Escolher": com d-pad vertical **não se chega** a "Trocar oferta" (só por `ui_left`/`ui_right`, se o vizinho geométrico ajudar). |
| Estilo | `UIThemeFactory.ChoiceCard(BlessingRarityColor(rarity))` (`:318`) e `AddSimpleFrame`. Inline: RichTextLabel com `[font_size=22]` em BBCode (`:322`), cor de substituição `#ffb347` (`:312`), 11 cores de palavra-chave em BBCode `[hint]` (`FormatEffectKeywords`, `:484-517`), fontes 27 e 32 e cor do "SANGUIS PURGATUS" (`:252-253`, `:261`). |
| Px fixos | Card de 1080×125; ícone de 92×92; texto de 790×115 (escrito duas vezes: 1040 em `:323` e depois 790 em `:345`); botão de 135×82. Com 3 cards (~375 px), a lista cabe em 524 px; com mais linhas de descrição, o `FitContent` pode estourar. |
| Texto | loc 1 (`hud.blessing_choice`, logo sobrescrito em `:307`); hardcoded ~30 (título, status ×4, botões, logs, toast e 11 textos de hint). |
| Estado | `BloodBlessingCatalog.RollOffers`, `RunState` (grupos exclusivos, `AddBloodBlessing`, multiplicadores de vida), `CompendiumTuning` (`blessing.{id}.effect.{i}.value`) e `PlayerSkillController` (reroll). |
| Interação por mouse | Clique no RichTextLabel também escolhe (`GuiInput`, `:327-330`); hover mostra aviso de substituição (`:331-335`). |

### 2.11 Bancada de armas (hub)

| Campo | Valor |
|---|---|
| Construtor | `BuildWeaponBenchOverlay` (`HUD.WeaponBench.cs:18`): ColorRect com âncoras 0.25..0.75 × 0.18..0.82 (0.025,0.015,0.02,0.96) → VBox → título, status, 1 `Button` por arma (`WeaponCatalog.All`) e Fechar. |
| Abre | `OpenWeaponBench()` (`:9`), via `CallGroup` em `scripts/systems/WeaponBench.cs:31`. |
| Fecha | Fechar (lambda, `:55`) ou `CloseTopmostOverlay` (`Pause.cs:455`). |
| Pausa | Não pausa. |
| Foco | `FocusFirst` → primeira arma. Não indica qual arma está equipada no botão, só no label de status. |
| Estilo | Tema puro; fundo inline; sem moldura; título no tamanho padrão. |
| Texto | loc 1 (`hud.close`); hardcoded ~4. Os textos "{nome} — {descrição}" vêm do `WeaponCatalog` (C#, não localizado). |

### 2.12 Compêndio ("Dicionário") com editores de tuning

| Campo | Valor |
|---|---|
| Construtor | `BuildCompendiumOverlay` (`HUD.Compendium.cs:9`): ColorRect em tela cheia (0.015,0,0,0.94) → VBox com âncoras 0.04..0.96 × 0.05..0.95 → título, status, 5 abas (`AddCompendiumTab`, `:113`, 180×40), "Redefinir valores originais" (280×40), ScrollContainer (mínimo de 520 px de altura) e Fechar (220×44). |
| Abre | `OpenCompendium()` (`:127`, privado), a partir dos botões "Dicionário" da pausa e da página de tuning. |
| Fecha | `CloseCompendium` (`:138`) ou `CloseTopmostOverlay` (`Pause.cs:439`); `ResumeGame` também o esconde. |
| Pausa | Direta ao abrir (`:133`) e Modal ao fechar (`RefreshModalPause`). Como o compêndio só abre a partir da pausa, o estado resultante é correto. |
| Foco | `FocusFirst` → primeira aba. **Ao fechar, o foco não volta** ao botão "Dicionário" da pausa: com controle, o jogador fica sem foco até navegar. |
| Atualização | `RefreshCompendium` (`:151`) destrói e recria toda a lista a cada troca de aba e a cada reset. Cada `SpinBox.ValueChanged` chama `CompendiumTuning.SetNumber`, `RunState.RecalculateArtifactBonuses`, `CallGroup("player","ApplyRuntimeTuning")` e `RefreshCharacterSheet` (`:419-426`) a cada passo do spin. |
| Estilo | Aba selecionada marcada por `Modulate` (1,0.35,0.32) (`:157`, mesma cor do slot do Baú). Entradas são `PanelContainer` com o estilo do tema (`AddCompendiumEntry`, `:335`), ícone de 48×48 e 3 labels sem hierarquia tipográfica. `SpinBox` sem estilo no tema. |
| Px fixos | Entrada com mínimo de 96 px de altura; editor com label de 220 px e SpinBox de 120×32. |
| Texto | loc 0; hardcoded ~80: abas, status por aba, todas as entradas de Ataques e Inimigos (`:244-323`) e rótulos de editor. |
| Compatibilidade | Chaves do `CompendiumTuning` usadas aqui (`{artifact}.weight`, `{artifact}.effect.{i}.value`, `{skill}.cost`, `{skill}.max_rank`, `enemy.basic.*`, `enemy.blood_mage.*`, `attack.{light_1,light_2,light_3,special,dash}.{damage,recovery,range,width,knockback}`, `attack.dash.damage_multiplier`, `attack.fury.{damage_multiplier,drain_per_second,max_fury}`) **não podem mudar**. Os IDs de aba ("Artifacts", "Blessings", "Skills", "Attacks", "Enemies") são strings internas. |

---

## 3. Telas fora do HUD

### 3.1 Menu principal (`scripts/ui/MainMenu.cs`)

| Campo | Valor |
|---|---|
| Construtor | `BuildInterface` (`:33`): fundo ColorRect, glow radial (`BuildBackdropGlow`, `:71`), emblema `icon.png` 128×128, título (fonte 54, cor e sombra inline, `:45-49`), subtítulo, divisor e 4 páginas (`CreatePage`, `:113`, VBox 600×400 centralizado). |
| Páginas | **main** (Jogar, Opções, Créditos, Sair); **slots** (`BuildSlotsPage`, `:124`: 3 linhas com botão do slot e "Apagar" de 95×54); **options** (idioma, resolução, tela cheia e 2 volumes); **credits** (`BuildCreditsPage`, `:90`: RichTextLabel em Scroll de 600×300). |
| Navegação | `ShowPage(page)` (`:171`) + `FocusFirst(page)`. `_Input` (`:25`): `ui_cancel` em qualquer página que não seja a main volta para a main. Ao voltar, o foco cai no primeiro botão ("Jogar"), não no botão que abriu a página. |
| Pausa | Não se aplica. |
| Localização | 22 chamadas; escuta `LanguageChanged` → `RebuildForLanguage` (`:197`), que dá `Free()` em todos os filhos e reconstrói, mantendo Opções aberto. É o único consumidor de `LanguageChanged`. |
| Estilo | `Theme = UIThemeFactory.Create()`. Inline: título, subtítulo, emblema e divisor (`Modulate` e cor); sem painel atrás dos botões. |
| Px fixos | Botões de 500×54 (`:121`); OptionButton de 500×48; slider de 500×36; linha de slot de 600×54. |
| Texto | hardcoded: os itens de resolução (neutros), o nome do autor e a lista de créditos de terceiros (texto de licença; pode ficar fixo). Não há texto de UI em PT sem chave. |
| Estado | `SaveGameManager.Instance` (resumo, existe, apagar, `StartSlot`), `GlobalSettings.Instance` e `RunState.FailRun()`. |
| Ação destrutiva | "Apagar" exclui o slot **sem confirmação** (`:134`) e reconstrói a interface com `QueueFree` + build no mesmo frame (`RebuildInterface`, `:186`). |

### 3.2 Overlay do Altar do Sacrifício (`scripts/systems/IntermissionChamber.cs`)

| Campo | Valor |
|---|---|
| Construtor | `BuildInterface` (`:295`): **CanvasLayer próprio** `ChamberInterface` (Always) → `_statusLabel` (status da câmara, ver 3.3) e `_upgradeOverlay` (ColorRect em tela cheia (0.012,0,0.018,0.96), `Theme = UIThemeFactory.Create()`, uma segunda instância do tema) → `altarPanel` (ColorRect ±620 × ±340) + `AddSimpleFrame("AltarFrame")`, título (fonte 27), status, VBox de escolhas (56,120)-(1184,585) com separação de 14 e "Voltar" (180×42 no canto inferior direito). |
| Abre | `OpenUpgradeOverlay` (`:110`), por `interact` no `_Process` (`:56-66`) dentro da área do altar. Cards montados inline (`:139-192`). |
| Fecha | `CloseUpgradeOverlay` (`:289`, botão Voltar), `_Input` com `ui_cancel` (`:68-74`) ou ao escolher (`SacrificeForUpgrade`, `:267`). |
| Pausa | Direta: `GetTree().Paused = true/false` (`:196`, `:292`), sem áudio. **Fora** de `HUD.IsGameplayInputBlocked`/`RefreshModalPause`. |
| Foco | `FocusFirst(_upgradeOverlay)` → primeiro "Aprimorar". Não devolve foco ao fechar. |
| Estilo | `ChoiceCard(BlessingRarityColor(...))` (`:157`): `BlessingRarityColor` é **cópia literal** de `HUD.BlessingOffer.cs:384-392` (`:257-265`). Card de 1080×135 (o de bênção é 1080×125), ícone de 92×92, RichTextLabel de 790×120 com `[font_size=22]`, botão de 145×82 (o de bênção é 135×82). Fundo do painel = ColorRect inline em vez de `PanelContainer`. |
| Texto | loc 0; hardcoded ~20 (título, status ×2, "Aprimorar", "Voltar", previews, 7 mensagens de `SetMessage` e 4 prompts 3D). |
| Estado | `RunState` (bênçãos, `TryUpgradeBloodBlessing`, `SacrificeMaxHealth`, multiplicadores), `BloodBlessingCatalog`, `BloodBlessingProgression`, `CompendiumTuning` e `Health` do jogador. Notifica o HUD com `CallGroup("hud","RefreshBlessingDisplays")` (`:281`). |
| Interação com o HUD | O HUD **também** está na cena. Com o altar aberto, `ui_cancel` passa antes pelo `HUD._Input` (o HUD é filho e a raiz da cena recebe `_Input` por último). Sem overlay do HUD aberto, não há consumo e o altar fecha normalmente. Já a ação `pause` (Start/Options, se não for também `ui_cancel`) abre a pausa do HUD **sobre** o altar; ao retomar, `RefreshModalPause` despausa a árvore com o altar ainda aberto (o altar não entra no cálculo). **Precisa de verificação com controle.** |

### 3.3 Status da Câmara (título sobre a moldura do HUD)

| Campo | Valor |
|---|---|
| Construtor | `IntermissionChamber.cs:299-307`: Label no CanvasLayer `ChamberInterface`, **sem tema** (fora do `Root` do HUD e fora do `_upgradeOverlay`): usa a fonte e a cor padrão do Godot, sem outline. |
| Atualização | `RefreshStatus` (`:387`, a cada `HealthChanged`) e `SetMessage` (`:382`, 2 linhas). |
| Px fixos | ±390 × (22..88), com autowrap; a moldura do HUD por baixo tem ±280 × (14..58) (§1.3). |
| Texto | hardcoded ~9. |

### 3.4 Sala da Bênção Inicial (`scripts/systems/RunStartBlessingRoom.cs`)

| Campo | Valor |
|---|---|
| UI própria | Nenhuma 2D. Usa o `Label3D` "Prompt" da cena (texto no `.tscn`: "Interagir — revelar uma Bênção de Sangue", `RunStartBlessingRoom.tscn:186`) e chama `hud.OpenMandatoryBloodBlessingOffer()` (`:37-38`). |
| Contrato | Entra no grupo `run_manager` (`:15`) para receber `CallGroup("run_manager","UnlockExitAfterReward")` do HUD. |

---

## 4. Métodos públicos do HUD chamados por string (não renomear)

Os itens chamados por `CallGroup` só falham em runtime se renomeados. Antes de mudar qualquer assinatura, rode `rg '"NomeDoMetodo"' scripts`.

| Método (arquivo:linha) | Assinatura | Chamadores |
|---|---|---|
| `AppendGameLog` (`HUD.Notifications.cs:9`) | `(string)` | `BossController.cs:54`, `:348`; `PlayerCombat.cs:911`; `RoomGenerator.cs:315`; `RunManager.cs:97` |
| `SyncEnemyProgress` (`:17`) | `(int,int)` | `RunManager.cs:60` |
| `NotifyEnemyDefeated` (`:22`) | `(int,int,int)` | `RunManager.cs:116` |
| `NotifyBloodCoinsRewarded` (`:29`) | `(int,int)` | `RunManager.cs:127` |
| `SyncBloodCoins` (`:34`) | `(int)` | `RunManager.cs:59` |
| `NotifyRoomCleared` (`:39`) | `()` | `RunManager.cs:143` |
| `NotifyWaveIncoming` (`:45`) | `(int,float)` | `RoomGenerator.cs:316` |
| `BindBoss` (`:51`) | `(Node,string)` | `RunManager.cs:240` |
| `OpenBloodFountain` (`:76`) | `()` | `scripts/systems/BloodFountain.cs:22` |
| `NotifySkillMessage` (`:86`) | `(string)` | `PlayerCombat.cs:186`, `:192` |
| `OpenArtifactChest` (`:99`) | `()` | `scripts/systems/ArtifactChest.cs:22` |
| `OpenArtifactOffer` (`HUD.ArtifactOffer.cs:9`) | `(RoomArtifact)` | `scripts/items/RoomArtifact.cs:41` |
| `OpenBloodBlessingOffer` (`HUD.BlessingOffer.cs:9`) | `()` | `RunManager.cs:147` |
| `RefreshBlessingDisplays` (`HUD.BlessingBar.cs:94`) | `()` | `IntermissionChamber.cs:281` |
| `OpenWeaponBench` (`HUD.WeaponBench.cs:9`) | `()` | `scripts/systems/WeaponBench.cs:31` |

São 15 métodos, chamados em 19 pontos de `CallGroup("hud", ...)`.

**Chamadas diretas (tipadas; quebram em compilação, não em runtime):**
- `IsGameplayInputBlocked()`: `PlayerController.cs:96`, `PlayerCombat.cs:96` e `:429-430`.
- `OpenMandatoryBloodBlessingOffer()`: `RunStartBlessingRoom.cs:38`.

**Público mas sem chamador externo:**
- `NotifyArtifactCollected(string)` (`Notifications.cs:92`) só é chamado internamente (`ArtifactOffer.cs:155`). Pode ser mantido público por segurança.

**Contratos que o HUD consome por string** (não renomear do outro lado):
- `CallGroup("run_manager","UnlockExitAfterReward")` (ArtifactOffer:97, :160; BlessingOffer:301, :413), atendido por RunManager e RunStartBlessingRoom.
- `CallGroup("run_manager","AddBloodCoins",25)` (BlessingOffer:297).
- `CallGroup("player","ApplyRuntimeTuning")` (BlessingOffer:405; ArtifactOffer:154; BloodFountain:270; Compendium:423, :439).
- `CallGroup("audio_manager", "SetPaused"|"PlayEffect"|"PlayGameOverMusic")`.
- Grupos: `player`, `run_manager`, `blood_essences`.
- Nomes de nó do jogador: `Health`, `PlayerCombat`, `BloodBlessingRuntime` e `PlayerSkillController` (`StatusBars.cs:345-348`, `:437`; `BlessingOffer.cs:380-381`); `Health` também no boss (`Notifications.cs:53`).

---

## 5. Componentes repetidos (candidatos a componente comum)

| # | Padrão | Ocorrências | Divergências atuais | Proposta |
|---|---|---|---|---|
| C1 | **Card de escolha** (PanelContainer + `ChoiceCard(raridade)` + ícone 92 + RichTextLabel `[font_size=22]` + botão de ação) | `HUD.BlessingOffer.cs:313-359`; `IntermissionChamber.cs:152-190`; equivalente "pobre" (Button multilinha) em `HUD.ArtifactOffer.cs:113-123` | Altura 125 vs 135; botão 135 vs 145; texto 790×115 vs 790×120; clique no texto só na bênção; artefato sem ícone nem cor | `UiFactory.ChoiceCard(icon, title, meta, bodyBbcode, accent, actionText, onChoose)` |
| C2 | **Cor por raridade** | `BlessingRarityColor` em `HUD.BlessingOffer.cs:384` e cópia em `IntermissionChamber.cs:257`. Artefatos (`ArtifactOffer`, `ArtifactChest`, `Compendium`) não usam cor de raridade | Mapa idêntico duplicado; sem "common" explícito (cai em branco) | Token único `UiPalette.Rarity(string)` (inclui raridades de artefato) |
| C3 | **Overlay modal de escolha** (ColorRect 1240×660/680 centralizado + `AddSimpleFrame` + título 27 + status + "Trocar oferta" absoluto + VBox de lista) | `BlessingOffer.cs:223-284`; `ArtifactOffer.cs:21-77` (sem frame e sem fonte 27); `IntermissionChamber.cs:309-379` | Offsets 340 vs 330; reroll em posição absoluta (980,68); fundo ColorRect vs PanelContainer | `ModalPanel(title, subtitle)` com cabeçalho, corpo rolável e rodapé de ações |
| C4 | **Fundo de overlay** (ColorRect escuro com alfa) | Pausa (0,0,0,0.68), GameOver (.02,0,0,.82), Ficha (.02,0,0,.86), Fonte (.02,0,0,.88), Baú (.02,0,0,.88), Oferta de artefato (.015,0,0,.93), Bênção (.025,0,.008,.95), Bancada (.025,.015,.02,.96), Compêndio (.015,0,0,.94), Popup de skill (.015,0,0,.94), Altar (.012,0,.018,.96) | 11 cores/alfas diferentes; alguns em tela cheia e outros como "painel" sem borda | `Scrim` (tela cheia) + `Panel` (`HudPanel`/`AddSimpleFrame`) separados |
| C5 | **Título de tela** (Label centralizado, ancorado no topo com offset 18..56) | Ficha `:23`, Fonte `:27`, Baú `:27`, Oferta de artefato `:40`, Bênção `:256` (fonte 27), Altar `:334` (fonte 27), Compêndio `:35`, Pausa `:94`, GameOver `:37`, Bancada `:42` | Só 2 com tamanho maior; resto no tamanho padrão (17); sem dourado | `ScreenHeader(title, subtitle)` |
| C6 | **Label de status/subtítulo** de overlay | `_artifactChestStatusLabel`, `_artifactOfferStatusLabel`, `_bloodBlessingStatusLabel`, `_bloodFountainStatusLabel`, `_compendiumStatusLabel`, `_pauseTuningStatusLabel`, `_weaponBenchStatusLabel`, `_upgradeStatusLabel`, mais os `Set*Status` (`ArtifactChest:242`, `BloodFountain:340`, `Compendium:475`, `Pause:507`) | 4 setters quase idênticos | Parte do `ScreenHeader` ou `StatusLine` |
| C7 | **Botão de menu** (Button com tamanho fixo + Pressed) | `AddPauseMenuButton` (`Pause.cs:246`, 360×52); `AddMenuButton` (`MainMenu.cs:119`, 500×54); botões soltos de 220×44 (rodapé da pausa, GameOver, Compêndio), 180×40 a 180×44 (Fechar/Voltar) | 5 tamanhos; destrutivo só por `font_color` inline ("Desistir") | `MenuButton(text, action, variant: primary/secondary/danger)` |
| C8 | **Botão Fechar/Voltar** | Fonte `:90` (canto superior direito), Baú `:117` (rodapé central), Popup de skill `:415`, Bancada `:54` (dentro da lista), Compêndio `:102`, Altar `:366` (canto inferior direito), Opções `:290`, tuning `:235` | Posição varia por tela; 3 textos ("Fechar", "Voltar", `hud.close`/`hud.back`) | Rodapé padrão com posição fixa e dica do botão (B/Círculo) |
| C9 | **Lista rolável** (ScrollContainer + VBox) | Ficha (só a coluna de bênçãos), Baú, Compêndio, tuning, popup de skill, créditos | Nem todas as listas longas rolam (Ficha: Atributos, Inventário e Loadout; ofertas); sem estilo de scrollbar | `ScrollList()` com estilo de scrollbar no tema e `FollowFocus = true` |
| C10 | **Linha de slider rotulado** | `AddPauseTuningSlider` (`Pause.cs:303`); `AddVolumeOption` (`Pause.cs:293`); `AddVolumeOption` (`MainMenu.cs:163`) | 3 implementações; só o tuning mostra o valor | `LabeledSlider(label, min, max, value, onChange, showValue)` |
| C11 | **Página de opções** (resolução, tela cheia, volumes) | `Pause.cs:269-291` (hardcoded, sem idioma) e `MainMenu.cs:140-169` (localizada, com idioma) | Duas cópias divergentes | `OptionsPanel` compartilhado |
| C12 | **Seleção por `Modulate` vermelho (1,0.35,0.32)** | Aba do Compêndio `:157`; slot do Baú `:170` | Hack visual que também tinge o texto | Estado "selecionado" no tema (StyleBox `pressed`/toggle) |
| C13 | **Ícone de bênção com moldura** | Barra do HUD (`BlessingBar.cs:20-46`, 44 px + `simple_panel`); Ficha (`CharacterSheet.cs:175`, 38 px sem moldura); cards (92 px sem moldura) | 3 tamanhos e 2 estilos | `BlessingIcon(record, size, showLevel)` |
| C14 | **Tooltip de bênção** | `FullBlessingTooltip` (`BlessingOffer.cs:519`), usado na barra, na ficha e nos cards; `BuildUpgradeTooltip` (`IntermissionChamber.cs:237`) | Tooltip nativo, sem tema de `TooltipPanel`; inacessível pelo controle | Estilo de tooltip no tema + painel de detalhe focável para o controle |
| C15 | **Popup de detalhe** | Popup de skill (`BloodFountain.cs:373`); aviso de substituição no status da bênção | Só um popup real; sem padrão | `DetailPopup` (empilhável em `CloseTopmostOverlay`) |
| C16 | **Formatação de efeito** (valor + operação + %) | `FormatArtifactOperation` (`Compendium.cs:463`), `FormatBloodBlessingEffects` (`BlessingOffer.cs:416`), `GetDisplayedBlessingValue` (`BlessingOffer.cs:456`) vs `FormatUpgradeValue` (`IntermissionChamber.cs:244`) | Lógica de clamp, round e % duplicada; vírgula decimal só em `FormatLocalizedNumber` | Mover para um formatador estático único (fora da UI, ou `UiText`) |
| C17 | **Painel de HUD** (`AddHudPanel` + `HudPanel(alpha)`) | `StatusBars.cs:19-21` (3 molduras com alfas 0.9/0.78/0.72) | Só o HUD in-game usa `HudPanel` | Reaproveitar nos overlays, como `Panel` padrão |
| C18 | **Rótulo com sombra** (`font_shadow_color` + offset 2,2) | `StatusBars.cs:81-83`, `BlessingBar.cs:67-69` e título do `MainMenu.cs:47-48` | Overrides repetidos | Variação de tema (`theme_type_variation` "HudValueLabel") |

---

## 6. Pilha de overlays e ordem de fechamento

### 6.1 Ordem em `CloseTopmostOverlay()` (`HUD.Pause.cs:436-458`)

A ordem do código define o que é considerado "topo". Ela **não** segue a ordem de desenho (z) de `BuildLayout`.

| Prioridade | Condição | Ação | Pausa após |
|---|---|---|---|
| 1 | `_skillDescriptionPopup` visível | esconde | sem mudança (a Fonte não pausa) |
| 2 | `_compendiumOverlay` visível | esconde | `RefreshModalPause` (fica pausado se a Pausa estiver aberta) |
| 3 | Pausa visível **e** página de tuning ou Opções visível | `ShowPauseMainMenu` | continua pausado |
| 4 | `_artifactOfferOverlay` visível | esconde (sem consumir o `RoomArtifact`) | `RefreshModalPause` |
| 5 | `_bloodBlessingOverlay` visível | **não fecha**: status "Escolha uma Bencao..." e refoco | continua pausado |
| 6 | `_characterSheetOverlay` visível | esconde | `RefreshModalPause` |
| 7 | `_bloodFountainOverlay` visível | esconde | `RefreshModalPause` |
| 8 | `_artifactChestOverlay` visível | esconde | `RefreshModalPause` |
| 9 | `_weaponBenchOverlay` visível | esconde | `RefreshModalPause` |
| 10 | `_pauseOverlay` visível | `ResumeGame` | `RefreshModalPause` |
| — | nada visível | `false` (com `ui_cancel` não faz nada; com `pause` abre a pausa) | — |

Fora do HUD:
- **Altar da Câmara:** `IntermissionChamber._Input` fecha com `ui_cancel` quando o HUD não consumiu o evento.
- **MainMenu:** `_Input` volta para a página main.

### 6.2 Combinações possíveis de empilhamento e problemas

| Pilha | Como acontece | Observação |
|---|---|---|
| Pausa → Compêndio | Botão "Dicionário" na pausa ou no tuning | O fechamento devolve à pausa, mas **sem foco**. |
| Pausa → Ficha | `character_sheet` (C) com a pausa aberta: `_UnhandledInput` não checa a pausa | A ficha desenha acima da pausa. O cancel fecha primeiro a ficha (prioridade 6 < 10), mas o foco não volta à pausa. |
| Ficha → Pausa | `pause` com a ficha aberta | **Não acontece:** `pause` fecha a ficha (CloseTopmost) em vez de abrir a pausa. |
| Fonte → Popup de skill | Clique direito no nó | Correto (popup = prioridade 1). |
| Fonte/Baú/Bancada → Ficha | C dentro do hub com overlay aberto | A ficha (z acima da Fonte, abaixo do Baú e da Bancada) **fica atrás** do Baú e da Bancada, mas é fechada antes deles (prioridade 6). Inconsistência entre o z e a ordem de fechamento. |
| Oferta de bênção → Ficha | C durante a escolha | A ficha fica **atrás** da oferta (z menor) e é fechável, mas fica invisível ao jogador. A ficha usa `SetModalPause(true)`; a oferta já pausou direto. |
| Altar (Câmara) → Pausa | `pause` com o altar aberto (se `pause` ≠ `ui_cancel`) | Ao retomar, `RefreshModalPause` despausa com o altar visível. |
| Game over sobre tudo | `OnPlayerDied` | Esconde só Oferta de artefato, Bênção, Compêndio e Pausa; GameOver tem z abaixo da Ficha, Fonte, Baú, Bancada e Compêndio. |

Sugestão para a reforma: uma pilha explícita (`Stack<Control>`) com `PushOverlay(overlay, pauses, focusReturn)` / `PopOverlay()`. Ela unifica o z (`MoveToFront`), a ordem de fechamento, a pausa (substitui o `RefreshModalPause` por lista) e o retorno de foco.

---

## 7. Riscos técnicos para a reforma

### 7.1 async / await e tweens

| Local | Risco |
|---|---|
| `OpenBloodBlessingOfferInternal` (`BlessingOffer.cs:19-98`) | `async void` com laço de `await ProcessFrame` (`:87`) que só checa `IsInstanceValid(player)`, não o HUD; depois `await GetTree().CreateTimer(0.42f, true)` (`:91`): o `SceneTreeTimer` sobrevive à troca de cena e, ao retornar, acessa `_bloodAbsorptionOverlay` e chama `BuildBloodBlessingChoices` num HUD possivelmente liberado (exceção de objeto descartado). Não há guarda de reentrada: duas chamadas seguidas (ex.: `NotifyRoomCleared` duplicado) rodariam duas animações. |
| `ShowRoomClearBanner` (`Notifications.cs:140`) | `await` no tween do HUD; após o retorno, não checa `IsInstanceValid`. Baixo risco (o tween morre junto com o HUD), mas sem padrão. |
| `ShowReceivedDamageNumber` (`StatusBars.cs:404`) | Um Label + tween por hit; `async void`. Em hits múltiplos por frame, empilha labels na mesma posição. |
| `SpawnSanguisArrivalPulse` (`BlessingOffer.cs:144`) | O HUD cria nós 3D na cena atual; timer sem `processAlways`, com `IsInstanceValid(root)` (ok). Responsabilidade 3D dentro da UI. |
| Tweens de flash de dano (`StatusBars.cs:400`) | Novo tween a cada dano, sem matar o anterior; os tweens concorrentes disputam `color:a`. |
| `ShowEnemyDefeatedToast` | Correto (mata o anterior e confere `IsInstanceValid`). **É o padrão a copiar.** |

### 7.2 Alocações e trabalho por frame

- `_Process` (`HUD.cs:191-202`): string do FPS por frame (só em debug); `new Color` da vinheta por frame (struct, barato); `_roomFrame.Visible`/`_roomDivider.Visible` reatribuídos a cada frame (barato, mas desnecessário).
- `UpdateCombinedHealthDisplay` faz `GetNodeOrNull<BloodBlessingRuntime>` a cada mudança de vida ou barreira (`StatusBars.cs:437`): convém cachear.
- O laço de absorção calcula a posição de N essências por frame em C#; é aceitável, mas vive no HUD.

### 7.3 Nós recriados a cada refresh

| Função | Gatilho | Efeito colateral |
|---|---|---|
| `RefreshCharacterSheet` (`CharacterSheet.cs:101`) | Ver §2.5: roda **mesmo com a ficha fechada**, inclusive a cada passo de SpinBox do Compêndio | Recria ~40 Labels/Buttons; perde o foco se estiver aberta |
| `RefreshBlessingHud` (`BlessingBar.cs:9`) | Escolha de bênção e altar | Recria até 8 ícones (`ResourceLoader.Load` usa cache) |
| `RefreshCompendium` (`Compendium.cs:151`) | Troca de aba e reset | Recria dezenas de cards e SpinBoxes |
| `RefreshArtifactCollectionList` (`ArtifactChest.cs:176`) | Abrir, equipar e selecionar slot | **Perde o foco do controle** (o botão focado é liberado) |
| `BuildBloodBlessingChoices` / `BuildArtifactOfferChoices` | Abrir e reroll | Ok (refoca na bênção; no artefato **não refoca** após o reroll) |
| `OpenUpgradeOverlay` (Câmara) | Abrir | `RemoveChild` + `QueueFree` (ok) |
| `MainMenu.RebuildInterface` / `RebuildForLanguage` | Apagar slot e trocar idioma | Reconstrói a tela toda (o segundo usa `Free()` imediato) |

Ponto de atenção: `QueueFree` seguido de `FocusFirst` no mesmo frame pode focar um nó que está na fila de remoção (`FindChildren` ainda o encontra). Hoje isso não ocorre porque nenhum desses fluxos chama `FocusFirst` depois do refresh, mas a reforma vai querer chamar: use `RemoveChild` + `QueueFree`, como a Câmara faz.

### 7.4 Dependências por string ou nome de nó

- `GetTree().CurrentScene.SceneFilePath.EndsWith("CrowsNest.tscn")` decide se o HUD de combate aparece (`StatusBars.cs:86`).
- `GetTree().CurrentScene is IntermissionChamber` mantém a moldura do título sempre visível (`StatusBars.cs:198`).
- Nós filhos do jogador: `"Health"`, `"PlayerCombat"`, `"BloodBlessingRuntime"` e `"PlayerSkillController"`; do boss, `"Health"`.
- `IntermissionChamber`: `"Player"`, `"BloodFountain/InteractionArea"`, `"SacrificeAltar/InteractionArea"` e os `.../Prompt` (`:31-37`). `RunStartBlessingRoom`: `"BloodFountain/InteractionArea"`, `"BloodFountain/Prompt"`, `"ExitPortal"` e `"ExitPortal/PortalArea"`.
- IDs de aba do Compêndio, chaves do `CompendiumTuning` (§2.12) e ID especial `"orlok_rats_blessing"` (`IntermissionChamber.cs:206`, `:216`).
- Regex de palavras-chave em português (`BlessingOffer.cs:486`, `:498`) e substituição numérica sobre a descrição (`FormatBloodBlessingDescription`, `:429`) dependem do **texto em PT** das descrições: a localização das descrições quebra o destaque e a substituição de valores.
- Nome do boss reconstruído com `Split('—')` (`Notifications.cs:66`).
- Propriedades de tween por string: `"color:a"`, `"modulate"`, `"modulate:a"`, `"offset_top"`, `"offset_bottom"`, `"position:y"` e `"scale"`.

### 7.5 Outros pontos para o plano de reforma

- **Localização:** o HUD não escuta `LanguageChanged`, mas como a troca de idioma só existe no MainMenu, hoje não há troca com o HUD visível. Se a pausa ganhar seletor de idioma (C11), o HUD vai precisar reconstruir os textos.
- **Pausa inconsistente:** Oferta de artefato, Oferta de bênção, Compêndio, Game over e Altar alteram `GetTree().Paused` direto, sem `SetPaused` no áudio. Centralizar em `SetModalPause` ou numa pilha (§6).
- **Viewport:** o layout da Fonte usa a largura do viewport só no build (`BloodFountain.cs:82`); as ofertas e o altar usam painéis fixos de 1240×660/680, que cabem em 1280×720 com apenas 20 px de margem lateral.
- **Código morto ou inerte:** `_controlsHintLabel` (sempre oculto); a coluna de slots do Baú (oculta, deixando "Equipar no slot N" sempre no slot 1); `_furyBar` com `CustomMinimumSize` de 360 (ok).
- **Tema incompleto:** faltam ScrollBar, SpinBox, CheckButton, ProgressBar, Tooltip e `theme_type_variation` para título, valor e perigo. Isso explica boa parte dos overrides inline (18 em StatusBars).
- **Contagem de overrides inline por arquivo** (grep):

  | Arquivo | `AddTheme*Override` | `new Color(` | `new StyleBoxFlat` | `UIThemeFactory.*` |
  |---|---|---|---|---|
  | StatusBars | 18 | 22 | 8 | 3 |
  | BlessingOffer | 4 | 16 | 0 | 2 |
  | BloodFountain | 0 | 10 | 0 | 0 |
  | MainMenu | 3 | 8 | 0 | 2 |
  | IntermissionChamber | 3 | 8 | 0 | 3 |
  | BlessingBar | 6 | 2 | 0 | 1 |
  | Pause | 1 | 3 | 0 | 0 |
  | ArtifactChest | 0 | 2 | 0 | 0 |
  | Compendium | 0 | 2 | 0 | 0 |
  | ArtifactOffer | 0 | 1 | 0 | 0 |
  | CharacterSheet | 0 | 1 | 0 | 0 |
  | Notifications | 0 | 1 | 0 | 0 |
  | WeaponBench | 0 | 1 | 0 | 0 |

- **Localização (resumo):** UI com ~250 strings hardcoded em PT contra 34 chamadas `Localization` (22 delas no MainMenu). Chaves existentes: `hud.{back,blessing_choice,blood_coins,boss,close,continue,enemies,game_over,paused,room_clear}` e as de `menu.*`, `options.*`, `credits.*` e `game.*`.

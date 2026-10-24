using Godot;
using System;
using System.Collections.Generic;

// Pausa, opções e game over (docs/design/ui-guia.md §7, §8.10, §8.11).
// A pausa continua como camada legada (IsLegacyPauseRequested lê _pauseOverlay.Visible): as demais telas ainda são
// legadas e um Push faria MoveToFront da pausa por cima do compêndio/ficha. Confirmações e game over entram na UiModalStack.
public partial class HUD
{
	// Causa usada por AbandonRun: o game over troca "derrotado por" por "desistiu".
	private const string AbandonRunDeathSource = "abandon_run";
	private const float GameOverButtonWidth = 280.0f;
	private const float GameOverWidth = 760.0f;
	// Quantas bênçãos entram no ranking de dano do game over (a primeira em destaque).
	private const int GameOverBlessingRankSize = 3;

	private UiWindow _pauseWindow;
	private VBoxContainer _pauseMainPage;
	private VBoxContainer _pauseOptionsPage;
	private Button _pauseContinueButton;
	private Button _pauseCompendiumButton;
	private Button _pauseAbandonButton;
	private Button _pauseMainMenuButton;
	private Button _pauseOptionsButton;
	private Control _pauseReturnFocus;
	private UiConfirm _pauseConfirm;
	private bool _pauseCompendiumHooked;
	private UiWindow _gameOverWindow;
	private Label _gameOverCauseLabel;
	private Label _gameOverLastHitLabel;
	private Label _gameOverReachedLabel;
	private GridContainer _gameOverStats;
	private VBoxContainer _gameOverBlessings;
	private Button _gameOverRebirthButton;

	// ---------- Game over (§8.11) ----------

	private void BuildGameOverOverlay(Control root)
	{
		_gameOverWindow = UiComponents.Window(root, "GameOverOverlay", WindowSize.Small, Localization.Text("hud.game_over"));
		_gameOverOverlay = _gameOverWindow.Root;
		_gameOverOverlay.Visible = false;

		// Small alargada para caber a grade de estatísticas em duas colunas; título Display.
		var panel = _gameOverWindow.Panel;
		panel.OffsetLeft = -GameOverWidth * 0.5f;
		panel.OffsetRight = GameOverWidth * 0.5f;
		panel.CustomMinimumSize = new Vector2(GameOverWidth, 0.0f);
		var title = _gameOverWindow.Title;
		title.ThemeTypeVariation = UiVariation.DisplayLabel;
		title.Uppercase = true;
		if (title.GetParent() is Control titleRow)
			titleRow.CustomMinimumSize = new Vector2(0.0f, 84.0f);

		var body = _gameOverWindow.Body;
		_gameOverCauseLabel = MakeGameOverLabel(UiVariation.BodyLabel);
		body.AddChild(_gameOverCauseLabel);
		_gameOverLastHitLabel = MakeGameOverLabel(UiVariation.SecondaryLabel);
		body.AddChild(_gameOverLastHitLabel);
		_gameOverReachedLabel = MakeGameOverLabel(UiVariation.SecondaryLabel);
		body.AddChild(_gameOverReachedLabel);

		// Estatísticas: duas colunas de pares rótulo/valor dentro de um painel rebaixado.
		var statsPanel = new PanelContainer { ThemeTypeVariation = UiVariation.InsetPanel, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddChild(statsPanel);
		_gameOverStats = new GridContainer { Name = "GameOverStats", Columns = 4 };
		_gameOverStats.AddThemeConstantOverride("h_separation", UiMetrics.S5);
		_gameOverStats.AddThemeConstantOverride("v_separation", UiMetrics.S2);
		statsPanel.AddChild(_gameOverStats);

		_gameOverBlessings = new VBoxContainer { Name = "GameOverBlessings" };
		_gameOverBlessings.AddThemeConstantOverride("separation", UiMetrics.S2);
		body.AddChild(_gameOverBlessings);

		var actions = _gameOverWindow.Actions;
		actions.Visible = true;
		actions.Alignment = BoxContainer.AlignmentMode.Center;
		_gameOverRebirthButton = UiComponents.MenuButton(Localization.Text("gameover.rebirth"), ContinueAfterGameOver, ButtonVariant.Primary, ButtonSize.Medium);
		_gameOverRebirthButton.Name = "RebirthButton";
		_gameOverRebirthButton.CustomMinimumSize = new Vector2(GameOverButtonWidth, _gameOverRebirthButton.CustomMinimumSize.Y);
		actions.AddChild(_gameOverRebirthButton);

		_gameOverWindow.Footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"));
	}

	private static Label MakeGameOverLabel(string variation)
	{
		return new Label
		{
			ThemeTypeVariation = variation,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(560.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
	}

	private void OnPlayerDied()
	{
		GetTree().CallGroup("audio_manager", "PlayEffect", "death");
		GetTree().CallGroup("audio_manager", "PlayGameOverMusic");
		_isGameOver = true;
		var source = _runtimeHealth?.LastDamageSource;
		var finalDamage = _runtimeHealth?.LastDamageAmount ?? 0.0f;
		if (source == AbandonRunDeathSource)
			DialogueProgress.FlagAbandon();
		AppendGameLog($"Virgil recebeu {Mathf.RoundToInt(finalDamage)} de dano fatal de {source}.");
		ShowGameOver(source, finalDamage);
	}

	// Igual a OnPlayerDied, sem áudio, log nem jogador real (causa e números fictícios).
	private void ShowPreviewGameOver()
	{
		_isGameOver = true;
		EnsurePreviewRunActive();
		var previewBlessings = new List<string>();
		foreach (var definition in BloodBlessingCatalog.All)
		{
			if (previewBlessings.Count >= GameOverBlessingRankSize) break;
			previewBlessings.Add(definition.Id);
		}
		RunStats.SeedPreview(previewBlessings);
		ShowGameOver(Localization.Text("boss.armand.name"), 42.0f);
	}

	// Preenche a janela com os dados da run (antes do FailRun), fecha tudo e entra no topo da pilha.
	// Pausa a árvore, mas não a música (pausesAudio: false; a fonte legada também exclui o game over).
	private void ShowGameOver(string source, float finalDamage)
	{
		var abandoned = source == AbandonRunDeathSource;
		_gameOverCauseLabel.Text = abandoned
			? Localization.Text("gameover.abandoned")
			: Localization.TextFormat("gameover.defeated_by", string.IsNullOrWhiteSpace(source) ? Localization.Text("gameover.unknown_cause") : source);
		var lastHit = Mathf.RoundToInt(finalDamage);
		_gameOverLastHitLabel.Text = Localization.TextFormat("gameover.last_hit", lastHit);
		_gameOverLastHitLabel.Visible = !abandoned && lastHit > 0;
		FillGameOverStats();

		HideOverlaysForGameOver();
		UiModalStack.Clear();
		UiModalStack.Push(_gameOverOverlay, pauses: true, initialFocus: _gameOverRebirthButton, mandatory: true, pausesAudio: false);
		_gameOverWindow.FadeIn();
	}

	// Lê RunState e RunStats antes do FailRun (que zera os dois; roda em ContinueAfterGameOver).
	private void FillGameOverStats()
	{
		ClearChildren(_gameOverStats);
		ClearChildren(_gameOverBlessings);

		_gameOverReachedLabel.Visible = RunState.IsRunActive;
		if (RunState.IsRunActive)
		{
			var map = RunState.CurrentMap;
			var room = RunState.IsBossRoom ? Localization.Text("hud.room.boss") : Localization.TextFormat("hud.room.progress", RunState.CurrentRoom, map.CommonRoomCount);
			_gameOverReachedLabel.Text = Localization.TextFormat("gameover.reached", map.Name, room);
		}

		AddGameOverStat(Localization.Text("gameover.stat.damage_dealt"), FormatStatNumber(RunStats.DamageDealt));
		AddGameOverStat(Localization.Text("gameover.stat.biggest_hit"), FormatStatNumber(RunStats.BiggestHit));
		AddGameOverStat(Localization.Text("gameover.stat.damage_taken"), FormatStatNumber(RunStats.DamageTaken));
		AddGameOverStat(Localization.Text("gameover.stat.healing"), FormatStatNumber(RunStats.HealingDone));
		AddGameOverStat(Localization.Text("gameover.stat.kills"), RunStats.EnemiesKilled.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.elites"), RunStats.ElitesKilled.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.crits"), RunStats.CriticalHits.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.rooms"), RunStats.RoomsCleared.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.blessings"), RunState.RunBlessings.Count.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.artifacts"), RunState.RunArtifacts.Count.ToString("N0"));
		// Moedas de Sangue desta run: o saldo só zera no FailRun. O Sangue Amaldiçoado permanente não entra.
		AddGameOverStat(Localization.Text("gameover.stat.coins"), RunState.RunBloodCoins.ToString("N0"));
		AddGameOverStat(Localization.Text("gameover.stat.time"), FormatRunTime(RunStats.ElapsedSeconds));

		FillGameOverBlessingRanking();
	}

	private void AddGameOverStat(string label, string value)
	{
		_gameOverStats.AddChild(new Label
		{
			Text = label,
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		});
		var valueLabel = new Label
		{
			Text = value,
			ThemeTypeVariation = UiVariation.BodyLabel,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		valueLabel.AddThemeColorOverride("font_color", UiPalette.Gold);
		_gameOverStats.AddChild(valueLabel);
	}

	// Bênção mais letal em destaque (ícone + nome + dano e % do total) e as seguintes numa linha menor cada.
	private void FillGameOverBlessingRanking()
	{
		var ranking = RunStats.GetBlessingDamageRanking();
		_gameOverBlessings.Visible = ranking.Count > 0;
		if (ranking.Count == 0)
			return;

		var header = new Label
		{
			Text = Localization.Text("gameover.top_blessing"),
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_gameOverBlessings.AddChild(header);

		var total = Mathf.Max(RunStats.DamageDealt, 1.0f);
		for (var i = 0; i < ranking.Count && i < GameOverBlessingRankSize; i++)
		{
			var (blessingId, damage) = ranking[i];
			var record = FindRunBlessingRecord(blessingId);
			var top = i == 0;
			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", UiMetrics.S3);
			var icon = UiComponents.BlessingIcon(record, top ? UiMetrics.IconM : UiMetrics.IconS, top);
			icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(icon);
			var name = new Label
			{
				Text = record.DisplayName,
				ThemeTypeVariation = top ? UiVariation.BodyLabel : UiVariation.SecondaryLabel,
				VerticalAlignment = VerticalAlignment.Center
			};
			name.AddThemeColorOverride("font_color", UiComponents.RarityColor(record.Rarity));
			row.AddChild(name);
			var percent = Mathf.RoundToInt(Mathf.Clamp(damage / total, 0.0f, 1.0f) * 100.0f);
			row.AddChild(new Label
			{
				Text = Localization.TextFormat("gameover.blessing_damage", FormatStatNumber(damage), percent),
				ThemeTypeVariation = UiVariation.SecondaryLabel,
				VerticalAlignment = VerticalAlignment.Center
			});
			_gameOverBlessings.AddChild(row);
		}
	}

	// Registro da run (raridade e nível do ícone); bênção que já saiu da run (grupo exclusivo) cai no catálogo.
	private static BloodBlessingRecord FindRunBlessingRecord(string blessingId)
	{
		foreach (var blessing in RunState.RunBlessings)
			if (blessing.Id == blessingId)
				return blessing;
		var name = BloodBlessingCatalog.TryGet(blessingId, out var definition) ? definition.DisplayName : blessingId;
		return new BloodBlessingRecord(blessingId, name, 1);
	}

	private static string FormatStatNumber(float value) => Mathf.RoundToInt(value).ToString("N0");

	private static string FormatRunTime(float seconds)
	{
		var time = TimeSpan.FromSeconds(Mathf.Max(seconds, 0.0f));
		return time.TotalHours >= 1.0 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	// Game over fica sozinho: esconde as telas legadas e o HUD de combate (§8.11).
	private void HideOverlaysForGameOver()
	{
		if (_pauseConfirm != null && _pauseConfirm.IsOpen)
			_pauseConfirm.Cancel();
		// Valores do Dicionário (debug) pendentes do debounce entram antes de a tela sumir.
		FlushCompendiumApply();
		foreach (var overlay in new[]
		{
			_pauseOverlay, _bloodBlessingOverlay, _anvilOverlay, _compendiumOverlay, _characterSheetOverlay,
			_bloodFountainOverlay, _weaponBenchOverlay,
			_playerHudFrame, _blessingHudList, _bossHealthPanel
		})
		{
			if (overlay != null)
				overlay.Visible = false;
		}
	}

	// "Renascer": encerra a run e volta ao Ninho do Corvo (a run já terminou, sem confirmação).
	private void ContinueAfterGameOver()
	{
		RunState.FailRun();
		SaveGameManager.RequestAutosave();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile(GameMode.Scene("res://scenes/game/CrowsNest.tscn"));
	}

	// ---------- Pausa (§8.10) ----------

	private void BuildPauseOverlay(Control root)
	{
		_pauseWindow = UiComponents.Window(root, "PauseOverlay", WindowSize.Medium, PauseTitle());
		_pauseOverlay = _pauseWindow.Root;
		_pauseOverlay.Visible = false;

		BuildPauseMainPage();
		_pauseOptionsPage = AddPausePage("PauseOptionsPage");
		var optionsPanel = UiComponents.OptionsPanel(true);
		_pauseOptionsPage.AddChild(optionsPanel);
		// Voltar explícito (além do rodapé), como nas opções do menu principal.
		_pauseOptionsPage.AddChild(UiComponents.Separator());
		var optionsBack = AddPauseMenuButton(_pauseOptionsPage, Localization.Text("hud.back"), ShowPauseMainMenu);
		optionsBack.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		optionsPanel.SetFooterTarget(optionsBack);
	}

	// Troca de idioma com a pausa aberta (opções): recria a janela no mesmo índice e volta à página em que estava.
	private void RebuildPauseForLanguage()
	{
		if (_pauseOverlay == null || _hudRoot == null || _pauseOverlay.GetParent() != _hudRoot)
			return;
		var wasVisible = IsVisible(_pauseOverlay);
		var onOptions = wasVisible && IsVisible(_pauseOptionsPage);
		if (_pauseConfirm != null && _pauseConfirm.IsOpen)
			_pauseConfirm.Cancel();
		var index = _pauseOverlay.GetIndex();
		var old = _pauseOverlay;
		_hudRoot.RemoveChild(old);
		old.QueueFree();
		BuildPauseOverlay(_hudRoot);
		_hudRoot.MoveChild(_pauseOverlay, index);
		if (!wasVisible)
			return;
		var runActive = RunState.IsRunActive;
		_pauseAbandonButton.Visible = runActive;
		_pauseMainMenuButton.ThemeTypeVariation = runActive ? UiVariation.DestructiveButton : UiVariation.SecondaryButton;
		_pauseOverlay.Visible = true;
		if (onOptions)
			ShowPausePage(_pauseOptionsPage, _pauseOptionsButton);
		else
			ShowPauseMainMenu();
		RefreshModalPause();
	}

	private VBoxContainer AddPausePage(string name)
	{
		var page = new VBoxContainer { Name = name, Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		page.AddThemeConstantOverride("separation", UiMetrics.S2);
		_pauseWindow.Body.AddChild(page);
		return page;
	}

	// "PAUSADO" (hud.paused) vira "Pausa" (§8.10); chave nova até a fase 4.
	private static string PauseTitle() => Localization.Text("pause.title");

	// Ordem: Continuar · Opções · Dicionário · [DEV: painel F1, reiniciar] · [divisor] · Desistir · Menu principal.
	private void BuildPauseMainPage()
	{
		_pauseMainPage = AddPausePage("PauseMainPage");
		var column = new VBoxContainer
		{
			Name = "PauseMainMenu",
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(460.0f, 0.0f)
		};
		column.AddThemeConstantOverride("separation", UiMetrics.S2);
		_pauseMainPage.AddChild(column);

		_pauseContinueButton = AddPauseMenuButton(column, Localization.Text("hud.continue"), ResumeGame, ButtonVariant.Primary);
		Button optionsButton = null;
		optionsButton = AddPauseMenuButton(column, Localization.Text("menu.options"), () => ShowPausePage(_pauseOptionsPage, optionsButton));
		_pauseOptionsButton = optionsButton;
		_pauseCompendiumButton = AddPauseMenuButton(column, Localization.Text("codex.title"), OpenCompendiumFromPause);

		if (OS.IsDebugBuild())
		{
			column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, UiMetrics.S1), MouseFilter = Control.MouseFilterEnum.Ignore });
			var dev = UiComponents.DevSection();
			column.AddChild(dev.Root);
			// Recarregar a sala mantém o RunState e permite repetir recompensas; só em debug.
			AddPauseMenuButton(dev.Body, "Painel de dev (F1)", OpenDevPanel);
			AddPauseMenuButton(dev.Body, "Reiniciar teste", RestartCurrentScene);
		}

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, UiMetrics.S1), MouseFilter = Control.MouseFilterEnum.Ignore });
		column.AddChild(UiComponents.Separator());
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, UiMetrics.S1), MouseFilter = Control.MouseFilterEnum.Ignore });
		_pauseAbandonButton = AddPauseMenuButton(column, Localization.Text("pause.abandon_run"), RequestAbandonRun, ButtonVariant.Destructive);
		_pauseAbandonButton.Name = "AbandonRunButton";
		_pauseMainMenuButton = AddPauseMenuButton(column, Localization.Text("pause.main_menu"), RequestReturnToMainMenu);
	}

	private Button AddPauseMenuButton(Container parent, string text, Action action, ButtonVariant variant = ButtonVariant.Secondary)
	{
		var button = UiComponents.MenuButton(text, action, variant, ButtonSize.Large);
		parent.AddChild(button);
		return button;
	}

	private void PauseGame()
	{
		var runActive = RunState.IsRunActive;
		_pauseAbandonButton.Visible = runActive;
		// Sem run ativa, voltar ao menu não perde nada: botão comum e sem confirmação.
		_pauseMainMenuButton.ThemeTypeVariation = runActive ? UiVariation.DestructiveButton : UiVariation.SecondaryButton;
		_pauseReturnFocus = null;
		_pauseWindow.FadeIn();
		ShowPauseMainMenu();
		SetModalPause(true);
	}

	private void ResumeGame()
	{
		if (_pauseConfirm != null && _pauseConfirm.IsOpen)
			_pauseConfirm.Cancel();
		if (IsVisible(_compendiumOverlay))
			CloseCompendium();
		_pauseOverlay.Visible = false;
		RefreshModalPause();
	}

	// Subpágina (opções): `opener` recebe o foco de volta em ShowPauseMainMenu.
	private void ShowPausePage(Control page, Control opener)
	{
		if (page == null)
			return;
		_pauseReturnFocus = opener;
		SetPausePageVisible(page);
		_pauseWindow.SetTitle(Localization.Text("options.title"));
		_pauseWindow.SetStatus(null);
		SetPauseFooter(page);
		// Opções: a vizinhança de foco é do próprio painel (abas); o ciclo plano daqui a sobrescreveria.
		var focusables = page == _pauseOptionsPage ? new List<Control>() : ApplyPauseFocusCycle(page);
		SetPauseWindowWide(page == _pauseOptionsPage);
		var first = page == _pauseOptionsPage ? UiComponents.FindOptionsPanel(page)?.PreferredFocus : focusables.Count > 0 ? focusables[0] : null;
		ControllerUiSupport.FocusInitial(first, page);
	}

	// Opções: rótulo 240 + controle 280 + valor 64 + barra de rolagem não cabem nos 640 da janela média.
	private void SetPauseWindowWide(bool wide)
	{
		_pauseWindow.Panel.CustomMinimumSize = new Vector2(wide ? 704.0f : 640.0f, 0.0f);
	}

	private void ShowPauseMainMenu()
	{
		SetPausePageVisible(_pauseMainPage);
		SetPauseWindowWide(false);
		_pauseWindow.SetTitle(PauseTitle());
		_pauseWindow.SetStatus(null);
		SetPauseFooter(_pauseMainPage);
		ApplyPauseFocusCycle(_pauseMainPage);
		var target = _pauseReturnFocus;
		_pauseReturnFocus = null;
		ControllerUiSupport.FocusInitial(ControllerUiSupport.CanTakeFocus(target) ? target : _pauseContinueButton, _pauseMainPage);
	}

	private void SetPausePageVisible(Control page)
	{
		foreach (var candidate in new Control[] { _pauseMainPage, _pauseOptionsPage })
		{
			if (candidate != null)
				candidate.Visible = candidate == page;
		}
	}

	private void SetPauseFooter(Control page)
	{
		var footer = _pauseWindow.Footer;
		footer.Clear();
		if (page == _pauseMainPage)
		{
			footer.Add(UiAction.Cancel, Localization.Text("hud.continue"), ResumeGame, left: true);
			footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"));
			return;
		}
		footer.Add(UiAction.Cancel, Localization.Text("hud.back"), ShowPauseMainMenu, left: true);
	}

	// Ciclo vertical explícito entre os controles visíveis da página (evita o foco escapar pela geometria).
	private static List<Control> ApplyPauseFocusCycle(Control page)
	{
		var focusables = new List<Control>();
		foreach (var node in page.FindChildren("*", "Control", true, false))
		{
			if (node is Control control && ControllerUiSupport.CanTakeFocus(control))
				focusables.Add(control);
		}
		ControllerUiSupport.SetVerticalCycle(focusables);
		return focusables;
	}

	// O compêndio (legado) abre por cima; ao fechar, o foco volta ao botão "Compêndio" (§8.12).
	private void OpenCompendiumFromPause()
	{
		if (_compendiumOverlay != null && !_pauseCompendiumHooked)
		{
			_pauseCompendiumHooked = true;
			_compendiumOverlay.VisibilityChanged += OnCompendiumVisibilityChangedForPause;
		}
		OpenCompendium();
	}

	private void OnCompendiumVisibilityChangedForPause()
	{
		if (_compendiumOverlay.Visible)
			return;
		// Deferred: ResumeGame esconde o compêndio e logo depois a pausa; só refoca se a pausa continuar aberta.
		Callable.From(() =>
		{
			if (IsVisible(_pauseOverlay) && IsVisible(_pauseMainPage) && ControllerUiSupport.CanTakeFocus(_pauseCompendiumButton))
				_pauseCompendiumButton.GrabFocus();
		}).CallDeferred();
	}

	// ---------- Confirmações (§3.3) ----------

	private void RequestAbandonRun()
	{
		if (!RunState.IsRunActive || _runtimeHealth == null || _runtimeHealth.IsDead)
			return;
		OpenAbandonRunConfirm();
	}

	private void OpenAbandonRunConfirm()
	{
		OpenPauseConfirm(
			Localization.Text("ui.confirm.abandon_run.title"),
			Localization.Text("ui.confirm.abandon_run.body"),
			Localization.Text("pause.abandon_run"),
			AbandonRun);
	}

	private void RequestReturnToMainMenu()
	{
		if (!RunState.IsRunActive)
		{
			ReturnToMainMenu();
			return;
		}
		OpenMainMenuConfirm();
	}

	private void OpenMainMenuConfirm()
	{
		OpenPauseConfirm(
			Localization.Text("ui.confirm.main_menu.title"),
			Localization.Text("ui.confirm.main_menu.body"),
			Localization.Text("pause.main_menu"),
			ReturnToMainMenu);
	}

	// Confirmação por cima da pausa, na pilha modal (CloseTopmostOverlay → CancelTop fecha ela primeiro).
	// A ação roda deferred: o Confirm devolve o foco (deferred) ao botão de origem antes de a pausa sumir.
	// O rodapé da pausa ("Continuar") some enquanto a confirmação está aberta (o espaço é mantido: a janela não pula).
	private void OpenPauseConfirm(string title, string body, string confirmText, Action onConfirm)
	{
		if (_pauseConfirm != null && _pauseConfirm.IsOpen)
			return;
		SetPauseFooterInert(true);
		var confirm = UiComponents.Confirm(_pauseOverlay, title, body, confirmText, () =>
		{
			SetPauseFooterInert(false);
			Callable.From(onConfirm).CallDeferred();
		}, ConfirmKind.Destructive, () => SetPauseFooterInert(false));
		_pauseConfirm = confirm;
		ControllerUiSupport.SetHorizontalCycle(new Control[] { confirm.CancelButton, confirm.ConfirmButton });
		UiModalStack.Push(confirm.Window.Root, pauses: true, initialFocus: confirm.CancelButton, onCancel: confirm.Cancel);
	}

	private void SetPauseFooterInert(bool inert)
	{
		if (_pauseWindow == null)
			return;
		// Só visual: o scrim da confirmação (tela inteira, MouseFilter Stop) já bloqueia o clique nas dicas de baixo.
		_pauseWindow.Footer.Root.Modulate = inert ? Colors.Transparent : Colors.White;
	}

	// ---------- Ações ----------

	private void AbandonRun()
	{
		if (!RunState.IsRunActive || _runtimeHealth == null || _runtimeHealth.IsDead)
			return;

		_pauseOverlay.Visible = false;
		UiModalStack.ApplyPause(false);
		_runtimeHealth.SetInvincible(false);
		_runtimeHealth.ForceDeath(AbandonRunDeathSource);
	}

	private void RestartCurrentScene()
	{
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ReloadCurrentScene();
	}

	private void ReturnToMainMenu()
	{
		// Run em andamento (pausa -> menu): encerra como desistencia aqui, senao o LoadSlot do menu a conta como morte.
		if (RunState.IsRunActive)
		{
			DialogueProgress.FlagAbandon();
			RunState.FailRun();
		}
		// Grava já: o menu lê o slot logo em seguida (sem isso mostraria dados velhos).
		SaveGameManager.RequestAutosave();
		SaveGameManager.Instance?.FlushPendingSave();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
	}

	// ---------- Fluxo de pausa (contrato §5.2; não renomear) ----------

	private bool CloseTopmostOverlay()
	{
		// Telas migradas para a pilha modal fecham primeiro; depois segue a cadeia legada.
		// Ofertas (obrigatórias: só avisam), ficha, Fonte, baú e bancada já respondem pelo CancelTop.
		if (UiModalStack.CancelTop()) return true;
		if (IsVisible(_pauseOverlay) && !IsVisible(_pauseMainPage))
		{
			ShowPauseMainMenu();
			return true;
		}
		if (IsVisible(_pauseOverlay)) { ResumeGame(); return true; }
		return false;
	}

	private void SetModalPause(bool paused)
	{
		UiModalStack.ApplyPause(paused);
	}

	// Pilha modal + fonte legada (IsLegacyPauseRequested, em HUD.cs).
	private void RefreshModalPause()
	{
		UiModalStack.RefreshPause();
	}

	// ---------- Pré-visualização (debug, UiPreview) ----------

	private bool OpenPausePreviewScreen(string screen)
	{
		switch (screen)
		{
			case "pause":
				EnsurePreviewRunActive();
				PauseGame();
				return true;
			case "options":
			case "options_video":
			case "options_audio":
			case "options_game":
			case "options_confirm":
			case "options_video_end":
				PauseGame();
				var optionsView = UiComponents.FindOptionsPanel(_pauseOptionsPage);
				optionsView?.SelectTab(screen == "options_audio" ? OptionsPanelView.TabAudio : screen == "options_game" ? OptionsPanelView.TabGame : OptionsPanelView.TabVideo);
				ShowPausePage(_pauseOptionsPage, null);
				if (screen == "options_confirm")
					optionsView?.PreviewKeepConfirm();
				else if (screen == "options_video_end")
					optionsView?.PreviewScrollToEnd();
				return true;
			case "dev_panel":
				return OpenDevPanelPreview();
			case "dev_panel_actions":
				return OpenDevPanelPreview(true);
			case "dev_console":
				return OpenDevConsolePreview();
			case "pause_confirm_abandon":
				EnsurePreviewRunActive();
				PauseGame();
				OpenAbandonRunConfirm();
				return true;
			case "pause_confirm_menu":
				EnsurePreviewRunActive();
				PauseGame();
				OpenMainMenuConfirm();
				return true;
			case "game_over":
				ShowPreviewGameOver();
				return true;
		}
		return false;
	}

	// Preview da pausa como numa run (Desistir visível, Menu principal destrutivo). Run volátil; sem slot carregado nada é gravado.
	private static void EnsurePreviewRunActive()
	{
		if (!RunState.IsRunActive)
			RunState.StartRun();
	}
}

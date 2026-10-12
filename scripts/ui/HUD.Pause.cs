using Godot;
using System;
using System.Collections.Generic;

// Pausa, opções, ajustes de tuning (debug) e game over (docs/design/ui-guia.md §7, §8.10, §8.11).
// A pausa continua como camada legada (IsLegacyPauseRequested lê _pauseOverlay.Visible): as demais telas ainda são
// legadas e um Push faria MoveToFront da pausa por cima do compêndio/ficha. Confirmações e game over entram na UiModalStack.
public partial class HUD
{
	// Causa usada por AbandonRun: o game over troca "derrotado por" por "desistiu".
	private const string AbandonRunDeathSource = "abandon_run";
	private const float GameOverButtonWidth = 280.0f;

	private sealed class PauseTuningEntry
	{
		public UiOptionRow Row;
		public Func<int> Get;
	}

	private UiWindow _pauseWindow;
	private VBoxContainer _pauseMainPage;
	private VBoxContainer _pauseOptionsPage;
	private VBoxContainer _pauseTuningPage;
	private Button _pauseContinueButton;
	private Button _pauseCompendiumButton;
	private Button _pauseAbandonButton;
	private Button _pauseMainMenuButton;
	private Button _pauseOptionsButton;
	private Control _pauseReturnFocus;
	private UiConfirm _pauseConfirm;
	private bool _pauseCompendiumHooked;
	private readonly List<PauseTuningEntry> _pauseTuningEntries = new();
	private UiWindow _gameOverWindow;
	private Label _gameOverCauseLabel;
	private Label _gameOverLastHitLabel;
	private VBoxContainer _gameOverSummary;
	private Button _gameOverRetryButton;
	private Button _gameOverMenuButton;

	// ---------- Game over (§8.11) ----------

	private void BuildGameOverOverlay(Control root)
	{
		_gameOverWindow = UiComponents.Window(root, "GameOverOverlay", WindowSize.Small, Localization.Text("hud.game_over"));
		_gameOverOverlay = _gameOverWindow.Root;
		_gameOverOverlay.Visible = false;

		// Small com 640 de largura (§8.11) e título Display.
		var panel = _gameOverWindow.Panel;
		panel.OffsetLeft = -320.0f;
		panel.OffsetRight = 320.0f;
		panel.CustomMinimumSize = new Vector2(640.0f, 0.0f);
		var title = _gameOverWindow.Title;
		title.ThemeTypeVariation = UiVariation.DisplayLabel;
		title.Uppercase = true;
		if (title.GetParent() is Control titleRow)
			titleRow.CustomMinimumSize = new Vector2(0.0f, 84.0f);
		_gameOverWindow.SetStatus(Localization.Text("gameover.subtitle"));

		var body = _gameOverWindow.Body;
		_gameOverCauseLabel = MakeGameOverLabel(UiVariation.BodyLabel);
		body.AddChild(_gameOverCauseLabel);
		_gameOverLastHitLabel = MakeGameOverLabel(UiVariation.BodyLabel);
		body.AddChild(_gameOverLastHitLabel);
		_gameOverSummary = new VBoxContainer { Name = "GameOverSummary" };
		_gameOverSummary.AddThemeConstantOverride("separation", UiMetrics.S1);
		body.AddChild(_gameOverSummary);

		var actions = _gameOverWindow.Actions;
		actions.Visible = true;
		actions.Alignment = BoxContainer.AlignmentMode.Center;
		_gameOverRetryButton = UiComponents.MenuButton(Localization.Text("gameover.return_nest"), ContinueAfterGameOver, ButtonVariant.Primary, ButtonSize.Medium);
		_gameOverRetryButton.Name = "RestartButton";
		actions.AddChild(_gameOverRetryButton);
		_gameOverMenuButton = UiComponents.MenuButton(Localization.Text("gameover.main_menu"), ReturnToMainMenuAfterGameOver, ButtonVariant.Secondary, ButtonSize.Medium);
		_gameOverMenuButton.Name = "GameOverMainMenuButton";
		actions.AddChild(_gameOverMenuButton);
		// Mesma largura mínima nos dois botões (o rótulo do primeiro é mais longo).
		foreach (var button in new[] { _gameOverRetryButton, _gameOverMenuButton })
			button.CustomMinimumSize = new Vector2(GameOverButtonWidth, button.CustomMinimumSize.Y);
		ControllerUiSupport.SetHorizontalCycle(new Control[] { _gameOverRetryButton, _gameOverMenuButton });

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

	// Igual a OnPlayerDied, sem áudio, log nem jogador real (causa fictícia).
	private void ShowPreviewGameOver()
	{
		_isGameOver = true;
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
		FillGameOverSummary();

		HideOverlaysForGameOver();
		UiModalStack.Clear();
		UiModalStack.Push(_gameOverOverlay, pauses: true, initialFocus: _gameOverRetryButton, mandatory: true, pausesAudio: false);
		_gameOverWindow.FadeIn();
	}

	// Resumo com dados já existentes em RunState/CharacterSheet; linha sem dado é omitida.
	private void FillGameOverSummary()
	{
		foreach (var child in _gameOverSummary.GetChildren())
		{
			_gameOverSummary.RemoveChild(child);
			child.QueueFree();
		}

		if (RunState.IsRunActive)
		{
			var map = RunState.CurrentMap;
			var room = RunState.IsBossRoom ? Localization.Text("hud.room.boss") : Localization.TextFormat("hud.room.progress", RunState.CurrentRoom, map.CommonRoomCount);
			AddGameOverSummaryLine(Localization.TextFormat("gameover.reached", map.Name, room));
			AddGameOverSummaryLine(Localization.TextFormat("gameover.run_summary", RunState.RunBlessings.Count, RunState.RunArtifacts.Count));
		}
		// Moedas de Sangue desta run (RunState.RunBloodCoins), lidas aqui: o saldo só zera em RunState.FailRun, que roda
		// depois (ContinueAfterGameOver / ReturnToMainMenuAfterGameOver). O Sangue Amaldiçoado permanente não entra no resumo.
		AddGameOverSummaryLine(Localization.TextFormat("hud.blood_coins", RunState.RunBloodCoins));
	}

	private void AddGameOverSummaryLine(string text)
	{
		var label = MakeGameOverLabel(UiVariation.SecondaryLabel);
		label.Text = text;
		_gameOverSummary.AddChild(label);
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
			_pauseOverlay, _bloodBlessingOverlay, _compendiumOverlay, _characterSheetOverlay,
			_bloodFountainOverlay, _weaponBenchOverlay,
			_playerHudFrame, _blessingHudList, _bossHealthPanel
		})
		{
			if (overlay != null)
				overlay.Visible = false;
		}
	}

	private void ContinueAfterGameOver()
	{
		RunState.FailRun();
		SaveGameManager.RequestAutosave();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
	}

	// A run já terminou: sem confirmação (§8.11).
	private void ReturnToMainMenuAfterGameOver()
	{
		RunState.FailRun();
		ReturnToMainMenu();
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
		if (OS.IsDebugBuild())
			BuildPauseTuningPage();
		RefreshPauseTuningValues();
	}

	// Troca de idioma com a pausa aberta (opções): recria a janela no mesmo índice e volta à página em que estava.
	private void RebuildPauseForLanguage()
	{
		if (_pauseOverlay == null || _hudRoot == null || _pauseOverlay.GetParent() != _hudRoot)
			return;
		var wasVisible = IsVisible(_pauseOverlay);
		var onOptions = wasVisible && IsVisible(_pauseOptionsPage);
		var onTuning = wasVisible && IsVisible(_pauseTuningPage);
		if (_pauseConfirm != null && _pauseConfirm.IsOpen)
			_pauseConfirm.Cancel();
		_pauseTuningEntries.Clear();
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
		else if (onTuning && _pauseTuningPage != null)
			ShowPausePage(_pauseTuningPage, null);
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

	// Ordem: Continuar · Opções · Dicionário · [DEV: ajustes, reiniciar] · [divisor] · Desistir · Menu principal.
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
			Button tuningButton = null;
			tuningButton = AddPauseMenuButton(dev.Body, "Ajustes de gameplay", () => ShowPausePage(_pauseTuningPage, tuningButton));
			// Recarregar a sala mantém o RunState e permite repetir recompensas; só em debug.
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

	// Ferramenta de dev (§7): seção DEV, sliders com valor visível; rodapé só "Voltar" e "Salvar".
	private void BuildPauseTuningPage()
	{
		_pauseTuningPage = AddPausePage("GameplayParametersPage");
		var dev = UiComponents.DevSection();
		_pauseTuningPage.AddChild(dev.Root);
		dev.Body.AddChild(UiComponents.MakeLabel("Ajustes temporários aplicados em tempo real. Salve para torná-los o padrão.", UiVariation.SecondaryLabel, true));

		var scroll = UiComponents.ScrollList(400.0f, out var list);
		scroll.Name = "PauseTuningScroll";
		list.AddThemeConstantOverride("separation", UiMetrics.S1);
		dev.Body.AddChild(scroll);

		AddPauseTuningSlider(list, "Velocidade de andar", () => GameTuning.WalkSpeedPercent, GameTuning.SetWalkSpeedPercent);
		AddPauseTuningSlider(list, "Velocidade de correr", () => GameTuning.RunSpeedPercent, GameTuning.SetRunSpeedPercent);
		AddPauseTuningSlider(list, "Velocidade do dash", () => GameTuning.DashSpeedPercent, GameTuning.SetDashSpeedPercent);
		AddPauseTuningSlider(list, "Distância do dash", () => GameTuning.DashDistancePercent, GameTuning.SetDashDistancePercent, 180);
		AddPauseTuningSlider(list, "Distância do zoom da câmera", () => GameTuning.CameraZoomDistancePercent, GameTuning.SetCameraZoomDistancePercent, 200);
		AddPauseTuningSlider(list, "Dano do jogador", () => GameTuning.DamagePercent, GameTuning.SetDamagePercent);
		AddPauseTuningSlider(list, "Vida máxima", () => GameTuning.HealthPercent, GameTuning.SetHealthPercent);
		AddPauseTuningSlider(list, "Velocidade da animação", () => GameTuning.AnimationSpeedPercent, GameTuning.SetAnimationSpeedPercent);
		AddPauseTuningSlider(list, "Velocidade do ataque", () => GameTuning.AttackSpeedPercent, GameTuning.SetAttackSpeedPercent);
		AddPauseTuningSlider(list, "Delay entre ataques", () => GameTuning.AttackDelayPercent, GameTuning.SetAttackDelayPercent);
		AddPauseTuningSlider(list, "Distância do ataque", () => GameTuning.AttackRangePercent, GameTuning.SetAttackRangePercent, 250);
	}

	private void AddPauseTuningSlider(VBoxContainer list, string title, Func<int> getter, Action<int> setter, int maxValue = 100)
	{
		var row = UiComponents.LabeledSlider(title, 1.0, maxValue, getter(), value =>
		{
			// Clamp na faixa do próprio slider (antes era sempre 100, prendendo dash/zoom/alcance).
			var percent = Mathf.Clamp(Mathf.RoundToInt((float)value), 1, maxValue);
			setter(percent);
			SetPauseTuningStatus($"Ajuste temporário aplicado: {title} = {percent}");
		});
		list.AddChild(row.Root);
		_pauseTuningEntries.Add(new PauseTuningEntry { Row = row, Get = getter });
	}

	private void PauseGame()
	{
		RefreshPauseTuningValues();
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

	// Subpágina (opções, ajustes): `opener` recebe o foco de volta em ShowPauseMainMenu.
	private void ShowPausePage(Control page, Control opener)
	{
		if (page == null)
			return;
		_pauseReturnFocus = opener;
		SetPausePageVisible(page);
		_pauseWindow.SetTitle(page == _pauseOptionsPage ? Localization.Text("options.title") : "Ajustes de gameplay");
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
		foreach (var candidate in new Control[] { _pauseMainPage, _pauseOptionsPage, _pauseTuningPage })
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
		if (page == _pauseTuningPage)
			footer.Add(UiAction.Alt, "Salvar", SavePauseTuning);
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

	// ui_alt (R / Y / △) = "Salvar" na página de ajustes. Chamado pelo HUD._UnhandledInput (RouteModalShortcut) com a pausa
	// visível e nada empilhado: tecla/controle vai só ao Control focado e não chegaria ao GuiInput da raiz da pausa.
	private bool HandlePauseShortcut(InputEvent @event)
	{
		if (!IsVisible(_pauseTuningPage) || !InputMap.HasAction("ui_alt") || !@event.IsActionPressed("ui_alt") || @event.IsEcho())
			return false;
		SavePauseTuning();
		return true;
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

	// ---------- Ajustes de tuning ----------

	private void SavePauseTuning()
	{
		GameTuning.Save();
		SetPauseTuningStatus("Parâmetros salvos como padrão definitivo.");
	}

	// Sem sinal: abrir a pausa não reaplica os valores nem troca o status.
	private void RefreshPauseTuningValues()
	{
		foreach (var entry in _pauseTuningEntries)
		{
			var value = entry.Get();
			entry.Row.Slider.SetValueNoSignal(value);
			entry.Row.Value.Text = value.ToString();
		}
	}

	private void SetPauseTuningStatus(string text)
	{
		if (IsVisible(_pauseTuningPage))
			_pauseWindow.SetStatus(text);
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
			case "tuning":
			case "pause_tuning":
				PauseGame();
				ShowPausePage(_pauseTuningPage, null);
				return true;
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

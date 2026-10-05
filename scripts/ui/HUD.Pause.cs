using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildGameOverOverlay(Control root)
	{
		_gameOverOverlay = new ColorRect
		{
			Name = "GameOverOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.82f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_gameOverOverlay);

		var panel = new VBoxContainer
		{
			Name = "GameOverPanel",
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -180.0f,
			OffsetTop = -85.0f,
			OffsetRight = 180.0f,
			OffsetBottom = 85.0f,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		_gameOverOverlay.AddChild(panel);

		var title = new Label
		{
			Name = "GameOverTitle",
			Text = Localization.Text("hud.game_over"),
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 42.0f)
		};
		panel.AddChild(title);

		_gameOverSubtitle = new Label
		{
			Name = "GameOverSubtitle",
			Text = "Virgil retornou ao caixao.",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 32.0f)
		};
		panel.AddChild(_gameOverSubtitle);

		var restartButton = new Button
		{
			Name = "RestartButton",
			Text = "Continuar no Ninho do Corvo",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		restartButton.Pressed += ContinueAfterGameOver;
		panel.AddChild(restartButton);
	}

	private void BuildPauseOverlay(Control root)
	{
		_pauseOverlay = new ColorRect
		{
			Name = "PauseOverlay",
			Color = new Color(0.0f, 0.0f, 0.0f, 0.68f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_pauseOverlay);

		var panel = new VBoxContainer
		{
			Name = "PausePanel",
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 48.0f,
			OffsetTop = 42.0f,
			OffsetRight = -48.0f,
			OffsetBottom = -42.0f,
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_pauseOverlay.AddChild(panel);

		var title = new Label
		{
			Name = "PauseTitle",
			Text = Localization.Text("hud.paused"),
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 42.0f)
		};
		panel.AddChild(title);

		_pauseMainMenu = new VBoxContainer
		{
			Name = "PauseMainMenu", Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(420, 440)
		};
		panel.AddChild(_pauseMainMenu);
		AddPauseMenuButton(_pauseMainMenu, "Continuar", ResumeGame);
		AddPauseMenuButton(_pauseMainMenu, "Parametros de gameplay", () => ShowPausePage(_pauseTuningPage));
		AddPauseMenuButton(_pauseMainMenu, "Opcoes", () => ShowPausePage(_optionsPage));
		_abandonRunButton = new Button
		{
			Name = "AbandonRunButton",
			Text = "Desistir da run",
			CustomMinimumSize = new Vector2(360, 52)
		};
		_abandonRunButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.32f, 0.32f));
		_abandonRunButton.Pressed += AbandonRun;
		_pauseMainMenu.AddChild(_abandonRunButton);
		AddPauseMenuButton(_pauseMainMenu, "Dicionario", OpenCompendium);
		// Recarregar a sala mantem o RunState e permite repetir recompensas; so em debug.
		if (OS.IsDebugBuild())
			AddPauseMenuButton(_pauseMainMenu, "Reiniciar teste", RestartCurrentScene);
		AddPauseMenuButton(_pauseMainMenu, "Voltar ao menu principal", ReturnToMainMenu);

		_pauseTuningPage = new VBoxContainer
		{
			Name = "GameplayParametersPage", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false
		};
		panel.AddChild(_pauseTuningPage);
		BuildOptionsPage(panel);

		var subtitle = new Label
		{
			Name = "PauseSubtitle",
			Text = "Ajuste os parametros em tempo real. A faixa de cada controle aparece no proprio slider.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 36.0f)
		};
		_pauseTuningPage.AddChild(subtitle);

		_pauseTuningStatusLabel = new Label
		{
			Name = "PauseTuningStatus",
			Text = "Nenhuma alteracao salva ainda.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		};
		_pauseTuningPage.AddChild(_pauseTuningStatusLabel);

		_pauseTuningScroll = new ScrollContainer
		{
			Name = "PauseTuningScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 420.0f)
		};
		_pauseTuningPage.AddChild(_pauseTuningScroll);

		_pauseTuningList = new VBoxContainer
		{
			Name = "PauseTuningList",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_pauseTuningScroll.AddChild(_pauseTuningList);

		AddPauseTuningSlider("Velocidade de andar", GameTuning.WalkSpeedPercent, GameTuning.SetWalkSpeedPercent, out _walkSpeedSlider, out _walkSpeedValueLabel);
		AddPauseTuningSlider("Velocidade de correr", GameTuning.RunSpeedPercent, GameTuning.SetRunSpeedPercent, out _runSpeedSlider, out _runSpeedValueLabel);
		AddPauseTuningSlider("Velocidade do dash", GameTuning.DashSpeedPercent, GameTuning.SetDashSpeedPercent, out _dashSpeedSlider, out _dashSpeedValueLabel);
		AddPauseTuningSlider("Distancia do dash", GameTuning.DashDistancePercent, GameTuning.SetDashDistancePercent, out _dashDistanceSlider, out _dashDistanceValueLabel, 180);
		AddPauseTuningSlider("Distancia do zoom da camera", GameTuning.CameraZoomDistancePercent, GameTuning.SetCameraZoomDistancePercent, out _cameraZoomDistanceSlider, out _cameraZoomDistanceValueLabel, 200);
		AddPauseTuningSlider("Dano do jogador", GameTuning.DamagePercent, GameTuning.SetDamagePercent, out _damageSlider, out _damageValueLabel);
		AddPauseTuningSlider("Vida maxima", GameTuning.HealthPercent, GameTuning.SetHealthPercent, out _healthSlider, out _healthValueLabel);
		AddPauseTuningSlider("Velocidade da animacao", GameTuning.AnimationSpeedPercent, GameTuning.SetAnimationSpeedPercent, out _animationSpeedSlider, out _animationSpeedValueLabel);
		AddPauseTuningSlider("Velocidade do ataque", GameTuning.AttackSpeedPercent, GameTuning.SetAttackSpeedPercent, out _attackSpeedSlider, out _attackSpeedValueLabel);
		AddPauseTuningSlider("Delay entre ataques", GameTuning.AttackDelayPercent, GameTuning.SetAttackDelayPercent, out _attackDelaySlider, out _attackDelayValueLabel);
		AddPauseTuningSlider("Distancia do ataque", GameTuning.AttackRangePercent, GameTuning.SetAttackRangePercent, out _attackRangeSlider, out _attackRangeValueLabel, 250);

		var footer = new HBoxContainer
		{
			Name = "PauseFooter",
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 56.0f)
		};
		_pauseTuningPage.AddChild(footer);

		var resumeButton = new Button
		{
			Name = "ResumeButton",
			Text = Localization.Text("hud.continue"),
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		resumeButton.Pressed += ResumeGame;
		footer.AddChild(resumeButton);

		var compendiumButton = new Button
		{
			Name = "OpenCompendiumButton",
			Text = "Dicionario",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		compendiumButton.Pressed += OpenCompendium;
		footer.AddChild(compendiumButton);

		var restartButton = new Button
		{
			Name = "PauseRestartButton",
			Text = "Reiniciar teste",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			Visible = OS.IsDebugBuild()
		};
		restartButton.Pressed += RestartCurrentScene;
		footer.AddChild(restartButton);

		var saveButton = new Button
		{
			Name = "SaveTuningButton",
			Text = "Salvar parametros",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		saveButton.Pressed += SavePauseTuning;
		footer.AddChild(saveButton);

		var backButton = new Button
		{
			Name = "BackFromParametersButton", Text = Localization.Text("hud.back"),
			CustomMinimumSize = new Vector2(180, 44)
		};
		backButton.Pressed += ShowPauseMainMenu;
		footer.AddChild(backButton);

		RefreshPauseTuningValues();
	}

	private void AddPauseMenuButton(VBoxContainer parent, string text, Action action)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(360, 52) };
		button.Pressed += action;
		parent.AddChild(button);
	}

	private void ShowPausePage(Control page)
	{
		_pauseMainMenu.Visible = false;
		_pauseTuningPage.Visible = page == _pauseTuningPage;
		_optionsPage.Visible = page == _optionsPage;
		ControllerUiSupport.FocusFirst(page);
	}

	private void ShowPauseMainMenu()
	{
		_pauseMainMenu.Visible = true;
		_pauseTuningPage.Visible = false;
		_optionsPage.Visible = false;
		ControllerUiSupport.FocusFirst(_pauseMainMenu);
	}

	private void BuildOptionsPage(VBoxContainer panel)
	{
		_optionsPage = new VBoxContainer
		{
			Name = "OptionsPage", Visible = false, Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		panel.AddChild(_optionsPage);
		_optionsPage.AddChild(new Label { Text = "OPCOES", HorizontalAlignment = HorizontalAlignment.Center });

		var resolution = new OptionButton { CustomMinimumSize = new Vector2(360, 46) };
		resolution.AddItem("1280 x 720"); resolution.AddItem("1600 x 900"); resolution.AddItem("1920 x 1080");
		resolution.Selected = GlobalSettings.Instance?.Data.ResolutionIndex ?? 1;
		resolution.ItemSelected += index => GlobalSettings.Instance?.SetResolution((int)index);
		_optionsPage.AddChild(resolution);

		var fullscreen = new CheckButton { Text = "Tela cheia", ButtonPressed = GlobalSettings.Instance?.Data.Fullscreen ?? false, CustomMinimumSize = new Vector2(360, 44) };
		fullscreen.Toggled += enabled => GlobalSettings.Instance?.SetFullscreen(enabled);
		_optionsPage.AddChild(fullscreen);
		AddVolumeOption("Volume da musica", GlobalSettings.Instance?.Data.MusicVolume ?? 70, value => GlobalSettings.Instance?.SetMusicVolume((float)value));
		AddVolumeOption("Volume dos efeitos", GlobalSettings.Instance?.Data.EffectsVolume ?? 85, value => GlobalSettings.Instance?.SetEffectsVolume((float)value));
		AddPauseMenuButton(_optionsPage, "Voltar", ShowPauseMainMenu);
	}

	private void AddVolumeOption(string title, double initialValue, Action<double> setter)
	{
		var row = new VBoxContainer { CustomMinimumSize = new Vector2(460, 64) };
		row.AddChild(new Label { Text = title });
		var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = initialValue };
		slider.ValueChanged += value => setter(value);
		row.AddChild(slider);
		_optionsPage.AddChild(row);
	}

	private void AddPauseTuningSlider(string title, int currentValue, Action<int> setter, out HSlider sliderRef, out Label valueLabelRef, int maxValue = 100)
	{
		if (_pauseTuningList == null)
		{
			sliderRef = null;
			valueLabelRef = null;
			return;
		}

		var row = new VBoxContainer
		{
			Name = $"{title.Replace(" ", "")}Row",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 66.0f)
		};
		_pauseTuningList.AddChild(row);

		var header = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(header);

		var label = new Label
		{
			Text = title,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		header.AddChild(label);

		var valueLabel = new Label
		{
			Text = currentValue.ToString(),
			HorizontalAlignment = HorizontalAlignment.Right,
			CustomMinimumSize = new Vector2(88.0f, 0.0f)
		};
		header.AddChild(valueLabel);

		var slider = new HSlider
		{
			MinValue = 1.0,
			MaxValue = maxValue,
			Step = 1.0,
			Value = currentValue,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		slider.ValueChanged += value =>
		{
			var percent = Mathf.Clamp((int)Mathf.Round((float)value), 1, 100);
			valueLabel.Text = percent.ToString();
			setter(percent);
			SetPauseTuningStatus($"Ajuste temporario aplicado: {title} = {percent}");
		};
		row.AddChild(slider);

		sliderRef = slider;
		valueLabelRef = valueLabel;
	}

	private void OnPlayerDied()
	{
		GetTree().CallGroup("audio_manager", "PlayEffect", "death");
		GetTree().CallGroup("audio_manager", "PlayGameOverMusic");
		_isGameOver = true;
		var finalDamage = _runtimeHealth?.LastDamageAmount ?? 0.0f;
		AppendGameLog($"Virgil recebeu {Mathf.RoundToInt(finalDamage)} de dano fatal de {_runtimeHealth?.LastDamageSource ?? "causa desconhecida"}.");
		_gameOverSubtitle.Text = $"Virgil foi derrotado por: {_runtimeHealth?.LastDamageSource ?? "causa desconhecida"}. Último golpe: {Mathf.RoundToInt(finalDamage)} de dano.";
		_gameOverOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_gameOverOverlay);
		if (_artifactOfferOverlay != null)
			_artifactOfferOverlay.Visible = false;
		if (_bloodBlessingOverlay != null)
			_bloodBlessingOverlay.Visible = false;
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		_pauseOverlay.Visible = false;
		GetTree().Paused = true;
	}

	private void ContinueAfterGameOver()
	{
		RunState.FailRun();
		SaveGameManager.RequestAutosave();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
	}

	private void AbandonRun()
	{
		if (!RunState.IsRunActive || _runtimeHealth == null || _runtimeHealth.IsDead)
			return;

		_pauseOverlay.Visible = false;
		UiModalStack.ApplyPause(false);
		_runtimeHealth.SetInvincible(false);
		_runtimeHealth.ForceDeath("Desistência da run");
	}

	private void RestartCurrentScene()
	{
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ReloadCurrentScene();
	}

	private void ReturnToMainMenu()
	{
		SaveGameManager.RequestAutosave();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
	}

	private void PauseGame()
	{
		RefreshPauseTuningValues();
		if (_abandonRunButton != null)
			_abandonRunButton.Visible = RunState.IsRunActive;
		ShowPauseMainMenu();
		_pauseOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_pauseMainMenu);
		SetModalPause(true);
	}

	private void ResumeGame()
	{
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		_pauseOverlay.Visible = false;
		RefreshModalPause();
	}

	private bool CloseTopmostOverlay()
	{
		// Telas migradas para a pilha modal fecham primeiro; depois segue a cadeia legada.
		if (UiModalStack.CancelTop()) return true;
		if (IsVisible(_skillDescriptionPopup)) { _skillDescriptionPopup.Visible = false; return true; }
		if (IsVisible(_compendiumOverlay)) { _compendiumOverlay.Visible = false; RefreshModalPause(); return true; }
		if (IsVisible(_pauseOverlay) && (IsVisible(_pauseTuningPage) || IsVisible(_optionsPage)))
		{
			ShowPauseMainMenu();
			return true;
		}
		if (IsVisible(_artifactOfferOverlay)) { _artifactOfferOverlay.Visible = false; RefreshModalPause(); return true; }
		if (IsVisible(_bloodBlessingOverlay))
		{
			_bloodBlessingStatusLabel.Text = "Escolha uma Bencao de Sangue para liberar a saida.";
			FocusBlessingChoice(_focusedBlessingChoice);
			return true;
		}
		if (IsVisible(_characterSheetOverlay)) { _characterSheetOverlay.Visible = false; RefreshModalPause(); return true; }
		if (IsVisible(_bloodFountainOverlay)) { _bloodFountainOverlay.Visible = false; RefreshModalPause(); return true; }
		if (IsVisible(_artifactChestOverlay)) { _artifactChestOverlay.Visible = false; RefreshModalPause(); return true; }
		if (IsVisible(_weaponBenchOverlay)) { _weaponBenchOverlay.Visible = false; RefreshModalPause(); return true; }
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

	private void SavePauseTuning()
	{
		GameTuning.Save();
		SetPauseTuningStatus("Parametros salvos como padrao definitivo.");
	}

	private void RefreshPauseTuningValues()
	{
		SyncSlider(_walkSpeedSlider, _walkSpeedValueLabel, GameTuning.WalkSpeedPercent);
		SyncSlider(_runSpeedSlider, _runSpeedValueLabel, GameTuning.RunSpeedPercent);
		SyncSlider(_dashSpeedSlider, _dashSpeedValueLabel, GameTuning.DashSpeedPercent);
		SyncSlider(_dashDistanceSlider, _dashDistanceValueLabel, GameTuning.DashDistancePercent);
		SyncSlider(_cameraZoomDistanceSlider, _cameraZoomDistanceValueLabel, GameTuning.CameraZoomDistancePercent);
		SyncSlider(_damageSlider, _damageValueLabel, GameTuning.DamagePercent);
		SyncSlider(_healthSlider, _healthValueLabel, GameTuning.HealthPercent);
		SyncSlider(_animationSpeedSlider, _animationSpeedValueLabel, GameTuning.AnimationSpeedPercent);
		SyncSlider(_attackSpeedSlider, _attackSpeedValueLabel, GameTuning.AttackSpeedPercent);
		SyncSlider(_attackDelaySlider, _attackDelayValueLabel, GameTuning.AttackDelayPercent);
		SyncSlider(_attackRangeSlider, _attackRangeValueLabel, GameTuning.AttackRangePercent);
	}

	private void SyncSlider(HSlider slider, Label valueLabel, int value)
	{
		if (slider != null)
			slider.Value = value;

		if (valueLabel != null)
			valueLabel.Text = value.ToString();
	}

	private void SetPauseTuningStatus(string text)
	{
		if (_pauseTuningStatusLabel != null)
			_pauseTuningStatusLabel.Text = text;
	}
}

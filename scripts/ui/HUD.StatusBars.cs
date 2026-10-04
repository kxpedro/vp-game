using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildLayout()
	{
		var root = new Control
		{
			Name = "Root",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Theme = UIThemeFactory.Create()
		};
		AddChild(root);
		_playerHudFrame = AddHudPanel(root, "PlayerFrame", new Vector4(0, 1, 0, 1), new Vector4(18, -142, 430, -18), 0.9f);
		AddHudPanel(root, "ResourceFrame", new Vector4(0, 0, 0, 0), new Vector4(18, 18, 310, 118), 0.78f);
		_roomFrame = AddHudPanel(root, "RoomFrame", new Vector4(0.5f, 0, 0.5f, 0), new Vector4(-280, 14, 280, 58), 0.72f);

		var leftStatus = new VBoxContainer
		{
			Name = "PlayerStatus",
			AnchorLeft = 0.0f,
			AnchorTop = 1.0f,
			AnchorRight = 0.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 34.0f,
			OffsetTop = -128.0f,
			OffsetRight = 414.0f,
			OffsetBottom = -28.0f
		};
		_playerStatus = leftStatus;
		root.AddChild(leftStatus);
		var playerHeader = new Label
		{
			Text = "VIRGIL",
			HorizontalAlignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(360.0f, 22.0f),
			Modulate = new Color(0.82f, 0.72f, 0.62f)
		};
		playerHeader.AddThemeFontSizeOverride("font_size", 13);
		leftStatus.AddChild(playerHeader);
		_fpsLabel = new Label
		{
			Name = "FpsCounter", Text = "FPS: 0",
			AnchorLeft = 1.0f, AnchorRight = 1.0f,
			OffsetLeft = -116, OffsetTop = 16, OffsetRight = -22, OffsetBottom = 42,
			HorizontalAlignment = HorizontalAlignment.Right,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.48f),
			Visible = OS.IsDebugBuild()
		};
		root.AddChild(_fpsLabel);

		var healthStack = new Control
		{
			Name = "HealthAndBarrier",
			CustomMinimumSize = new Vector2(360.0f, 22.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_barrierBar = CreateBar("Barreira", 100.0f);
		_barrierBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		healthStack.AddChild(_barrierBar);
		_healthBar = CreateBar("Vida", 100.0f);
		_healthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		healthStack.AddChild(_healthBar);
		_furyBar = CreateBar("Furia", 100.0f);
		_furyBar.Visible = false;
		ApplyHealthBarStyle();
		leftStatus.AddChild(healthStack);
		_healthPointsLabel = new Label
		{
			Text = "100/100", MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = new Vector2(0, -1), Size = new Vector2(360, 22),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Modulate = Colors.White
		};
		_healthPointsLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
		_healthPointsLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		_healthPointsLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		_healthBar.AddChild(_healthPointsLabel);
		leftStatus.AddChild(_furyBar);
		var showCombatHud = !GetTree().CurrentScene.SceneFilePath.EndsWith("CrowsNest.tscn", StringComparison.OrdinalIgnoreCase);
		_playerHudFrame.Visible = showCombatHud;
		_playerStatus.Visible = showCombatHud;

		_blessingHudList = new HBoxContainer
		{
			Name = "BlessingHudList",
			AnchorLeft = 0.5f, AnchorTop = 1.0f, AnchorRight = 0.5f, AnchorBottom = 1.0f,
			OffsetLeft = -246.0f, OffsetTop = -66.0f, OffsetRight = 246.0f, OffsetBottom = -18.0f,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		_blessingHudList.AddThemeConstantOverride("separation", 8);
		root.AddChild(_blessingHudList);
		RefreshBlessingHud();

		_damageFlash = new ColorRect
		{
			Name = "DamageFlash", Color = new Color(0.9f, 0, 0, 0),
			AnchorRight = 1, AnchorBottom = 1,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(_damageFlash);
		_criticalHealthVignette = new ColorRect
		{
			Name = "CriticalHealthVignette",
			Color = new Color(0.75f, 0.0f, 0.025f, 0.0f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(_criticalHealthVignette);

		_gameLog = new RichTextLabel
		{
			Name = "GameLog", AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
			OffsetLeft = -382, OffsetTop = -132, OffsetRight = -24, OffsetBottom = -24,
			BbcodeEnabled = true, FitContent = false, ScrollActive = false, ScrollFollowing = true,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0.55f)
		};
		root.AddChild(_gameLog);

		var resourcePanel = new VBoxContainer
		{
			Name = "ResourcePanel",
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 0.0f,
			AnchorBottom = 0.0f,
			OffsetLeft = 32.0f,
			OffsetTop = 27.0f,
			OffsetRight = 294.0f,
			OffsetBottom = 108.0f
		};
		root.AddChild(resourcePanel);
		_coinsLabel = new Label
		{
			Name = "BloodCoinsLabel",
			Text = Localization.TextFormat("hud.blood_coins", 0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
		resourcePanel.AddChild(_coinsLabel);
		_weaponLabel = new Label
		{
			Name = "EquippedWeaponLabel",
			Text = $"Arma: {WeaponLoadout.EquippedWeapon.DisplayName}",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		resourcePanel.AddChild(_weaponLabel);

		_enemyProgressLabel = new Label
		{
			Name = "EnemyProgressLabel",
			Text = Localization.TextFormat("hud.enemies", 0, 0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
		resourcePanel.AddChild(_enemyProgressLabel);

		_roomLabel = new Label
		{
			Name = "RoomLabel",
			Text = "",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -258.0f,
			OffsetTop = 19.0f,
			OffsetRight = 258.0f,
			OffsetBottom = 49.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_roomLabel.AddThemeFontSizeOverride("font_size", 17);
		_roomLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.78f, 0.68f));
		root.AddChild(_roomLabel);
		var roomDivider = new TextureRect
		{
			Name = "RoomDivider",
			Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimpleDividerPath),
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -190.0f,
			OffsetTop = 52.0f,
			OffsetRight = 190.0f,
			OffsetBottom = 56.0f,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Modulate = new Color(0.55f, 0.46f, 0.42f, 0.44f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(roomDivider);
		_roomDivider = roomDivider;
		// A Camara de Hemomancia escreve o proprio titulo sobre esta moldura.
		_roomFrameAlwaysVisible = GetTree().CurrentScene is IntermissionChamber;
		_controlsHintLabel = new Label
		{
			Name = "ControlsHint",
			Visible = false,
			Text = "Teclado: WASD mover • Mouse atacar • Espaço esquiva • Q especial • F fúria • C ficha\nControle: L mover/mirar • □/X atacar • △/Y especial • X/A esquiva/confirmar • R1/RB fúria",
			AnchorLeft = 0.5f, AnchorTop = 1.0f, AnchorRight = 0.5f, AnchorBottom = 1.0f,
			OffsetLeft = -430.0f, OffsetTop = -62.0f, OffsetRight = 430.0f, OffsetBottom = -18.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.62f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(_controlsHintLabel);

		_bossHealthPanel = new VBoxContainer
		{
			Name = "BossHealthPanel", AnchorLeft = 0.22f, AnchorTop = 0.075f, AnchorRight = 0.78f, AnchorBottom = 0.075f,
			OffsetBottom = 78, Visible = false
		};
		root.AddChild(_bossHealthPanel);
		_bossHealthLabel = new Label { Text = Localization.Text("hud.boss"), HorizontalAlignment = HorizontalAlignment.Center };
		_bossHealthLabel.AddThemeFontSizeOverride("font_size", 24);
		_bossHealthPanel.AddChild(_bossHealthLabel);
		_bossHealthBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 28) };
		_bossHealthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.52f, 0.015f, 0.025f), BorderWidthTop = 2, BorderWidthBottom = 2 });
		_bossHealthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.04f, 0.01f, 0.012f) });
		_bossHealthPanel.AddChild(_bossHealthBar);

		_enemyDefeatedToast = new Label
		{
			Name = "EnemyDefeatedToast",
			Text = "+1 inimigo derrotado",
			AnchorLeft = 0.5f,
			AnchorTop = 0.28f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.28f,
			OffsetLeft = -170.0f,
			OffsetTop = -18.0f,
			OffsetRight = 170.0f,
			OffsetBottom = 18.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = Colors.Transparent,
			Visible = false
		};
		root.AddChild(_enemyDefeatedToast);

		BuildPauseOverlay(root);
		BuildGameOverOverlay(root);
		BuildRoomClearBanner(root);
		BuildCharacterSheetOverlay(root);
		BuildBloodFountainOverlay(root);
		BuildArtifactChestOverlay(root);
		BuildArtifactOfferOverlay(root);
		BuildBloodBlessingOverlay(root);
		BuildWeaponBenchOverlay(root);
		BuildCompendiumOverlay(root);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
	}

	private ProgressBar CreateBar(string label, float maxValue)
	{
		return new ProgressBar
		{
			Name = label,
			MaxValue = maxValue,
			Value = maxValue,
			CustomMinimumSize = new Vector2(360.0f, 22.0f),
			ShowPercentage = false,
			TooltipText = label
		};
	}

	private static PanelContainer AddHudPanel(Control root, string name, Vector4 anchors, Vector4 offsets, float alpha = 0.84f)
	{
		var panel = new PanelContainer
		{
			Name = name, MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = anchors.X, AnchorTop = anchors.Y, AnchorRight = anchors.Z, AnchorBottom = anchors.W,
			OffsetLeft = offsets.X, OffsetTop = offsets.Y, OffsetRight = offsets.Z, OffsetBottom = offsets.W
		};
		panel.AddThemeStyleboxOverride("panel", UIThemeFactory.HudPanel(alpha));
		root.AddChild(panel);
		return panel;
	}

	private void ApplyHealthBarStyle()
	{
		var fill = new StyleBoxFlat
		{
			BgColor = new Color(0.86f, 0.02f, 0.04f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		};
		var background = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		};

		_healthBar.AddThemeStyleboxOverride("fill", fill);
		_healthBar.AddThemeStyleboxOverride("background", background);
		_barrierBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.52f, 0.95f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		});
		_barrierBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.0f, 0.0f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		});
		_furyBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = new Color(0.94f, 0.37f, 0.04f),
			BorderColor = new Color(1.0f, 0.72f, 0.2f),
			BorderWidthTop = 1,
			BorderWidthBottom = 1
		});
		_furyBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.09f, 0.025f, 0.012f),
			BorderColor = new Color(0.33f, 0.12f, 0.04f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1
		});
	}

	private void BindRuntimeNodes()
	{
		if (_isRuntimeBound)
			return;

		var player = GetTree().GetFirstNodeInGroup("player");
		var health = player?.GetNodeOrNull<Health>("Health");
		var controller = player as PlayerController;
		var combat = player?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
		var barrier = player?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		var runManager = GetTree().GetFirstNodeInGroup("run_manager") as RunManager;
		_runtimePlayerController = controller;

		if (health != null)
		{
			_runtimeHealth = health;
			_healthBar.MaxValue = health.MaxHealth;
			_healthBar.Value = health.CurrentHealth;
			OnHealthChanged(health.CurrentHealth, health.MaxHealth);
			health.HealthChanged += OnHealthChanged;
			health.Damaged += OnPlayerDamaged;
			health.Died += OnPlayerDied;
		}
		if (barrier != null)
		{
			barrier.BarrierChanged += OnBarrierChanged;
			OnBarrierChanged(barrier.CurrentBarrier, barrier.MaximumBarrier);
		}

		if (combat != null)
		{
			_runtimePlayerCombat = combat;
			_furyBar.MaxValue = combat.MaxFury;
			_furyBar.Value = combat.CurrentFury;
			combat.FuryChanged += OnFuryChanged;
			RefreshFuryVisibility();
		}

		if (runManager != null)
		{
			runManager.BloodCoinsChanged += OnBloodCoinsChanged;
			runManager.BloodCoinsRewarded += OnBloodCoinsRewarded;
			runManager.EnemyProgressChanged += OnEnemyProgressChanged;
			runManager.RoomCleared += OnRoomCleared;
			OnBloodCoinsChanged(runManager.BloodCoins);
			SyncEnemyProgress(runManager.DefeatedEnemies, runManager.TotalEnemies);
			SyncRoomInformation();
			_isRuntimeBound = true;
		}

	}

	private void OnPlayerDamaged(float amount)
	{
		if (amount > 0)
		{
			AppendGameLog($"Virgil recebeu {Mathf.RoundToInt(amount)} de dano.");
			ShowReceivedDamageNumber(amount);
		}
		if (_damageFlash == null) return;
		_damageFlash.Color = new Color(0.9f, 0, 0, 0.28f);
		var tween = _damageFlash.CreateTween();
		tween.TweenProperty(_damageFlash, "color:a", 0.0f, 0.16f);
	}

	private async void ShowReceivedDamageNumber(float amount)
	{
		var label = new Label
		{
			Text = $"-{Mathf.RoundToInt(amount)}", Modulate = new Color(1, 0.08f, 0.05f),
			AnchorLeft = 0.5f, AnchorTop = 0.66f, AnchorRight = 0.5f, AnchorBottom = 0.66f,
			OffsetLeft = -80, OffsetTop = -20, OffsetRight = 80, OffsetBottom = 30,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 34);
		AddChild(label);
		var tween = label.CreateTween();
		tween.TweenProperty(label, "position:y", label.Position.Y - 55, 0.55f);
		tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.55f);
		await ToSignal(tween, Tween.SignalName.Finished);
		label.QueueFree();
	}

	private void OnHealthChanged(float current, float max)
	{
		UpdateCombinedHealthDisplay();
	}

	private void OnBarrierChanged(float current, float maximum)
	{
		UpdateCombinedHealthDisplay();
	}

	private void UpdateCombinedHealthDisplay()
	{
		if (_runtimeHealth == null || _healthBar == null || _barrierBar == null)
			return;

		var barrier = _runtimePlayerController?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		var barrierCurrent = barrier?.CurrentBarrier ?? 0.0f;
		var barrierMaximum = barrier?.MaximumBarrier ?? 0.0f;
		var combinedMaximum = Mathf.Max(_runtimeHealth.MaxHealth + barrierMaximum, 1.0f);
		_barrierBar.MaxValue = combinedMaximum;
		_barrierBar.Value = Mathf.Min(_runtimeHealth.CurrentHealth + barrierCurrent, combinedMaximum);
		_healthBar.MaxValue = combinedMaximum;
		_healthBar.Value = _runtimeHealth.CurrentHealth;
		if (_healthPointsLabel != null)
		{
			_healthPointsLabel.Text = barrierCurrent > 0.01f
				? $"{Mathf.CeilToInt(_runtimeHealth.CurrentHealth)} + {Mathf.CeilToInt(barrierCurrent)} / {Mathf.CeilToInt(_runtimeHealth.MaxHealth)}"
				: $"{Mathf.CeilToInt(_runtimeHealth.CurrentHealth)}/{Mathf.CeilToInt(_runtimeHealth.MaxHealth)}";
			_healthPointsLabel.Modulate = barrierCurrent > 0.01f ? new Color(0.72f, 0.9f, 1.0f) : Colors.White;
		}
	}

	private void UpdateCriticalHealthIndicator(float delta)
	{
		if (_criticalHealthVignette == null || _runtimeHealth == null || _runtimeHealth.MaxHealth <= 0.0f)
			return;

		var healthRatio = _runtimeHealth.CurrentHealth / _runtimeHealth.MaxHealth;
		if (healthRatio >= 0.3f || _runtimeHealth.IsDead)
		{
			_criticalHealthVignette.Color = new Color(0.75f, 0.0f, 0.025f, 0.0f);
			return;
		}

		_criticalHealthPulse += delta * 3.4f;
		var severity = Mathf.Clamp((0.3f - healthRatio) / 0.3f, 0.0f, 1.0f);
		var pulse = 0.5f + 0.5f * Mathf.Sin(_criticalHealthPulse);
		_criticalHealthVignette.Color = new Color(0.75f, 0.0f, 0.025f, 0.08f + severity * 0.12f + pulse * 0.055f);
	}

	private void OnFuryChanged(float current, float max, bool isActive)
	{
		_furyBar.MaxValue = max;
		_furyBar.Value = current;
		RefreshFuryVisibility();
		var furyDrainPerSecond = CompendiumTuning.GetFloat("attack.fury.drain_per_second", 10.0f);
		_furyBar.TooltipText = isActive
			? $"Furia Bestial ativa: consome {furyDrainPerSecond:0.#} pontos por segundo e explode ao chegar a zero"
			: RunState.GetEffectTotal("UnlockBestialFury.Flat") > 0.0f
				? "Furia Bestial - pressione F quando a barra estiver cheia"
				: "Furia Bestial bloqueada - encontre uma Bencao de Sangue compativel";
	}

	private void RefreshFuryVisibility()
	{
		if (_furyBar == null)
			return;
		_furyBar.Visible = RunState.GetEffectTotal("UnlockBestialFury.Flat") > 0.0f;
	}

	private void OnBloodCoinsChanged(int amount)
	{
		_coinsLabel.Text = Localization.TextFormat("hud.blood_coins", amount);
		RefreshVisibleCoinOverlays();
	}

	// Ficha e Fonte sao reconstruidas ao abrir; enquanto fechadas nao precisam refazer a arvore a cada kill.
	private void RefreshVisibleCoinOverlays()
	{
		if (IsVisible(_characterSheetOverlay))
			RefreshCharacterSheet();
		if (IsVisible(_bloodFountainOverlay))
			RefreshBloodFountain();
	}

	private void OnBloodCoinsRewarded(int reward, int totalAmount)
	{
		OnBloodCoinsChanged(totalAmount);
	}

	private void OnEnemyProgressChanged(int defeatedEnemies, int totalEnemies)
	{
		SyncEnemyProgress(defeatedEnemies, totalEnemies);
	}

	private void OnRoomCleared()
	{
		if (_roomClearAnnounced)
			return;

		_roomClearAnnounced = true;
		_roomLabel.Text = "Sala concluida - portal vermelho liberado";
		ShowRoomClearBanner();
	}
}

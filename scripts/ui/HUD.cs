using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD : CanvasLayer
{
	private ProgressBar _healthBar;
	private ProgressBar _barrierBar;
	private Label _healthPointsLabel;
	private Control _playerHudFrame;
	private Control _playerStatus;
	private Label _controlsHintLabel;
	private Label _fpsLabel;
	private ColorRect _damageFlash;
	private ColorRect _criticalHealthVignette;
	private RichTextLabel _gameLog;
	private readonly Queue<string> _gameLogEntries = new();
	private ProgressBar _furyBar;
	private Label _coinsLabel;
	private Label _weaponLabel;
	private Control _weaponBenchOverlay;
	private Label _weaponBenchStatusLabel;
	private Label _enemyProgressLabel;
	private Label _roomLabel;
	private Control _bossHealthPanel;
	private ProgressBar _bossHealthBar;
	private Label _bossHealthLabel;
	private Label _enemyDefeatedToast;
	private Control _roomClearBanner;
	private Control _characterSheetOverlay;
	private VBoxContainer _characterBlessingsColumn;
	private VBoxContainer _characterAttributesColumn;
	private VBoxContainer _characterInventoryColumn;
	private VBoxContainer _characterLoadoutColumn;
	private Control _bloodFountainOverlay;
	private Control _skillTreeCanvas;
	private Label _bloodFountainStatusLabel;
	private Button _bloodCoinCheatButton;
	private readonly Dictionary<string, Button> _skillTreeButtons = new();
	private Control _skillDescriptionPopup;
	private Label _skillDescriptionLabel;
	private Control _artifactChestOverlay;
	private Label _artifactChestStatusLabel;
	private VBoxContainer _artifactCollectionList;
	private readonly Button[] _artifactSlotButtons = new Button[CharacterSheet.EquippedArtifactSlotCount];
	private Control _artifactOfferOverlay;
	private Label _artifactOfferStatusLabel;
	private VBoxContainer _artifactOfferList;
	private Button _artifactRerollButton;
	private RoomArtifact _pendingArtifactSource;
	private Control _bloodBlessingOverlay;
	private Label _bloodBlessingStatusLabel;
	private VBoxContainer _bloodBlessingList;
	private Button _blessingRerollButton;
	private readonly List<Button> _blessingChoiceButtons = new();
	private int _focusedBlessingChoice;
	private ColorRect _bloodAbsorptionOverlay;
	private Label _bloodAbsorptionLabel;
	private HBoxContainer _blessingHudList;
	private Control _pauseOverlay;
	private VBoxContainer _pauseMainMenu;
	private VBoxContainer _pauseTuningPage;
	private VBoxContainer _optionsPage;
	private Label _pauseTuningStatusLabel;
	private ScrollContainer _pauseTuningScroll;
	private VBoxContainer _pauseTuningList;
	private Control _compendiumOverlay;
	private Label _compendiumStatusLabel;
	private VBoxContainer _compendiumList;
	private readonly Dictionary<string, Button> _compendiumTabButtons = new();
	private HSlider _walkSpeedSlider;
	private Label _walkSpeedValueLabel;
	private HSlider _runSpeedSlider;
	private Label _runSpeedValueLabel;
	private HSlider _dashSpeedSlider;
	private Label _dashSpeedValueLabel;
	private HSlider _dashDistanceSlider;
	private Label _dashDistanceValueLabel;
	private HSlider _cameraZoomDistanceSlider;
	private Label _cameraZoomDistanceValueLabel;
	private HSlider _damageSlider;
	private Label _damageValueLabel;
	private HSlider _healthSlider;
	private Label _healthValueLabel;
	private HSlider _animationSpeedSlider;
	private Label _animationSpeedValueLabel;
	private HSlider _attackSpeedSlider;
	private Label _attackSpeedValueLabel;
	private HSlider _attackDelaySlider;
	private Label _attackDelayValueLabel;
	private HSlider _attackRangeSlider;
	private Label _attackRangeValueLabel;
	private Control _gameOverOverlay;
	private Label _gameOverSubtitle;
	private Button _abandonRunButton;
	private Health _runtimeHealth;
	private bool _isGameOver;
	private bool _isRuntimeBound;
	private bool _roomClearAnnounced;
	private PlayerController _runtimePlayerController;
	private PlayerCombat _runtimePlayerCombat;
	private int _selectedArtifactSlot;
	private string _selectedCompendiumTab = "Artifacts";
	private float _bloodFountainZoom = 1.0f;
	private bool _isDraggingBloodFountain;
	private Vector2 _lastBloodFountainMousePosition;
	private Vector2 _skillTreePan = Vector2.Zero;
	private float _criticalHealthPulse;

	public override void _Ready()
	{
		ControllerUiSupport.EnsureMappings();
		AddToGroup("hud");
		ProcessMode = ProcessModeEnum.Always;
		BuildLayout();
		CallDeferred(nameof(BindRuntimeNodes));
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !_isGameOver)
		{
			if (CloseTopmostOverlay())
			{
				GetViewport().SetInputAsHandled();
				return;
			}
		}
		if (IsVisible(_bloodBlessingOverlay) && _blessingChoiceButtons.Count > 0 &&
			(@event.IsActionPressed("ui_up") || @event.IsActionPressed("ui_down")))
		{
			var direction = @event.IsActionPressed("ui_up") ? -1 : 1;
			FocusBlessingChoice((_focusedBlessingChoice + direction + _blessingChoiceButtons.Count) % _blessingChoiceButtons.Count);
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause") && !_isGameOver)
		{
			if (!CloseTopmostOverlay())
				PauseGame();

			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("character_sheet") && !_isGameOver)
		{
			ToggleCharacterSheet();
			GetViewport().SetInputAsHandled();
		}

		if (_bloodFountainOverlay != null && _bloodFountainOverlay.Visible && @event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				AdjustBloodFountainZoom(0.1f);
				GetViewport().SetInputAsHandled();
			}

			if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				AdjustBloodFountainZoom(-0.1f);
				GetViewport().SetInputAsHandled();
			}
		}

		if (_bloodFountainOverlay != null && _bloodFountainOverlay.Visible && @event is InputEventMouseButton dragButton && dragButton.ButtonIndex == MouseButton.Left)
		{
			_isDraggingBloodFountain = dragButton.Pressed;
			_lastBloodFountainMousePosition = dragButton.Position;
		}

		if (_bloodFountainOverlay != null && _bloodFountainOverlay.Visible && _isDraggingBloodFountain && @event is InputEventMouseMotion motion)
		{
			var delta = motion.Position - _lastBloodFountainMousePosition;
			_lastBloodFountainMousePosition = motion.Position;
			PanBloodFountain(delta);
		}
	}

	public override void _Process(double delta)
	{
		UpdateCriticalHealthIndicator((float)delta);
		if (_fpsLabel != null)
			_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
	}

	public bool IsGameplayInputBlocked()
	{
		return IsVisible(_weaponBenchOverlay)
			|| IsVisible(_characterSheetOverlay)
			|| IsVisible(_bloodFountainOverlay)
			|| IsVisible(_artifactChestOverlay)
			|| IsVisible(_artifactOfferOverlay)
			|| IsVisible(_bloodBlessingOverlay)
			|| IsVisible(_pauseOverlay)
			|| IsVisible(_compendiumOverlay)
			|| IsVisible(_gameOverOverlay)
			|| IsVisible(_skillDescriptionPopup);
	}

	private static bool IsVisible(Control control) => control != null && control.Visible;

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
		AddHudPanel(root, "RoomFrame", new Vector4(0.5f, 0, 0.5f, 0), new Vector4(-280, 14, 280, 58), 0.72f);

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
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.48f)
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
			BbcodeEnabled = true, FitContent = false, ScrollActive = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0.55f)
		};
		root.AddChild(_gameLog);
		AppendGameLog("Interface iniciada.");

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

	public void OpenWeaponBench()
	{
		if (_weaponBenchOverlay == null)
			return;
		_weaponBenchStatusLabel.Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}";
		_weaponBenchOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_weaponBenchOverlay);
	}

	private void BuildWeaponBenchOverlay(Control root)
	{
		_weaponBenchOverlay = new ColorRect
		{
			Name = "WeaponBenchOverlay",
			Color = new Color(0.025f, 0.015f, 0.02f, 0.96f),
			AnchorLeft = 0.25f,
			AnchorTop = 0.18f,
			AnchorRight = 0.75f,
			AnchorBottom = 0.82f,
			Visible = false
		};
		root.AddChild(_weaponBenchOverlay);

		var list = new VBoxContainer
		{
			AnchorRight = 1,
			AnchorBottom = 1,
			OffsetLeft = 30,
			OffsetTop = 24,
			OffsetRight = -30,
			OffsetBottom = -24
		};
		_weaponBenchOverlay.AddChild(list);
		list.AddChild(new Label { Text = "Bancada de Armas", HorizontalAlignment = HorizontalAlignment.Center });
		_weaponBenchStatusLabel = new Label { Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}" };
		list.AddChild(_weaponBenchStatusLabel);

		foreach (var weapon in WeaponCatalog.All)
		{
			var captured = weapon;
			var button = new Button { Text = $"{weapon.DisplayName} — {weapon.Description}" };
			button.Pressed += () => EquipWeapon(captured.Id);
			list.AddChild(button);
		}

		var close = new Button { Text = Localization.Text("hud.close") };
		close.Pressed += () => _weaponBenchOverlay.Visible = false;
		list.AddChild(close);
	}

	private void EquipWeapon(string weaponId)
	{
		WeaponLoadout.Equip(weaponId);
		_weaponLabel.Text = $"Arma: {WeaponLoadout.EquippedWeapon.DisplayName}";
		_weaponBenchStatusLabel.Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}";
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

	public void AppendGameLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		_gameLogEntries.Enqueue($"[{Time.GetTimeStringFromSystem()}] {message}");
		while (_gameLogEntries.Count > 10) _gameLogEntries.Dequeue();
		if (_gameLog != null) _gameLog.Text = string.Join("\n", _gameLogEntries);
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

	public void SyncEnemyProgress(int defeatedEnemies, int totalEnemies)
	{
		_enemyProgressLabel.Text = Localization.TextFormat("hud.enemies", defeatedEnemies, totalEnemies);
	}

	public void NotifyEnemyDefeated(int defeatedEnemies, int totalEnemies, int bloodCoinsReward)
	{
		SyncEnemyProgress(defeatedEnemies, totalEnemies);
		ShowEnemyDefeatedToast($"+1 inimigo derrotado ({defeatedEnemies}/{totalEnemies}) | +{bloodCoinsReward} moedas");
		AppendGameLog($"{bloodCoinsReward} moedas recebidas; {defeatedEnemies}/{totalEnemies} inimigos derrotados.");
	}

	public void NotifyBloodCoinsRewarded(int reward, int totalAmount)
	{
		OnBloodCoinsChanged(totalAmount);
		RefreshCharacterSheet();
		RefreshBloodFountain();
	}

	public void SyncBloodCoins(int totalAmount)
	{
		OnBloodCoinsChanged(totalAmount);
	}

	public void NotifyRoomCleared()
	{
		AppendGameLog("Sala limpa. A saida foi liberada.");
		OnRoomCleared();
	}

	public void NotifyWaveIncoming(int enemyCount, float seconds)
	{
		ShowEnemyDefeatedToast($"PROXIMA WAVE: {enemyCount} inimigos surgem em {seconds:0}s");
		AppendGameLog($"Proxima wave detectada: {enemyCount} inimigos.");
	}

	public void BindBoss(Node bossNode, string displayName)
	{
		var health = bossNode?.GetNodeOrNull<Health>("Health");
		if (health == null) return;
		_bossHealthPanel.Visible = true;
		_bossHealthLabel.Text = displayName;
		UpdateBossHealth(health.CurrentHealth, health.MaxHealth);
		health.HealthChanged += UpdateBossHealth;
		health.Died += () => _bossHealthPanel.Visible = false;
	}

	private void UpdateBossHealth(float current, float maximum)
	{
		_bossHealthBar.MaxValue = maximum;
		_bossHealthBar.Value = current;
		_bossHealthLabel.Text = $"{_bossHealthLabel.Text.Split('—')[0].Trim()} — {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(maximum)}";
	}

	private void SyncRoomInformation()
	{
		_roomLabel.Text = RunState.IsBossRoom
			? $"{RunState.CurrentMap.Name} — BOSS"
			: $"{RunState.CurrentMap.Name} — Sala {RunState.CurrentRoom}/{RunState.CurrentMap.CommonRoomCount}";
	}

	public void OpenBloodFountain()
	{
		if (_bloodFountainOverlay == null)
			return;

		RefreshBloodFountain();
		_bloodFountainOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_bloodFountainOverlay);
	}

	public void NotifySkillMessage(string message)
	{
		AppendGameLog(message);
		ShowEnemyDefeatedToast(message);
	}

	public void NotifyArtifactCollected(string artifactName)
	{
		AppendGameLog($"Artefato obtido: {artifactName}.");
		ShowEnemyDefeatedToast($"{artifactName} adicionado aos artefatos temporarios");
		RefreshCharacterSheet();
	}

	public void OpenArtifactChest()
	{
		if (_artifactChestOverlay == null)
			return;

		RefreshArtifactChest();
		_artifactChestOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_artifactChestOverlay);
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
		RefreshCharacterSheet();
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

	private void BuildCharacterSheetOverlay(Control root)
	{
		_characterSheetOverlay = new ColorRect
		{
			Name = "CharacterSheetOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.86f),
			AnchorLeft = 0.02f,
			AnchorTop = 0.04f,
			AnchorRight = 0.98f,
			AnchorBottom = 0.96f,
			Visible = false
		};
		root.AddChild(_characterSheetOverlay);

		var title = new Label
		{
			Name = "CharacterSheetTitle",
			Text = "FICHA DE PERSONAGEM",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_characterSheetOverlay.AddChild(title);

		var columns = new HBoxContainer
		{
			Name = "CharacterSheetColumns",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 34.0f,
			OffsetTop = 76.0f,
			OffsetRight = -34.0f,
			OffsetBottom = -28.0f
		};
		_characterSheetOverlay.AddChild(columns);

		var blessingScroll = new ScrollContainer
		{
			Name = "CharacterBlessingsScroll",
			CustomMinimumSize = new Vector2(230.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_characterBlessingsColumn = CreateCharacterSheetColumn("Bênçãos equipadas", 230.0f);
		blessingScroll.AddChild(_characterBlessingsColumn);

		_characterAttributesColumn = CreateCharacterSheetColumn("Atributos", 220.0f);
		_characterInventoryColumn = CreateCharacterSheetColumn("Inventario", 270.0f);
		_characterLoadoutColumn = CreateCharacterSheetColumn("Habilidades e Artefatos", 270.0f);
		columns.AddChild(blessingScroll);
		columns.AddChild(_characterAttributesColumn);
		columns.AddChild(_characterInventoryColumn);
		columns.AddChild(_characterLoadoutColumn);
	}

	private VBoxContainer CreateCharacterSheetColumn(string title, float width)
	{
		var column = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(width, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};

		column.AddChild(new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 32.0f)
		});

		return column;
	}

	private void BuildBloodFountainOverlay(Control root)
	{
		_bloodFountainOverlay = new ColorRect
		{
			Name = "BloodFountainOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.88f),
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			Visible = false
		};
		root.AddChild(_bloodFountainOverlay);

		var title = new Label
		{
			Name = "BloodFountainTitle",
			Text = "FONTE DE SANGUE",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 22.0f,
			OffsetBottom = 62.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(title);

		_bloodFountainStatusLabel = new Label
		{
			Name = "BloodFountainStatus",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 66.0f,
			OffsetBottom = 98.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(_bloodFountainStatusLabel);

		_bloodCoinCheatButton = new Button
		{
			Name = "BloodCoinCheatButton",
			Text = "Cheat +500 moedas",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -100.0f,
			OffsetTop = 102.0f,
			OffsetRight = 100.0f,
			OffsetBottom = 138.0f
		};
		_bloodCoinCheatButton.Pressed += AddCheatBloodCoins;
		_bloodFountainOverlay.AddChild(_bloodCoinCheatButton);

		_skillTreeCanvas = new Control
		{
			Name = "SkillTreeCanvas",
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			PivotOffset = Vector2.Zero
		};
		_skillTreeCanvas.MouseFilter = Control.MouseFilterEnum.Ignore;
		_bloodFountainOverlay.AddChild(_skillTreeCanvas);

		var viewportWidth = Mathf.Max(GetViewport().GetVisibleRect().Size.X, 1152.0f);
		var leftBoard = Mathf.Max(80.0f, viewportWidth * 0.12f);
		var rightBoard = Mathf.Max(leftBoard + 500.0f, viewportWidth * 0.56f);
		BuildSkillTreeBoard("Bestialis", SkillTree.Bestialis, leftBoard);
		BuildSkillTreeBoard("Humanitas", SkillTree.Humanitas, rightBoard);

		BuildSkillDescriptionPopup();

		var closeButton = new Button
		{
			Name = "CloseBloodFountainButton",
			Text = "Fechar",
			AnchorLeft = 1.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 0.0f,
			OffsetLeft = -154.0f,
			OffsetTop = 22.0f,
			OffsetRight = -24.0f,
			OffsetBottom = 62.0f
		};
		closeButton.Pressed += CloseBloodFountain;
		_bloodFountainOverlay.AddChild(closeButton);

		_bloodFountainOverlay.MoveChild(_bloodCoinCheatButton, _bloodFountainOverlay.GetChildCount() - 1);
		_bloodFountainOverlay.MoveChild(closeButton, _bloodFountainOverlay.GetChildCount() - 1);
	}

	private void BuildSkillTreeBoard(string treeName, SkillTree tree, float offsetLeft)
	{
		var title = new Label
		{
			Name = $"{treeName}TreeTitle",
			Text = treeName,
			HorizontalAlignment = HorizontalAlignment.Center,
			OffsetLeft = offsetLeft,
			OffsetTop = 112.0f,
			OffsetRight = offsetLeft + 372.0f,
			OffsetBottom = 146.0f
		};
		_skillTreeCanvas.AddChild(title);

		var skills = new List<SkillDefinition>(SkillCatalog.GetTree(tree));
		for (var i = 0; i < skills.Count; i++)
		{
			var definition = skills[i];
			var nodePosition = GetSkillNodePosition(offsetLeft, definition);
			CreateSkillNode(definition, nodePosition);

			foreach (var prerequisiteId in definition.Prerequisites)
			{
				if (!SkillCatalog.TryGet(prerequisiteId, out var prerequisite))
					continue;

				if (prerequisite.Tree != tree)
					continue;

				CreateSkillLink(GetSkillNodePosition(offsetLeft, prerequisite), nodePosition);
			}
		}
	}

	private Vector2 GetSkillNodePosition(float treeOffsetLeft, SkillDefinition definition)
	{
		var x = treeOffsetLeft + 20.0f + definition.GridColumn * 130.0f;
		var y = 170.0f + definition.GridRow * 112.0f;
		return new Vector2(x, y);
	}

	private void CreateSkillNode(SkillDefinition definition, Vector2 position)
	{
		var button = new Button
		{
			Name = $"{definition.Id.Replace(".", "_")}Node",
			Text = definition.IconText,
			OffsetLeft = position.X,
			OffsetTop = position.Y,
			OffsetRight = position.X + 72.0f,
			OffsetBottom = position.Y + 72.0f,
			TooltipText = $"{definition.DisplayName} - clique direito para detalhes" +
				(definition.IsImplemented ? "" : "\nEm planejamento; compra indisponível.")
		};
		button.Pressed += () => BuySkill(definition.Id);
		button.GuiInput += @event =>
		{
			if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Right)
				return;

			ShowSkillDescription(definition);
			button.AcceptEvent();
		};
		_skillTreeCanvas.AddChild(button);
		_skillTreeButtons[definition.Id] = button;

		var label = new Label
		{
			Name = $"{definition.Id.Replace(".", "_")}Label",
			Text = definition.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			OffsetLeft = position.X - 56.0f,
			OffsetTop = position.Y + 76.0f,
			OffsetRight = position.X + 128.0f,
			OffsetBottom = position.Y + 122.0f
		};
		_skillTreeCanvas.AddChild(label);
	}

	private void CreateSkillLink(Vector2 from, Vector2 to)
	{
		var start = from + new Vector2(36.0f, 36.0f);
		var end = to + new Vector2(36.0f, 36.0f);
		var corner = new Vector2(end.X, start.Y);
		CreateSkillLinkSegment(start, corner);
		CreateSkillLinkSegment(corner, end);
	}

	private void CreateSkillLinkSegment(Vector2 from, Vector2 to)
	{
		var horizontal = Mathf.Abs(to.X - from.X) >= Mathf.Abs(to.Y - from.Y);
		var link = new ColorRect { Name = "SkillLink", Color = new Color(0.55f, 0.08f, 0.08f, 0.8f) };
		if (horizontal)
		{
			link.OffsetLeft = Mathf.Min(from.X, to.X);
			link.OffsetTop = from.Y - 2.0f;
			link.OffsetRight = Mathf.Max(from.X, to.X) + 4.0f;
			link.OffsetBottom = from.Y + 2.0f;
		}
		else
		{
			link.OffsetLeft = from.X - 2.0f;
			link.OffsetTop = Mathf.Min(from.Y, to.Y);
			link.OffsetRight = from.X + 2.0f;
			link.OffsetBottom = Mathf.Max(from.Y, to.Y) + 4.0f;
		}
		_skillTreeCanvas.AddChild(link);
	}

	private void BuySkill(string skillId)
	{
		if (!SkillCatalog.TryGet(skillId, out var definition))
		{
			SetBloodFountainStatus("Habilidade desconhecida.");
			return;
		}

		if (!definition.IsImplemented)
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda está em planejamento.");
			return;
		}

		var currentRank = CharacterSheet.GetSkillRank(skillId);
		var maxRank = GetSkillMaxRank(definition);
		if (currentRank >= maxRank)
		{
			SetBloodFountainStatus($"{definition.DisplayName} ja esta no nivel maximo ({maxRank}).");
			return;
		}

		if (!SkillCatalog.HasPrerequisites(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda esta bloqueada.");
			return;
		}

		if (SkillCatalog.HasExclusiveConflict(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} conflita com outra escolha já adquirida.");
			return;
		}

		var cost = GetSkillUpgradeCost(definition);
		if (!CharacterSheet.SpendBloodCoins(cost))
		{
			SetBloodFountainStatus($"Moedas insuficientes para {definition.DisplayName}.");
			return;
		}

		if (!CharacterSheet.AddSkill(skillId))
		{
			CharacterSheet.AddBloodCoins(cost);
			SetBloodFountainStatus($"Nao foi possivel adquirir {definition.DisplayName}.");
			return;
		}

		_coinsLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins}";
		RefreshCharacterSheet();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		RefreshBloodFountain();
		SetBloodFountainStatus($"{definition.DisplayName} aprimorada para o nivel {currentRank + 1}/{maxRank}.");
	}

	private static int GetSkillMaxRank(SkillDefinition definition)
	{
		return Mathf.Max(CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank), 1);
	}

	private static int GetSkillUpgradeCost(SkillDefinition definition)
	{
		var baseCost = CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost);
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		return Mathf.RoundToInt(baseCost * (1.0f + currentRank * 0.5f));
	}

	private void AddCheatBloodCoins()
	{
		CharacterSheet.AddBloodCoins(500);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
		SetBloodFountainStatus("+500 moedas adicionadas para teste.");
	}

	private void RefreshBloodFountain()
	{
		if (_bloodFountainStatusLabel == null)
			return;

		_bloodFountainStatusLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins}";
		RefreshSkillTreeButtons();
	}

	private void RefreshSkillTreeButtons()
	{
		foreach (var pair in _skillTreeButtons)
		{
			if (!SkillCatalog.TryGet(pair.Key, out var definition))
				continue;

			var button = pair.Value;
			var currentRank = CharacterSheet.GetSkillRank(definition.Id);
			var maxRank = GetSkillMaxRank(definition);
			var hasSkill = currentRank > 0;
			var isMaxRank = currentRank >= maxRank;
			var unlocked = SkillCatalog.HasPrerequisites(definition);
			var exclusiveConflict = SkillCatalog.HasExclusiveConflict(definition);
			var cost = GetSkillUpgradeCost(definition);
			var canAfford = CharacterSheet.BloodCoins >= cost;

			button.Disabled = false;
			button.Text = $"{definition.IconText}\n{currentRank}/{maxRank}";

			if (!definition.IsImplemented)
				button.Modulate = new Color(0.48f, 0.34f, 0.58f);
			else if (isMaxRank)
				button.Modulate = new Color(0.75f, 1.0f, 0.78f);
			else if (hasSkill && canAfford)
				button.Modulate = new Color(0.9f, 0.72f, 1.0f);
			else if (exclusiveConflict)
				button.Modulate = new Color(0.32f, 0.28f, 0.34f);
			else if (!unlocked)
				button.Modulate = new Color(0.42f, 0.42f, 0.46f);
			else if (canAfford)
				button.Modulate = new Color(1.0f, 0.82f, 0.78f);
			else
				button.Modulate = new Color(0.78f, 0.58f, 0.56f);
		}
	}

	private void SetBloodFountainStatus(string text)
	{
		if (_bloodFountainStatusLabel != null)
			_bloodFountainStatusLabel.Text = $"{text}\nMoedas de Sangue: {CharacterSheet.BloodCoins}";
	}

	private void AdjustBloodFountainZoom(float delta)
	{
		_bloodFountainZoom = Mathf.Clamp(_bloodFountainZoom + delta, 0.65f, 1.65f);

		ApplySkillTreeTransform();

		if (_bloodFountainStatusLabel != null)
			_bloodFountainStatusLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins} | Zoom {_bloodFountainZoom:0.00}x";
	}

	private void PanBloodFountain(Vector2 delta)
	{
		_skillTreePan += delta;
		_skillTreePan.X = Mathf.Clamp(_skillTreePan.X, -700.0f, 700.0f);
		_skillTreePan.Y = Mathf.Clamp(_skillTreePan.Y, -420.0f, 420.0f);
		ApplySkillTreeTransform();
	}

	private void ApplySkillTreeTransform()
	{
		if (_skillTreeCanvas == null)
			return;

		_skillTreeCanvas.Scale = Vector2.One * _bloodFountainZoom;
		_skillTreeCanvas.Position = _skillTreePan;
	}

	private void BuildSkillDescriptionPopup()
	{
		_skillDescriptionPopup = new ColorRect
		{
			Name = "SkillDescriptionPopup",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.94f),
			AnchorLeft = 0.22f,
			AnchorTop = 0.14f,
			AnchorRight = 0.78f,
			AnchorBottom = 0.86f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			Visible = false
		};
		_bloodFountainOverlay.AddChild(_skillDescriptionPopup);

		var descriptionScroll = new ScrollContainer
		{
			Name = "SkillDescriptionScroll",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 24.0f,
			OffsetTop = 22.0f,
			OffsetRight = -24.0f,
			OffsetBottom = -88.0f,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_skillDescriptionPopup.AddChild(descriptionScroll);

		_skillDescriptionLabel = new Label
		{
			Name = "SkillDescriptionText",
			CustomMinimumSize = new Vector2(360.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		descriptionScroll.AddChild(_skillDescriptionLabel);

		var closeButton = new Button
		{
			Name = "CloseSkillDescription",
			Text = "Fechar",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -76.0f,
			OffsetTop = -66.0f,
			OffsetRight = 76.0f,
			OffsetBottom = -22.0f
		};
		closeButton.Pressed += () => _skillDescriptionPopup.Visible = false;
		_skillDescriptionPopup.AddChild(closeButton);
	}

	private void ShowSkillDescription(SkillDefinition definition)
	{
		if (_skillDescriptionPopup == null || _skillDescriptionLabel == null)
			return;

		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		var maxRank = GetSkillMaxRank(definition);
		var hasSkill = currentRank > 0;
		var status = !definition.IsImplemented
			? hasSkill
				? "Adquirida em save anterior - efeito planejado"
				: "Planejada - compra indisponível"
			: currentRank >= maxRank
				? "Nivel maximo"
				: hasSkill
					? "Disponivel para aprimorar"
				: SkillCatalog.HasExclusiveConflict(definition)
					? "Bloqueada por escolha exclusiva"
					: SkillCatalog.HasPrerequisites(definition)
						? "Disponível"
						: "Bloqueada";

		_skillDescriptionLabel.Text =
			$"{definition.DisplayName}\n" +
			$"{definition.Tree} - Passiva - Tier {definition.Tier}\n\n" +
			$"{definition.Description}\n\n{definition.FlavorText}\n\n" +
			$"Nivel: {currentRank}/{maxRank}\n" +
			$"Custo do proximo nivel: {(currentRank >= maxRank ? 0 : GetSkillUpgradeCost(definition))} moedas\n" +
			$"Estado: {status}\n" +
			$"Pre-requisitos: {FormatPrerequisites(definition)}";
		_skillDescriptionPopup.Visible = true;
	}

	private string FormatPrerequisites(SkillDefinition definition)
	{
		if (definition.Prerequisites.Length == 0)
			return "nenhum";

		var text = "";
		for (var i = 0; i < definition.Prerequisites.Length; i++)
		{
			if (SkillCatalog.TryGet(definition.Prerequisites[i], out var prerequisite))
				text += prerequisite.DisplayName;
			else
				text += definition.Prerequisites[i];

			if (i < definition.Prerequisites.Length - 1)
				text += ", ";
		}

		return text;
	}

	private void CloseBloodFountain()
	{
		if (_bloodFountainOverlay != null)
			_bloodFountainOverlay.Visible = false;
	}

	private void BuildArtifactChestOverlay(Control root)
	{
		_artifactChestOverlay = new ColorRect
		{
			Name = "ArtifactChestOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.88f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -520.0f,
			OffsetTop = -300.0f,
			OffsetRight = 520.0f,
			OffsetBottom = 300.0f,
			Visible = false
		};
		root.AddChild(_artifactChestOverlay);

		var title = new Label
		{
			Text = "BAU DE ARTEFATOS",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(title);

		_artifactChestStatusLabel = new Label
		{
			Text = "Artefatos encontrados na run atual e na colecao permanente.",
			AnchorRight = 1.0f,
			OffsetTop = 58.0f,
			OffsetBottom = 92.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(_artifactChestStatusLabel);

		var slotsTitle = new Label
		{
			Text = "Equipados para a proxima run",
			OffsetLeft = 40.0f,
			OffsetTop = 110.0f,
			OffsetRight = 420.0f,
			OffsetBottom = 140.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		slotsTitle.Visible = false;
		_artifactChestOverlay.AddChild(slotsTitle);

		var slotColumn = new VBoxContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 150.0f,
			OffsetRight = 400.0f,
			OffsetBottom = 360.0f
		};
		slotColumn.Visible = false;
		_artifactChestOverlay.AddChild(slotColumn);

		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var slotIndex = i;
			var button = new Button
			{
				Text = $"{slotIndex + 1}: vazio",
				CustomMinimumSize = new Vector2(320.0f, 42.0f)
			};
			button.Pressed += () => SelectArtifactSlot(slotIndex);
			_artifactSlotButtons[i] = button;
			slotColumn.AddChild(button);
		}

		var unequipButton = new Button
		{
			Text = "Remover do slot selecionado",
			CustomMinimumSize = new Vector2(320.0f, 40.0f)
		};
		unequipButton.Pressed += UnequipSelectedArtifactSlot;
		slotColumn.AddChild(unequipButton);

		var collectionTitle = new Label
		{
			Text = "Artefatos encontrados",
			OffsetLeft = 56.0f,
			OffsetTop = 110.0f,
			OffsetRight = 980.0f,
			OffsetBottom = 140.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(collectionTitle);

		var collectionScroll = new ScrollContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 150.0f,
			OffsetRight = 984.0f,
			OffsetBottom = 470.0f
		};
		_artifactChestOverlay.AddChild(collectionScroll);

		_artifactCollectionList = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(900.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		collectionScroll.AddChild(_artifactCollectionList);

		var closeButton = new Button
		{
			Text = "Fechar",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -90.0f,
			OffsetTop = -58.0f,
			OffsetRight = 90.0f,
			OffsetBottom = -18.0f
		};
		closeButton.Pressed += CloseArtifactChest;
		_artifactChestOverlay.AddChild(closeButton);
	}

	private void SelectArtifactSlot(int slotIndex)
	{
		_selectedArtifactSlot = slotIndex;
		RefreshArtifactChest();
	}

	private void EquipArtifactInSelectedSlot(string artifactName)
	{
		if (!CharacterSheet.EquipArtifact(_selectedArtifactSlot, artifactName))
		{
			SetArtifactChestStatus($"Nao foi possivel equipar {artifactName}.");
			return;
		}

		SetArtifactChestStatus($"{artifactName} equipado no slot {_selectedArtifactSlot + 1}.");
		RefreshArtifactChest();
		RefreshCharacterSheet();
	}

	private void UnequipSelectedArtifactSlot()
	{
		CharacterSheet.UnequipArtifact(_selectedArtifactSlot);
		SetArtifactChestStatus($"Slot {_selectedArtifactSlot + 1} vazio.");
		RefreshArtifactChest();
		RefreshCharacterSheet();
	}

	private void RefreshArtifactChest()
	{
		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var button = _artifactSlotButtons[i];
			if (button == null)
				continue;

			var artifactName = CharacterSheet.GetEquippedArtifactSlot(i);
			button.Text = $"{i + 1}: {(string.IsNullOrWhiteSpace(artifactName) ? "vazio" : artifactName)}";
			button.Modulate = i == _selectedArtifactSlot ? new Color(1.0f, 0.35f, 0.32f) : Colors.White;
		}

		RefreshArtifactCollectionList();
	}

	private void RefreshArtifactCollectionList()
	{
		if (_artifactCollectionList == null)
			return;

		foreach (var child in _artifactCollectionList.GetChildren())
			child.QueueFree();

		if (RunState.RunArtifacts.Count == 0 && CharacterSheet.FoundArtifacts.Count == 0)
		{
			_artifactCollectionList.AddChild(new Label
			{
				Text = "Nenhum artefato encontrado nesta run ou na colecao permanente.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(480.0f, 60.0f)
			});
			return;
		}

		var displayed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var artifact in RunState.RunArtifacts)
		{
			AddArtifactCollectionEntry(artifact, "RUN ATUAL");
			displayed.Add(ArtifactCollectionKey(artifact));
		}

		foreach (var artifact in CharacterSheet.FoundArtifacts)
			if (displayed.Add(ArtifactCollectionKey(artifact)))
				AddArtifactCollectionEntry(artifact, "COLECAO PERMANENTE");
	}

	private void AddArtifactCollectionEntry(ArtifactRecord artifact, string source)
	{
		var details = ArtifactCatalog.TryGet(artifact.Id, out var definition)
			? $"[{source}] {definition.DisplayName} | {definition.Rarity}\n{definition.Description}\nEfeitos: {FormatArtifactEffects(definition)}"
			: $"[{source}] {artifact.Name} | {artifact.Effect} {artifact.Amount:+0.##;-0.##;0}";
		_artifactCollectionList.AddChild(new Label
		{
			Text = details,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(880.0f, 82.0f)
		});
	}

	private static string ArtifactCollectionKey(ArtifactRecord artifact)
	{
		return string.IsNullOrWhiteSpace(artifact.Id) ? artifact.Name : artifact.Id;
	}

	private void SetArtifactChestStatus(string text)
	{
		if (_artifactChestStatusLabel != null)
			_artifactChestStatusLabel.Text = text;
	}

	private void CloseArtifactChest()
	{
		if (_artifactChestOverlay != null)
			_artifactChestOverlay.Visible = false;
	}

	public void OpenArtifactOffer(RoomArtifact source)
	{
		if (_artifactOfferOverlay == null)
			return;

		_pendingArtifactSource = source;
		BuildArtifactOfferChoices();
		_artifactOfferOverlay.Visible = true;
		GetTree().Paused = true;
		ControllerUiSupport.FocusFirst(_artifactOfferOverlay);
	}

	private void BuildArtifactOfferOverlay(Control root)
	{
		_artifactOfferOverlay = new ColorRect
		{
			Name = "ArtifactOfferOverlay",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.93f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -620.0f,
			OffsetTop = -330.0f,
			OffsetRight = 620.0f,
			OffsetBottom = 330.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_artifactOfferOverlay);

		var title = new Label
		{
			Text = "TESOURO DE ARTEFATOS",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactOfferOverlay.AddChild(title);

		_artifactOfferStatusLabel = new Label
		{
			Text = "Escolha um dos tres artefatos.",
			AnchorRight = 1.0f,
			OffsetTop = 58.0f,
			OffsetBottom = 92.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_artifactOfferOverlay.AddChild(_artifactOfferStatusLabel);

		_artifactRerollButton = new Button
		{
			Text = "Trocar oferta",
			OffsetLeft = 980.0f, OffsetTop = 68.0f, OffsetRight = 1180.0f, OffsetBottom = 106.0f
		};
		_artifactRerollButton.Pressed += RerollArtifactOffer;
		_artifactOfferOverlay.AddChild(_artifactRerollButton);

		_artifactOfferList = new VBoxContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 112.0f,
			OffsetRight = 1184.0f,
			OffsetBottom = 570.0f
		};
		_artifactOfferOverlay.AddChild(_artifactOfferList);
	}

	private void BuildArtifactOfferChoices()
	{
		ClearArtifactOfferList();

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var choices = ArtifactCatalog.RollChoices(rng, 3, BuildExcludedArtifactOfferIds());
		if (_artifactRerollButton != null)
			_artifactRerollButton.Disabled = !(GetPlayerSkillController()?.CanRerollReward() ?? false);

		if (choices.Count == 0)
		{
			_artifactOfferStatusLabel.Text = "Nenhum artefato disponivel no catalogo.";
			_artifactOfferOverlay.Visible = false;
			GetTree().Paused = false;
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}

		_artifactOfferStatusLabel.Text = "Escolha um artefato. Raridade e peso influenciam as opcoes que aparecem.";
		foreach (var artifact in choices)
			AddArtifactOfferButton(artifact);
	}

	private void RerollArtifactOffer()
	{
		if (GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildArtifactOfferChoices();
		_artifactOfferStatusLabel.Text = "O Espelho do Destino reescreveu a oferta deste mapa.";
	}

	private void AddArtifactOfferButton(ArtifactDefinition artifact)
	{
		var button = new Button
		{
			Text = $"{artifact.DisplayName} | {artifact.Rarity} | {artifact.Slot}\n{artifact.Description}\nEfeitos: {FormatArtifactEffects(artifact)}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1080.0f, 112.0f)
		};
		button.Pressed += () => SelectArtifactOffer(artifact);
		_artifactOfferList.AddChild(button);
	}

	private HashSet<string> BuildExcludedArtifactOfferIds()
	{
		var excluded = new HashSet<string>();

		foreach (var artifact in RunState.RunArtifacts)
		{
			if (!string.IsNullOrWhiteSpace(artifact.Id))
				excluded.Add(artifact.Id);
		}

		foreach (var artifact in CharacterSheet.GetEquippedArtifacts())
		{
			if (!string.IsNullOrWhiteSpace(artifact.Id))
				excluded.Add(artifact.Id);
		}

		return excluded;
	}

	private void SelectArtifactOffer(ArtifactDefinition artifact)
	{
		var record = ArtifactCatalog.CreateRecord(artifact);

		CollectArtifact(record);
	}

	private void CollectArtifact(ArtifactRecord artifact)
	{
		RunState.AddTemporaryArtifact(artifact);
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		NotifyArtifactCollected(artifact.Name);
		_pendingArtifactSource?.Consume();
		_pendingArtifactSource = null;
		_artifactOfferOverlay.Visible = false;
		GetTree().Paused = false;
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
		RefreshCharacterSheet();
	}

	private void ClearArtifactOfferList()
	{
		if (_artifactOfferList == null)
			return;

		foreach (var child in _artifactOfferList.GetChildren())
			child.QueueFree();
	}

	public void OpenBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(false);
	}

	public void OpenMandatoryBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(true);
	}

	private async void OpenBloodBlessingOfferInternal(bool mandatory)
	{
		if (_bloodBlessingOverlay == null) return;
		if (mandatory)
		{
			AppendGameLog("A fonte primordial revelou a primeira Bencao de Sangue desta run.");
			BuildBloodBlessingChoices();
			return;
		}
		GetTree().Paused = false;
		_bloodAbsorptionOverlay.Visible = true;
		_bloodAbsorptionOverlay.Modulate = Colors.White;
		_bloodAbsorptionOverlay.Color = new Color(0.16f, 0.0f, 0.018f, 0.0f);
		_bloodAbsorptionLabel.Text = "SANGUIS PURGATUS";
		_bloodAbsorptionLabel.Modulate = Colors.Transparent;
		var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		var essences = new List<Node3D>();
		var startingPositions = new List<Vector3>();
		var spiralPhases = new List<float>();
		foreach (var node in GetTree().GetNodesInGroup("blood_essences"))
		{
			if (node is not Node3D essence)
				continue;
			essences.Add(essence);
			startingPositions.Add(essence.GlobalPosition);
			spiralPhases.Add((float)GD.RandRange(0.0, Mathf.Tau));
			AttachSanguisTrail(essence);
		}
		var screenTween = _bloodAbsorptionLabel.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		screenTween.TweenProperty(_bloodAbsorptionLabel, "modulate", Colors.White, 0.4f);
		screenTween.TweenInterval(2.55f);
		screenTween.TweenProperty(_bloodAbsorptionLabel, "modulate", Colors.Transparent, 0.5f);
		var vignetteTween = _bloodAbsorptionOverlay.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.11f, 0.5f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.035f, 2.35f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.0f, 0.55f);

		const float absorptionDuration = 3.45f;
		var elapsed = 0.0f;
		while (elapsed < absorptionDuration && player != null && IsInstanceValid(player))
		{
			var delta = Mathf.Max((float)GetProcessDeltaTime(), 0.001f);
			var destination = player.GlobalPosition + Vector3.Up * 0.95f;
			for (var i = 0; i < essences.Count; i++)
			{
				var essence = essences[i];
				if (!IsInstanceValid(essence))
					continue;

				var delay = Mathf.Min(i * 0.024f, 0.42f);
				var progress = Mathf.Clamp((elapsed - delay) / Mathf.Max(absorptionDuration - delay, 0.1f), 0.0f, 1.0f);
				var eased = progress * progress * (3.0f - 2.0f * progress);
				var start = startingPositions[i];
				var planar = destination - start;
				planar.Y = 0.0f;
				var direction = planar.LengthSquared() > 0.001f ? planar.Normalized() : Vector3.Forward;
				var tangent = new Vector3(-direction.Z, 0.0f, direction.X);
				var distance = start.DistanceTo(destination);
				var spiralRadius = Mathf.Sin(progress * Mathf.Pi)
					* Mathf.Min(distance * 0.22f, 1.35f)
					* (1.0f - progress * 0.28f);
				var angle = spiralPhases[i] + progress * Mathf.Tau * 2.25f;
				var spiralOffset = tangent * Mathf.Cos(angle) * spiralRadius
					+ Vector3.Up * Mathf.Sin(angle) * spiralRadius * 0.48f;
				essence.GlobalPosition = start.Lerp(destination, eased) + spiralOffset;
				essence.Scale = Vector3.One * Mathf.Lerp(1.0f, 0.055f, Mathf.Pow(progress, 1.65f));
			}
			elapsed += delta;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		if (player != null && IsInstanceValid(player))
			SpawnSanguisArrivalPulse(player.GlobalPosition + Vector3.Up * 0.92f);
		await ToSignal(GetTree().CreateTimer(0.42f, true), SceneTreeTimer.SignalName.Timeout);
		foreach (var essence in essences)
			if (IsInstanceValid(essence))
				essence.QueueFree();
		_bloodAbsorptionOverlay.Visible = false;
		AppendGameLog("O sangue dos inimigos foi absorvido e condensado em uma Bencao de Sangue.");
		BuildBloodBlessingChoices();
	}

	private static void AttachSanguisTrail(Node3D essence)
	{
		var moteMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.34f, 0.008f, 0.018f, 0.72f),
			EmissionEnabled = true,
			Emission = new Color(0.28f, 0.004f, 0.012f),
			EmissionEnergyMultiplier = 0.85f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var particleMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.045f,
			Gravity = new Vector3(0.0f, -0.08f, 0.0f),
			InitialVelocityMin = 0.01f,
			InitialVelocityMax = 0.09f,
			ScaleMin = 0.35f,
			ScaleMax = 1.0f,
			Color = new Color(0.72f, 0.035f, 0.05f, 0.78f)
		};
		var trail = new GpuParticles3D
		{
			Name = "SanguisTrail",
			Amount = 14,
			Lifetime = 0.52f,
			Randomness = 0.62f,
			LocalCoords = false,
			ProcessMaterial = particleMaterial,
			DrawPass1 = new SphereMesh
			{
				Radius = 0.026f,
				Height = 0.052f,
				RadialSegments = 6,
				Rings = 3,
				Material = moteMaterial
			},
			VisibilityAabb = new Aabb(new Vector3(-2.0f, -2.0f, -2.0f), Vector3.One * 4.0f)
		};
		essence.AddChild(trail);
		trail.Emitting = true;
	}

	private async void SpawnSanguisArrivalPulse(Vector3 position)
	{
		var scene = GetTree().CurrentScene;
		if (scene == null)
			return;

		var root = new Node3D { Name = "SanguisArrivalPulse" };
		scene.AddChild(root);
		root.GlobalPosition = position;
		var pulseMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.32f, 0.006f, 0.018f, 0.26f),
			EmissionEnabled = true,
			Emission = new Color(0.22f, 0.003f, 0.01f),
			EmissionEnergyMultiplier = 0.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var pulse = new MeshInstance3D
		{
			Name = "AbsorptionPulse",
			Mesh = new SphereMesh
			{
				Radius = 0.42f,
				Height = 0.84f,
				RadialSegments = 16,
				Rings = 8,
				Material = pulseMaterial
			},
			Scale = Vector3.One * 0.08f
		};
		root.AddChild(pulse);
		var pulseTween = root.CreateTween().SetParallel();
		pulseTween.TweenProperty(pulse, "scale", Vector3.One * 1.55f, 0.38f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		pulseTween.TweenProperty(pulse, "transparency", 1.0f, 0.38f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		var random = new RandomNumberGenerator();
		random.Randomize();
		for (var i = 0; i < 11; i++)
		{
			var angle = random.RandfRange(0.0f, Mathf.Tau);
			var radius = random.RandfRange(0.22f, 0.68f);
			var start = new Vector3(Mathf.Cos(angle) * radius, random.RandfRange(-0.3f, 0.34f), Mathf.Sin(angle) * radius);
			var moteMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.5f, 0.01f, 0.025f, 0.82f),
				EmissionEnabled = true,
				Emission = new Color(0.38f, 0.006f, 0.018f),
				EmissionEnergyMultiplier = 0.9f,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
			var mote = new MeshInstance3D
			{
				Mesh = new SphereMesh
				{
					Radius = random.RandfRange(0.018f, 0.036f),
					Height = random.RandfRange(0.04f, 0.085f),
					RadialSegments = 6,
					Rings = 3,
					Material = moteMaterial
				},
				Position = start
			};
			root.AddChild(mote);
			var moteTween = root.CreateTween().SetParallel();
			moteTween.TweenProperty(mote, "position", Vector3.Zero, random.RandfRange(0.18f, 0.3f))
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			moteTween.TweenProperty(mote, "scale", Vector3.One * 0.08f, 0.3f);
			moteTween.TweenProperty(mote, "transparency", 1.0f, 0.32f);
		}

		await ToSignal(GetTree().CreateTimer(0.48f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(root))
			root.QueueFree();
	}

	private void BuildBloodBlessingOverlay(Control root)
	{
		_bloodBlessingOverlay = new ColorRect
		{
			Name = "BloodBlessingOverlay", Color = new Color(0.025f, 0.0f, 0.008f, 0.95f),
			AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = -620, OffsetTop = -340, OffsetRight = 620, OffsetBottom = 340,
			Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodBlessingOverlay);
		UIThemeFactory.AddSimpleFrame(
			_bloodBlessingOverlay,
			"BlessingChoiceFrame",
			new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
			Vector4.Zero,
			new Color(0.12f, 0.055f, 0.07f, 0.96f),
			12);
		_bloodAbsorptionOverlay = new ColorRect
		{
			Name = "BloodAbsorptionOverlay", Color = Colors.Transparent,
			AnchorRight = 1, AnchorBottom = 1, Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodAbsorptionOverlay);
		_bloodAbsorptionLabel = new Label
		{
			AnchorLeft = 0.2f, AnchorTop = 0.43f, AnchorRight = 0.8f, AnchorBottom = 0.57f,
			Text = "SANGUIS PURGATUS", HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_bloodAbsorptionLabel.AddThemeFontSizeOverride("font_size", 32);
		_bloodAbsorptionLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.02f, 0.05f));
		_bloodAbsorptionOverlay.AddChild(_bloodAbsorptionLabel);

		var title = new Label
		{
			Text = "Hereditas Sanguinis", AnchorRight = 1, OffsetTop = 18, OffsetBottom = 58,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 27);
		_bloodBlessingOverlay.AddChild(title);

		_bloodBlessingStatusLabel = new Label
		{
			Text = Localization.Text("hud.blessing_choice"), AnchorRight = 1, OffsetTop = 62, OffsetBottom = 100,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodBlessingOverlay.AddChild(_bloodBlessingStatusLabel);

		_blessingRerollButton = new Button
		{
			Text = "Trocar oferta",
			OffsetLeft = 980.0f, OffsetTop = 68.0f, OffsetRight = 1180.0f, OffsetBottom = 106.0f
		};
		_blessingRerollButton.Pressed += RerollBlessingOffer;
		_bloodBlessingOverlay.AddChild(_blessingRerollButton);

		_bloodBlessingList = new VBoxContainer
		{
			OffsetLeft = 56, OffsetTop = 116, OffsetRight = 1184, OffsetBottom = 640
		};
		_bloodBlessingOverlay.AddChild(_bloodBlessingList);
	}

	private void BuildBloodBlessingChoices()
	{
		foreach (var child in _bloodBlessingList.GetChildren()) child.QueueFree();
		_blessingChoiceButtons.Clear();
		_focusedBlessingChoice = 0;
		var rng = new RandomNumberGenerator(); rng.Randomize();
		var choices = BloodBlessingCatalog.RollOffers(rng, 3, out var family, RunState.RunBlessings.Count == 0);
		if (_blessingRerollButton != null)
			_blessingRerollButton.Disabled = !(GetPlayerSkillController()?.CanRerollReward() ?? false);
		if (choices.Count == 0)
		{
			GetTree().CallGroup("run_manager", "AddBloodCoins", 25);
			AppendGameLog("Todas as bencaos unicas foram encontradas. Sangue convertido em 25 moedas.");
			_bloodBlessingOverlay.Visible = false;
			GetTree().Paused = false;
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}

		_bloodBlessingOverlay.Visible = true;
		GetTree().Paused = true;
		_bloodBlessingStatusLabel.Text = $"O sangue revelou a familia {family}. Escolha uma bencao unica:";
		foreach (var offer in choices)
		{
			var blessing = offer.Definition;
			var replaced = RunState.GetBlessingInExclusiveGroup(blessing.ExclusiveGroup);
			var replacementWarning = replaced != null ? $"\n[color=#ffb347]SUBSTITUI: {replaced.Name}[/color]" : "";
			var card = new PanelContainer
			{
				CustomMinimumSize = new Vector2(1080, 125),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			card.AddThemeStyleboxOverride("panel", UIThemeFactory.ChoiceCard(BlessingRarityColor(offer.Rarity)));
			var content = new RichTextLabel
			{
				BbcodeEnabled = true, FitContent = true, ScrollActive = false,
				Text = $"[font_size=22][b]{blessing.DisplayName}[/b] | {FormatBlessingLineage(blessing)} | {offer.Rarity}[/font_size]\n{FormatEffectKeywords(FormatBloodBlessingDescription(blessing, offer.PowerMultiplier))}{replacementWarning}",
				CustomMinimumSize = new Vector2(1040, 115),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand,
				TooltipText = replaced != null ? $"Ao escolher, substitui {replaced.Name}." : FullBlessingTooltip(blessing, offer.Rarity, offer.PowerMultiplier)
			};
			content.GuiInput += input =>
			{
				if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) SelectBloodBlessing(offer);
			};
			content.MouseEntered += () =>
			{
				if (replaced != null) _bloodBlessingStatusLabel.Text = $"ATENÇÃO: esta escolha substituirá {replaced.Name}.";
			};
			content.MouseExited += () => _bloodBlessingStatusLabel.Text = $"O sangue revelou a familia {family}. Escolha uma bencao unica:";
			var row = new HBoxContainer();
			row.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath),
				CustomMinimumSize = new Vector2(92, 92),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			content.CustomMinimumSize = new Vector2(790, 115);
			row.AddChild(content);
			var chooseButton = new Button
			{
				Text = "Escolher",
				CustomMinimumSize = new Vector2(135.0f, 82.0f),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
			};
			chooseButton.Pressed += () => SelectBloodBlessing(offer);
			var choiceIndex = _blessingChoiceButtons.Count;
			chooseButton.FocusEntered += () => _focusedBlessingChoice = choiceIndex;
			_blessingChoiceButtons.Add(chooseButton);
			row.AddChild(chooseButton);
			card.AddChild(row);
			_bloodBlessingList.AddChild(card);
		}
		FocusBlessingChoice(0);
	}

	private void FocusBlessingChoice(int index)
	{
		if (_blessingChoiceButtons.Count == 0) return;
		_focusedBlessingChoice = Mathf.Clamp(index, 0, _blessingChoiceButtons.Count - 1);
		_blessingChoiceButtons[_focusedBlessingChoice].GrabFocus();
	}

	private void RerollBlessingOffer()
	{
		if (GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildBloodBlessingChoices();
		_bloodBlessingStatusLabel.Text = "O Espelho do Destino revelou uma nova linhagem.";
	}

	private PlayerSkillController GetPlayerSkillController()
	{
		return _runtimePlayerController?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")
			?? GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
	}

	private static Color BlessingRarityColor(string rarity) => rarity.ToLowerInvariant() switch
	{
		"stirps" => new Color(1.0f, 0.08f, 0.22f),
		"uncommon" => new Color(0.2f, 0.9f, 0.3f),
		"rare" => new Color(0.2f, 0.48f, 1.0f),
		"epic" => new Color(0.68f, 0.25f, 0.95f),
		"legendary" => new Color(1.0f, 0.48f, 0.08f),
		_ => Colors.White
	};

	private static string FormatBlessingLineage(BloodBlessingDefinition blessing)
	{
		return blessing.ParentFamilies.Length > 0
			? $"{blessing.Family}: {string.Join(" + ", blessing.ParentFamilies)}"
			: blessing.Family;
	}

	private void SelectBloodBlessing(BloodBlessingOffer offer)
	{
		var blessing = offer.Definition;
		if (!RunState.AddBloodBlessing(offer)) return;
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_select");
		AppendGameLog($"Bencao adquirida: {blessing.DisplayName} [{offer.Rarity}].");
		ShowEnemyDefeatedToast($"{offer.Rarity}: {blessing.DisplayName}");
		_bloodBlessingOverlay.Visible = false;
		GetTree().Paused = false;
		RefreshCharacterSheet();
		RefreshBlessingHud();
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
	}

	private string FormatBloodBlessingEffects(BloodBlessingDefinition blessing, float powerMultiplier = 1.0f)
	{
		var text = "Efeitos: ";
		for (var i = 0; i < blessing.Effects.Length; i++)
		{
			var effect = blessing.Effects[i];
			var value = effect.BooleanValue ? 1.0f : GetDisplayedBlessingValue(effect, effect.Value * powerMultiplier);
			text += $"{ArtifactEffectRegistry.FriendlyName(effect.Type)} {FormatArtifactOperation(effect.Operation, value)}";
			if (i < blessing.Effects.Length - 1) text += " | ";
		}
		return text;
	}

	private static string FormatBloodBlessingDescription(BloodBlessingDefinition blessing, float effectivePowerMultiplier)
	{
		var description = blessing.Description;
		for (var i = 0; i < blessing.Effects.Length; i++)
		{
			var effect = blessing.Effects[i];
			if (effect.BooleanValue || effect.Operation is "Set" or "SetBool")
				continue;

			var baseValue = CompendiumTuning.GetFloat($"blessing.{blessing.Id}.effect.{i}.value", effect.Value);
			var percentValue = effect.Value * 100.0f;
			var percentPattern = NumberPattern(percentValue) + @"\s*%";
			var usesPercent = effect.Operation.Contains("Multiplier", StringComparison.OrdinalIgnoreCase)
				|| effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase)
				|| Regex.IsMatch(description, percentPattern, RegexOptions.IgnoreCase);
			var sourcePattern = usesPercent ? percentPattern : NumberPattern(effect.Value);
			var effectiveValue = GetDisplayedBlessingValue(effect, baseValue * effectivePowerMultiplier);
			if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat")
				effectiveValue *= RunState.MaxHealthMultiplier * RunState.SacrificedMaxHealthMultiplier;
			var replacement = usesPercent
				? $"{FormatLocalizedNumber(effectiveValue * 100.0f)}%"
				: FormatLocalizedNumber(effectiveValue);
			description = ReplaceFirst(description, sourcePattern, replacement);
		}
		return description;
	}

	private static float GetDisplayedBlessingValue(BloodBlessingEffectDefinition effect, float value)
	{
		if (effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase))
			return Mathf.Clamp(value, 0.0f, 1.0f);
		if (effect.Type == "PestilenceSpreadTargets")
			return Mathf.Round(value);
		return value;
	}

	private static string NumberPattern(float value)
	{
		var invariant = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		return $@"(?<![\d.,]){Regex.Escape(invariant).Replace(@"\.", @"[.,]")}(?![\d.,])";
	}

	private static string FormatLocalizedNumber(float value)
	{
		return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
	}

	private static string ReplaceFirst(string text, string pattern, string replacement)
	{
		var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
		if (!match.Success)
			return text;
		return text[..match.Index] + replacement + text[(match.Index + match.Length)..];
	}

	private static string FormatEffectKeywords(string text)
	{
		text = Regex.Replace(text, @"\b(Ataques? básicos?|Ataques? rápidos?|Ataques? especiais?|Fúria Bestial)\b", match =>
		{
			var lower = match.Value.ToLowerInvariant();
			if (lower.Contains("básic"))
				return $"[hint=\"Ataque principal no botão esquerdo; encadeia o combo da arma equipada.\"][color=#ffd166][b]{match.Value}[/b][/color][/hint]";
			if (lower.Contains("rápid"))
				return $"[hint=\"Golpe executado ao atacar durante a janela da esquiva.\"][color=#72e6ff][b]{match.Value}[/b][/color][/hint]";
			if (lower.Contains("especial"))
				return $"[hint=\"Ataque da arma no Q; interrompe o combo básico e usa a forma especial da arma.\"][color=#d58cff][b]{match.Value}[/b][/color][/hint]";
			return $"[hint=\"Estado ativado no F quando desbloqueado; consome a barra de Fúria para ampliar o poder ofensivo.\"][color=#ff4d5f][b]{match.Value}[/b][/color][/hint]";
		}, RegexOptions.IgnoreCase);

		return Regex.Replace(text, @"\b(Sangramento|Sangrando|Pestilência|Pestilento|Pestilenta|Encanto|Encantar|Encantado|Encantada|Encantados|Encantadas|Barreira|Fascínio|Hospedeiro|Hospedeiros|Interrompido|Interrompida|Ferida recuperável)\b", match =>
		{
			var lower = match.Value.ToLowerInvariant();
			if (lower.StartsWith("sangr"))
				return $"[hint=\"Causa dano periódico cumulativo em um único inimigo.\"][color=#ff4055]{match.Value}[/color][/hint]";
			if (lower.StartsWith("pest"))
				return $"[hint=\"Cada acúmulo aumenta o dano recebido; ao morrer, a infecção pode se espalhar.\"][color=#78d957]{match.Value}[/color][/hint]";
			if (lower.StartsWith("barreira"))
				return $"[hint=\"Vida temporária consumida antes da vida normal.\"][color=#63c7ff]{match.Value}[/color][/hint]";
			if (lower.StartsWith("fasc"))
				return $"[hint=\"Chefes e inimigos imunes a Encanto sofrem 25% mais dano.\"][color=#ff6fe8]{match.Value}[/color][/hint]";
			if (lower.StartsWith("hosp"))
				return $"[hint=\"Recebem mais dano e espalham um acúmulo de Pestilência em área ao serem atingidos.\"][color=#b4ff6a]{match.Value}[/color][/hint]";
			if (lower.StartsWith("interromp"))
				return $"[hint=\"Interrupção impede temporariamente a reação ou o ataque.\"][color=#ffd166]{match.Value}[/color][/hint]";
			if (lower.StartsWith("ferida"))
				return $"[hint=\"Parte do dano é restaurada gradualmente se você não sofrer outro golpe.\"][color=#ff9f9f]{match.Value}[/color][/hint]";
			return $"[hint=\"O inimigo passa a atacar outros inimigos; sem alvo, vagueia pela sala.\"][color=#dd77ff]{match.Value}[/color][/hint]";
		}, RegexOptions.IgnoreCase);
	}

	private string FullBlessingTooltip(BloodBlessingDefinition blessing, string rarity, float powerMultiplier, int level = 1)
	{
		return $"{blessing.DisplayName} [{rarity}] — N{level}\n{FormatBloodBlessingDescription(blessing, powerMultiplier)}\n\n{blessing.FlavorText}";
	}

	private void RefreshBlessingHud()
	{
		if (_blessingHudList == null) return;
		RefreshFuryVisibility();
		foreach (var child in _blessingHudList.GetChildren()) child.QueueFree();
		const int visibleBlessingLimit = 7;
		var visibleCount = Mathf.Min(RunState.RunBlessings.Count, visibleBlessingLimit);
		for (var i = 0; i < visibleCount; i++)
		{
			var record = RunState.RunBlessings[i];
			if (!BloodBlessingCatalog.TryGet(record.Id, out var blessing)) continue;
			var iconContainer = new Control
			{
				CustomMinimumSize = new Vector2(44.0f, 44.0f),
				TooltipText = FullBlessingTooltip(blessing, record.Rarity, record.EffectivePowerMultiplier, record.Level),
				MouseFilter = Control.MouseFilterEnum.Pass,
				MouseDefaultCursorShape = Control.CursorShape.Help
			};
			iconContainer.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimplePanelPath),
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Modulate = new Color(0.12f, 0.08f, 0.09f, 0.72f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			var icon = new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath),
				AnchorRight = 1.0f, AnchorBottom = 1.0f,
				OffsetLeft = 4.0f, OffsetTop = 4.0f, OffsetRight = -4.0f, OffsetBottom = -4.0f,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			iconContainer.AddChild(icon);
			var blessingLevel = record.Level;
			if (blessingLevel > 1)
			{
				var levelBadge = new Label
				{
					Text = $"N{blessingLevel}",
					AnchorLeft = 1.0f,
					AnchorTop = 1.0f,
					AnchorRight = 1.0f,
					AnchorBottom = 1.0f,
					OffsetLeft = -24.0f,
					OffsetTop = -18.0f,
					OffsetRight = 1.0f,
					OffsetBottom = 1.0f,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				levelBadge.AddThemeFontSizeOverride("font_size", 13);
				levelBadge.AddThemeColorOverride("font_color", Colors.White);
				levelBadge.AddThemeColorOverride("font_shadow_color", Colors.Black);
				levelBadge.AddThemeConstantOverride("shadow_offset_x", 2);
				levelBadge.AddThemeConstantOverride("shadow_offset_y", 2);
				iconContainer.AddChild(levelBadge);
			}
			_blessingHudList.AddChild(iconContainer);
		}

		if (RunState.RunBlessings.Count <= visibleBlessingLimit)
			return;

		var hiddenCount = RunState.RunBlessings.Count - visibleBlessingLimit;
		var more = new Label
		{
			Text = "+",
			TooltipText = $"Mais {hiddenCount} bênção(ões). Pressione C para ver todas.",
			CustomMinimumSize = new Vector2(44.0f, 40.0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Pass,
			MouseDefaultCursorShape = Control.CursorShape.Help,
			Modulate = new Color(1.0f, 0.3f, 0.34f)
		};
		more.AddThemeFontSizeOverride("font_size", 30);
		_blessingHudList.AddChild(more);
	}

	public void RefreshBlessingDisplays()
	{
		RefreshBlessingHud();
		RefreshCharacterSheet();
	}

	private void ToggleCharacterSheet()
	{
		if (_characterSheetOverlay == null)
			return;

		_characterSheetOverlay.Visible = !_characterSheetOverlay.Visible;
		if (_characterSheetOverlay.Visible)
		{
			SetModalPause(true);
			RefreshCharacterSheet();
			ControllerUiSupport.FocusFirst(_characterSheetOverlay);
		}
		else
			RefreshModalPause();
	}

	private void RefreshCharacterSheet()
	{
		if (_characterBlessingsColumn == null || _characterAttributesColumn == null || _characterInventoryColumn == null || _characterLoadoutColumn == null)
			return;

		ClearCharacterSheetColumn(_characterBlessingsColumn);
		ClearCharacterSheetColumn(_characterAttributesColumn);
		ClearCharacterSheetColumn(_characterInventoryColumn);
		ClearCharacterSheetColumn(_characterLoadoutColumn);

		AddCharacterSheetBlessings(_characterBlessingsColumn);

		AddCharacterSheetText(_characterAttributesColumn, $"Moedas de Sangue: {CharacterSheet.BloodCoins}");
		AddCharacterSheetText(_characterAttributesColumn, $"Forca: {CharacterSheet.GetAttribute(CharacterAttribute.Strength)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Destreza: {CharacterSheet.GetAttribute(CharacterAttribute.Dexterity)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vitalidade: {CharacterSheet.GetAttribute(CharacterAttribute.Vitality)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Determinacao: {CharacterSheet.GetAttribute(CharacterAttribute.Resolve)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Sangue: {CharacterSheet.GetAttribute(CharacterAttribute.Bloodcraft)}");
		AddCharacterSheetSpacer(_characterAttributesColumn);
		AddCharacterSheetSection(_characterAttributesColumn, "Status detalhados");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano atributos: x{CharacterSheet.GetDamageMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano run: x{RunState.DamageMultiplier:0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano tuning: x{GameTuning.GetDamageMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vida bonus atributos: +{CharacterSheet.GetMaxHealthBonus():0}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vida bonus artefatos: +{RunState.MaxHealthBonus:0}");
		AddCharacterSheetText(_characterAttributesColumn, $"Velocidade: x{CharacterSheet.GetMoveSpeedMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Furia: x{CharacterSheet.GetFuryMultiplier():0.00}");

		AddCharacterSheetSection(_characterInventoryColumn, "Artefatos da run");
		AddCharacterSheetLines(_characterInventoryColumn, FormatRunArtifacts());
		AddCharacterSheetSection(_characterInventoryColumn, "Colecao permanente");
		AddCharacterSheetLines(_characterInventoryColumn, FormatArtifacts());

		AddCharacterSheetSection(_characterLoadoutColumn, "Habilidades passivas");
		AddCharacterSheetSkills(_characterLoadoutColumn);
		AddCharacterSheetSection(_characterLoadoutColumn, "Artefatos equipados");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatEquippedArtifacts());
		AddCharacterSheetSection(_characterLoadoutColumn, "Impacto dos artefatos");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatArtifactImpacts());
	}

	private void ClearCharacterSheetColumn(VBoxContainer column)
	{
		for (var i = column.GetChildCount() - 1; i >= 1; i--)
			column.GetChild(i).QueueFree();
	}

	private void AddCharacterSheetSection(VBoxContainer column, string title)
	{
		AddCharacterSheetSpacer(column);
		AddCharacterSheetText(column, title, true);
	}

	private void AddCharacterSheetLines(VBoxContainer column, string lines)
	{
		foreach (var line in lines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			AddCharacterSheetText(column, line);
	}

	private void AddCharacterSheetBlessings(VBoxContainer column)
	{
		if (RunState.RunBlessings.Count == 0)
		{
			AddCharacterSheetText(column, "- nenhuma");
			return;
		}
		foreach (var record in RunState.RunBlessings)
		{
			if (!BloodBlessingCatalog.TryGet(record.Id, out var blessing)) continue;
			var row = new HBoxContainer
			{
				TooltipText = FullBlessingTooltip(blessing, record.Rarity, record.EffectivePowerMultiplier, record.Level),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath), CustomMinimumSize = new Vector2(38, 38),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			row.AddChild(new Label
			{
				Text = $"[{record.Rarity}] {record.Name} — N{record.Level}",
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			column.AddChild(row);
		}
	}

	private void AddCharacterSheetText(VBoxContainer column, string text, bool centered = false)
	{
		column.AddChild(new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left
		});
	}

	private void AddCharacterSheetSpacer(VBoxContainer column)
	{
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 10.0f) });
	}

	private string FormatSkills()
	{
		if (CharacterSheet.AcquiredSkills.Count == 0)
			return "- nenhuma\n";

		var text = "";
		foreach (var skill in CharacterSheet.AcquiredSkills)
		{
			var name = SkillCatalog.TryGet(skill, out var definition) ? definition.DisplayName : skill;
			text += $"- {name} nv. {CharacterSheet.GetSkillRank(skill)}\n";
		}

		return text;
	}

	private void AddCharacterSheetSkills(VBoxContainer column)
	{
		if (CharacterSheet.AcquiredSkills.Count == 0)
		{
			AddCharacterSheetText(column, "- nenhuma");
			return;
		}

		foreach (var skillId in CharacterSheet.AcquiredSkills)
		{
			var rank = CharacterSheet.GetSkillRank(skillId);
			if (!SkillCatalog.TryGet(skillId, out var definition))
			{
				AddCharacterSheetText(column, $"• {skillId} nv. {rank}");
				continue;
			}
			var entry = new Button
			{
				Text = $"{definition.IconText}  {definition.DisplayName}  {rank}/{definition.MaxRank}",
				TooltipText = $"{definition.DisplayName}\n{definition.Description}\n\n{definition.FlavorText}",
				Alignment = HorizontalAlignment.Left,
				Flat = true,
				MouseDefaultCursorShape = Control.CursorShape.Help,
				CustomMinimumSize = new Vector2(0.0f, 38.0f)
			};
			column.AddChild(entry);
		}
	}

	private string FormatRunBlessings()
	{
		if (RunState.RunBlessings.Count == 0) return "- nenhuma\n";
		var latestStacks = new Dictionary<string, BloodBlessingRecord>();
		foreach (var blessing in RunState.RunBlessings) latestStacks[blessing.Id] = blessing;
		var text = "";
		foreach (var blessing in latestStacks.Values) text += $"- [{blessing.Rarity}] {blessing.Name} — N{blessing.Level}\n";
		return text;
	}

	private string FormatArtifacts()
	{
		if (CharacterSheet.FoundArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in CharacterSheet.FoundArtifacts)
			text += $"- {artifact.Name} ({artifact.Effect} +{artifact.Amount:0.##})\n";

		return text;
	}

	private string FormatRunArtifacts()
	{
		if (RunState.RunArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in RunState.RunArtifacts)
			text += $"- {artifact.Name} ({artifact.Effect} +{artifact.Amount:0.##})\n";

		return text;
	}

	private string FormatArtifactImpacts()
	{
		if (RunState.RunArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in RunState.RunArtifacts)
			text += $"- {artifact.Name}: {artifact.Effect} +{artifact.Amount:0.##}\n";

		return text;
	}

	private string FormatEquippedArtifacts()
	{
		var text = "";
		for (var i = 0; i < CharacterSheet.EquippedArtifactSlotCount; i++)
		{
			var artifactName = CharacterSheet.GetEquippedArtifactSlot(i);
			text += $"{i + 1}: {(string.IsNullOrWhiteSpace(artifactName) ? "vazio" : artifactName)}\n";
		}

		return text;
	}

	private void BuildRoomClearBanner(Control root)
	{
		_roomClearBanner = new ColorRect
		{
			Name = "RoomClearBanner",
			Color = new Color(0.08f, 0.0f, 0.0f, 0.72f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.18f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.18f,
			OffsetLeft = -280.0f,
			OffsetTop = -36.0f,
			OffsetRight = 280.0f,
			OffsetBottom = 36.0f,
			Visible = false,
			Modulate = Colors.Transparent
		};
		root.AddChild(_roomClearBanner);

		var label = new Label
		{
			Name = "RoomClearLabel",
			Text = Localization.Text("hud.room_clear"),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_roomClearBanner.AddChild(label);
	}

	private async void ShowRoomClearBanner()
	{
		if (_roomClearBanner == null)
			return;

		_roomClearBanner.Visible = true;
		_roomClearBanner.Modulate = Colors.Transparent;
		_roomClearBanner.Scale = new Vector2(0.94f, 0.94f);

		var tween = CreateTween();
		tween.TweenProperty(_roomClearBanner, "modulate", Colors.White, 0.18f);
		tween.Parallel().TweenProperty(_roomClearBanner, "scale", Vector2.One, 0.18f);
		tween.TweenInterval(1.35f);
		tween.TweenProperty(_roomClearBanner, "modulate", Colors.Transparent, 0.45f);
		await ToSignal(tween, Tween.SignalName.Finished);

		_roomClearBanner.Visible = false;
	}

	private async void ShowEnemyDefeatedToast(string message)
	{
		if (_enemyDefeatedToast == null)
			return;

		_enemyDefeatedToast.Text = message;
		_enemyDefeatedToast.Visible = true;
		_enemyDefeatedToast.Modulate = Colors.White;
		_enemyDefeatedToast.OffsetTop = -18.0f;
		_enemyDefeatedToast.OffsetBottom = 18.0f;

		var tween = CreateTween();
		tween.TweenProperty(_enemyDefeatedToast, "offset_top", -30.0f, 0.35f);
		tween.Parallel().TweenProperty(_enemyDefeatedToast, "offset_bottom", 6.0f, 0.35f);
		tween.Parallel().TweenProperty(_enemyDefeatedToast, "modulate:a", 0.0f, 0.55f);
		await ToSignal(tween, Tween.SignalName.Finished);

		_enemyDefeatedToast.Visible = false;
	}

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
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
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

	private static void ApplyResolution(int index)
	{
		var sizes = new[] { new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(1920, 1080) };
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetSize(sizes[Mathf.Clamp(index, 0, sizes.Length - 1)]);
		var screen = DisplayServer.ScreenGetSize();
		DisplayServer.WindowSetPosition((screen - DisplayServer.WindowGetSize()) / 2);
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

	private void BuildCompendiumOverlay(Control root)
	{
		_compendiumOverlay = new ColorRect
		{
			Name = "CompendiumOverlay",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.94f),
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_compendiumOverlay);

		var panel = new VBoxContainer
		{
			Name = "CompendiumPanel",
			AnchorLeft = 0.04f,
			AnchorTop = 0.05f,
			AnchorRight = 0.96f,
			AnchorBottom = 0.95f,
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_compendiumOverlay.AddChild(panel);

		var title = new Label
		{
			Name = "CompendiumTitle",
			Text = "DICIONARIO",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 42.0f)
		};
		panel.AddChild(title);

		_compendiumStatusLabel = new Label
		{
			Name = "CompendiumStatus",
			Text = "Conteudo disponivel no jogo.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 32.0f)
		};
		panel.AddChild(_compendiumStatusLabel);

		var tabRow = new HBoxContainer
		{
			Name = "CompendiumTabs",
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 52.0f)
		};
		panel.AddChild(tabRow);

		AddCompendiumTab(tabRow, "Artifacts", "Artefatos");
		AddCompendiumTab(tabRow, "Blessings", "Bênçãos");
		AddCompendiumTab(tabRow, "Skills", "Habilidades");
		AddCompendiumTab(tabRow, "Attacks", "Ataques");
		AddCompendiumTab(tabRow, "Enemies", "Inimigos");

		var toolRow = new HBoxContainer
		{
			Name = "CompendiumTools",
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 48.0f)
		};
		panel.AddChild(toolRow);

		var resetButton = new Button
		{
			Name = "ResetCompendiumValuesButton",
			Text = "Redefinir valores originais",
			CustomMinimumSize = new Vector2(280.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		resetButton.Pressed += ResetCompendiumValues;
		toolRow.AddChild(resetButton);

		var scroll = new ScrollContainer
		{
			Name = "CompendiumScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 520.0f)
		};
		panel.AddChild(scroll);

		_compendiumList = new VBoxContainer
		{
			Name = "CompendiumList",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		scroll.AddChild(_compendiumList);

		var closeButton = new Button
		{
			Name = "CloseCompendiumButton",
			Text = "Fechar",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		closeButton.Pressed += CloseCompendium;
		panel.AddChild(closeButton);
	}

	private void AddCompendiumTab(HBoxContainer tabRow, string tabId, string label)
	{
		var button = new Button
		{
			Name = $"Compendium{tabId}Tab",
			Text = label,
			CustomMinimumSize = new Vector2(180.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		button.Pressed += () => SelectCompendiumTab(tabId);
		tabRow.AddChild(button);
		_compendiumTabButtons[tabId] = button;
	}

	private void OpenCompendium()
	{
		if (_compendiumOverlay == null)
			return;

		_compendiumOverlay.Visible = true;
		GetTree().Paused = true;
		RefreshCompendium();
		ControllerUiSupport.FocusFirst(_compendiumOverlay);
	}

	private void CloseCompendium()
	{
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		RefreshModalPause();
	}

	private void SelectCompendiumTab(string tabId)
	{
		_selectedCompendiumTab = tabId;
		RefreshCompendium();
	}

	private void RefreshCompendium()
	{
		if (_compendiumList == null)
			return;

		foreach (var pair in _compendiumTabButtons)
			pair.Value.Modulate = pair.Key == _selectedCompendiumTab ? new Color(1.0f, 0.35f, 0.32f) : Colors.White;

		foreach (var child in _compendiumList.GetChildren())
			child.QueueFree();

		switch (_selectedCompendiumTab)
		{
			case "Skills":
				RefreshSkillCompendium();
				break;
			case "Attacks":
				RefreshAttackCompendium();
				break;
			case "Enemies":
				RefreshEnemyCompendium();
				break;
			case "Blessings":
				RefreshBlessingCompendium();
				break;
			default:
				RefreshArtifactCompendium();
				break;
		}
	}

	private void RefreshArtifactCompendium()
	{
		SetCompendiumStatus($"{ArtifactCatalog.All.Count} artefatos cadastrados em data/artifacts/artifacts.json.");

		foreach (var artifact in ArtifactCatalog.All)
		{
			var entry = AddCompendiumEntry(
				artifact.DisplayName,
				$"{artifact.Rarity} | {artifact.Slot} | peso {GetCompendiumNumber($"{artifact.Id}.weight", artifact.Weight):0.##} | {artifact.Id}",
				$"{artifact.Description}\n{artifact.FlavorText}\nEfeitos: {FormatArtifactEffects(artifact)}\nTags: {FormatTags(artifact.Tags)}",
				artifact.IconPath
			);
			AddCompendiumNumberEditor(entry, "Peso de drop", $"{artifact.Id}.weight", artifact.Weight);

			for (var i = 0; i < artifact.Effects.Length; i++)
			{
				var effect = artifact.Effects[i];
				AddCompendiumNumberEditor(entry, $"{effect.Type} ({effect.Operation})", $"{artifact.Id}.effect.{i}.value", effect.Value, 0.01, 999.0);
			}
		}
	}

	private void RefreshBlessingCompendium()
	{
		SetCompendiumStatus($"{BloodBlessingCatalog.All.Count} bencaos em {BloodBlessingCatalog.AllFamilies.Count} linhagens. Stirps possui raridade propria e exige pre-condicoes.");
		var rarityText = "Raridades: " + string.Join(" | ", BloodBlessingCatalog.AllRarities.Select(r => $"{r.DisplayName} x{r.PowerMultiplier:0.##}"));
		foreach (var blessing in BloodBlessingCatalog.All)
		{
			var isStirps = blessing.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
			AddCompendiumEntry(
				blessing.DisplayName,
				$"{FormatBlessingLineage(blessing)} | {(isStirps ? "Raridade Stirps" : "Todas as raridades")} | {blessing.Id}",
				$"{blessing.Description}\n{blessing.FlavorText}\n{(isStirps ? "Poder fixo Stirps" : "Base Common")}: {FormatBloodBlessingEffects(blessing)}\n{(isStirps ? "Desbloqueada apenas quando suas condicoes forem atendidas." : rarityText)}",
				blessing.IconPath
			);
		}
	}

	private void RefreshSkillCompendium()
	{
		var skills = new List<SkillDefinition>(SkillCatalog.All);
		SetCompendiumStatus($"{skills.Count} habilidades cadastradas no SkillCatalog.");

		foreach (var skill in skills)
		{
			var cost = GetCompendiumNumber($"{skill.Id}.cost", skill.Cost);
			var maxRank = GetCompendiumNumber($"{skill.Id}.max_rank", skill.MaxRank);
			var entry = AddCompendiumEntry(
				skill.DisplayName,
				$"{skill.Tree} | Passiva | custo {cost:0} | {skill.Id}",
				$"{skill.Description}\nRank maximo: {maxRank:0}\nPre-requisitos: {FormatPrerequisites(skill)}",
				"res://images/generic.png"
			);
			AddCompendiumNumberEditor(entry, "Custo", $"{skill.Id}.cost", skill.Cost);
			AddCompendiumNumberEditor(entry, "Rank maximo", $"{skill.Id}.max_rank", skill.MaxRank);
		}
	}

	private void RefreshEnemyCompendium()
	{
		SetCompendiumStatus("Inimigos cadastrados a partir dos scripts e cenas atuais.");

		var basic = AddCompendiumEntry(
			"Inimigo Basico",
			"EnemyBasic.tscn | patrulha, detecta, persegue e ataca em area travada",
			"Valores atuais das salas comuns. A dificuldade ainda escala por sala no RunManager.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(basic, "Vida base", "enemy.basic.health", 80.0);
		AddCompendiumNumberEditor(basic, "Dano base", "enemy.basic.damage", 18.0);
		AddCompendiumNumberEditor(basic, "Velocidade", "enemy.basic.move_speed", 3.2, 0.1, 100.0);
		AddCompendiumNumberEditor(basic, "Recompensa minima", "enemy.basic.min_blood_coins", 1.0);
		AddCompendiumNumberEditor(basic, "Recompensa maxima", "enemy.basic.max_blood_coins", 10.0);
		AddCompendiumNumberEditor(basic, "Furia ao morrer", "enemy.basic.fury_on_death", 12.0);

		var boss = AddCompendiumEntry(
			"Mago de Sangue",
			"Chefe da sala 6 | variante configurada no RunManager",
			"Ao vencer, os artefatos temporarios sao confirmados.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(boss, "Vida", "enemy.blood_mage.health", 260.0);
		AddCompendiumNumberEditor(boss, "Dano", "enemy.blood_mage.damage", 30.0);
		AddCompendiumNumberEditor(boss, "Velocidade", "enemy.blood_mage.move_speed", 2.7, 0.1, 100.0);
		AddCompendiumNumberEditor(boss, "Alcance", "enemy.blood_mage.attack_range", 2.25, 0.05, 100.0);
		AddCompendiumNumberEditor(boss, "Recompensa minima", "enemy.blood_mage.min_blood_coins", 8.0);
		AddCompendiumNumberEditor(boss, "Recompensa maxima", "enemy.blood_mage.max_blood_coins", 10.0);
	}

	private void RefreshAttackCompendium()
	{
		SetCompendiumStatus("Ataques atuais do jogador definidos em PlayerCombat.");

		var lightOne = AddCompendiumEntry(
			"Ataque leve I",
			"Mouse esquerdo | combo de espada longa",
			"Primeiro golpe do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightOne, "attack.light_1", 32.0, 0.68, 0.32, 1.55, 1.65, 4.2);

		var lightTwo = AddCompendiumEntry(
			"Ataque leve II",
			"Mouse esquerdo em sequencia | combo de espada longa",
			"Segundo golpe do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightTwo, "attack.light_2", 38.0, 0.70, 0.30, 1.75, 1.75, 4.8);

		var lightThree = AddCompendiumEntry(
			"Ataque leve III",
			"Mouse esquerdo em sequencia | finalizador do combo",
			"Finalizador do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightThree, "attack.light_3", 52.0, 0.72, 0.28, 2.05, 1.95, 6.4);

		var special = AddCompendiumEntry(
			"Ataque especial",
			"Q | ataque especial da arma equipada",
			"Interrompe imediatamente o combo basico atual.",
			"res://images/generic.png"
		);
		AddAttackEditors(special, "attack.special", 56.0, 0.28, 0.42, 3.15, 3.15, 11.0);

		var dash = AddCompendiumEntry(
			"Ataque rápido",
			"Dash + ataque imediato | golpe rápido ao finalizar o dash",
			"Cancela o ataque padrao, preserva invencibilidade e executa dano em circulo ao redor do personagem.",
			"res://images/generic.png"
		);
		AddAttackEditors(dash, "attack.dash", 68.0, 0.12, 0.18, 2.25, 2.05, 7.4);
		AddCompendiumNumberEditor(dash, "Multiplicador de dano", "attack.dash.damage_multiplier", 1.35, 0.05, 20.0);

		var fury = AddCompendiumEntry(
			"Furia Bestial",
			"F | requer uma Bencao de Sangue que desperte a Furia",
			"Consome a barra cheia e ativa temporariamente o estado de dano aumentado.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(fury, "Multiplicador de dano", "attack.fury.damage_multiplier", 1.75, 0.05, 20.0);
		AddCompendiumNumberEditor(fury, "Consumo por segundo", "attack.fury.drain_per_second", 10.0, 0.1, 999.0);
		AddCompendiumNumberEditor(fury, "Furia maxima", "attack.fury.max_fury", 100.0);
	}

	private void RefreshItemCompendium()
	{
		SetCompendiumStatus("Nenhum catalogo de itens foi criado ainda.");
		AddCompendiumEntry(
			"Itens",
			"Catalogo pendente",
			"Quando criarmos data/items/items.json, esta aba pode listar pocoes, moedas, chaves, consumiveis e materiais usando o mesmo padrao dos artefatos.",
			"res://images/generic.png"
		);
	}

	private void AddAttackEditors(VBoxContainer entry, string keyPrefix, double damage, double hitDelay, double recovery, double range, double width, double knockback)
	{
		AddCompendiumNumberEditor(entry, "Dano", $"{keyPrefix}.damage", damage, 1.0, 9999.0);
		AddCompendiumNumberEditor(entry, "Recuperacao", $"{keyPrefix}.recovery", recovery, 0.01, 1.0);
		AddCompendiumNumberEditor(entry, "Alcance", $"{keyPrefix}.range", range, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Largura", $"{keyPrefix}.width", width, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Knockback", $"{keyPrefix}.knockback", knockback, 0.1, 999.0);
	}

	private VBoxContainer AddCompendiumEntry(string title, string subtitle, string body, string iconPath)
	{
		var card = new PanelContainer
		{
			Name = $"{title.Replace(" ", "")}Entry",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 96.0f)
		};
		_compendiumList.AddChild(card);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddChild(row);

		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(48.0f, 48.0f),
			Size = new Vector2(48.0f, 48.0f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		if (!string.IsNullOrWhiteSpace(iconPath))
			icon.Texture = GD.Load<Texture2D>(iconPath);
		row.AddChild(icon);

		var textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(textColumn);

		textColumn.AddChild(new Label
		{
			Text = title,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		});

		textColumn.AddChild(new Label
		{
			Text = subtitle,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		});

		textColumn.AddChild(new Label
		{
			Text = body,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		});

		return textColumn;
	}

	private void AddCompendiumNumberEditor(VBoxContainer parent, string label, string key, double originalValue, double step = 1.0, double maxValue = 9999.0)
	{
		var row = new HBoxContainer
		{
			Name = $"{key.Replace(".", "")}Editor",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 34.0f)
		};
		parent.AddChild(row);

		row.AddChild(new Label
		{
			Text = label,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(220.0f, 0.0f)
		});

		var valueInput = new SpinBox
		{
			MinValue = 0.0,
			MaxValue = maxValue,
			Step = step,
			Value = GetCompendiumNumber(key, originalValue),
			CustomMinimumSize = new Vector2(120.0f, 32.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};
		valueInput.ValueChanged += value =>
		{
			CompendiumTuning.SetNumber(key, value);
			RunState.RecalculateArtifactBonuses();
			GetTree().CallGroup("player", "ApplyRuntimeTuning");
			RefreshCharacterSheet();
			SetCompendiumStatus($"Valor customizado: {label} = {value:0.##}");
		};
		row.AddChild(valueInput);
	}

	private double GetCompendiumNumber(string key, double originalValue)
	{
		return CompendiumTuning.GetNumber(key, originalValue);
	}

	private void ResetCompendiumValues()
	{
		CompendiumTuning.Reset();
		RunState.RecalculateArtifactBonuses();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		SetCompendiumStatus("Valores originais restaurados.");
		RefreshCompendium();
	}

	private string FormatArtifactEffects(ArtifactDefinition artifact)
	{
		if (artifact.Effects.Length == 0)
			return "nenhum";

		var text = "";
		for (var i = 0; i < artifact.Effects.Length; i++)
		{
			var effect = artifact.Effects[i];
			var value = GetCompendiumNumber($"{artifact.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.BossDropPowerMultiplier;
			text += $"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {FormatArtifactOperation(effect.Operation, value)}";

			if (i < artifact.Effects.Length - 1)
				text += ", ";
		}

		return text;
	}

	private static string FormatArtifactOperation(string operation, double value)
	{
		if (operation.Contains("Multiplier")) return $"{value * 100:+0;-0;0}%";
		if (operation is "Set" or "SetBool") return value > 0 ? "ativo" : "inativo";
		return $"{value:+0.##;-0.##;0}";
	}

	private string FormatTags(string[] tags)
	{
		return tags.Length == 0 ? "nenhuma" : string.Join(", ", tags);
	}

	private void SetCompendiumStatus(string text)
	{
		if (_compendiumStatusLabel != null)
			_compendiumStatusLabel.Text = text;
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
		GetTree().Paused = false;
		GetTree().CallGroup("audio_manager", "SetPaused", false);
		GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
	}

	private void AbandonRun()
	{
		if (!RunState.IsRunActive || _runtimeHealth == null || _runtimeHealth.IsDead)
			return;

		_pauseOverlay.Visible = false;
		GetTree().Paused = false;
		GetTree().CallGroup("audio_manager", "SetPaused", false);
		_runtimeHealth.SetInvincible(false);
		_runtimeHealth.ForceDeath("Desistência da run");
	}

	private void RestartCurrentScene()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	private void ReturnToMainMenu()
	{
		SaveGameManager.RequestAutosave();
		GetTree().Paused = false;
		GetTree().CallGroup("audio_manager", "SetPaused", false);
		GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
	}

	private void TogglePause()
	{
		if (GetTree().Paused)
			ResumeGame();
		else
			PauseGame();
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
		GetTree().CallGroup("audio_manager", "SetPaused", paused);
		GetTree().Paused = paused;
	}

	private void RefreshModalPause()
	{
		var paused = _isGameOver
			|| IsVisible(_pauseOverlay)
			|| IsVisible(_characterSheetOverlay)
			|| IsVisible(_compendiumOverlay)
			|| IsVisible(_artifactOfferOverlay)
			|| IsVisible(_bloodBlessingOverlay);
		SetModalPause(paused);
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

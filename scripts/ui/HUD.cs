using Godot;
using System;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private ProgressBar _healthBar;
	private ProgressBar _furyBar;
	private Label _coinsLabel;
	private Label _activeSkillsLabel;
	private Label _enemyProgressLabel;
	private VBoxContainer _cooldownPanel;
	private Label _dashCooldownLabel;
	private readonly Label[] _skillCooldownLabels = new Label[CharacterSheet.ActiveSkillSlotCount];
	private Label _roomLabel;
	private Label _enemyDefeatedToast;
	private Control _roomClearBanner;
	private Control _characterSheetOverlay;
	private VBoxContainer _characterAttributesColumn;
	private VBoxContainer _characterInventoryColumn;
	private VBoxContainer _characterLoadoutColumn;
	private Control _bloodFountainOverlay;
	private Control _skillTreeCanvas;
	private Label _bloodFountainStatusLabel;
	private Button _bloodCoinCheatButton;
	private readonly Dictionary<string, Button> _skillTreeButtons = new();
	private readonly Button[] _activeSkillSlotButtons = new Button[CharacterSheet.ActiveSkillSlotCount];
	private VBoxContainer _activeSkillList;
	private Label _activeSkillLoadoutStatusLabel;
	private Control _skillDescriptionPopup;
	private Label _skillDescriptionLabel;
	private Control _artifactChestOverlay;
	private Label _artifactChestStatusLabel;
	private VBoxContainer _artifactCollectionList;
	private readonly Button[] _artifactSlotButtons = new Button[CharacterSheet.EquippedArtifactSlotCount];
	private Control _artifactOfferOverlay;
	private Label _artifactOfferStatusLabel;
	private VBoxContainer _artifactOfferList;
	private RoomArtifact _pendingArtifactSource;
	private ArtifactRecord _pendingActiveArtifact;
	private Control _pauseOverlay;
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
	private HSlider _dashCooldownSlider;
	private Label _dashCooldownValueLabel;
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
	private bool _isGameOver;
	private bool _isRuntimeBound;
	private bool _roomClearAnnounced;
	private PlayerController _runtimePlayerController;
	private PlayerSkillController _runtimeSkillController;
	private int _selectedActiveSkillSlot;
	private int _selectedArtifactSlot;
	private string _selectedCompendiumTab = "Artifacts";
	private float _bloodFountainZoom = 1.0f;
	private bool _isDraggingBloodFountain;
	private Vector2 _lastBloodFountainMousePosition;
	private Vector2 _skillTreePan = Vector2.Zero;

	public override void _Ready()
	{
		AddToGroup("hud");
		ProcessMode = ProcessModeEnum.Always;
		BuildLayout();
		CallDeferred(nameof(BindRuntimeNodes));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause") && !_isGameOver)
		{
			if (_artifactOfferOverlay != null && _artifactOfferOverlay.Visible)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_compendiumOverlay != null && _compendiumOverlay.Visible)
				CloseCompendium();
			else
				TogglePause();

			GetViewport().SetInputAsHandled();
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
		UpdateCooldownPanel();
	}

	private void BuildLayout()
	{
		var root = new Control
		{
			Name = "Root",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f
		};
		AddChild(root);

		var leftStatus = new VBoxContainer
		{
			Name = "PlayerStatus",
			AnchorLeft = 0.0f,
			AnchorTop = 1.0f,
			AnchorRight = 0.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 24.0f,
			OffsetTop = -86.0f,
			OffsetRight = 330.0f,
			OffsetBottom = -24.0f
		};
		root.AddChild(leftStatus);

		_healthBar = CreateBar("Vida", 100.0f);
		_furyBar = CreateBar("Furia", 100.0f);
		ApplyHealthBarStyle();
		leftStatus.AddChild(_healthBar);
		leftStatus.AddChild(_furyBar);

		var resourcePanel = new VBoxContainer
		{
			Name = "ResourcePanel",
			AnchorLeft = 1.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 0.0f,
			OffsetLeft = -330.0f,
			OffsetTop = 24.0f,
			OffsetRight = -24.0f,
			OffsetBottom = 120.0f
		};
		root.AddChild(resourcePanel);
		_coinsLabel = new Label
		{
			Name = "BloodCoinsLabel",
			Text = "Moedas de Sangue: 0",
			HorizontalAlignment = HorizontalAlignment.Right
		};
		resourcePanel.AddChild(_coinsLabel);

		_activeSkillsLabel = new Label
		{
			Name = "ActiveSkillsLabel",
			Text = "Habilidades: 1 vazio | 2 vazio | 3 vazio | 4 vazio",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -420.0f,
			OffsetTop = -56.0f,
			OffsetRight = 420.0f,
			OffsetBottom = -24.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		root.AddChild(_activeSkillsLabel);

		_enemyProgressLabel = new Label
		{
			Name = "EnemyProgressLabel",
			Text = "Inimigos: 0/0",
			HorizontalAlignment = HorizontalAlignment.Right
		};
		resourcePanel.AddChild(_enemyProgressLabel);

		_cooldownPanel = new VBoxContainer
		{
			Name = "CooldownPanel",
			AnchorLeft = 1.0f,
			AnchorTop = 1.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = -260.0f,
			OffsetTop = -220.0f,
			OffsetRight = -24.0f,
			OffsetBottom = -82.0f,
			Visible = false
		};
		root.AddChild(_cooldownPanel);

		_dashCooldownLabel = CreateCooldownLabel("Dash");
		_cooldownPanel.AddChild(_dashCooldownLabel);
		for (var i = 0; i < _skillCooldownLabels.Length; i++)
		{
			_skillCooldownLabels[i] = CreateCooldownLabel($"{i + 1}");
			_cooldownPanel.AddChild(_skillCooldownLabels[i]);
		}

		_roomLabel = new Label
		{
			Name = "RoomLabel",
			Text = "WASD move | CAPSLOCK caminha | 1-4 habilidades | Q/R artefatos | ESC pausa",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -360.0f,
			OffsetTop = 18.0f,
			OffsetRight = 360.0f,
			OffsetBottom = 44.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		root.AddChild(_roomLabel);

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
			CustomMinimumSize = new Vector2(280.0f, 22.0f),
			ShowPercentage = false,
			TooltipText = label
		};
	}

	private Label CreateCooldownLabel(string label)
	{
		return new Label
		{
			Text = label,
			HorizontalAlignment = HorizontalAlignment.Right,
			Visible = false
		};
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
			BgColor = new Color(0.12f, 0.0f, 0.0f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		};

		_healthBar.AddThemeStyleboxOverride("fill", fill);
		_healthBar.AddThemeStyleboxOverride("background", background);
	}

	private void BindRuntimeNodes()
	{
		if (_isRuntimeBound)
			return;

		var player = GetTree().GetFirstNodeInGroup("player");
		var health = player?.GetNodeOrNull<Health>("Health");
		var controller = player as PlayerController;
		var combat = player?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
		var skillController = player?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
		var runManager = GetTree().GetFirstNodeInGroup("run_manager") as RunManager;

		if (health != null)
		{
			_healthBar.MaxValue = health.MaxHealth;
			_healthBar.Value = health.CurrentHealth;
			health.HealthChanged += OnHealthChanged;
			health.Died += OnPlayerDied;
		}

		if (combat != null)
		{
			_furyBar.MaxValue = combat.MaxFury;
			_furyBar.Value = combat.CurrentFury;
			combat.FuryChanged += OnFuryChanged;
		}

		if (skillController != null)
		{
			_runtimeSkillController = skillController;
			skillController.ActiveSkillUsed += OnActiveSkillUsed;
			skillController.ActiveSkillFailed += OnActiveSkillFailed;
		}

		_runtimePlayerController = controller;

		if (runManager != null)
		{
			runManager.BloodCoinsChanged += OnBloodCoinsChanged;
			runManager.BloodCoinsRewarded += OnBloodCoinsRewarded;
			runManager.EnemyProgressChanged += OnEnemyProgressChanged;
			runManager.RoomCleared += OnRoomCleared;
			OnBloodCoinsChanged(runManager.BloodCoins);
			SyncEnemyProgress(runManager.DefeatedEnemies, runManager.TotalEnemies);
			_isRuntimeBound = true;
		}

		RefreshActiveSkills();
	}

	public void SyncEnemyProgress(int defeatedEnemies, int totalEnemies)
	{
		_enemyProgressLabel.Text = $"Inimigos: {defeatedEnemies}/{totalEnemies}";
	}

	public void NotifyEnemyDefeated(int defeatedEnemies, int totalEnemies, int bloodCoinsReward)
	{
		SyncEnemyProgress(defeatedEnemies, totalEnemies);
		ShowEnemyDefeatedToast($"+1 inimigo derrotado ({defeatedEnemies}/{totalEnemies}) | +{bloodCoinsReward} moedas");
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
		OnRoomCleared();
	}

	public void OpenBloodFountain()
	{
		if (_bloodFountainOverlay == null)
			return;

		RefreshBloodFountain();
		_bloodFountainOverlay.Visible = true;
	}

	public void NotifySkillMessage(string message)
	{
		ShowEnemyDefeatedToast(message);
	}

	public void NotifyArtifactCollected(string artifactName)
	{
		ShowEnemyDefeatedToast($"{artifactName} adicionado aos artefatos temporarios");
		RefreshCharacterSheet();
	}

	public void OpenArtifactChest()
	{
		if (_artifactChestOverlay == null)
			return;

		RefreshArtifactChest();
		_artifactChestOverlay.Visible = true;
	}

	private void OnHealthChanged(float current, float max)
	{
		_healthBar.MaxValue = max;
		_healthBar.Value = current;
	}

	private void OnFuryChanged(float current, float max, bool isActive)
	{
		_furyBar.MaxValue = max;
		_furyBar.Value = isActive ? max : current;
		_furyBar.TooltipText = isActive ? "Furia ativa" : "Furia";
	}

	private void OnActiveSkillUsed(int slotIndex, string skillId, float cooldownSeconds)
	{
		var name = SkillCatalog.TryGet(skillId, out var definition) ? definition.DisplayName : skillId;
		ShowEnemyDefeatedToast($"{slotIndex + 1}: {name} ativada");
	}

	private void OnActiveSkillFailed(int slotIndex, string message)
	{
		ShowEnemyDefeatedToast($"{slotIndex + 1}: {message}");
	}

	private void UpdateCooldownPanel()
	{
		if (_cooldownPanel == null)
			return;

		var hasCooldown = false;

		if (_runtimePlayerController != null && _runtimePlayerController.DodgeCooldownRemaining > 0.0f)
		{
			_dashCooldownLabel.Text = $"Dash {Mathf.CeilToInt(_runtimePlayerController.DodgeCooldownRemaining)}s";
			_dashCooldownLabel.Visible = true;
			hasCooldown = true;
		}
		else if (_dashCooldownLabel != null)
		{
			_dashCooldownLabel.Visible = false;
		}

		for (var i = 0; i < _skillCooldownLabels.Length; i++)
		{
			var label = _skillCooldownLabels[i];
			if (label == null)
				continue;

			var remaining = _runtimeSkillController?.GetCooldownRemaining(i) ?? 0.0f;
			if (remaining > 0.0f)
			{
				var skillId = CharacterSheet.GetActiveSkillSlot(i);
				var skillName = !string.IsNullOrWhiteSpace(skillId) && SkillCatalog.TryGet(skillId, out var definition)
					? definition.IconText
					: $"{i + 1}";
				label.Text = $"{skillName} {Mathf.CeilToInt(remaining)}s";
				label.Visible = true;
				hasCooldown = true;
			}
			else
			{
				label.Visible = false;
			}
		}

		_cooldownPanel.Visible = hasCooldown;
	}

	private void OnBloodCoinsChanged(int amount)
	{
		_coinsLabel.Text = $"Moedas de Sangue: {amount}";
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
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -620.0f,
			OffsetTop = -310.0f,
			OffsetRight = 620.0f,
			OffsetBottom = 310.0f,
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

		_characterAttributesColumn = CreateCharacterSheetColumn("Atributos", 340.0f);
		_characterInventoryColumn = CreateCharacterSheetColumn("Inventario", 420.0f);
		_characterLoadoutColumn = CreateCharacterSheetColumn("Habilidades e Artefatos", 420.0f);
		columns.AddChild(_characterAttributesColumn);
		columns.AddChild(_characterInventoryColumn);
		columns.AddChild(_characterLoadoutColumn);
	}

	private VBoxContainer CreateCharacterSheetColumn(string title, float width)
	{
		var column = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(width, 0.0f),
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

		BuildSkillTreeBoard("Bestialis", SkillTree.Bestialis, 230.0f);
		BuildSkillTreeBoard("Humanitas", SkillTree.Humanitas, 910.0f);

		BuildActiveSkillLoadoutPanel();
		BuildSkillDescriptionPopup();

		var closeButton = new Button
		{
			Name = "CloseBloodFountainButton",
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
			var nodePosition = GetSkillNodePosition(offsetLeft, i);
			CreateSkillNode(definition, nodePosition);

			foreach (var prerequisiteId in definition.Prerequisites)
			{
				if (!SkillCatalog.TryGet(prerequisiteId, out var prerequisite))
					continue;

				var prerequisiteIndex = skills.FindIndex(skill => skill.Id == prerequisite.Id);
				if (prerequisiteIndex < 0)
					continue;

				CreateSkillLink(GetSkillNodePosition(offsetLeft, prerequisiteIndex), nodePosition);
			}
		}
	}

	private Vector2 GetSkillNodePosition(float treeOffsetLeft, int index)
	{
		var x = treeOffsetLeft + 190.0f;
		var y = 170.0f + index * 112.0f;
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
			TooltipText = $"{definition.DisplayName} - clique direito para detalhes"
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
		var link = new ColorRect
		{
			Name = "SkillLink",
			Color = new Color(0.55f, 0.08f, 0.08f, 0.8f),
			OffsetLeft = from.X + 34.0f,
			OffsetTop = from.Y + 72.0f,
			OffsetRight = from.X + 38.0f,
			OffsetBottom = to.Y
		};
		_skillTreeCanvas.AddChild(link);
	}

	private void BuildActiveSkillLoadoutPanel()
	{
		var loadoutTitle = new Label
		{
			Name = "ActiveSkillLoadoutTitle",
			Text = "Habilidades Ativas",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 650.0f,
			OffsetBottom = 680.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(loadoutTitle);

		_activeSkillLoadoutStatusLabel = new Label
		{
			Name = "ActiveSkillLoadoutStatus",
			Text = "Selecione um slot 1-4 e clique em uma habilidade ativa adquirida para vincular.",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 682.0f,
			OffsetBottom = 710.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(_activeSkillLoadoutStatusLabel);

		var slotRow = new HBoxContainer
		{
			Name = "ActiveSkillSlotRow",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -520.0f,
			OffsetTop = 720.0f,
			OffsetRight = -120.0f,
			OffsetBottom = 810.0f
		};
		_bloodFountainOverlay.AddChild(slotRow);

		for (var i = 0; i < CharacterSheet.ActiveSkillSlotCount; i++)
		{
			var slotIndex = i;
			var button = new Button
			{
				Name = $"ActiveSkillSlot{slotIndex + 1}",
				Text = $"{slotIndex + 1}: vazio",
				CustomMinimumSize = new Vector2(86.0f, 58.0f)
			};
			button.Pressed += () => SelectActiveSkillSlot(slotIndex);
			_activeSkillSlotButtons[slotIndex] = button;
			slotRow.AddChild(button);
		}

		_activeSkillList = new VBoxContainer
		{
			Name = "AvailableActiveSkillList",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -70.0f,
			OffsetTop = 720.0f,
			OffsetRight = 520.0f,
			OffsetBottom = 810.0f
		};
		_bloodFountainOverlay.AddChild(_activeSkillList);
	}

	private void SelectActiveSkillSlot(int slotIndex)
	{
		_selectedActiveSkillSlot = slotIndex;
		RefreshActiveSkillLoadout();
	}

	private void EquipSelectedActiveSkill(string skillId)
	{
		if (!SkillCatalog.TryGet(skillId, out var definition))
			return;

		if (!CharacterSheet.EquipActiveSkill(_selectedActiveSkillSlot, skillId))
		{
			SetActiveSkillLoadoutStatus($"Nao foi possivel vincular {definition.DisplayName}.");
			return;
		}

		SetActiveSkillLoadoutStatus($"{definition.DisplayName} vinculada no slot {_selectedActiveSkillSlot + 1}.");
		RefreshActiveSkills();
		RefreshCharacterSheet();
		RefreshActiveSkillLoadout();
	}

	private void BuySkill(string skillId)
	{
		if (!SkillCatalog.TryGet(skillId, out var definition))
		{
			SetBloodFountainStatus("Habilidade desconhecida.");
			return;
		}

		if (CharacterSheet.HasSkill(skillId))
		{
			SetBloodFountainStatus($"{definition.DisplayName} ja foi adquirida.");
			return;
		}

		if (!SkillCatalog.HasPrerequisites(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda esta bloqueada.");
			return;
		}

		var cost = CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost);
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

		if (definition.ActivationMode == SkillActivationMode.Active)
			CharacterSheet.EquipFirstOpenActiveSkill(skillId);

		_coinsLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins}";
		RefreshCharacterSheet();
		RefreshBloodFountain();
		RefreshActiveSkills();
		SetBloodFountainStatus($"{definition.DisplayName} adquirida.");
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
		RefreshActiveSkillLoadout();
	}

	private void RefreshSkillTreeButtons()
	{
		foreach (var pair in _skillTreeButtons)
		{
			if (!SkillCatalog.TryGet(pair.Key, out var definition))
				continue;

			var button = pair.Value;
			var hasSkill = CharacterSheet.HasSkill(definition.Id);
			var unlocked = SkillCatalog.HasPrerequisites(definition);
			var cost = CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost);
			var canAfford = CharacterSheet.BloodCoins >= cost;

			button.Disabled = false;
			button.Text = hasSkill ? "✓" : definition.IconText;

			if (hasSkill)
				button.Modulate = new Color(0.75f, 1.0f, 0.78f);
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

	private void RefreshActiveSkillLoadout()
	{
		for (var i = 0; i < _activeSkillSlotButtons.Length; i++)
		{
			var button = _activeSkillSlotButtons[i];
			if (button == null)
				continue;

			var skillId = CharacterSheet.GetActiveSkillSlot(i);
			var skillName = !string.IsNullOrWhiteSpace(skillId) && SkillCatalog.TryGet(skillId, out var definition)
				? definition.DisplayName
				: "vazio";

			button.Text = $"{i + 1}: {skillName}";
			button.Disabled = false;
			button.Modulate = i == _selectedActiveSkillSlot ? new Color(1.0f, 0.35f, 0.32f) : Colors.White;
		}

		RefreshAvailableActiveSkillList();
	}

	private void RefreshAvailableActiveSkillList()
	{
		if (_activeSkillList == null)
			return;

		foreach (var child in _activeSkillList.GetChildren())
			child.QueueFree();

		var hasActiveSkill = false;
		foreach (var skillId in CharacterSheet.AcquiredSkills)
		{
			if (!SkillCatalog.TryGet(skillId, out var definition) || definition.ActivationMode != SkillActivationMode.Active)
				continue;

			hasActiveSkill = true;
			var button = new Button
			{
				Text = definition.DisplayName,
				CustomMinimumSize = new Vector2(340.0f, 36.0f)
			};
			button.Pressed += () => EquipSelectedActiveSkill(skillId);
			_activeSkillList.AddChild(button);
		}

		if (hasActiveSkill)
			return;

		_activeSkillList.AddChild(new Label
		{
			Text = "Nenhuma habilidade ativa adquirida. Habilidades passivas nao ocupam slot. Compre Transformacao Bestial para testar.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(340.0f, 46.0f)
		});
	}

	private void SetActiveSkillLoadoutStatus(string text)
	{
		if (_activeSkillLoadoutStatusLabel != null)
			_activeSkillLoadoutStatusLabel.Text = text;
	}

	private void BuildSkillDescriptionPopup()
	{
		_skillDescriptionPopup = new ColorRect
		{
			Name = "SkillDescriptionPopup",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.94f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -220.0f,
			OffsetTop = -150.0f,
			OffsetRight = 220.0f,
			OffsetBottom = 150.0f,
			Visible = false
		};
		_bloodFountainOverlay.AddChild(_skillDescriptionPopup);

		_skillDescriptionLabel = new Label
		{
			Name = "SkillDescriptionText",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 20.0f,
			OffsetTop = 18.0f,
			OffsetRight = -20.0f,
			OffsetBottom = -62.0f,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_skillDescriptionPopup.AddChild(_skillDescriptionLabel);

		var closeButton = new Button
		{
			Name = "CloseSkillDescription",
			Text = "Fechar",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -70.0f,
			OffsetTop = -50.0f,
			OffsetRight = 70.0f,
			OffsetBottom = -14.0f
		};
		closeButton.Pressed += () => _skillDescriptionPopup.Visible = false;
		_skillDescriptionPopup.AddChild(closeButton);
	}

	private void ShowSkillDescription(SkillDefinition definition)
	{
		if (_skillDescriptionPopup == null || _skillDescriptionLabel == null)
			return;

		var status = CharacterSheet.HasSkill(definition.Id)
			? "Adquirida"
			: SkillCatalog.HasPrerequisites(definition)
				? "Disponivel"
				: "Bloqueada";

		_skillDescriptionLabel.Text =
			$"{definition.DisplayName}\n" +
			$"{definition.Tree} - {definition.ActivationMode}\n\n" +
			$"{definition.Description}\n\n" +
			$"Custo: {CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost)} moedas\n" +
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
			Text = "Escolha um slot e equipe um artefato adquirido.",
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
		_artifactChestOverlay.AddChild(slotsTitle);

		var slotColumn = new VBoxContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 150.0f,
			OffsetRight = 400.0f,
			OffsetBottom = 360.0f
		};
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
			Text = "Colecao permanente",
			OffsetLeft = 470.0f,
			OffsetTop = 110.0f,
			OffsetRight = 980.0f,
			OffsetBottom = 140.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(collectionTitle);

		_artifactCollectionList = new VBoxContainer
		{
			OffsetLeft = 470.0f,
			OffsetTop = 150.0f,
			OffsetRight = 980.0f,
			OffsetBottom = 470.0f
		};
		_artifactChestOverlay.AddChild(_artifactCollectionList);

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

		if (CharacterSheet.FoundArtifacts.Count == 0)
		{
			_artifactCollectionList.AddChild(new Label
			{
				Text = "Nenhum artefato permanente ainda. Derrote um chefe para confirmar os artefatos temporarios da run.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(480.0f, 60.0f)
			});
			return;
		}

		foreach (var artifact in CharacterSheet.FoundArtifacts)
		{
			var button = new Button
			{
				Text = $"{artifact.Name} | {artifact.Effect} +{artifact.Amount:0.##}",
				CustomMinimumSize = new Vector2(480.0f, 38.0f)
			};
			button.Pressed += () => EquipArtifactInSelectedSlot(artifact.Name);
			_artifactCollectionList.AddChild(button);
		}
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
		_pendingActiveArtifact = default;
		BuildArtifactOfferChoices();
		_artifactOfferOverlay.Visible = true;
		GetTree().Paused = true;
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

		if (choices.Count == 0)
		{
			_artifactOfferStatusLabel.Text = "Nenhum artefato disponivel no catalogo.";
			return;
		}

		_artifactOfferStatusLabel.Text = "Escolha um artefato. Raridade e peso influenciam as opcoes que aparecem.";
		foreach (var artifact in choices)
			AddArtifactOfferButton(artifact);
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

		for (var i = 0; i < 2; i++)
		{
			var active = RunState.GetActiveArtifactSlot(i);
			if (!string.IsNullOrWhiteSpace(active.Id))
				excluded.Add(active.Id);
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

		if (record.Slot == "Active")
		{
			_pendingActiveArtifact = record;
			ShowActiveArtifactSlotChoice(record);
			return;
		}

		CollectArtifact(record);
	}

	private void ShowActiveArtifactSlotChoice(ArtifactRecord artifact)
	{
		ClearArtifactOfferList();
		_artifactOfferStatusLabel.Text = $"{artifact.Name} e um artefato ativo. Escolha o slot.";

		AddActiveArtifactSlotButton(0, "Q");
		AddActiveArtifactSlotButton(1, "R");
	}

	private void AddActiveArtifactSlotButton(int slotIndex, string keyName)
	{
		var current = RunState.GetActiveArtifactSlot(slotIndex);
		var currentText = string.IsNullOrWhiteSpace(current.Name) ? "vazio" : current.Name;
		var button = new Button
		{
			Text = $"Slot {keyName}: {currentText}",
			CustomMinimumSize = new Vector2(1080.0f, 82.0f)
		};
		button.Pressed += () =>
		{
			if (!RunState.EquipActiveArtifact(slotIndex, _pendingActiveArtifact))
			{
				_artifactOfferStatusLabel.Text = "Nao foi possivel equipar o artefato ativo.";
				return;
			}

			CollectArtifact(_pendingActiveArtifact);
		};
		_artifactOfferList.AddChild(button);
	}

	private void CollectArtifact(ArtifactRecord artifact)
	{
		RunState.AddTemporaryArtifact(artifact);
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		NotifyArtifactCollected(artifact.Name);
		_pendingArtifactSource?.Consume();
		_pendingArtifactSource = null;
		_pendingActiveArtifact = default;
		_artifactOfferOverlay.Visible = false;
		GetTree().Paused = false;
		RefreshCharacterSheet();
	}

	private void ClearArtifactOfferList()
	{
		if (_artifactOfferList == null)
			return;

		foreach (var child in _artifactOfferList.GetChildren())
			child.QueueFree();
	}

	private void ToggleCharacterSheet()
	{
		if (_characterSheetOverlay == null)
			return;

		_characterSheetOverlay.Visible = !_characterSheetOverlay.Visible;
		if (_characterSheetOverlay.Visible)
			RefreshCharacterSheet();
	}

	private void RefreshCharacterSheet()
	{
		if (_characterAttributesColumn == null || _characterInventoryColumn == null || _characterLoadoutColumn == null)
			return;

		ClearCharacterSheetColumn(_characterAttributesColumn);
		ClearCharacterSheetColumn(_characterInventoryColumn);
		ClearCharacterSheetColumn(_characterLoadoutColumn);

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

		AddCharacterSheetSection(_characterLoadoutColumn, "Habilidades ativas");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatActiveSkillSlots());
		AddCharacterSheetSection(_characterLoadoutColumn, "Habilidades adquiridas");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatSkills());
		AddCharacterSheetSection(_characterLoadoutColumn, "Artefatos ativos");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatActiveArtifactSlots());
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

	private string FormatActiveSkillSlots()
	{
		var text = "";
		for (var i = 0; i < CharacterSheet.ActiveSkillSlotCount; i++)
		{
			var skillId = CharacterSheet.GetActiveSkillSlot(i);
			var skillName = !string.IsNullOrWhiteSpace(skillId) && SkillCatalog.TryGet(skillId, out var definition)
				? definition.DisplayName
				: "vazio";
			text += $"{i + 1}: {skillName}\n";
		}

		return text;
	}

	private string FormatActiveArtifactSlots()
	{
		var q = RunState.GetActiveArtifactSlot(0);
		var r = RunState.GetActiveArtifactSlot(1);
		return
			$"Q: {(string.IsNullOrWhiteSpace(q.Name) ? "vazio" : q.Name)}\n" +
			$"R: {(string.IsNullOrWhiteSpace(r.Name) ? "vazio" : r.Name)}\n";
	}

	private void RefreshActiveSkills()
	{
		if (_activeSkillsLabel == null)
			return;

		var text = "Habilidades:";
		for (var i = 0; i < CharacterSheet.ActiveSkillSlotCount; i++)
		{
			var skillId = CharacterSheet.GetActiveSkillSlot(i);
			var skillName = !string.IsNullOrWhiteSpace(skillId) && SkillCatalog.TryGet(skillId, out var definition)
				? definition.DisplayName
				: "vazio";
			text += $" {i + 1} {skillName}";
			if (i < CharacterSheet.ActiveSkillSlotCount - 1)
				text += " |";
		}

		_activeSkillsLabel.Text = text;
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
			Text = "SALA LIMPA",
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
			Visible = false
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
			Text = "GAME OVER",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 42.0f)
		};
		panel.AddChild(title);

		var subtitle = new Label
		{
			Name = "GameOverSubtitle",
			Text = "Virgil retornou ao caixao.",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 32.0f)
		};
		panel.AddChild(subtitle);

		var restartButton = new Button
		{
			Name = "RestartButton",
			Text = "Reiniciar teste",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		restartButton.Pressed += RestartCurrentScene;
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
			Text = "PAUSADO",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(360.0f, 42.0f)
		};
		panel.AddChild(title);

		var subtitle = new Label
		{
			Name = "PauseSubtitle",
			Text = "Ajuste os parametros em tempo real. Cada slider vai de 1 a 100.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 36.0f)
		};
		panel.AddChild(subtitle);

		_pauseTuningStatusLabel = new Label
		{
			Name = "PauseTuningStatus",
			Text = "Nenhuma alteracao salva ainda.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		};
		panel.AddChild(_pauseTuningStatusLabel);

		_pauseTuningScroll = new ScrollContainer
		{
			Name = "PauseTuningScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 420.0f)
		};
		panel.AddChild(_pauseTuningScroll);

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
		AddPauseTuningSlider("Recarga do dash", GameTuning.DashCooldownPercent, GameTuning.SetDashCooldownPercent, out _dashCooldownSlider, out _dashCooldownValueLabel);
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
		panel.AddChild(footer);

		var resumeButton = new Button
		{
			Name = "ResumeButton",
			Text = "Continuar",
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

		RefreshPauseTuningValues();
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
		AddCompendiumTab(tabRow, "Skills", "Habilidades");
		AddCompendiumTab(tabRow, "Attacks", "Ataques");
		AddCompendiumTab(tabRow, "Enemies", "Inimigos");
		AddCompendiumTab(tabRow, "Items", "Itens");

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
	}

	private void CloseCompendium()
	{
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
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
			case "Items":
				RefreshItemCompendium();
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

	private void RefreshSkillCompendium()
	{
		var skills = new List<SkillDefinition>(SkillCatalog.All);
		SetCompendiumStatus($"{skills.Count} habilidades cadastradas no SkillCatalog.");

		foreach (var skill in skills)
		{
			var cost = GetCompendiumNumber($"{skill.Id}.cost", skill.Cost);
			var cooldown = GetCompendiumNumber($"{skill.Id}.cooldown", skill.CooldownSeconds);
			var maxRank = GetCompendiumNumber($"{skill.Id}.max_rank", skill.MaxRank);
			var entry = AddCompendiumEntry(
				skill.DisplayName,
				$"{skill.Tree} | {skill.ActivationMode} | custo {cost:0} | {skill.Id}",
				$"{skill.Description}\nRecarga: {cooldown:0.##}s | Rank maximo: {maxRank:0}\nPre-requisitos: {FormatPrerequisites(skill)}",
				"res://images/generic.png"
			);
			AddCompendiumNumberEditor(entry, "Custo", $"{skill.Id}.cost", skill.Cost);
			AddCompendiumNumberEditor(entry, "Rank maximo", $"{skill.Id}.max_rank", skill.MaxRank);
			AddCompendiumNumberEditor(entry, "Recarga", $"{skill.Id}.cooldown", skill.CooldownSeconds, 0.1, 999.0);
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

		var heavy = AddCompendiumEntry(
			"Ataque pesado",
			"Mouse direito | golpe unico de espada longa",
			"Golpe unico mais forte, sem fila de combo.",
			"res://images/generic.png"
		);
		AddAttackEditors(heavy, "attack.heavy", 78.0, 0.76, 0.24, 2.35, 2.15, 8.5);

		var dash = AddCompendiumEntry(
			"Ataque com dash",
			"Dash + ataque imediato | golpe rapido ao finalizar o dash",
			"Cancela o ataque padrao, preserva invencibilidade e executa dano em circulo ao redor do personagem.",
			"res://images/generic.png"
		);
		AddAttackEditors(dash, "attack.dash", 68.0, 0.12, 0.18, 2.25, 2.05, 7.4);
		AddCompendiumNumberEditor(dash, "Multiplicador de dano", "attack.dash.damage_multiplier", 1.35, 0.05, 20.0);

		var fury = AddCompendiumEntry(
			"Furia",
			"Habilidade ativada quando a barra esta cheia",
			"Modo temporario de dano aumentado. Inimigos derrotados concedem furia.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(fury, "Multiplicador de dano", "attack.fury.damage_multiplier", 1.75, 0.05, 20.0);
		AddCompendiumNumberEditor(fury, "Duracao", "attack.fury.duration", 6.0, 0.1, 999.0);
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
			var value = GetCompendiumNumber($"{artifact.Id}.effect.{i}.value", effect.Value);
			text += $"{effect.Type} {effect.Operation} {value:0.##}";

			if (i < artifact.Effects.Length - 1)
				text += ", ";
		}

		return text;
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
		RunState.FailRun();
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
		_isGameOver = true;
		if (_artifactOfferOverlay != null)
			_artifactOfferOverlay.Visible = false;
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		_pauseOverlay.Visible = false;
	}

	private void RestartCurrentScene()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
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
		_pauseOverlay.Visible = true;
		GetTree().Paused = true;
	}

	private void ResumeGame()
	{
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		_pauseOverlay.Visible = false;
		GetTree().Paused = false;
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
		SyncSlider(_dashCooldownSlider, _dashCooldownValueLabel, GameTuning.DashCooldownPercent);
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

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
	private PanelContainer _roomFrame;
	private Tween _toastTween;
	private TextureRect _roomDivider;
	private bool _roomFrameAlwaysVisible;
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
		if (_roomFrame != null && _roomLabel != null)
		{
			var showRoomFrame = _roomFrameAlwaysVisible || !string.IsNullOrEmpty(_roomLabel.Text);
			_roomFrame.Visible = showRoomFrame;
			_roomDivider.Visible = showRoomFrame;
		}
		if (_fpsLabel != null && _fpsLabel.Visible)
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
}

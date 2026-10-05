using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD : CanvasLayer
{
	private Control _playerHudFrame;
	private Label _fpsLabel;
	private PanelContainer _roomFrame;
	private bool _roomFrameAlwaysVisible;
	private readonly Queue<string> _gameLogEntries = new();
	private Label _coinsLabel;
	private Label _weaponLabel;
	private Control _weaponBenchOverlay;
	private Label _weaponBenchStatusLabel;
	private Label _enemyProgressLabel;
	private Label _roomLabel;
	private Control _bossHealthPanel;
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
	private Func<bool> _legacyPauseSource;
	private Func<bool> _legacyAudioPauseSource;

	public override void _Ready()
	{
		// Debug: --ui-preview/--ui-lang/--ui-device (sem argumento, nada acontece). Antes do layout por causa do idioma.
		UiPreview.Attach(this);
		ControllerUiSupport.EnsureMappings();
		AddToGroup("hud");
		ProcessMode = ProcessModeEnum.Always;
		BuildLayout();
		// Overlays ainda fora da UiModalStack entram na pausa por esta fonte.
		_legacyPauseSource = IsLegacyPauseRequested;
		_legacyAudioPauseSource = IsLegacyAudioPauseRequested;
		UiModalStack.RegisterPauseSource(_legacyPauseSource, _legacyAudioPauseSource);
		CallDeferred(nameof(BindRuntimeNodes));
	}

	public override void _ExitTree()
	{
		UiModalStack.UnregisterPauseSource(_legacyPauseSource);
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
		}
		if (_fpsLabel != null && _fpsLabel.Visible)
			_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
	}

	public bool IsGameplayInputBlocked()
	{
		return UiModalStack.AnyOpen
			|| IsVisible(_weaponBenchOverlay)
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

	// Mesmos flags que RefreshModalPause usava antes da pilha (Fonte, Baú e Bancada não pausam).
	private bool IsLegacyPauseRequested()
	{
		return _isGameOver
			|| IsVisible(_pauseOverlay)
			|| IsVisible(_characterSheetOverlay)
			|| IsVisible(_compendiumOverlay)
			|| IsVisible(_artifactOfferOverlay)
			|| IsVisible(_bloodBlessingOverlay);
	}

	// Música: mesmas telas, mas o game over pausa só a árvore (a música de game over continua).
	private bool IsLegacyAudioPauseRequested()
	{
		return !_isGameOver && IsLegacyPauseRequested();
	}

	private static bool IsVisible(Control control) => control != null && control.Visible;

	// Pré-visualização (debug, ver UiPreview.cs): abre a tela pedida com dados fictícios. False = nome desconhecido.
	internal bool OpenPreviewScreen(string screen, string tab, Node dataParent)
	{
		switch (screen)
		{
			case "hud_combat":
				PreviewHudCombat();
				return true;
			case "critical_health":
				PreviewCriticalHealth();
				return true;
			case "damage_flash":
				PreviewDamageFlash();
				return true;
			case "damage_number":
				PreviewDamageNumber();
				return true;
			case "blessing_bar":
				PreviewBlessingBar();
				return true;
			case "event_log":
				PreviewEventLog();
				return true;
			case "character_sheet":
				if (!IsVisible(_characterSheetOverlay))
					ToggleCharacterSheet();
				return true;
			case "blood_fountain":
				OpenBloodFountain();
				return true;
			case "skill_popup":
				OpenBloodFountain();
				if (SkillCatalog.All.Count > 0)
					ShowSkillDescription(SkillCatalog.All[0]);
				return true;
			case "artifact_chest":
				OpenArtifactChest();
				return true;
			case "artifact_offer":
				// Sem RoomArtifact: escolher só coleta na run volátil, nada é consumido no mundo.
				OpenArtifactOffer(null);
				return true;
			case "blessing_offer":
				// Caminho obrigatório: abre direto, sem a animação de absorção.
				OpenMandatoryBloodBlessingOffer();
				return true;
			case "weapon_bench":
				OpenWeaponBench();
				return true;
			case "pause":
				PauseGame();
				return true;
			case "options":
				PauseGame();
				ShowPausePage(_optionsPage);
				return true;
			case "tuning":
				PauseGame();
				ShowPausePage(_pauseTuningPage);
				return true;
			case "compendium":
				_selectedCompendiumTab = ResolvePreviewCompendiumTab(tab);
				OpenCompendium();
				return true;
			case "game_over":
				ShowPreviewGameOver();
				return true;
			case "room_clear":
				ShowRoomClearBanner();
				return true;
			case "toast":
				PreviewToasts();
				return true;
			case "wave_warning":
				NotifyWaveIncoming(5, 3.0f);
				return true;
			case "boss_bar":
				BindPreviewBoss(dataParent);
				return true;
		}
		return false;
	}

	private string ResolvePreviewCompendiumTab(string tab)
	{
		if (string.IsNullOrEmpty(tab))
			return _selectedCompendiumTab;
		foreach (var tabId in _compendiumTabButtons.Keys)
			if (string.Equals(tabId, tab, StringComparison.OrdinalIgnoreCase))
				return tabId;
		GD.PushWarning($"UiPreview: aba '{tab}' inexistente. Use: {string.Join(", ", _compendiumTabButtons.Keys)}.");
		return _selectedCompendiumTab;
	}

	// Igual a OnPlayerDied, sem áudio, log nem jogador real (causa fictícia).
	private void ShowPreviewGameOver()
	{
		_isGameOver = true;
		_gameOverSubtitle.Text = "Virgil foi derrotado por: Lorde Corvo. Último golpe: 42 de dano.";
		if (_artifactOfferOverlay != null)
			_artifactOfferOverlay.Visible = false;
		if (_bloodBlessingOverlay != null)
			_bloodBlessingOverlay.Visible = false;
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		_pauseOverlay.Visible = false;
		_gameOverOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_gameOverOverlay);
		RefreshModalPause();
	}

	// Chefe fictício: nó com Health (60% de vida) filho do nó de preview.
	private void BindPreviewBoss(Node dataParent)
	{
		var boss = new Node { Name = "PreviewBoss" };
		var health = new Health { Name = "Health", MaxHealth = 1200.0f };
		boss.AddChild(health);
		(dataParent ?? this).AddChild(boss);
		health.SetCurrentHealth(720.0f);
		BindBoss(boss, BossCatalog.Get(RunState.CurrentMapIndex).DisplayName);
	}
}

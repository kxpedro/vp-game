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
	private Label _enemyProgressLabel;
	private Label _roomLabel;
	private Control _bossHealthPanel;
	private Control _roomClearBanner;
	private Control _characterSheetOverlay;
	private Control _bloodFountainOverlay;
	private Control _skillTreeCanvas;
	private Button _bloodCoinCheatButton;
	private Control _artifactChestOverlay;
	private Control _artifactOfferOverlay;
	private Control _bloodBlessingOverlay;
	private Label _bloodBlessingStatusLabel;
	private VBoxContainer _bloodBlessingList;
	private readonly List<Button> _blessingChoiceButtons = new();
	private int _focusedBlessingChoice;
	private ColorRect _bloodAbsorptionOverlay;
	private Label _bloodAbsorptionLabel;
	private HBoxContainer _blessingHudList;
	private Control _pauseOverlay;
	private Control _compendiumOverlay;
	private Label _compendiumStatusLabel;
	private VBoxContainer _compendiumList;
	private readonly Dictionary<string, Button> _compendiumTabButtons = new();
	private Control _gameOverOverlay;
	private Health _runtimeHealth;
	private bool _isGameOver;
	private bool _isRuntimeBound;
	private bool _roomClearAnnounced;
	private PlayerController _runtimePlayerController;
	private PlayerCombat _runtimePlayerCombat;
	private string _selectedCompendiumTab = "Artifacts";
	private float _bloodFountainZoom = 1.0f;
	private bool _isDraggingBloodFountain;
	private Vector2 _lastBloodFountainMousePosition;
	private Vector2 _skillTreePan = Vector2.Zero;
	private float _criticalHealthPulse;
	private Func<bool> _legacyPauseSource;
	private Func<bool> _legacyAudioPauseSource;
	// HUD de combate (painéis entre a vinheta e o primeiro overlay): some sob a pausa e o game over (§8.10/§8.11).
	private Control _combatHudLayer;
	// Atalhos de tela por topo da pilha modal: no Godot 4 tecla/controle vai só ao Control focado (não sobe ao GuiInput do pai).
	private readonly Dictionary<Control, Func<InputEvent, bool>> _modalShortcutHandlers = new();
	// Hover só conta com movimento real do mouse (lista rolando sob o cursor parado não troca a seleção).
	private ulong _lastMouseMotionMs;

	public override void _Ready()
	{
		// Debug: --ui-preview/--ui-lang/--ui-device (sem argumento, nada acontece). Antes do layout por causa do idioma.
		UiPreview.Attach(this);
		ControllerUiSupport.EnsureMappings();
		AddToGroup("hud");
		ProcessMode = ProcessModeEnum.Always;
		BuildLayout();
		GroupCombatHud();
		RegisterModalShortcuts(_compendiumOverlay, HandleCompendiumShortcut);
		RegisterModalShortcuts(_bloodBlessingOverlay, HandleBlessingOfferShortcut);
		RegisterModalShortcuts(_artifactOfferOverlay, HandleArtifactOfferShortcut);
		RegisterModalShortcuts(_characterSheetOverlay, HandleCharacterSheetShortcut);
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
		if (@event is InputEventMouseMotion)
			_lastMouseMotionMs = Time.GetTicksMsec();
		if (@event.IsActionPressed("ui_cancel") && !_isGameOver)
		{
			if (CloseTopmostOverlay())
			{
				GetViewport().SetInputAsHandled();
				return;
			}
		}
		// Cima/baixo nas ofertas: vizinhos de foco dos próprios cards (SetVerticalCycle), só com a oferta no topo da pilha.
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (RouteModalShortcut(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("pause") && !_isGameOver)
		{
			if (!CloseTopmostOverlay())
				PauseGame();

			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("character_sheet") && !_isGameOver)
		{
			// Só abre sem nenhuma tela por cima do jogo (ofertas obrigatórias, altar, pausa, Dicionário...) e só fecha com a ficha no topo.
			var sheetOnTop = UiModalStack.Top == _characterSheetOverlay;
			if (sheetOnTop || (!UiModalStack.AnyOpen && !IsGameplayInputBlocked()))
				ToggleCharacterSheet();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>
	/// Registra atalhos (abas, rolagem, ui_alt...) de uma tela da UiModalStack. O handler só roda com a tela no topo,
	/// a partir do _UnhandledInput (o Control focado e campos de texto têm prioridade); retorne true para consumir.
	/// </summary>
	private void RegisterModalShortcuts(Control layer, Func<InputEvent, bool> handler)
	{
		if (layer != null && handler != null)
			_modalShortcutHandlers[layer] = handler;
	}

	private bool RouteModalShortcut(InputEvent @event)
	{
		if (@event is InputEventMouse)
			return false;
		var top = UiModalStack.Top;
		if (top != null)
			return _modalShortcutHandlers.TryGetValue(top, out var handler) && handler(@event);
		// Pausa (camada legada, fora da pilha): só sem nada empilhado por cima.
		return IsVisible(_pauseOverlay) && HandlePauseShortcut(@event);
	}

	// Move os painéis de combate (entre a vinheta e o primeiro overlay) para uma camada própria, para esconder tudo de uma vez.
	private void GroupCombatHud()
	{
		if (_hudRoot == null || _pauseOverlay == null || _pauseOverlay.GetParent() != _hudRoot)
			return;
		var start = _edgeVignette != null && _edgeVignette.GetParent() == _hudRoot ? _edgeVignette.GetIndex() + 1 : 0;
		var end = _pauseOverlay.GetIndex();
		var nodes = new List<Node>();
		for (var i = start; i < end; i++)
			nodes.Add(_hudRoot.GetChild(i));
		_combatHudLayer = new Control { Name = "CombatHud", MouseFilter = Control.MouseFilterEnum.Ignore };
		_combatHudLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_hudRoot.AddChild(_combatHudLayer);
		_hudRoot.MoveChild(_combatHudLayer, start);
		foreach (var node in nodes)
		{
			_hudRoot.RemoveChild(node);
			_combatHudLayer.AddChild(node);
		}
	}

	public override void _Process(double delta)
	{
		UpdateCriticalHealthIndicator((float)delta);
		// Zoom/pan da Fonte de Sangue por controle e teclado (mouse fica no GuiInput da própria tela).
		ProcessBloodFountainInput((float)delta);
		if (_roomFrame != null && _roomLabel != null)
		{
			var showRoomFrame = _roomFrameAlwaysVisible || !string.IsNullOrEmpty(_roomLabel.Text);
			_roomFrame.Visible = showRoomFrame;
		}
		if (_combatHudLayer != null)
		{
			var hideCombatHud = _isGameOver || IsVisible(_pauseOverlay) || IsVisible(_compendiumOverlay);
			if (_combatHudLayer.Visible == hideCombatHud)
				_combatHudLayer.Visible = !hideCombatHud;
		}
		if (_fpsLabel != null && _fpsLabel.IsVisibleInTree())
			_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
	}

	public bool IsGameplayInputBlocked()
	{
		// Ofertas, ficha, Fonte, baú e bancada estão na UiModalStack (AnyOpen); aqui só as camadas ainda legadas.
		return UiModalStack.AnyOpen
			|| IsVisible(_pauseOverlay)
			|| IsVisible(_compendiumOverlay)
			|| IsVisible(_gameOverOverlay);
	}

	// Telas ainda fora da pilha que pausam (as da pilha pausam pelo próprio Push).
	private bool IsLegacyPauseRequested()
	{
		return _isGameOver
			|| IsVisible(_pauseOverlay)
			|| IsVisible(_compendiumOverlay);
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
				// Dados fictícios só de exibição (nada entra no RunState nem no save); --ui-tab escolhe a aba.
				if (!IsVisible(_characterSheetOverlay))
					OpenCharacterSheetPreview(tab);
				return true;
			case "blood_fountain":
				// Compra e cheat só simulam no preview (nada é gravado).
				OpenBloodFountainPreview(false);
				return true;
			case "skill_popup":
				OpenBloodFountainPreview(true);
				return true;
			case "artifact_chest":
				// Dados fictícios em memória: equipar/remover só simulam.
				OpenArtifactChestPreview();
				return true;
			case "artifact_offer":
			case "artifact_offer_4":
				// Sem RoomArtifact nem sorteio: escolher só coleta na run volátil, nada é consumido no mundo.
				OpenArtifactOfferPreview(screen == "artifact_offer_4" ? 4 : 3);
				return true;
			case "blessing_offer":
				// Caminho obrigatório: abre direto, sem a animação de absorção (reroll só simulado).
				PreviewBlessingOffer(false);
				return true;
			case "blessing_offer_replace":
				PreviewBlessingOffer(true);
				return true;
			case "weapon_bench":
				OpenWeaponBench();
				return true;
			case "compendium":
				_selectedCompendiumTab = ResolvePreviewCompendiumTab(tab);
				OpenCompendium();
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
		// Pausa, opções, ajustes, confirmações e game over: HUD.Pause.cs.
		return OpenPausePreviewScreen(screen);
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

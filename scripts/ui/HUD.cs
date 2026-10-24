using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HUD : CanvasLayer
{
	private Control _playerHudFrame;
	private Label _fpsLabel;
	private PanelContainer _roomFrame;
	private bool _roomFrameAlwaysVisible;
	private readonly Queue<string> _gameLogEntries = new();
	private Label _coinsLabel;
	private Label _coinsNameLabel;
	private Label _weaponLabel;
	private Control _weaponBenchOverlay;
	private Label _enemyProgressLabel;
	private Label _roomLabel;
	private Control _bossHealthPanel;
	private Control _characterSheetOverlay;
	private Control _bloodFountainOverlay;
	private Control _skillTreeCanvas;
	private Button _cursedBloodCheatButton;
	private Control _bloodBlessingOverlay;
	private Label _bloodBlessingStatusLabel;
	private VBoxContainer _bloodBlessingList;
	private readonly List<Button> _blessingChoiceButtons = new();
	private int _focusedBlessingChoice;
	private ColorRect _bloodAbsorptionOverlay;
	private Label _bloodAbsorptionLabel;
	private VBoxContainer _blessingHudList;
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
		BuildDevPanel(_hudRoot); // só debug; fica por último, acima dos outros overlays
		BuildDevConsole(_hudRoot); // só debug; acima até do painel F1
		RegisterModalShortcuts(_compendiumOverlay, HandleCompendiumShortcut);
		RegisterModalShortcuts(_bloodBlessingOverlay, HandleBlessingOfferShortcut);
		RegisterModalShortcuts(_characterSheetOverlay, HandleCharacterSheetShortcut);
		// Overlays ainda fora da UiModalStack entram na pausa por esta fonte.
		_legacyPauseSource = IsLegacyPauseRequested;
		_legacyAudioPauseSource = IsLegacyAudioPauseRequested;
		UiModalStack.RegisterPauseSource(_legacyPauseSource, _legacyAudioPauseSource);
		CallDeferred(nameof(BindRuntimeNodes));
		Callable.From(LocalizeSceneLabels).CallDeferred();
		if (Localization.Instance != null)
			Localization.Instance.LanguageChanged += OnLanguageChanged;
	}

	public override void _ExitTree()
	{
		UiModalStack.UnregisterPauseSource(_legacyPauseSource);
		if (Localization.Instance != null)
			Localization.Instance.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion)
			_lastMouseMotionMs = Time.GetTicksMsec();
		if (HandleDevConsoleToggle(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
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
		if (HandleDevPanelToggle(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

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
		return false;
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
		UpdateDialogue((float)delta);
		// Zoom/pan da Fonte de Sangue por controle e teclado (mouse fica no GuiInput da própria tela).
		ProcessBloodFountainInput((float)delta);
		if (_roomFrame != null && _roomLabel != null)
		{
			// Nas salas de combate o título da sala saiu da tela (limpeza da HUD); só a Câmara usa a moldura para o status dela.
			var showRoomFrame = _roomFrameAlwaysVisible;
			_roomFrame.Visible = showRoomFrame;
		}
		if (_combatHudLayer != null)
		{
			var hideCombatHud = _isGameOver || IsVisible(_pauseOverlay) || IsVisible(_compendiumOverlay) || IsVisible(_devPanelOverlay);
			if (_combatHudLayer.Visible == hideCombatHud)
				_combatHudLayer.Visible = !hideCombatHud;
		}
		if (_fpsLabel != null && _fpsLabel.IsVisibleInTree())
			_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
	}

	public bool IsGameplayInputBlocked()
	{
		// Ofertas, ficha, Fonte, loja e bancada estão na UiModalStack (AnyOpen); aqui só as camadas ainda legadas.
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

	// ---------- Idioma ----------

	// Troca de idioma (opções da pausa): reconstrói os textos fixos do HUD e dos overlays. Telas montadas ao abrir
	// (ficha, Fonte, loja, bancada, ofertas, Dicionário) são refeitas se estiverem abertas.
	private void OnLanguageChanged(string language)
	{
		Callable.From(ApplyLanguage).CallDeferred();
	}

	private void ApplyLanguage()
	{
		if (!IsInsideTree())
			return;
		LocalizeSceneLabels();
		RefreshRoomProgressText();
		if (_bossResourceBar != null && _bossHealthPanel != null && _bossHealthPanel.Visible && RunState.IsRunActive)
		{
			_bossDisplayName = BossCatalog.Get(RunState.CurrentMapIndex).DisplayName;
			_bossResourceBar.SetName(_bossDisplayName);
		}
		if (_roomLabel != null && !string.IsNullOrEmpty(_roomLabel.Text) && RunState.IsRunActive)
			SyncRoomInformation();
		if (_coinGlyph != null)
			_coinGlyph.TooltipText = Localization.Text("hud.blood_coins_name");
		if (_cursedBloodGlyph != null)
			_cursedBloodGlyph.TooltipText = Localization.Text("hud.cursed_blood_name");
		RefreshResourceIndicator();
		if (_furyResourceBar != null && _furyResourceBar.Bar != null)
			_furyResourceBar.Bar.TooltipText = Localization.Text("hud.fury.tooltip_ready");
		if (_weaponLabel != null)
			_weaponLabel.Text = Localization.TextFormat("hud.weapon", WeaponLoadout.EquippedWeapon.DisplayName);
		_furyResourceBar?.SetFuryBadgeText(Localization.Text("hud.fury"));
		RefreshBlessingHud();
		RebuildOverlaysForLanguage();
	}

	// Os overlays montam os textos fixos no Build*: na troca de idioma são recriados no mesmo lugar da árvore.
	// A troca só acontece pelas opções da pausa, então só a pausa pode estar aberta (ela volta na página de opções);
	// as demais telas fecham antes de a pausa abrir e se reconstroem com dados atuais ao abrir.
	private void RebuildOverlaysForLanguage()
	{
		if (_hudRoot == null)
			return;
		RebuildOverlay(_gameOverOverlay, () => BuildGameOverOverlay(_hudRoot), () => _gameOverOverlay);
		RebuildPauseForLanguage();
		if (_compendiumOverlay != null && !IsVisible(_compendiumOverlay))
		{
			_modalShortcutHandlers.Remove(_compendiumOverlay);
			_pauseCompendiumHooked = false;
			RebuildOverlay(_compendiumOverlay, () => BuildCompendiumOverlay(_hudRoot), () => _compendiumOverlay);
			RegisterModalShortcuts(_compendiumOverlay, HandleCompendiumShortcut);
		}
		if (_characterSheetOverlay != null && !IsVisible(_characterSheetOverlay))
		{
			_modalShortcutHandlers.Remove(_characterSheetOverlay);
			RebuildOverlay(_characterSheetOverlay, () => BuildCharacterSheetOverlay(_hudRoot), () => _characterSheetOverlay);
			RegisterModalShortcuts(_characterSheetOverlay, HandleCharacterSheetShortcut);
		}
		if (_bloodFountainOverlay != null && !IsVisible(_bloodFountainOverlay))
			RebuildOverlay(_bloodFountainOverlay, () => BuildBloodFountainOverlay(_hudRoot), () => _bloodFountainOverlay);
		if (_bloodBlessingOverlay != null && !IsVisible(_bloodBlessingOverlay) && !_blessingAbsorptionRunning)
		{
			_modalShortcutHandlers.Remove(_bloodBlessingOverlay);
			// O builder também cria a camada da absorção (irmã logo depois da janela).
			var absorption = _bloodAbsorptionOverlay;
			RebuildOverlay(_bloodBlessingOverlay, () => BuildBloodBlessingOverlay(_hudRoot), () => _bloodBlessingOverlay);
			if (absorption != null && IsInstanceValid(absorption))
			{
				var index = absorption.GetIndex();
				absorption.GetParent()?.RemoveChild(absorption);
				absorption.QueueFree();
				_hudRoot.MoveChild(_bloodAbsorptionOverlay, Mathf.Min(index, _hudRoot.GetChildCount() - 1));
			}
			RegisterModalShortcuts(_bloodBlessingOverlay, HandleBlessingOfferShortcut);
		}
		if (_anvilOverlay != null && !IsVisible(_anvilOverlay))
			RebuildOverlay(_anvilOverlay, () => BuildAnvilOfferOverlay(_hudRoot), () => _anvilOverlay);
		if (_weaponBenchOverlay != null && !IsVisible(_weaponBenchOverlay))
			RebuildOverlay(_weaponBenchOverlay, () => BuildWeaponBenchOverlay(_hudRoot), () => _weaponBenchOverlay);
		if (_shopOverlay != null && !IsVisible(_shopOverlay))
			RebuildOverlay(_shopOverlay, () => BuildShopOverlay(_hudRoot), () => _shopOverlay);
		RefreshDialogueLanguage();
	}

	// Remove a camada antiga (sai da árvore já: listeners de TreeExiting rodam antes do novo Build) e põe a nova no mesmo índice.
	private void RebuildOverlay(Control old, Action build, Func<Control> current)
	{
		if (old == null || !IsInstanceValid(old) || old.GetParent() != _hudRoot)
			return;
		var index = old.GetIndex();
		_hudRoot.RemoveChild(old);
		old.QueueFree();
		build();
		var created = current();
		if (created != null && created.GetParent() == _hudRoot)
			_hudRoot.MoveChild(created, index);
	}

	// Rótulos 3D fixos das cenas, escritos aqui porque as cenas não têm script próprio para eles. Os nomes flutuantes dos
	// objetos (Fonte, Bancada, Altar) ficam ocultos: o prompt de interação já diz o que é ao se aproximar.
	private void LocalizeSceneLabels()
	{
		var scene = GetTree().CurrentScene;
		if (scene == null)
			return;
		var sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.SceneFilePath ?? "");
		foreach (var (path, text) in SceneLabelTexts(sceneName))
			if (scene.GetNodeOrNull<Label3D>(path) is Label3D label)
				label.Text = text;
		foreach (var path in HiddenSceneTitles(sceneName))
			if (scene.GetNodeOrNull<Label3D>(path) is Label3D title)
				InteractPrompt.HideTitle(title);
	}

	private static (string Path, string Text)[] SceneLabelTexts(string sceneName) => sceneName switch
	{
		"CrowsNest" => new[]
		{
			("StartRunPortal/PortalLabel", Localization.Text("scene.crows_nest.start_portal"))
		},
		_ => Array.Empty<(string, string)>()
	};

	private static string[] HiddenSceneTitles(string sceneName) => sceneName switch
	{
		"CrowsNest" => new[] { "BloodFountain/BloodFountainLabel", "WeaponBench/WeaponBenchLabel" },
		"RunStartBlessingRoom" => new[] { "BloodFountain/Title" },
		"IntermissionChamber" => new[] { "BloodFountain/Title", "SacrificeAltar/Title" },
		_ => Array.Empty<string>()
	};

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
			case "blessing_offer":
				// Caminho obrigatório: abre direto, sem a animação de absorção (reroll só simulado).
				PreviewBlessingOffer(false);
				return true;
			case "blessing_offer_replace":
				PreviewBlessingOffer(true);
				return true;
			case "anvil_offer":
				PreviewAnvilOffer(3);
				return true;
			case "anvil_offer_single":
				PreviewAnvilOffer(1);
				return true;
			case "weapon_bench":
				OpenWeaponBench();
				return true;
			case "shop":
				// Estoque e saldo fictícios: comprar só simula (nada no RunState nem no save).
				OpenShopPreview();
				return true;
			case "dialogue":
				OpenDialoguePreview(false);
				return true;
			case "dialogue_choice":
				OpenDialoguePreview(true);
				return true;
			case "dialogue_bark":
				PreviewDialogueBark();
				return true;
			case "dialogue_long":
				OpenDialogueStressPreview(false);
				return true;
			case "dialogue_choice_long":
				OpenDialogueStressPreview(true);
				return true;
			case "dialogue_bark_long":
				PreviewDialogueBarkStress();
				return true;
			case "hub_npc":
				PreviewHubNpc();
				return true;
			case "compendium":
				_selectedCompendiumTab = ResolvePreviewCompendiumTab(tab);
				OpenCompendium();
				return true;
			case "toast":
				PreviewToasts();
				return true;
			case "boss_bar":
				BindPreviewBoss(dataParent);
				return true;
			case "hub":
				// Só a cena do hub, sem nenhuma tela por cima (captura do cenário), com o jogador junto do arco de entrada.
				if (GetTree().GetFirstNodeInGroup("player") is Node3D hubPlayer)
					hubPlayer.GlobalPosition = new Vector3(0.0f, hubPlayer.GlobalPosition.Y, -8.5f);
				return true;
			case "hub_cast":
				// Hub com o jogador diante dos 3 sarcófagos de personagens (sala leste; CrowsNestCrypt.CreateCharacterPlaceholders).
				if (GetTree().GetFirstNodeInGroup("player") is Node3D castPlayer)
					castPlayer.GlobalPosition = new Vector3(9.0f, castPlayer.GlobalPosition.Y, 0.0f);
				return true;
			case "reward_doors":
				// Câmara de Intervalo: o jogador diante das portas de recompensa da parede norte (IntermissionChamber.BuildRewardDoors).
				if (GetTree().CurrentScene?.GetNodeOrNull<Node3D>("NorthWall") is Node3D northWall
					&& GetTree().GetFirstNodeInGroup("player") is Node3D doorsPlayer)
					doorsPlayer.GlobalPosition = new Vector3(0.0f, doorsPlayer.GlobalPosition.Y, northWall.Position.Z + 4.0f);
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

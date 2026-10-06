using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// HUD em jogo — painel do jogador, recursos, título da sala, barra do chefe e feedback de dano (guia de UI §4.2, §6, §8.1–8.4, §8.7).
public partial class HUD
{
	private const float CriticalHealthRatio = 0.3f;
	private const float DamageFlashPeak = 0.45f;
	private const float CriticalVignetteCap = 0.55f;
	private const float VignetteCap = 0.70f;

	private Control _hudRoot;
	private TextureRect _edgeVignette;
	private Tween _damageFlashTween;
	private float _damageFlashAmount;
	private float _lastVignetteAlpha = -1.0f;
	private int _damageNumberStep;
	private float _lastDamageNumberTime = -1.0f;
	private float _hudClock;
	private UiResourceBar _healthResourceBar;
	private UiResourceBar _furyResourceBar;
	private UiResourceBar _bossResourceBar;
	private string _bossDisplayName;
	private Control _roomTitleGroup;
	private Label _roomProgressSeparator;
	private PanelContainer _resourceFrame;
	// Debug (previews): congela o painel do jogador com dados fictícios; critical < 0 = sem vida crítica forçada.
	private bool _statusPreviewActive;
	private float _previewHealthRatio = -1.0f;

	private void BuildLayout()
	{
		var root = new Control
		{
			Name = "Root",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Theme = UIThemeFactory.Create()
		};
		AddChild(root);
		_hudRoot = root;

		// Vinheta de borda (§6): primeiro filho = abaixo de todos os painéis, acima do mundo. Miolo transparente.
		_edgeVignette = UiComponents.EdgeVignette(root);

		BuildPlayerPanel(root);
		BuildResourcePanel(root);
		BuildRoomTitle(root);

		_fpsLabel = new Label
		{
			Name = "FpsCounter", Text = "FPS: 0",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			AnchorLeft = 1.0f, AnchorRight = 1.0f,
			OffsetLeft = -140, OffsetTop = UiMetrics.SafeMargin, OffsetRight = -UiMetrics.SafeMargin, OffsetBottom = UiMetrics.SafeMargin + 22,
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = OS.IsDebugBuild()
		};
		root.AddChild(_fpsLabel);

		_blessingHudList = new HBoxContainer
		{
			Name = "BlessingHudList",
			AnchorLeft = 0.5f, AnchorTop = 1.0f, AnchorRight = 0.5f, AnchorBottom = 1.0f,
			OffsetLeft = -246.0f, OffsetTop = -72.0f, OffsetRight = 246.0f, OffsetBottom = -UiMetrics.SafeMargin,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		_blessingHudList.AddThemeConstantOverride("separation", 8);
		root.AddChild(_blessingHudList);
		RefreshBlessingHud();

		BuildBossBar(root);

		// Contrato hud-ingame T1/T2: log (debug), toasts, alerta de wave e banner de sala limpa (HUD.Notifications.cs).
		// Acima dos painéis do HUD e abaixo dos overlays modais.
		BuildNotificationsLayout(root);

		BuildPauseOverlay(root);
		BuildGameOverOverlay(root);
		BuildCharacterSheetOverlay(root);
		BuildBloodFountainOverlay(root);
		BuildArtifactChestOverlay(root);
		BuildArtifactOfferOverlay(root);
		BuildBloodBlessingOverlay(root);
		BuildWeaponBenchOverlay(root);
		BuildCompendiumOverlay(root);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
	}

	// §8.1: painel inferior esquerdo (24, −24), altura automática: só a vida; a fúria entra quando liberada.
	private void BuildPlayerPanel(Control root)
	{
		var panel = new PanelContainer
		{
			Name = "PlayerFrame",
			ThemeTypeVariation = UiVariation.HudPanel,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.0f, AnchorTop = 1.0f, AnchorRight = 0.0f, AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.End,
			GrowVertical = Control.GrowDirection.Begin
		};
		root.AddChild(panel);
		_playerHudFrame = panel;

		var column = new VBoxContainer { Name = "PlayerStatus", MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", UiMetrics.S2);
		panel.AddChild(column);

		// Linha da vida: barra 260 · valor 56 (fora da barra, legível sobre chip e barreira) · "+N" de barreira 40.
		// 260 + 8 + 56 + 8 + 40 = 372 de conteúdo; barreira atrás da vida sobre o máximo combinado.
		_healthResourceBar = UiComponents.ResourceBar(ResourceBarKind.Health, 260);
		_healthResourceBar.Bar.MouseFilter = Control.MouseFilterEnum.Ignore;
		_healthResourceBar.Root.Name = "HealthAndBarrier";
		column.AddChild(_healthResourceBar.Root);

		// Fúria com a mesma largura da barra de vida (bordas alinhadas).
		_furyResourceBar = UiComponents.ResourceBar(ResourceBarKind.Fury, 260);
		_furyResourceBar.Root.Name = "Fury";
		_furyResourceBar.Root.Visible = false;
		column.AddChild(_furyResourceBar.Root);

		FitPlayerPanel();
		var showCombatHud = !IsHubScene();
		panel.Visible = showCombatHud;
	}

	// Recolhe o painel ao conteúdo atual (o Control fora de contêiner não encolhe sozinho) e cresce para cima.
	private void FitPlayerPanel()
	{
		if (_playerHudFrame == null)
			return;
		_playerHudFrame.OffsetLeft = UiMetrics.SafeMargin;
		_playerHudFrame.OffsetRight = UiMetrics.SafeMargin;
		_playerHudFrame.OffsetTop = -UiMetrics.SafeMargin;
		_playerHudFrame.OffsetBottom = -UiMetrics.SafeMargin;
	}

	// §8.2: só moedas (gota + número). A arma fica na ficha e na bancada; o progresso vai para o título da sala.
	private void BuildResourcePanel(Control root)
	{
		var panel = new PanelContainer
		{
			Name = "ResourceFrame",
			ThemeTypeVariation = UiVariation.HudPanel,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			OffsetLeft = UiMetrics.SafeMargin, OffsetTop = UiMetrics.SafeMargin,
			OffsetRight = UiMetrics.SafeMargin, OffsetBottom = UiMetrics.SafeMargin
		};
		root.AddChild(panel);
		_resourceFrame = panel;
		var row = new HBoxContainer { Name = "ResourcePanel", MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		panel.AddChild(row);
		var coin = UiComponents.CoinGlyph(24);
		coin.TooltipText = "Moedas de Sangue";
		row.AddChild(coin);
		_coinsLabel = new Label
		{
			Name = "BloodCoinsLabel",
			Text = "0",
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_coinsLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
		row.AddChild(_coinsLabel);

		// Fora do HUD (§8.2). Continua existindo, escondido, porque HUD.WeaponBench.cs ainda escreve nele.
		_weaponLabel = new Label
		{
			Name = "EquippedWeaponLabel",
			Text = $"Arma: {WeaponLoadout.EquippedWeapon.DisplayName}",
			Visible = false
		};
		root.AddChild(_weaponLabel);
	}

	// §8.3: nome do mapa (Section, Gold) + progresso (Secondary) na mesma linha; some na sala do chefe.
	private void BuildRoomTitle(Control root)
	{
		_roomTitleGroup = new Control
		{
			Name = "RoomTitle",
			AnchorRight = 1.0f, AnchorBottom = 1.0f,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(_roomTitleGroup);

		_roomFrame = new PanelContainer
		{
			Name = "RoomFrame",
			ThemeTypeVariation = UiVariation.HudPanel,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			// Largura acompanha o conteúdo (mín. 320), centrada; ver o ajuste da Câmara no fim do método.
			AnchorLeft = 0.5f, AnchorRight = 0.5f,
			OffsetLeft = 0.0f, OffsetTop = 16.0f, OffsetRight = 0.0f, OffsetBottom = 64.0f,
			CustomMinimumSize = new Vector2(320.0f, 0.0f),
			GrowHorizontal = Control.GrowDirection.Both
		};
		_roomTitleGroup.AddChild(_roomFrame);
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		_roomFrame.AddChild(row);

		_roomLabel = new Label
		{
			Name = "RoomLabel",
			Text = "",
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddChild(_roomLabel);
		_roomProgressSeparator = new Label
		{
			Text = "·",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddChild(_roomProgressSeparator);
		_enemyProgressLabel = new Label
		{
			Name = "EnemyProgressLabel",
			Text = "",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddChild(_enemyProgressLabel);

		// A Camara de Hemomancia escreve o proprio status (2 linhas centradas, ~260 px) sobre esta moldura vazia:
		// só a altura precisa cobrir as duas linhas; a largura segue o mínimo comum, sem a faixa fixa de 800.
		_roomFrameAlwaysVisible = GetTree().CurrentScene is IntermissionChamber;
		if (_roomFrameAlwaysVisible)
			_roomFrame.CustomMinimumSize = new Vector2(360.0f, 76.0f);
	}

	// §8.4: barra do chefe no lugar do título (âncoras 0.25..0.75, topo 16), nome e valor em rótulos separados.
	private void BuildBossBar(Control root)
	{
		var panel = new PanelContainer
		{
			Name = "BossHealthPanel",
			ThemeTypeVariation = UiVariation.HudPanel,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.25f, AnchorRight = 0.75f,
			OffsetTop = 16.0f, OffsetBottom = 16.0f,
			GrowVertical = Control.GrowDirection.End,
			Visible = false
		};
		root.AddChild(panel);
		_bossHealthPanel = panel;
		_bossResourceBar = UiComponents.ResourceBar(ResourceBarKind.Boss);
		_bossResourceBar.Root.Name = "BossBar";
		_bossResourceBar.Bar.MouseFilter = Control.MouseFilterEnum.Ignore;
		_bossResourceBar.NameLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
		_bossResourceBar.ValueLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
		panel.AddChild(_bossResourceBar.Root);
		_bossDisplayName = Localization.Text("hud.boss");
		_bossResourceBar.SetName(_bossDisplayName);
		// BindBoss/UpdateBossHealth (HUD.Notifications.cs) escrevem direto no componente (SetName/SetValue).
		panel.VisibilityChanged += RefreshRoomTitleVisibility;
	}

	private void RefreshRoomTitleVisibility()
	{
		if (_roomTitleGroup != null)
			_roomTitleGroup.Visible = !IsVisible(_bossHealthPanel);
	}

	private bool IsHubScene()
	{
		var scenePath = GetTree().CurrentScene?.SceneFilePath ?? "";
		return scenePath.EndsWith("CrowsNest.tscn", StringComparison.OrdinalIgnoreCase);
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
			combat.FuryChanged += OnFuryChanged;
			OnFuryChanged(combat.CurrentFury, combat.MaxFury, combat.IsFuryActive);
		}

		if (runManager != null)
		{
			runManager.BloodCoinsChanged += OnBloodCoinsChanged;
			runManager.BloodCoinsRewarded += OnBloodCoinsRewarded;
			runManager.EnemyProgressChanged += OnEnemyProgressChanged;
			runManager.RoomCleared += OnRoomCleared;
			OnBloodCoinsChanged(runManager.BloodCoins);
			OnEnemyProgressChanged(runManager.DefeatedEnemies, runManager.TotalEnemies);
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
		PlayDamageFeedback();
	}

	// §6: a vinheta de borda sobe a 0,45 em 0,03 s e cai em 0,12 s; a borda da barra de vida pisca junto.
	private void PlayDamageFeedback()
	{
		_healthResourceBar?.FlashDamage();
		if (_edgeVignette == null)
			return;
		_damageFlashTween?.Kill();
		_damageFlashTween = _edgeVignette.CreateTween();
		_damageFlashTween.TweenMethod(Callable.From<float>(SetDamageFlash), _damageFlashAmount, DamageFlashPeak, UiMotion.FlashRise);
		_damageFlashTween.TweenMethod(Callable.From<float>(SetDamageFlash), DamageFlashPeak, 0.0f, UiMotion.FlashFall);
	}

	private void SetDamageFlash(float amount)
	{
		_damageFlashAmount = amount;
		ApplyVignetteAlpha();
	}

	// §3.14: número no tema, saindo de cima do jogador projetado na tela; sem câmera, acima do painel do jogador.
	private void ShowReceivedDamageNumber(float amount)
	{
		if (_hudRoot == null)
			return;
		// Golpes em sequência (< 0,35 s) abrem em leque (centro, direita, esquerda) para não se sobreporem (máx. 3 vivos).
		_damageNumberStep = _hudClock - _lastDamageNumberTime < 0.35f ? (_damageNumberStep + 1) % 3 : 0;
		_lastDamageNumberTime = _hudClock;
		var step = _damageNumberStep;
		var fan = new Vector2(step == 1 ? 64.0f : step == 2 ? -64.0f : 0.0f, -14.0f * step);
		UiComponents.DamageNumber(_hudRoot, amount, ResolveDamageNumberOrigin() + fan, step == 0);
	}

	private Vector2 ResolveDamageNumberOrigin()
	{
		var camera = GetViewport()?.GetCamera3D();
		if (camera != null && _runtimePlayerController != null && IsInstanceValid(_runtimePlayerController))
		{
			var worldPosition = _runtimePlayerController.GlobalPosition;
			if (!camera.IsPositionBehind(worldPosition))
				return camera.UnprojectPosition(worldPosition) + new Vector2(0, -80);
		}
		if (_playerHudFrame != null)
			return _playerHudFrame.GetGlobalRect().Position + new Vector2(_playerHudFrame.Size.X * 0.5f, -24);
		return _hudRoot.Size * new Vector2(0.5f, 0.6f);
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
		if (_statusPreviewActive || _runtimeHealth == null || _healthResourceBar == null)
			return;

		var barrier = _runtimePlayerController?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		var barrierCurrent = barrier?.CurrentBarrier ?? 0.0f;
		var barrierMaximum = barrier?.MaximumBarrier ?? 0.0f;
		_healthResourceBar.SetHealth(_runtimeHealth.CurrentHealth, _runtimeHealth.MaxHealth, barrierCurrent, barrierMaximum);
		FitPlayerPanel();
	}

	// Chamado todo quadro por HUD._Process. Sem alocação; só reescreve o alpha quando ele muda.
	private void UpdateCriticalHealthIndicator(float delta)
	{
		// Relógio do HUD em tempo de jogo (o mesmo do --fixed-fps nas capturas); volta a zero para não perder precisão.
		_hudClock = Mathf.PosMod(_hudClock + delta, 3600.0f);
		var ratio = CurrentHealthRatioForVignette();
		if (ratio >= 0.0f && ratio < CriticalHealthRatio)
		{
			var periodSeconds = 1.0f / UiMotion.CriticalPulseHz;
			_criticalHealthPulse = Mathf.PosMod(_criticalHealthPulse + delta, periodSeconds);
		}
		else
		{
			_criticalHealthPulse = 0.0f;
		}
		ApplyVignetteAlpha();
	}

	// −1 = sem vida crítica possível (sem jogador, morto ou fora de combate).
	private float CurrentHealthRatioForVignette()
	{
		if (_previewHealthRatio >= 0.0f)
			return _previewHealthRatio;
		if (_runtimeHealth == null || !IsInstanceValid(_runtimeHealth) || _runtimeHealth.MaxHealth <= 0.0f || _runtimeHealth.IsDead)
			return -1.0f;
		return _runtimeHealth.CurrentHealth / _runtimeHealth.MaxHealth;
	}

	private void ApplyVignetteAlpha()
	{
		if (_edgeVignette == null)
			return;
		var alpha = 0.0f;
		var ratio = CurrentHealthRatioForVignette();
		if (ratio >= 0.0f && ratio < CriticalHealthRatio)
		{
			var severity = Mathf.Clamp(1.0f - ratio / CriticalHealthRatio, 0.0f, 1.0f);
			var pulse = 0.5f + 0.5f * Mathf.Sin(Mathf.Tau * UiMotion.CriticalPulseHz * _criticalHealthPulse);
			alpha = Mathf.Min(0.20f + 0.25f * severity + 0.10f * pulse, CriticalVignetteCap);
		}
		alpha = Mathf.Min(alpha + _damageFlashAmount, VignetteCap);
		if (Mathf.IsEqualApprox(alpha, _lastVignetteAlpha))
			return;
		_lastVignetteAlpha = alpha;
		_edgeVignette.Modulate = new Color(1, 1, 1, alpha);
	}

	private void OnFuryChanged(float current, float max, bool isActive)
	{
		if (_furyResourceBar == null || _statusPreviewActive)
			return;
		_furyResourceBar.SetValue(current, max);
		_furyResourceBar.SetFuryState(max > 0.0f && current >= max, isActive);
		RefreshFuryVisibility();
		var furyDrainPerSecond = CompendiumTuning.GetFloat("attack.fury.drain_per_second", 10.0f);
		_furyResourceBar.Bar.TooltipText = isActive
			? $"Furia Bestial ativa: consome {furyDrainPerSecond:0.#} pontos por segundo e explode ao chegar a zero"
			: "Furia Bestial - pressione F (R1/RB) quando a barra estiver cheia";
	}

	// A fúria só aparece quando liberada por uma Bênção de Sangue (§8.1).
	private void RefreshFuryVisibility()
	{
		if (_furyResourceBar == null || _statusPreviewActive)
			return;
		var unlocked = RunState.GetEffectTotal("UnlockBestialFury.Flat") > 0.0f;
		if (_furyResourceBar.Root.Visible == unlocked)
			return;
		_furyResourceBar.Root.Visible = unlocked;
		FitPlayerPanel();
	}

	private void OnBloodCoinsChanged(int amount)
	{
		_coinsLabel.Text = amount.ToString();
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
		// Só a sala de combate (TestArena) tem RunManager, única fonte deste sinal: hub, câmara e sala inicial nunca
		// mostram progresso. Antes da 1ª wave (total 0) o texto é só "Sala x/y" (SyncEnemyProgress). Sem nome de
		// mapa a moldura inteira some (HUD._Process), então não é preciso esperar SyncRoomInformation.
		SetRoomProgressVisible(true);
	}

	private void SetRoomProgressVisible(bool visible)
	{
		if (_enemyProgressLabel != null)
			_enemyProgressLabel.Visible = visible;
		if (_roomProgressSeparator != null)
			_roomProgressSeparator.Visible = visible;
	}

	private void OnRoomCleared()
	{
		if (_roomClearAnnounced)
			return;

		_roomClearAnnounced = true;
		// O nome do mapa continua; a parte secundária vira "Sala limpa". O portal só abre depois da recompensa
		// (RunManager.UnlockExitAfterReward → NotifyPortalOpened troca para "Portal aberto").
		_enemyProgressLabel.Text = "Sala limpa";
		SetRoomProgressVisible(!string.IsNullOrEmpty(_roomLabel.Text));
		ShowRoomClearBanner();
	}

	// ---------- Pré-visualização (debug, --ui-preview; ligada em HUD.OpenPreviewScreen) ----------

	// hud_combat: vida 83/100 + barreira 20, fúria cheia (pronta), 500 moedas.
	internal void PreviewHudCombat()
	{
		ApplyStatusPreview(83.0f, 100.0f, 20.0f, 20.0f);
		_furyResourceBar.Root.Visible = true;
		_furyResourceBar.SetValue(100.0f, 100.0f);
		_furyResourceBar.SetFuryState(true, false);
		FitPlayerPanel();
		_coinsLabel.Text = "500";
	}

	// critical_health: vida 14/100, vinheta de vida crítica pulsando.
	internal void PreviewCriticalHealth()
	{
		ApplyStatusPreview(14.0f, 100.0f, 0.0f, 0.0f);
		_previewHealthRatio = 0.14f;
	}

	// damage_flash: um golpe (flash de borda + borda da barra). Repetir a cada ~0,5 s para aparecer na captura.
	internal void PreviewDamageFlash()
	{
		ApplyStatusPreview(62.0f, 100.0f, 0.0f, 0.0f);
		PlayDamageFeedback();
	}

	// damage_number: três golpes seguidos (12, 27, 9) com flash; o chip de dano aparece na barra.
	internal void PreviewDamageNumber()
	{
		ApplyStatusPreview(100.0f, 100.0f, 0.0f, 0.0f);
		var hits = new[] { 12.0f, 27.0f, 9.0f };
		var tween = CreateTween();
		var remaining = 100.0f;
		foreach (var hit in hits)
		{
			var amount = hit;
			remaining -= amount;
			var after = remaining;
			tween.TweenCallback(Callable.From(() =>
			{
				_healthResourceBar.SetHealth(after, 100.0f, 0.0f, 0.0f);
				ShowReceivedDamageNumber(amount);
				PlayDamageFeedback();
			}));
			tween.TweenInterval(0.18f);
		}
	}

	private void ApplyStatusPreview(float health, float maxHealth, float barrier, float barrierMax)
	{
		_statusPreviewActive = false;
		_previewHealthRatio = -1.0f;
		_playerHudFrame.Visible = true;
		_healthResourceBar.SetHealth(health, maxHealth, barrier, barrierMax);
		_statusPreviewActive = true;
		FitPlayerPanel();
	}
}

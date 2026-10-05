using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	// Guia §3.9/§3.10/§8.5: alerta de wave, toasts empilhados, "+N" de kill, log só em debug e banner de sala limpa.
	private const int GameLogMemoryEntries = 20;
	private UiToastStack _toastStack;
	private UiEventLog _eventLog;
	private Tween _roomClearTween;
	private string _lastToastText;
	private ulong _lastToastTicks;
	private bool _portalOpenAnnounced;

	// Contrato hud-ingame T1/T2: BuildLayout (StatusBars) chama este método; T2 constrói aqui log, toasts e banner.
	// Idempotente: pode ser chamado mais de uma vez sem duplicar nós.
	private void BuildNotificationsLayout(Control root)
	{
		if (_toastStack != null || root == null)
			return;

		if (OS.IsDebugBuild())
		{
			_eventLog = UiComponents.EventLog(root);
			_eventLog.MaxLines = 3;
		}

		_toastStack = UiComponents.ToastStack(root);
		// Notificações de combate congelam com o jogo pausado (contagem da wave não corre sob a pausa).
		_toastStack.Root.ProcessMode = ProcessModeEnum.Pausable;

		BuildRoomClearBannerPanel(root);
	}

	public void AppendGameLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		// Sem horário (Aud §5). No release só guarda as últimas entradas em memória, sem UI (decisão D4).
		_gameLogEntries.Enqueue(message);
		while (_gameLogEntries.Count > GameLogMemoryEntries) _gameLogEntries.Dequeue();
		_eventLog?.Append(message);
	}

	// §8.3: parte secundária do título — "Sala x/y · n/m" (antes da 1ª wave só "Sala x/y"; chefe: "Chefe").
	// Depois de limpar fica "Sala limpa" e, quando o portal abre de fato, "Portal aberto" (NotifyPortalOpened).
	public void SyncEnemyProgress(int defeatedEnemies, int totalEnemies)
	{
		if (_enemyProgressLabel == null || _roomClearAnnounced)
			return;
		var room = $"Sala {RunState.CurrentRoom}/{RunState.CurrentMap.CommonRoomCount}";
		_enemyProgressLabel.Text = RunState.IsBossRoom
			? "Chefe"
			: totalEnemies > 0 ? $"{room} · {defeatedEnemies}/{totalEnemies}" : room;
	}

	// Canal (c): o "+N" sai em NotifyBloodCoinsRewarded (toda entrada de moedas), sem frase nem toast.
	public void NotifyEnemyDefeated(int defeatedEnemies, int totalEnemies, int bloodCoinsReward)
	{
		SyncEnemyProgress(defeatedEnemies, totalEnemies);
		AppendGameLog($"+{bloodCoinsReward} moedas · {defeatedEnemies}/{totalEnemies} inimigos");
	}

	public void NotifyBloodCoinsRewarded(int reward, int totalAmount)
	{
		OnBloodCoinsChanged(totalAmount);
		// Host = moldura do painel: o "+N" sai fora da borda, à direita (posição lida após o relayout).
		if (reward > 0)
			_toastStack?.ShowKill(_resourceFrame, reward);
	}

	public void SyncBloodCoins(int totalAmount)
	{
		OnBloodCoinsChanged(totalAmount);
	}

	public void NotifyRoomCleared()
	{
		AppendGameLog("Sala limpa. Escolha sua recompensa.");
		OnRoomCleared();
	}

	// Chamado via CallGroup("hud", "NotifyPortalOpened") por RunManager.UnlockExitAfterReward, quando o portal de
	// saída abre de fato (depois da recompensa; no chefe, depois de pegar o artefato). Não renomear sem procurar
	// a string em scripts/. Idempotente: UnlockExitAfterReward pode ser chamado mais de uma vez pelas ofertas.
	public void NotifyPortalOpened()
	{
		if (_portalOpenAnnounced)
			return;
		_portalOpenAnnounced = true;
		_roomClearAnnounced = true;
		if (_enemyProgressLabel != null)
		{
			_enemyProgressLabel.Text = "Portal aberto";
			SetRoomProgressVisible(!string.IsNullOrEmpty(_roomLabel?.Text));
		}
		AppendGameLog("O portal de saída foi aberto.");
		ShowNotificationToast("O portal foi aberto", false);
	}

	// Canal (a): faixa de perigo com contagem regressiva; não é substituída por kills nem por toasts.
	// O log da wave já é escrito por quem chama (RoomGenerator).
	public void NotifyWaveIncoming(int enemyCount, float seconds)
	{
		var body = enemyCount == 1 ? "1 inimigo" : $"{enemyCount} inimigos";
		_toastStack?.ShowCombatAlert("Próxima onda", body, Mathf.Max(0.0f, seconds));
	}

	public void BindBoss(Node bossNode, string displayName)
	{
		var health = bossNode?.GetNodeOrNull<Health>("Health");
		if (health == null) return;
		if (_bossResourceBar == null) return;
		// Nome e valor em rótulos separados do componente (§8.4); o valor é escrito por SetValue.
		_bossDisplayName = string.IsNullOrWhiteSpace(displayName) ? Localization.Text("hud.boss") : displayName;
		_bossResourceBar.SetName(_bossDisplayName);
		_bossHealthPanel.Visible = true;
		UpdateBossHealth(health.CurrentHealth, health.MaxHealth);
		health.HealthChanged += UpdateBossHealth;
		health.Died += () => _bossHealthPanel.Visible = false;
	}

	private void UpdateBossHealth(float current, float maximum)
	{
		_bossResourceBar?.SetValue(current, maximum);
	}

	// §8.3: só o nome do mapa (dourado); sala e progresso vão para _enemyProgressLabel.
	private void SyncRoomInformation()
	{
		_roomLabel.Text = RunState.CurrentMap.Name;
	}

	public void OpenBloodFountain()
	{
		if (_bloodFountainOverlay == null)
			return;

		RefreshBloodFountain();
		_bloodFountainOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_bloodFountainOverlay);
	}

	// Canal (b) informativo (borda neutra): mensagens de skill e da Câmara.
	public void NotifySkillMessage(string message)
	{
		AppendGameLog(message);
		ShowNotificationToast(message, false);
	}

	public void NotifyArtifactCollected(string artifactName)
	{
		AppendGameLog($"Artefato obtido: {artifactName}.");
		ShowNotificationToast($"Artefato obtido: {artifactName}", true);
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

	// Banner: PanelHud com borda 2 WindowBorder, 560 × auto, âncora 0.18; título Banner 34 Gold + linha secundária.
	private void BuildRoomClearBannerPanel(Control root)
	{
		var banner = new PanelContainer
		{
			Name = "RoomClearBanner",
			// Slot do alerta de wave (y 132); ao aparecer, esconde o alerta e desce a pilha de toasts para y 240.
			AnchorLeft = 0.5f,
			AnchorTop = 0.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.0f,
			OffsetLeft = -280.0f,
			OffsetTop = 132.0f,
			OffsetRight = 280.0f,
			OffsetBottom = 132.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Pausable,
			Visible = false,
			Modulate = new Color(1, 1, 1, 0)
		};
		banner.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelHud, UiPalette.WindowBorder, UiMetrics.BorderWindow, UiMetrics.S5, UiMetrics.S3));
		root.AddChild(banner);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", UiMetrics.S1);
		banner.AddChild(column);

		column.AddChild(new Label
		{
			Name = "RoomClearLabel",
			Text = Localization.Text("hud.room_clear"),
			ThemeTypeVariation = UiVariation.BannerLabel,
			Uppercase = true,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
		column.AddChild(new Label
		{
			// O portal só abre depois da recompensa (NotifyPortalOpened); aqui não se afirma que ele abriu.
			Name = "RoomClearSubtitle",
			Text = "Escolha sua recompensa",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
		_roomClearBanner = banner;
	}

	private void ShowRoomClearBanner()
	{
		var banner = _roomClearBanner;
		if (banner == null || !IsInstanceValid(banner) || !banner.IsInsideTree())
			return;

		_roomClearTween?.Kill();
		_toastStack?.SetBannerSlot(true);
		banner.Visible = true;
		// Pivô no centro: a escala cresce a partir do meio, não do canto.
		var size = new Vector2(Mathf.Max(banner.Size.X, 560.0f), Mathf.Max(banner.Size.Y, banner.GetCombinedMinimumSize().Y));
		banner.PivotOffset = size * 0.5f;
		banner.Modulate = new Color(1, 1, 1, 0);
		banner.Scale = new Vector2(0.94f, 0.94f);

		var tween = banner.CreateTween();
		_roomClearTween = tween;
		tween.TweenProperty(banner, "modulate:a", 1.0f, UiMotion.BannerIn);
		tween.Parallel().TweenProperty(banner, "scale", Vector2.One, UiMotion.BannerIn).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		tween.TweenInterval(UiMotion.BannerHold);
		tween.TweenProperty(banner, "modulate:a", 0.0f, UiMotion.BannerOut);
		tween.TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(banner))
				banner.Visible = false;
			_toastStack?.SetBannerSlot(false);
		}));
	}

	// Nome mantido por compatibilidade (BlessingOffer e o preview "toast" chamam): agora é um toast de recompensa (canal b).
	private void ShowEnemyDefeatedToast(string message)
	{
		ShowNotificationToast(message, true);
	}

	// Canal (b). Ignora a repetição imediata do mesmo texto (ex.: apertar Fúria várias vezes sem carga).
	private void ShowNotificationToast(string message, bool reward, Texture2D icon = null)
	{
		if (_toastStack == null || string.IsNullOrWhiteSpace(message))
			return;

		var now = Time.GetTicksMsec();
		var holdMs = (ulong)((UiMotion.ToastIn + UiMotion.ToastHold) * 1000.0f);
		if (message == _lastToastText && now - _lastToastTicks < holdMs)
			return;

		_lastToastText = message;
		_lastToastTicks = now;
		_toastStack.ShowToast(message, icon, reward);
	}

	// Preview "event_log" (§8.5): preenche o log com linhas de exemplo. Só tem efeito em debug.
	internal void PreviewEventLog()
	{
		AppendGameLog("Wave com 5 inimigos chega em 3 segundos.");
		AppendGameLog("Virgil recebeu 12 de dano.");
		AppendGameLog("+5 moedas · 7/20 inimigos");
	}

	// Preview "toast" completo (§8.5): três recompensas em fila + "+N" ao lado das moedas.
	internal void PreviewToasts()
	{
		_lastToastText = null;
		ShowNotificationToast("Artefato obtido: Cálice Rachado", true);
		_lastToastText = null;
		ShowNotificationToast("Raro: Sede Escarlate", true);
		_lastToastText = null;
		ShowNotificationToast("Fúria Bestial ainda não está pronta (40/100).", false);
		_toastStack?.ShowKill(_resourceFrame, 5);
	}
}

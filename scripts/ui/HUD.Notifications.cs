using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HUD
{
	// Guia §3.9/§3.10/§8.5: toasts empilhados, "+N" de kill e log só em debug. O alerta de wave e o banner de sala limpa
	// saíram da HUD (limpeza de 2026-10): a sala limpa aparece só no título e no log.
	private const int GameLogMemoryEntries = 20;
	private UiToastStack _toastStack;
	private UiEventLog _eventLog;
	private string _lastToastText;
	private ulong _lastToastTicks;
	private bool _portalOpenAnnounced;
	private int _lastDefeatedEnemies;
	private int _lastTotalEnemies;

	// Contrato hud-ingame T1/T2: BuildLayout (StatusBars) chama este método; T2 constrói aqui log e toasts.
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
	}

	public void AppendGameLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		DevEcho(message);
		// Sem horário (Aud §5). No release só guarda as últimas entradas em memória, sem UI (decisão D4).
		_gameLogEntries.Enqueue(message);
		while (_gameLogEntries.Count > GameLogMemoryEntries) _gameLogEntries.Dequeue();
		_eventLog?.Append(message);
	}

	// §8.3: parte secundária do título — "Sala x/y · n/m" (antes da 1ª wave só "Sala x/y"; chefe: "Chefe").
	// Depois de limpar fica "Sala limpa" e, quando a saída abre de fato, "Portal aberto" ou, com portas de recompensa,
	// "escolha uma porta" (NotifyPortalOpened).
	public void SyncEnemyProgress(int defeatedEnemies, int totalEnemies)
	{
		_lastDefeatedEnemies = defeatedEnemies;
		_lastTotalEnemies = totalEnemies;
		RefreshRoomProgressText();
	}

	// A saída desta sala tem conjunto de portas de recompensa (RunState guarda o conjunto por origem = sala atual).
	// O boss e a saída única não têm conjunto, então devolvem false.
	private static bool RoomHasRewardDoors() =>
		RunState.GetRoomRewardSet(RunState.CurrentMapIndex, RunState.CurrentRoom) != null;

	// Reescreve a parte secundária do título no idioma atual (também chamado na troca de idioma).
	private void RefreshRoomProgressText()
	{
		if (_enemyProgressLabel == null)
			return;
		if (_portalOpenAnnounced)
		{
			// Com portas de recompensa, a sala pede a escolha; sem elas (boss, saída única), segue "Portal aberto".
			if (RoomHasRewardDoors())
				_enemyProgressLabel.Text = Localization.Text("hud.room.choose_door");
			else
				_enemyProgressLabel.Text = Localization.Text("hud.room.portal_open");
			return;
		}
		if (_roomClearAnnounced)
		{
			_enemyProgressLabel.Text = Localization.Text("hud.room.cleared");
			return;
		}
		if (RunState.IsBossRoom)
		{
			_enemyProgressLabel.Text = Localization.Text("hud.room.boss");
			return;
		}
		var room = RunState.CurrentRoom;
		var rooms = RunState.CurrentMap.CommonRoomCount;
		_enemyProgressLabel.Text = _lastTotalEnemies > 0
			? Localization.TextFormat("hud.room.progress_enemies", room, rooms, _lastDefeatedEnemies, _lastTotalEnemies)
			: Localization.TextFormat("hud.room.progress", room, rooms);
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

	// Sangue Amaldiçoado (recurso PERMANENTE do hub, não as Moedas da run): aviso próprio logo acima do painel de Moedas,
	// em violeta com losango e linha de total, para nunca ser confundido com o "+N" dourado das Moedas.
	// Chamado via CallGroup("hud", "NotifyCursedBloodRewarded", amount, total). Não renomear sem procurar a string em scripts/.
	private const float CursedBloodHold = 2.6f;
	private PanelContainer _cursedBloodPopup;
	private Label _cursedBloodRewardLabel;
	private Label _cursedBloodTotalLabel;
	private Tween _cursedBloodTween;

	public void NotifyCursedBloodRewarded(int amount, int total)
	{
		if (amount <= 0)
			return;
		AppendGameLog(Localization.TextFormat("hud.cursed_blood.reward", amount));
		ShowCursedBloodPopup(amount, total);
	}

	private void ShowCursedBloodPopup(int amount, int total)
	{
		if (_resourceFrame == null || !IsInstanceValid(_resourceFrame) || !_resourceFrame.IsInsideTree())
			return;
		if (_cursedBloodPopup == null || !IsInstanceValid(_cursedBloodPopup))
			BuildCursedBloodPopup();
		if (_cursedBloodPopup == null)
			return;

		_cursedBloodRewardLabel.Text = Localization.TextFormat("hud.cursed_blood.reward", amount);
		_cursedBloodTotalLabel.Text = Localization.TextFormat("hud.cursed_blood.total", total);
		// Logo acima da moldura de Moedas (canto inferior esquerdo), alinhado à esquerda dela; cresce para a direita e para cima.
		var parent = _cursedBloodPopup.GetParent() as Control;
		var frameRect = _resourceFrame.GetGlobalRect();
		var origin = frameRect.Position - (parent?.GetGlobalRect().Position ?? Vector2.Zero);
		var bottom = origin.Y - UiMetrics.S2;
		_cursedBloodPopup.OffsetLeft = origin.X;
		_cursedBloodPopup.OffsetRight = origin.X;
		_cursedBloodPopup.OffsetTop = bottom;
		_cursedBloodPopup.OffsetBottom = bottom;

		_cursedBloodTween?.Kill();
		_cursedBloodPopup.Visible = true;
		_cursedBloodPopup.Modulate = new Color(1, 1, 1, 0);
		var popup = _cursedBloodPopup;
		_cursedBloodTween = popup.CreateTween();
		_cursedBloodTween.TweenProperty(popup, "modulate:a", 1.0f, UiMotion.ToastIn);
		_cursedBloodTween.TweenInterval(CursedBloodHold);
		_cursedBloodTween.TweenProperty(popup, "modulate:a", 0.0f, UiMotion.ToastOut);
		_cursedBloodTween.TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(popup))
				popup.Visible = false;
		}));
	}

	private void BuildCursedBloodPopup()
	{
		// Irmão do canto inferior esquerdo (o contêiner dele reposicionaria o aviso).
		var parent = _bottomLeftHud?.GetParent() as Control;
		if (parent == null)
			return;

		var popup = new PanelContainer
		{
			Name = "CursedBloodPopup",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Pausable,
			Visible = false,
			GrowHorizontal = Control.GrowDirection.End,
			GrowVertical = Control.GrowDirection.Begin
		};
		if (!UiPixelKit.ApplyPanel(popup, UiMetrics.S4, UiMetrics.S2))
			popup.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelHud, UiPalette.CursedBlood, UiMetrics.BorderWindow, UiMetrics.S4, UiMetrics.S2));
		parent.AddChild(popup);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		popup.AddChild(row);

		// Losango violeta (a gota é das Moedas): forma própria além da cor; no 2D, a gema em pixel art.
		var glyph = UiComponents.CursedBloodGlyph(24);
		row.AddChild(glyph);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 0);
		row.AddChild(column);
		_cursedBloodRewardLabel = new Label
		{
			Name = "CursedBloodRewardLabel",
			ThemeTypeVariation = UiVariation.HudValueLabel,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_cursedBloodRewardLabel.AddThemeColorOverride("font_color", UiPalette.CursedBlood);
		column.AddChild(_cursedBloodRewardLabel);
		_cursedBloodTotalLabel = new Label
		{
			Name = "CursedBloodTotalLabel",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_cursedBloodTotalLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
		column.AddChild(_cursedBloodTotalLabel);
		_cursedBloodPopup = popup;
	}

	public void NotifyRoomCleared()
	{
		AppendGameLog("Sala limpa. Escolha sua recompensa.");
		OnRoomCleared();
	}

	// Chamado via CallGroup("hud", "NotifyPortalOpened") por RunManager.UnlockExitAfterReward, quando o portal de
	// saída abre de fato (depois de pegar o último item do chão ou fechar a oferta de bênção; sem portas de recompensa, como no chefe, abre a saída única). Não renomear sem procurar
	// a string em scripts/. Idempotente: UnlockExitAfterReward pode ser chamado mais de uma vez pelas ofertas.
	public void NotifyPortalOpened()
	{
		if (_portalOpenAnnounced)
			return;
		_portalOpenAnnounced = true;
		_roomClearAnnounced = true;
		if (_enemyProgressLabel != null)
		{
			RefreshRoomProgressText();
			SetRoomProgressVisible(!string.IsNullOrEmpty(_roomLabel?.Text));
		}
		AppendGameLog("O portal de saída foi aberto.");
		ShowNotificationToast(Localization.Text("hud.toast.portal_open"), false);
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

	// Canal (b) informativo (borda neutra): mensagens de skill e da Câmara.
	public void NotifySkillMessage(string message)
	{
		AppendGameLog(message);
		ShowNotificationToast(message, false);
	}

	public void NotifyArtifactCollected(ArtifactRecord artifact)
	{
		var displayName = ArtifactCatalog.DisplayNameFor(artifact);
		AppendGameLog($"Artefato obtido: {displayName}.");
		ShowNotificationToast(Localization.TextFormat("hud.toast.artifact", displayName), true);
		RefreshCharacterSheet();
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
		ShowNotificationToast(Localization.TextFormat("hud.toast.artifact", Localization.Text("artifact.ashen_heart.name")), true);
		_lastToastText = null;
		ShowNotificationToast(Localization.TextFormat("hud.toast.blessing", UiComponents.RarityLabel("rare"), Localization.Text("blessing.bathory_crimson_bath.name")), true);
		_lastToastText = null;
		ShowNotificationToast(Localization.TextFormat("combat.fury.not_ready", 40, 100), false);
		_toastStack?.ShowKill(_resourceFrame, 5);
		NotifyCursedBloodRewarded(25, 140);
	}
}

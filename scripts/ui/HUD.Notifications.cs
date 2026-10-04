using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	public void AppendGameLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		_gameLogEntries.Enqueue($"[{Time.GetTimeStringFromSystem()}] {message}");
		while (_gameLogEntries.Count > 5) _gameLogEntries.Dequeue();
		if (_gameLog != null) _gameLog.Text = string.Join("\n", _gameLogEntries);
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

		// Um toast novo cancela o anterior; so o tween mais recente pode esconder o label.
		_toastTween?.Kill();
		var tween = CreateTween();
		_toastTween = tween;
		tween.TweenProperty(_enemyDefeatedToast, "offset_top", -30.0f, 0.35f);
		tween.Parallel().TweenProperty(_enemyDefeatedToast, "offset_bottom", 6.0f, 0.35f);
		tween.Parallel().TweenProperty(_enemyDefeatedToast, "modulate:a", 0.0f, 0.55f);
		await ToSignal(tween, Tween.SignalName.Finished);

		if (_toastTween == tween && IsInstanceValid(this))
			_enemyDefeatedToast.Visible = false;
	}
}

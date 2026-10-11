using Godot;
using System.Collections.Generic;

/// <summary>
/// Pré-visualização de telas por linha de comando (só em build de debug; guia de UI, Anexo A).
/// Sem argumentos de usuário, ou em release, nada acontece.
///
/// Comando (argumentos de usuário vêm depois de "--"):
///   F:/GODOT/App/Godot.exe --path . res://scenes/game/CrowsNest.tscn -- --ui-preview=blood_fountain
/// Captura:
///   F:/GODOT/App/Godot.exe --path . --resolution 1600x900 --fixed-fps 30 --write-movie C:/tmp/blood_fountain/f.png
///     --quit-after 60 res://scenes/game/CrowsNest.tscn -- --ui-preview=blood_fountain
///
/// Argumentos:
///   --ui-preview=TELA        tela a abrir (lista abaixo)
///   --ui-lang=pt-BR|en|es    idioma da sessão (não grava nas configurações)
///   --ui-device=kbm|xbox|ps  último dispositivo (glifos e cursor); "keyboard"/"playstation" também valem.
///                            Fica travado na sessão (DeviceLocked): o GameCursor ignora mouse/teclado/controle reais.
///   --ui-tab=ABA             aba do Dicionário (compendium): Artifacts, Blessings, Skills, Attacks, Enemies, Weapons;
///                            aba da ficha (character_sheet): Attributes, Blessings, Artifacts, Skills
///   --ui-codex-locked[=sample]  Dicionário na visão de release (entradas bloqueadas); "=sample" finge algumas descobertas
///
/// Telas (cena sugerida):
///   MainMenu:   menu_main, menu_slots, menu_confirm_delete, menu_options, menu_credits
///   TestArena:  hud_combat, critical_health, damage_flash, damage_number, blessing_bar, event_log,
///               character_sheet, artifact_offer, artifact_offer_4, blessing_offer, blessing_offer_replace,
///               pause, options, tuning, pause_confirm_abandon, pause_confirm_menu,
///               compendium, game_over, room_clear, toast, wave_warning, boss_bar
///   (pause e pause_confirm_* iniciam uma run volátil se não houver, para mostrar "Desistir da run")
///   CrowsNest:  blood_fountain, skill_popup, artifact_chest, weapon_bench
///   IntermissionChamber: altar (host = a própria Câmara)
///   (as telas do HUD abrem em qualquer cena com HUD: CrowsNest, RunStartBlessingRoom, TestArena, IntermissionChamber)
/// Lote (um único processo do Godot, sem --write-movie): percorre as telas x idiomas, recarregando a cena entre uma e outra,
/// e salva um PNG por combinação. Evita abrir/fechar o jogo a cada tela.
///   F:/GODOT/App/Godot.exe --path . --resolution 1600x900 -- --ui-batch --ui-shots=C:/tmp/shots [--ui-langs=pt-BR,en,es] [--ui-screens=pause,compendium]
///   --ui-batch               ativa o lote (cena inicial qualquer; o lote escolhe a cena de cada tela)
///   --ui-shots=PASTA         onde gravar os PNGs (tela[_aba]_idioma.png); padrão user://ui_shots
///   --ui-langs=a,b,c         idiomas (padrão pt-BR,en,es)
///   --ui-screens=a,b         só estas telas (nomes da lista acima; padrão todas)
/// Apelidos: skill_detail = skill_popup, pause_tuning = tuning, wave_alert = wave_warning.
/// Toast, aviso de wave, banner de sala limpa, flash de dano (0,5 s) e número de dano (1 s) se repetem
/// para aparecerem em qualquer quadro da captura. event_log só aparece em build de debug.
/// Dados fictícios: chefe com 60% de vida, causa de game over e oferta de artefato sem altar. Nada é salvo.
/// </summary>
public partial class UiPreview : Node
{
	private const int OpenDelayFrames = 3;

	private static bool _parsed;
	private static bool _settingsApplied;
	private static bool _opened;
	private static string _screen;
	private static string _language;
	private static string _device;
	private static string _tab;
	private static bool _batch;
	private static string _batchShots;
	private static string _batchLangs;
	private static string _batchScreens;
	private static UiPreview _runner;

	private const string SceneMenu = "res://scenes/ui/MainMenu.tscn";
	private const string SceneArena = "res://scenes/game/TestArena.tscn";
	private const string SceneNest = "res://scenes/game/CrowsNest.tscn";
	private const string SceneChamber = "res://scenes/game/IntermissionChamber.tscn";
	private const float BatchSettleSeconds = 0.8f;
	private const float BatchOpenTimeoutSeconds = 6.0f;

	private sealed record BatchJob(string Screen, string Tab, string Scene, string Language)
	{
		public string FileName => $"{Screen}{(Tab == null ? "" : "_" + Tab)}_{Language}.png";
	}

	// Tela, aba e cena de cada captura do lote (mesma lista do cabeçalho).
	private static readonly (string Screen, string Tab, string Scene)[] BatchScreens =
	{
		("menu_main", null, SceneMenu), ("menu_slots", null, SceneMenu), ("menu_confirm_delete", null, SceneMenu),
		("menu_options", null, SceneMenu), ("menu_credits", null, SceneMenu),
		("hud_combat", null, SceneArena), ("blessing_bar", null, SceneArena), ("pause", null, SceneArena),
		("options", null, SceneArena), ("tuning", null, SceneArena), ("pause_confirm_abandon", null, SceneArena),
		("pause_confirm_menu", null, SceneArena), ("game_over", null, SceneArena),
		("compendium", "Artifacts", SceneArena), ("compendium", "Blessings", SceneArena), ("compendium", "Skills", SceneArena),
		("compendium", "Attacks", SceneArena), ("compendium", "Enemies", SceneArena), ("compendium", "Weapons", SceneArena),
		("character_sheet", "Attributes", SceneArena), ("character_sheet", "Blessings", SceneArena),
		("character_sheet", "Artifacts", SceneArena), ("character_sheet", "Skills", SceneArena),
		("artifact_offer", null, SceneArena), ("artifact_offer_4", null, SceneArena), ("blessing_offer", null, SceneArena),
		("blessing_offer_replace", null, SceneArena), ("toast", null, SceneArena), ("wave_warning", null, SceneArena),
		("room_clear", null, SceneArena), ("boss_bar", null, SceneArena),
		("blood_fountain", null, SceneNest), ("skill_popup", null, SceneNest), ("artifact_chest", null, SceneNest),
		("weapon_bench", null, SceneNest),
		("altar", null, SceneChamber)
	};

	private List<BatchJob> _jobs;
	private int _jobIndex;
	private int _shotCount;
	private bool _jobStarted;
	private float _jobElapsed;
	private float _settleElapsed;

	/// <summary>True quando --ui-device foi aplicado (só debug): o GameCursor não troca o dispositivo durante o preview.</summary>
	public static bool DeviceLocked { get; private set; }

	private Node _host;
	private int _frames;
	private float _repeatInterval;
	private float _repeatTimer;

	// Chamado no _Ready do HUD, do MainMenu e da IntermissionChamber. Aplica idioma/dispositivo e agenda a tela (uma vez por processo).
	public static void Attach(Node host)
	{
		if (host == null || !OS.IsDebugBuild())
			return;
		ParseArguments();
		ApplySettingsOnce();
		if (_batch && _runner == null)
			StartBatch(host);
		if (_opened || string.IsNullOrEmpty(_screen) || !CanHandle(host, _screen))
			return;
		host.AddChild(new UiPreview { Name = "UiPreview", _host = host, ProcessMode = ProcessModeEnum.Always });
	}

	public override void _Process(double delta)
	{
		if (this == _runner)
		{
			RunBatch((float)delta);
			return;
		}
		if (!IsInstanceValid(_host))
		{
			QueueFree();
			return;
		}
		// Espera alguns quadros: BindRuntimeNodes do HUD é deferred e a cena precisa terminar o _Ready.
		if (_frames < OpenDelayFrames)
		{
			_frames++;
			if (_frames == OpenDelayFrames)
				Open();
			return;
		}
		_repeatTimer -= (float)delta;
		if (_repeatTimer > 0.0f)
			return;
		_repeatTimer = _repeatInterval;
		ShowScreen();
	}

	// ---------- Lote: um processo, várias telas x idiomas ----------

	private static void StartBatch(Node host)
	{
		var jobs = BuildBatchJobs();
		_runner = new UiPreview { Name = "UiPreviewBatch", ProcessMode = ProcessModeEnum.Always, _jobs = jobs };
		// Fica na raiz para sobreviver às trocas de cena; deferido porque a raiz está ocupada durante o _Ready da cena.
		host.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _runner);
		GD.Print($"UiPreviewBatch: {jobs.Count} captura(s) planejada(s).");
	}

	private static List<BatchJob> BuildBatchJobs()
	{
		var languages = (string.IsNullOrEmpty(_batchLangs) ? "pt-BR,en,es" : _batchLangs).Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
		HashSet<string> only = null;
		if (!string.IsNullOrEmpty(_batchScreens))
		{
			only = new HashSet<string>();
			foreach (var name in _batchScreens.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
				only.Add(NormalizeScreen(name.ToLowerInvariant()));
		}

		var jobs = new List<BatchJob>();
		foreach (var language in languages)
			foreach (var (screen, tab, scene) in BatchScreens)
				if (only == null || only.Contains(screen))
					jobs.Add(new BatchJob(screen, tab, scene, Localization.NormalizeLanguage(language)));
		return jobs;
	}

	private void RunBatch(float delta)
	{
		if (_jobs == null)
			return;
		if (!_jobStarted)
		{
			if (_jobIndex >= _jobs.Count)
			{
				GD.Print($"UiPreviewBatch: concluído, {_shotCount} captura(s).");
				SetProcess(false);
				GetTree().Quit();
				return;
			}
			StartBatchJob(_jobs[_jobIndex]);
			return;
		}

		var job = _jobs[_jobIndex];
		_jobElapsed += delta;
		if (!_opened)
		{
			if (_jobElapsed > BatchOpenTimeoutSeconds)
			{
				GD.PushWarning($"UiPreviewBatch: '{job.FileName}' não abriu em {BatchOpenTimeoutSeconds:0}s; ignorada.");
				NextBatchJob();
			}
			return;
		}

		_settleElapsed += delta;
		if (_settleElapsed < BatchSettleSeconds)
			return;
		CaptureBatchShot(job);
		NextBatchJob();
	}

	private void StartBatchJob(BatchJob job)
	{
		_jobStarted = true;
		_jobElapsed = 0.0f;
		_settleElapsed = 0.0f;
		_screen = job.Screen;
		_tab = job.Tab;
		_opened = false;
		// Idioma antes de trocar a cena: a cena nova já monta os textos no idioma certo.
		Localization.Instance?.SetLanguage(job.Language);
		// Telas de pausa deixam a árvore pausada; a cena nova precisa começar rodando.
		GetTree().Paused = false;
		var error = GetTree().ChangeSceneToFile(job.Scene);
		if (error != Error.Ok)
		{
			GD.PushWarning($"UiPreviewBatch: não foi possível abrir {job.Scene} ({error}).");
			NextBatchJob();
		}
	}

	private void NextBatchJob()
	{
		_jobIndex++;
		_jobStarted = false;
	}

	private void CaptureBatchShot(BatchJob job)
	{
		var directory = string.IsNullOrEmpty(_batchShots) ? "user://ui_shots" : _batchShots;
		if (directory.StartsWith("user://") || directory.StartsWith("res://"))
			directory = ProjectSettings.GlobalizePath(directory);
		DirAccess.MakeDirRecursiveAbsolute(directory);
		var path = $"{directory}/{job.FileName}";
		var error = GetViewport().GetTexture().GetImage().SavePng(path);
		if (error != Error.Ok)
		{
			GD.PushWarning($"UiPreviewBatch: falha ao gravar {path} ({error}).");
			return;
		}
		_shotCount++;
		GD.Print($"UiPreviewBatch: {job.FileName} ({_shotCount}/{_jobs.Count})");
	}

	private void Open()
	{
		_opened = true;
		if (!ShowScreen())
		{
			GD.PushWarning($"UiPreview: tela '{_screen}' desconhecida. Veja a lista no topo de scripts/ui/UiPreview.cs.");
			QueueFree();
			return;
		}
		GD.Print($"UiPreview: '{_screen}' aberta.");
		_repeatInterval = _screen switch
		{
			"toast" => 1.0f,
			"wave_warning" => 1.0f,
			"room_clear" => 2.2f,
			"damage_flash" => 0.5f,
			"damage_number" => 1.0f,
			_ => 0.0f
		};
		_repeatTimer = _repeatInterval;
		// Sem repetição o nó fica parado (é o pai do chefe fictício no boss_bar).
		if (_repeatInterval <= 0.0f)
			SetProcess(false);
	}

	private bool ShowScreen()
	{
		return _host switch
		{
			HUD hud => hud.OpenPreviewScreen(_screen, _tab, this),
			MainMenu menu => menu.ShowPreviewPage(_screen),
			IntermissionChamber chamber => chamber.OpenPreviewScreen(_screen),
			_ => false
		};
	}

	private static bool CanHandle(Node host, string screen)
	{
		// altar: tela da própria Câmara (IntermissionChamber chama Attach no _Ready), não do HUD.
		if (screen == "altar")
			return host is IntermissionChamber;
		var isMenuScreen = screen.StartsWith("menu_");
		return host is MainMenu ? isMenuScreen : host is HUD && !isMenuScreen;
	}

	private static void ParseArguments()
	{
		if (_parsed)
			return;
		_parsed = true;
		foreach (var argument in OS.GetCmdlineUserArgs())
		{
			var value = ValueOf(argument, "--ui-preview=");
			if (value != null) _screen = NormalizeScreen(value.ToLowerInvariant());
			value = ValueOf(argument, "--ui-lang=");
			if (value != null) _language = value;
			value = ValueOf(argument, "--ui-device=");
			if (value != null) _device = value.ToLowerInvariant();
			value = ValueOf(argument, "--ui-tab=");
			if (value != null) _tab = value;
			if (argument == "--ui-batch" || argument.StartsWith("--ui-batch="))
				_batch = true;
			value = ValueOf(argument, "--ui-shots=");
			if (value != null) _batchShots = value;
			value = ValueOf(argument, "--ui-langs=");
			if (value != null) _batchLangs = value;
			value = ValueOf(argument, "--ui-screens=");
			if (value != null) _batchScreens = value;
		}
	}

	private static string ValueOf(string argument, string prefix)
	{
		if (argument == null || !argument.StartsWith(prefix))
			return null;
		var value = argument.Substring(prefix.Length).Trim();
		return value.Length > 0 ? value : null;
	}

	private static string NormalizeScreen(string screen) => screen switch
	{
		"skill_detail" => "skill_popup",
		"pause_tuning" => "tuning",
		"wave_alert" => "wave_warning",
		_ => screen
	};

	private static void ApplySettingsOnce()
	{
		if (_settingsApplied)
			return;
		_settingsApplied = true;

		// Só a sessão: Localization muda, GlobalSettings (e o arquivo) não.
		if (_language != null)
			Localization.Instance?.SetLanguage(_language);

		if (_device == null)
			return;
		InputDevice? device = _device switch
		{
			"kbm" or "keyboard" or "mouse" => InputDevice.KeyboardMouse,
			"xbox" => InputDevice.Xbox,
			"ps" or "playstation" => InputDevice.PlayStation,
			_ => null
		};
		if (device == null)
		{
			GD.PushWarning($"UiPreview: dispositivo '{_device}' desconhecido (use kbm, xbox ou ps).");
			return;
		}
		UiInput.Report(device.Value);
		DeviceLocked = true;
		if (device.Value != InputDevice.KeyboardMouse)
			Input.MouseMode = Input.MouseModeEnum.Hidden;
	}
}

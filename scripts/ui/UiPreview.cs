using Godot;

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
		if (_opened || string.IsNullOrEmpty(_screen) || !CanHandle(host, _screen))
			return;
		host.AddChild(new UiPreview { Name = "UiPreview", _host = host, ProcessMode = ProcessModeEnum.Always });
	}

	public override void _Process(double delta)
	{
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

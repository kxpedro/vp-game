using Godot;
using System.Linq;

// Console de desenvolvedor (só OS.IsDebugBuild()): tecla abaixo do Esc (' no ABNT2, ` no US) ou F2, ação "dev_console".
// Faixa no topo da tela com a saída e uma linha de comando; pausa o jogo pela UiModalStack enquanto aberto.
// Enter executa, Tab completa, ↑/↓ percorrem o histórico, PgUp/PgDn rolam a saída, Esc (ou a mesma tecla) fecha.
// Parser e comandos ficam em scripts/systems/dev/ (DevConsole, DevCommands.*). Textos literais em pt-BR (regra de debug).
public partial class HUD
{
	private static readonly Color DevConsoleSuccessColor = new(0.56f, 0.86f, 0.56f, 1.0f);

	private Control _devConsoleOverlay;
	private RichTextLabel _devConsoleOutput;
	private LineEdit _devConsoleInput;
	private int _devConsoleHistoryIndex = -1;
	private string _devConsoleDraft = "";

	private bool DevConsoleAvailable => OS.IsDebugBuild() && _devConsoleOverlay != null;

	private void BuildDevConsole(Control root)
	{
		if (!OS.IsDebugBuild() || root == null)
			return;

		_devConsoleOverlay = new Control { Name = "DevConsoleOverlay", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		_devConsoleOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_devConsoleOverlay);

		var panel = new PanelContainer { Name = "DevConsolePanel", AnchorRight = 1.0f, AnchorBottom = 0.48f };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(UiPalette.ScreenBg, 0.94f),
			BorderColor = UiPalette.WindowBorder,
			BorderWidthBottom = UiMetrics.BorderWindow,
			ContentMarginLeft = UiMetrics.PadWindowX, ContentMarginRight = UiMetrics.PadWindowX,
			ContentMarginTop = UiMetrics.PadPanelY, ContentMarginBottom = UiMetrics.PadPanelY
		};
		panel.AddThemeStyleboxOverride("panel", style);
		_devConsoleOverlay.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", UiMetrics.S2);
		panel.AddChild(column);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", UiMetrics.S3);
		header.AddChild(UiComponents.MakeLabel("Console de desenvolvedor", UiVariation.SectionGoldLabel));
		header.AddChild(UiComponents.Badge("DEV", UiPalette.PanelInset, UiPalette.TextSecondary));
		var hint = UiComponents.MakeLabel("help = comandos · Tab completa · ↑/↓ histórico · Esc fecha", UiVariation.SecondaryLabel);
		hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hint.HorizontalAlignment = HorizontalAlignment.Right;
		header.AddChild(hint);
		column.AddChild(header);

		var mono = new SystemFont { FontNames = new[] { "Consolas", "Cascadia Mono", "DejaVu Sans Mono", "Liberation Mono", "monospace" } };
		_devConsoleOutput = new RichTextLabel
		{
			Name = "DevConsoleOutput",
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SelectionEnabled = true,
			ContextMenuEnabled = true,
			FocusMode = Control.FocusModeEnum.None,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_devConsoleOutput.AddThemeFontOverride("normal_font", mono);
		_devConsoleOutput.AddThemeFontSizeOverride("normal_font_size", 15);
		_devConsoleOutput.AddThemeColorOverride("default_color", UiPalette.TextPrimary);
		_devConsoleOutput.AddThemeStyleboxOverride("normal", new StyleBoxFlat
		{
			BgColor = UiPalette.PanelInset,
			ContentMarginLeft = UiMetrics.S3, ContentMarginRight = UiMetrics.S3,
			ContentMarginTop = UiMetrics.S2, ContentMarginBottom = UiMetrics.S2
		});
		column.AddChild(_devConsoleOutput);

		_devConsoleInput = new LineEdit
		{
			Name = "DevConsoleInput",
			PlaceholderText = "/comando argumentos   (ex.: spawn boon anvil · add 50 health · list boons orlok)",
			KeepEditingOnTextSubmit = true,
			ClearButtonEnabled = true,
			CaretBlink = true,
			CustomMinimumSize = new Vector2(0.0f, UiMetrics.ButtonS)
		};
		_devConsoleInput.AddThemeFontOverride("font", mono);
		_devConsoleInput.AddThemeFontSizeOverride("font_size", 16);
		_devConsoleInput.TextSubmitted += OnDevConsoleSubmitted;
		_devConsoleInput.GuiInput += OnDevConsoleInputGui;
		column.AddChild(_devConsoleInput);

		DevConsole.EnsureRegistered();
		if (DevConsole.Log.Count == 0)
			DevConsole.Write(DevConsoleLevel.Info, "Console de desenvolvedor. Digite help para ver os comandos (a \"/\" é opcional).");
		RedrawDevConsole();

		// O registro é estático e sobrevive à troca de cena; este HUD só escuta enquanto estiver na árvore.
		DevConsole.LineAdded += OnDevConsoleLine;
		DevConsole.Cleared += RedrawDevConsole;
		TreeExiting += () =>
		{
			DevConsole.LineAdded -= OnDevConsoleLine;
			DevConsole.Cleared -= RedrawDevConsole;
		};
		CallDeferred(nameof(StartDevRun));
	}

	// ---------- Linha de comando (só debug) ----------
	// --dev-run="give boon x; jump boss": roda os comandos na primeira cena que abrir (uma vez por processo).
	// Enquanto vale, a saída do console, o log de eventos e as falas vão para o stdout ("[dev] ...").
	// --dev-auto-dialogue: as conversas avançam sozinhas (testes em headless, onde ninguém aperta o botão).

	private static bool _devRunConsumed;
	private static bool _devEcho;
	private static bool _devAutoDialogue;
	private float _devAutoDialogueTimer;

	private static void DevEcho(string text)
	{
		if (_devEcho) GD.Print("[dev] " + text);
	}

	private async void StartDevRun()
	{
		if (_devRunConsumed || !OS.IsDebugBuild())
			return;
		_devRunConsumed = true;
		string commands = null;
		foreach (var argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--dev-run="))
				commands = argument.Substring("--dev-run=".Length).Trim('"');
			else if (argument == "--dev-auto-dialogue")
				_devAutoDialogue = true;
		}
		if (string.IsNullOrWhiteSpace(commands))
			return;
		_devEcho = true;
		DevConsole.LineAdded += (_, text) => DevEcho(text);
		// Espera a cena terminar de montar (sala, jogador, HUD).
		await ToSignal(GetTree().CreateTimer(0.5, true, false, true), SceneTreeTimer.SignalName.Timeout);
		DevEcho($"--dev-run: {commands}");
		DevConsole.Execute(commands, GetTree(), this);
	}

	// No _Input (antes do LineEdit): a tecla do console não pode virar caractere digitado. Com Shift/Ctrl/Alt
	// não abre, para a mesma tecla física ainda digitar aspas (Shift+' no ABNT2).
	private bool HandleDevConsoleToggle(InputEvent input)
	{
		if (!DevConsoleAvailable || !InputMap.HasAction("dev_console"))
			return false;
		if (input is not InputEventKey key || !key.Pressed || key.Echo || key.ShiftPressed || key.CtrlPressed || key.AltPressed || key.MetaPressed)
			return false;
		if (!input.IsActionPressed("dev_console"))
			return false;

		if (UiModalStack.IsOpen(_devConsoleOverlay))
			CloseDevConsole();
		else if (!_isGameOver)
			OpenDevConsole();
		return true;
	}

	private void OpenDevConsole()
	{
		if (!DevConsoleAvailable)
			return;
		_devConsoleHistoryIndex = -1;
		UiModalStack.Push(_devConsoleOverlay, true, _devConsoleInput, CloseDevConsole);
		Callable.From(() =>
		{
			if (IsInstanceValid(_devConsoleInput) && _devConsoleInput.IsVisibleInTree())
			{
				_devConsoleInput.GrabFocus();
				_devConsoleInput.Edit();
				_devConsoleInput.CaretColumn = _devConsoleInput.Text.Length;
			}
		}).CallDeferred();
	}

	private void CloseDevConsole()
	{
		if (_devConsoleOverlay == null)
			return;
		_devConsoleInput?.Unedit();
		UiModalStack.Pop(_devConsoleOverlay);
	}

	private void OnDevConsoleSubmitted(string text)
	{
		_devConsoleInput.Clear();
		_devConsoleHistoryIndex = -1;
		_devConsoleDraft = "";
		if (string.IsNullOrWhiteSpace(text))
			return;
		if (DevConsole.Execute(text, GetTree(), this))
			CloseDevConsole();
	}

	private void OnDevConsoleInputGui(InputEvent input)
	{
		if (input is not InputEventKey { Pressed: true } key)
			return;
		switch (key.Keycode)
		{
			case Key.Tab:
				CompleteDevConsoleLine();
				break;
			case Key.Up:
				StepDevConsoleHistory(-1);
				break;
			case Key.Down:
				StepDevConsoleHistory(1);
				break;
			case Key.Pageup:
				ScrollDevConsole(-1);
				break;
			case Key.Pagedown:
				ScrollDevConsole(1);
				break;
			default:
				return;
		}
		_devConsoleInput.AcceptEvent();
	}

	private void CompleteDevConsoleLine()
	{
		var matches = DevConsole.Complete(_devConsoleInput.Text, GetTree(), this, out var completed);
		if (matches.Count == 0)
			return;
		_devConsoleInput.Text = completed;
		_devConsoleInput.CaretColumn = completed.Length;
		if (matches.Count > 1)
		{
			const int maxShown = 40;
			var shown = string.Join("  ", matches.Take(maxShown));
			DevConsole.Write(DevConsoleLevel.Info, matches.Count > maxShown ? $"{shown}  ... (+{matches.Count - maxShown})" : shown);
		}
	}

	// -1 = mais antigo; 1 = mais novo. Sair do fim do histórico devolve o que estava sendo digitado.
	private void StepDevConsoleHistory(int direction)
	{
		var history = DevConsole.History;
		if (history.Count == 0)
			return;
		if (_devConsoleHistoryIndex == -1)
		{
			if (direction > 0)
				return;
			_devConsoleDraft = _devConsoleInput.Text;
			_devConsoleHistoryIndex = history.Count - 1;
		}
		else
		{
			_devConsoleHistoryIndex += direction;
			if (_devConsoleHistoryIndex >= history.Count)
			{
				_devConsoleHistoryIndex = -1;
				_devConsoleInput.Text = _devConsoleDraft;
				_devConsoleInput.CaretColumn = _devConsoleDraft.Length;
				return;
			}
			_devConsoleHistoryIndex = Mathf.Max(_devConsoleHistoryIndex, 0);
		}
		var line = history[_devConsoleHistoryIndex];
		_devConsoleInput.Text = line;
		_devConsoleInput.CaretColumn = line.Length;
	}

	private void ScrollDevConsole(int direction)
	{
		var bar = _devConsoleOutput?.GetVScrollBar();
		if (bar == null)
			return;
		bar.Value = Mathf.Clamp(bar.Value + direction * bar.Page * 0.8, bar.MinValue, bar.MaxValue);
	}

	private void OnDevConsoleLine(DevConsoleLevel level, string text)
	{
		if (IsInstanceValid(_devConsoleOutput))
			_devConsoleOutput.AppendText(FormatDevConsoleLine(level, text) + "\n");
	}

	private void RedrawDevConsole()
	{
		if (!IsInstanceValid(_devConsoleOutput))
			return;
		_devConsoleOutput.Clear();
		foreach (var (level, text) in DevConsole.Log)
			_devConsoleOutput.AppendText(FormatDevConsoleLine(level, text) + "\n");
	}

	private static string FormatDevConsoleLine(DevConsoleLevel level, string text)
	{
		var color = level switch
		{
			DevConsoleLevel.Input => UiPalette.Gold,
			DevConsoleLevel.Success => DevConsoleSuccessColor,
			DevConsoleLevel.Warning => UiPalette.Warn,
			DevConsoleLevel.Error => UiPalette.Danger,
			_ => UiPalette.TextPrimary
		};
		return $"[color=#{color.ToHtml(false)}]{DevText.EscapeBbcode(text)}[/color]";
	}

	// ---------- Usados pelos comandos (DevCommands.*) ----------

	/// <summary>Reaplica o tuning do jogador e atualiza ficha, barra de bênçãos e recursos depois de um comando mexer na run.</summary>
	internal void DevConsoleRefreshRun()
	{
		DevAfterRewardGiven();
		SyncBloodCoins(RunState.RunBloodCoins);
		RefreshResourceIndicator();
	}

	internal void DevConsoleJump(int mapIndex, int room)
	{
		CloseDevConsole();
		DevJump(mapIndex, room);
	}

	internal void DevConsoleTimeChanged() => RefreshDevTimeWarning();

	internal void DevConsoleApplyTuning() => FlushDevApplyNow();

	// ---------- Pré-visualização (UiPreview: --ui-preview=dev_console) ----------

	private bool OpenDevConsolePreview()
	{
		if (!DevConsoleAvailable)
			return false;
		DevConsole.Clear();
		DevConsole.Execute("help", GetTree(), this);
		DevConsole.Execute("list boons orlok", GetTree(), this);
		OpenDevConsole();
		_devConsoleInput.Text = "give boon orlok_";
		return true;
	}
}

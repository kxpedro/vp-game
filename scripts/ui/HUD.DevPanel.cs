using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Painel de desenvolvedor (só OS.IsDebugBuild()): F1 ou "Painel de dev" no menu de pausa.
// Lista os parâmetros de DevParameterRegistry por grupo, com busca, filtro "só alterados" e reset por item/grupo/tudo.
// Textos literais em pt-BR (regra do projeto para debug). As linhas são montadas só para o grupo selecionado/filtrado.
public partial class HUD
{
	private const string DevActionsGroup = "\u0000acoes"; // aba "Ações" (T7 preenche BuildDevActionsSection)
	private const int DevPanelMaxRows = 100;
	private const double DevPanelApplyDelay = 0.2;

	private UiWindow _devPanelWindow;
	private Control _devPanelOverlay;
	private LineEdit _devSearch;
	private CheckBox _devOnlyModified;
	private VBoxContainer _devGroupList;
	private VBoxContainer _devRows;
	private ScrollContainer _devRowsScroll;
	private Label _devSummary;
	private readonly Dictionary<string, Button> _devGroupButtons = new();
	private string _devSelectedGroup;
	private int _devApplyGeneration;
	private bool _devTimeResetHooked;

	// Contrato com o F1 e a pausa: nada disso existe em release.
	private bool DevPanelAvailable => OS.IsDebugBuild() && _devPanelOverlay != null;

	private void BuildDevPanel(Control root)
	{
		if (!OS.IsDebugBuild() || root == null)
			return;

		if (!_devTimeResetHooked)
		{
			// A escala de tempo do painel não pode vazar para o menu/outra cena (o HUD sai da árvore na troca).
			_devTimeResetHooked = true;
			TreeExiting += () => CombatFeedback.DevSetBaseTimeScale(1.0f);
		}

		BuildDevTimeWarning(root);
		_devPanelWindow = UiComponents.Window(root, "DevPanelOverlay", WindowSize.Full, "Painel de desenvolvedor");
		_devPanelOverlay = _devPanelWindow.Root;
		_devPanelOverlay.Visible = false;
		_devPanelWindow.HeaderRight.AddChild(UiComponents.Badge("DEV", UiPalette.PanelInset, UiPalette.TextSecondary));

		var body = _devPanelWindow.Body;

		// Barra de ferramentas: busca, só alterados, resets.
		var toolbar = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		toolbar.AddThemeConstantOverride("separation", UiMetrics.S2);
		body.AddChild(toolbar);

		_devSearch = new LineEdit
		{
			Name = "DevSearch",
			PlaceholderText = "Buscar por nome ou chave (ex.: moedas)",
			ClearButtonEnabled = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(320.0f, 36.0f)
		};
		_devSearch.TextChanged += _ => RefreshDevRows();
		toolbar.AddChild(_devSearch);

		_devOnlyModified = new CheckBox { Name = "DevOnlyModified", Text = "Só alterados" };
		_devOnlyModified.Toggled += _ => RefreshDevRows();
		toolbar.AddChild(_devOnlyModified);

		var resetGroup = UiComponents.MenuButton("Resetar grupo", ResetDevGroup, ButtonVariant.Secondary, ButtonSize.Small);
		resetGroup.Name = "DevResetGroup";
		toolbar.AddChild(resetGroup);
		var resetAll = UiComponents.MenuButton("Resetar tudo", ConfirmResetDevAll, ButtonVariant.Destructive, ButtonSize.Small);
		resetAll.Name = "DevResetAll";
		toolbar.AddChild(resetAll);

		_devSummary = UiComponents.MakeLabel("", UiVariation.SecondaryLabel);
		body.AddChild(_devSummary);

		var split = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		split.AddThemeConstantOverride("separation", UiMetrics.S4);
		body.AddChild(split);

		var groupScroll = UiComponents.ScrollList(300.0f, out _devGroupList);
		groupScroll.Name = "DevGroupScroll";
		groupScroll.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		groupScroll.CustomMinimumSize = new Vector2(320.0f, 300.0f);
		_devGroupList.AddThemeConstantOverride("separation", UiMetrics.S1);
		split.AddChild(groupScroll);

		_devRowsScroll = UiComponents.ScrollList(300.0f, out _devRows);
		_devRowsScroll.Name = "DevRowsScroll";
		_devRows.AddThemeConstantOverride("separation", UiMetrics.S1);
		split.AddChild(_devRowsScroll);

		_devPanelWindow.Footer.Add(UiAction.Cancel, "Fechar (F1)", CloseDevPanel, left: true);
	}

	private void OpenDevPanel()
	{
		if (!DevPanelAvailable || _isGameOver)
			return;

		DevParameterRegistry.EnsureRegistered();
		RebuildDevGroupList();
		if (string.IsNullOrEmpty(_devSelectedGroup) || (_devSelectedGroup != DevActionsGroup && !DevParameterRegistry.Groups.Contains(_devSelectedGroup)))
			_devSelectedGroup = DevParameterRegistry.Groups.FirstOrDefault() ?? DevActionsGroup;
		RefreshDevRows();

		Control focus = _devSearch;
		UiModalStack.Push(_devPanelOverlay, true, focus, CloseDevPanel);
		_devPanelWindow.FadeIn();
	}

	private void CloseDevPanel()
	{
		FlushDevApply();
		if (_devPanelOverlay != null)
			UiModalStack.Pop(_devPanelOverlay);
	}

	// F1: abre sem nada empilhado (a pausa legada pode estar por baixo); fecha com o painel no topo.
	private bool HandleDevPanelToggle(InputEvent input)
	{
		if (!OS.IsDebugBuild() || _devPanelOverlay == null || !InputMap.HasAction("dev_panel"))
			return false;
		if (!input.IsActionPressed("dev_panel") || input.IsEcho())
			return false;

		if (UiModalStack.Top == _devPanelOverlay)
			CloseDevPanel();
		else if (!UiModalStack.AnyOpen && !_isGameOver)
			OpenDevPanel();
		return true;
	}

	// ---------- Grupos ----------

	private void RebuildDevGroupList()
	{
		foreach (var child in _devGroupList.GetChildren())
		{
			_devGroupList.RemoveChild(child);
			child.QueueFree();
		}
		_devGroupButtons.Clear();

		foreach (var group in DevParameterRegistry.Groups)
			AddDevGroupButton(group, group);
		AddDevGroupButton(DevActionsGroup, "Ações");
		UpdateDevGroupButtons();
	}

	private void AddDevGroupButton(string group, string title)
	{
		var button = UiComponents.MenuButton(title, () => SelectDevGroup(group), ButtonVariant.Secondary, ButtonSize.Small);
		button.Alignment = HorizontalAlignment.Left;
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.ToggleMode = true;
		button.ClipText = true;
		button.CustomMinimumSize = new Vector2(0.0f, UiMetrics.ButtonS);
		_devGroupList.AddChild(button);
		_devGroupButtons[group] = button;
	}

	private void UpdateDevGroupButtons()
	{
		foreach (var (group, button) in _devGroupButtons)
		{
			if (group == DevActionsGroup)
			{
				button.Text = "Ações";
			}
			else
			{
				var all = DevParameterRegistry.InGroup(group).ToList();
				var modified = all.Count(p => DevParameterRegistry.IsModified(p.Key));
				button.Text = modified > 0 ? $"{group} ({all.Count}) *{modified}" : $"{group} ({all.Count})";
			}
			button.SetPressedNoSignal(group == _devSelectedGroup);
		}
	}

	private void SelectDevGroup(string group)
	{
		_devSelectedGroup = group;
		// Escolher um grupo sai da busca global.
		if (_devSearch != null && _devSearch.Text.Length > 0)
			_devSearch.Text = "";
		RefreshDevRows();
	}

	// ---------- Linhas ----------

	private static string DevNormalize(string text)
	{
		if (string.IsNullOrEmpty(text))
			return "";
		var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
		var builder = new StringBuilder(decomposed.Length);
		foreach (var c in decomposed)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
				builder.Append(c);
		}
		return builder.ToString();
	}

	private void RefreshDevRows()
	{
		if (_devRows == null)
			return;

		foreach (var child in _devRows.GetChildren())
		{
			_devRows.RemoveChild(child);
			child.QueueFree();
		}

		var query = DevNormalize(_devSearch?.Text?.Trim());
		var onlyModified = _devOnlyModified != null && _devOnlyModified.ButtonPressed;
		var searching = query.Length > 0 || onlyModified;

		if (_devSelectedGroup == DevActionsGroup && !searching)
		{
			_devSummary.Text = "Ações de teste";
			BuildDevActionsSection(_devRows);
			UpdateDevGroupButtons();
			return;
		}

		IEnumerable<DevParameter> source = searching && query.Length > 0
			? DevParameterRegistry.All
			: _devSelectedGroup == DevActionsGroup ? DevParameterRegistry.All : DevParameterRegistry.InGroup(_devSelectedGroup);
		// Só alterados sem busca: restringe ao grupo atual, a menos que o campo de busca esteja preenchido.
		if (onlyModified && query.Length == 0 && _devSelectedGroup != DevActionsGroup)
			source = DevParameterRegistry.InGroup(_devSelectedGroup);

		var matches = new List<DevParameter>();
		foreach (var p in source)
		{
			if (query.Length > 0 && !DevNormalize(p.Label).Contains(query) && !DevNormalize(p.Key).Contains(query) && !DevNormalize(p.Group).Contains(query))
				continue;
			if (onlyModified && !DevParameterRegistry.IsModified(p.Key))
				continue;
			matches.Add(p);
		}

		var shown = Math.Min(matches.Count, DevPanelMaxRows);
		var scope = query.Length > 0 ? "em todos os grupos" : $"em {_devSelectedGroup}";
		_devSummary.Text = matches.Count > shown
			? $"{matches.Count} parâmetros {scope}; mostrando {shown}. Refine a busca."
			: $"{matches.Count} parâmetros {scope}.";

		string lastGroup = null;
		for (var i = 0; i < shown; i++)
		{
			var p = matches[i];
			if (query.Length > 0 && p.Group != lastGroup)
			{
				lastGroup = p.Group;
				var header = UiComponents.MakeLabel(p.Group, UiVariation.SectionGoldLabel);
				_devRows.AddChild(header);
			}
			AddDevParameterRow(_devRows, p);
		}

		if (matches.Count == 0)
			_devRows.AddChild(UiComponents.MakeLabel("Nenhum parâmetro encontrado.", UiVariation.SecondaryLabel));

		_devRowsScroll.ScrollVertical = 0;
		UpdateDevGroupButtons();
	}

	private static string DevFormat(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	private static string DevApplyNote(DevApplyMode mode) => mode switch
	{
		DevApplyMode.NewObjects => "⚠ vale só para objetos novos",
		DevApplyMode.NextRoom => "⚠ vale a partir da próxima sala",
		DevApplyMode.Reapply => "⚠ vale após reaplicar (automático ao editar)",
		_ => null
	};

	private void AddDevParameterRow(VBoxContainer parent, DevParameter p)
	{
		var row = new HBoxContainer
		{
			Name = $"DevRow_{p.Key.Replace('.', '_')}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 44.0f)
		};
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		parent.AddChild(row);

		var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(300.0f, 0.0f) };
		info.AddThemeConstantOverride("separation", 0);
		row.AddChild(info);

		var name = UiComponents.MakeLabel(p.Label, UiVariation.BodyLabel);
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.TooltipText = p.Key;
		info.AddChild(name);

		var detail = p.Key;
		var applyNote = DevApplyNote(p.Apply);
		var keyLabel = UiComponents.MakeLabel(detail, UiVariation.SecondaryLabel);
		keyLabel.AddThemeFontSizeOverride("font_size", 12);
		keyLabel.AddThemeColorOverride("font_color", UiPalette.TextDisabled);
		keyLabel.ClipText = true;
		keyLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		info.AddChild(keyLabel);

		if (applyNote != null || !string.IsNullOrEmpty(p.Note))
		{
			var text = applyNote != null && !string.IsNullOrEmpty(p.Note) ? $"{applyNote} · {p.Note}" : applyNote ?? p.Note;
			var note = UiComponents.MakeLabel(text, UiVariation.SecondaryLabel, true);
			note.AddThemeFontSizeOverride("font_size", 12);
			note.AddThemeColorOverride("font_color", applyNote != null ? UiPalette.Warn : UiPalette.TextSecondary);
			info.AddChild(note);
		}

		var step = p.Step > 0.0 ? p.Step : (p.IsInt ? 1.0 : 0.01);
		var current = DevParameterRegistry.Get(p.Key);

		var spin = new SpinBox
		{
			MinValue = p.Min,
			MaxValue = p.Max,
			Step = p.IsInt ? Math.Max(1.0, Math.Round(step)) : step,
			Value = current,
			CustomMinimumSize = new Vector2(130.0f, 32.0f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			TooltipText = p.Key
		};

		var range = p.Max - p.Min;
		HSlider slider = null;
		if (range > 0.0 && range / step <= 2000.0 && range / step >= 2.0)
		{
			slider = new HSlider
			{
				MinValue = p.Min,
				MaxValue = p.Max,
				Step = spin.Step,
				Value = current,
				CustomMinimumSize = new Vector2(220.0f, 24.0f),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
			};
			row.AddChild(slider);
		}
		else
		{
			// Mantém as colunas alinhadas.
			row.AddChild(new Control { CustomMinimumSize = new Vector2(220.0f, 0.0f), MouseFilter = Control.MouseFilterEnum.Ignore });
		}
		row.AddChild(spin);

		var defaultLabel = UiComponents.MakeLabel("", UiVariation.SecondaryLabel);
		defaultLabel.CustomMinimumSize = new Vector2(110.0f, 0.0f);
		defaultLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(defaultLabel);

		var resetButton = UiComponents.MenuButton("↺", null, ButtonVariant.Flat, ButtonSize.Small);
		resetButton.CustomMinimumSize = new Vector2(40.0f, 32.0f);
		resetButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		resetButton.TooltipText = "Voltar ao padrão";
		row.AddChild(resetButton);

		void RefreshMarker()
		{
			var modified = DevParameterRegistry.IsModified(p.Key);
			name.Text = modified ? "● " + p.Label : p.Label;
			name.AddThemeColorOverride("font_color", modified ? UiPalette.Gold : UiPalette.TextPrimary);
			defaultLabel.Text = modified ? "padrão: " + DevFormat(p.Default) : "";
			defaultLabel.AddThemeColorOverride("font_color", UiPalette.Warn);
			resetButton.Disabled = !modified;
		}

		void Commit(double value)
		{
			DevParameterRegistry.Set(p.Key, value);
			RefreshMarker();
			UpdateDevGroupButtons();
			ScheduleDevApply();
		}

		spin.ValueChanged += value =>
		{
			slider?.SetValueNoSignal(value);
			Commit(value);
		};
		if (slider != null)
		{
			slider.ValueChanged += value =>
			{
				spin.SetValueNoSignal(value);
				Commit(value);
			};
		}
		resetButton.Pressed += () =>
		{
			DevParameterRegistry.Reset(p.Key);
			var value = DevParameterRegistry.Get(p.Key);
			spin.SetValueNoSignal(value);
			slider?.SetValueNoSignal(value);
			RefreshMarker();
			UpdateDevGroupButtons();
			ScheduleDevApply();
		};
		RefreshMarker();
	}

	// ---------- Resets ----------

	private void ResetDevGroup()
	{
		if (string.IsNullOrEmpty(_devSelectedGroup) || _devSelectedGroup == DevActionsGroup)
			return;
		DevParameterRegistry.ResetGroup(_devSelectedGroup);
		FlushDevApplyNow();
		RefreshDevRows();
	}

	private void ConfirmResetDevAll()
	{
		var parent = _devPanelOverlay?.GetParent() as Control;
		if (parent == null)
			return;
		var confirm = UiComponents.Confirm(parent, "Resetar tudo?",
			"Todos os parâmetros do painel de dev voltam ao padrão e o arquivo dev_tuning.json é apagado.", "Resetar",
			() => { DevParameterRegistry.ResetAll(); FlushDevApplyNow(); RefreshDevRows(); },
			ConfirmKind.Destructive);
		UiModalStack.Push(confirm.Window.Root, true, confirm.CancelButton, confirm.Cancel);
	}

	// ---------- Aplicação (debounce) ----------

	private void ScheduleDevApply()
	{
		var generation = ++_devApplyGeneration;
		var timer = GetTree().CreateTimer(DevPanelApplyDelay, true, false, true);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(this) || generation != _devApplyGeneration)
				return;
			FlushDevApplyNow();
		};
	}

	private void FlushDevApply()
	{
		if (_devApplyGeneration != 0)
			FlushDevApplyNow();
	}

	private void FlushDevApplyNow()
	{
		_devApplyGeneration = 0;
		ApplyCompendiumTuning();
	}

	// ---------- Ações (T7) ----------

	// Ações pontuais de teste (só debug). As APIs são públicas: RunManager.Dev*, RunState, CharacterSheet, Health.
	// Os controles são recriados a cada abertura da aba; os valores digitados ficam nos campos abaixo.
	private double _devCoinInput = 500;
	private double _devCursedInput = 500;
	private double _devHealthInput = 50;
	private int _devJumpMap = 1;
	private int _devJumpRoom = 1;
	private int _devBlessingIndex;
	private int _devRarityIndex;
	private int _devArtifactIndex;
	private Label _devActionStatus;
	private Label _devTimeScaleWarning;

	private void DevActionStatus(string message, bool warn = false)
	{
		if (_devActionStatus != null && IsInstanceValid(_devActionStatus))
		{
			_devActionStatus.Text = message;
			_devActionStatus.AddThemeColorOverride("font_color", warn ? UiPalette.Warn : UiPalette.TextPrimary);
		}
		AppendGameLog("[dev] " + message);
	}

	private Node DevPlayer() => GetTree().GetFirstNodeInGroup("player");
	private Health DevPlayerHealth() => DevPlayer()?.GetNodeOrNull<Health>("Health");
	private RunManager DevRunManager() => GetTree().GetFirstNodeInGroup("run_manager") as RunManager;

	private void DevHeader(Control parent, string title)
	{
		var spacer = new Control { CustomMinimumSize = new Vector2(0.0f, UiMetrics.S2), MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(spacer);
		parent.AddChild(UiComponents.MakeLabel(title, UiVariation.SectionGoldLabel));
		parent.AddChild(UiComponents.Separator());
	}

	private HBoxContainer DevRow(Control parent, string label = null)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0.0f, 36.0f) };
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		parent.AddChild(row);
		if (label != null)
		{
			var text = UiComponents.MakeLabel(label, UiVariation.BodyLabel);
			text.CustomMinimumSize = new Vector2(190.0f, 0.0f);
			text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(text);
		}
		return row;
	}

	private static SpinBox DevSpin(double min, double max, double value, double step, Action<double> onChanged)
	{
		var spin = new SpinBox
		{
			MinValue = min, MaxValue = max, Step = step, Value = value,
			CustomMinimumSize = new Vector2(130.0f, 32.0f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		spin.ValueChanged += v => onChanged(v);
		return spin;
	}

	private static Button DevButton(Control row, string text, Action action, ButtonVariant variant = ButtonVariant.Secondary)
	{
		var button = UiComponents.MenuButton(text, action, variant, ButtonSize.Small);
		row.AddChild(button);
		return button;
	}

	private static CheckBox DevCheck(Control row, string text, bool value, Action<bool> onToggled)
	{
		var check = new CheckBox { Text = text, ButtonPressed = value };
		check.Toggled += pressed => onToggled(pressed);
		row.AddChild(check);
		return check;
	}

	private void BuildDevActionsSection(Control parent)
	{
		_devActionStatus = UiComponents.MakeLabel("Escolha uma ação. Funciona com o jogo pausado.", UiVariation.SecondaryLabel, true);
		parent.AddChild(_devActionStatus);

		BuildDevDoorsSection(parent);

		// --- Moedas de Sangue (run) ---
		DevHeader(parent, "Moedas de Sangue (run)");
		var coinRow = DevRow(parent, "Quantidade");
		coinRow.AddChild(DevSpin(0, 999999, _devCoinInput, 50, v => _devCoinInput = v));
		DevButton(coinRow, "Dar", DevGiveCoins);
		DevButton(coinRow, "Definir para", DevSetCoins);
		DevButton(coinRow, "Zerar", DevZeroCoins, ButtonVariant.Destructive);

		// --- Sangue Amaldiçoado (permanente) ---
		DevHeader(parent, "Sangue Amaldiçoado (permanente, vai ao save)");
		var cursedRow = DevRow(parent, "Quantidade");
		cursedRow.AddChild(DevSpin(0, 999999, _devCursedInput, 10, v => _devCursedInput = v));
		DevButton(cursedRow, "Dar", DevGiveCursedBlood);
		DevButton(cursedRow, "Definir para", DevSetCursedBlood);

		// --- Jogador ---
		DevHeader(parent, "Jogador");
		var healRow = DevRow(parent, "Vida");
		DevButton(healRow, "Curar tudo", DevHealFull);
		healRow.AddChild(DevSpin(1, 99999, _devHealthInput, 1, v => _devHealthInput = v));
		DevButton(healRow, "Definir vida atual", DevSetHealth);
		var toggleRow = DevRow(parent, "Invencibilidade");
		DevCheck(toggleRow, "Modo deus", Health.DevGodMode, on => { Health.DevGodMode = on; DevActionStatus(on ? "Modo deus ligado." : "Modo deus desligado."); });
		DevCheck(toggleRow, "Stamina infinita", PlayerController.DevInfiniteStamina, on => { PlayerController.DevInfiniteStamina = on; DevActionStatus(on ? "Stamina infinita ligada." : "Stamina infinita desligada."); });
		var furyRow = DevRow(parent, "Fúria");
		DevButton(furyRow, "Fúria cheia", DevFillFury);

		// --- Sala ---
		DevHeader(parent, "Sala");
		var roomRow = DevRow(parent, "Sala atual");
		DevButton(roomRow, "Matar todos os inimigos", DevKillEnemies);
		DevButton(roomRow, "Completar sala", DevCompleteRoom);
		var jumpRow = DevRow(parent, "Pular para");
		jumpRow.AddChild(UiComponents.MakeLabel("Mapa", UiVariation.SecondaryLabel));
		jumpRow.AddChild(DevSpin(1, CampaignCatalog.Count, _devJumpMap, 1, v => _devJumpMap = (int)v));
		jumpRow.AddChild(UiComponents.MakeLabel("Sala", UiVariation.SecondaryLabel));
		jumpRow.AddChild(DevSpin(1, 20, _devJumpRoom, 1, v => _devJumpRoom = (int)v));
		DevButton(jumpRow, "Pular", () => DevJump(_devJumpMap - 1, _devJumpRoom));
		DevButton(jumpRow, "Ir para o boss", () => DevJump(RunState.IsRunActive ? RunState.CurrentMapIndex : _devJumpMap - 1, int.MaxValue));
		parent.AddChild(UiComponents.MakeLabel("Pular recarrega a arena (inicia a run se estiver no hub; descarta a recompensa pendente). A sala do boss é a última do mapa.", UiVariation.SecondaryLabel, true));

		// --- Recompensas ---
		DevHeader(parent, "Recompensas (da run)");
		var blessings = BloodBlessingCatalog.All;
		var blessingRow = DevRow(parent, "Bênção");
		var blessingPick = new OptionButton { CustomMinimumSize = new Vector2(320.0f, 32.0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, FitToLongestItem = false, ClipText = true };
		foreach (var definition in blessings)
			blessingPick.AddItem($"{definition.DisplayName} ({definition.Id})");
		if (blessings.Count > 0)
			blessingPick.Select(Math.Clamp(_devBlessingIndex, 0, blessings.Count - 1));
		blessingPick.ItemSelected += i => _devBlessingIndex = (int)i;
		blessingRow.AddChild(blessingPick);
		var rarities = BloodBlessingCatalog.AllRarities;
		var rarityPick = new OptionButton { CustomMinimumSize = new Vector2(140.0f, 32.0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		foreach (var rarity in rarities)
			rarityPick.AddItem(rarity.DisplayName);
		if (rarities.Count > 0)
			rarityPick.Select(Math.Clamp(_devRarityIndex, 0, rarities.Count - 1));
		rarityPick.ItemSelected += i => _devRarityIndex = (int)i;
		blessingRow.AddChild(rarityPick);
		DevButton(blessingRow, "Dar bênção", DevGiveBlessing);

		var artifacts = ArtifactCatalog.All;
		var artifactRow = DevRow(parent, "Artefato");
		var artifactPick = new OptionButton { CustomMinimumSize = new Vector2(320.0f, 32.0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, ClipText = true };
		foreach (var definition in artifacts)
			artifactPick.AddItem($"{definition.DisplayName} ({definition.Id})");
		if (artifacts.Count > 0)
			artifactPick.Select(Math.Clamp(_devArtifactIndex, 0, artifacts.Count - 1));
		artifactPick.ItemSelected += i => _devArtifactIndex = (int)i;
		artifactRow.AddChild(artifactPick);
		DevButton(artifactRow, "Dar artefato", DevGiveArtifact);

		var otherRow = DevRow(parent, "Outros");
		DevButton(otherRow, "Dar Fruta do Diabo", DevGiveFruit);
		DevButton(otherRow, "Abrir oferta de Bênção", DevOpenBlessingOffer);

		// --- Tempo ---
		DevHeader(parent, "Tempo");
		var timeRow = DevRow(parent, "Escala de tempo");
		var timeValue = UiComponents.MakeLabel(DevTimeText(), UiVariation.BodyLabel);
		timeValue.CustomMinimumSize = new Vector2(64.0f, 0.0f);
		var slider = new HSlider
		{
			MinValue = 0.1, MaxValue = 4.0, Step = 0.1, Value = Engine.TimeScale,
			CustomMinimumSize = new Vector2(260.0f, 24.0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		slider.ValueChanged += v =>
		{
			CombatFeedback.DevSetBaseTimeScale((float)v);
			timeValue.Text = DevTimeText();
			RefreshDevTimeWarning();
		};
		timeRow.AddChild(slider);
		timeRow.AddChild(timeValue);
		DevButton(timeRow, "Normal (1×)", () =>
		{
			CombatFeedback.DevSetBaseTimeScale(1.0f);
			slider.SetValueNoSignal(1.0);
			timeValue.Text = DevTimeText();
			RefreshDevTimeWarning();
			DevActionStatus("Escala de tempo: 1× (normal).");
		});
		parent.AddChild(UiComponents.MakeLabel("O valor continua até voltar a 1×; um aviso fica visível na tela enquanto for diferente de 1×.", UiVariation.SecondaryLabel, true));

		// --- Tuning e dados ---
		DevHeader(parent, "Tuning e dados");
		var tuningRow = DevRow(parent);
		DevButton(tuningRow, "Aplicar tuning agora", () => { FlushDevApplyNow(); DevActionStatus("Tuning reaplicado."); });
		DevButton(tuningRow, "Abrir pasta de dados", DevOpenDataFolder);
		DevButton(tuningRow, "Exportar overrides", DevExportOverrides);
	}

	// ---------- Portas de recompensa (forçar o que aparece nas próximas portas) ----------

	private static readonly string[] DevDoorCountNames = { "Auto", "1 porta", "2 portas" };
	private static readonly string[] DevDoorTypeNames = { "Auto", "Bênção", "Bigorna", "Fruta do Diabo", "Moedas" };
	private CheckBox _devDoorsCheck;
	private OptionButton _devDoorsCountPick;
	private readonly OptionButton[] _devDoorsTypePicks = new OptionButton[2];
	private Label _devDoorsInfo;

	private static RoomRewardType? DevDoorTypeFromIndex(int index) => index switch
	{
		1 => RoomRewardType.Blessing,
		2 => RoomRewardType.Anvil,
		3 => RoomRewardType.DevilsFruit,
		4 => RoomRewardType.Coins,
		_ => null
	};

	private static int DevDoorTypeToIndex(RoomRewardType? type) => type switch
	{
		RoomRewardType.Blessing => 1,
		RoomRewardType.Anvil => 2,
		RoomRewardType.DevilsFruit => 3,
		RoomRewardType.Coins => 4,
		_ => 0
	};

	private static string DevDoorTypeName(RoomRewardType type) => DevDoorTypeNames[DevDoorTypeToIndex(type)];

	// Origem do conjunto desta cena: nas salas da arena e a sala atual; na Sala Inicial/Câmara/hub e a entrada do mapa (0).
	private int DevDoorsOrigin() => DevRunManager() != null ? RunState.CurrentRoom : 0;

	private OptionButton DevDoorsOption(Control row, string[] names, int selected, Action<int> onSelected)
	{
		var pick = new OptionButton { CustomMinimumSize = new Vector2(150.0f, 32.0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		foreach (var name in names)
			pick.AddItem(name);
		pick.Select(selected);
		pick.ItemSelected += i => onSelected((int)i);
		row.AddChild(pick);
		return pick;
	}

	private void BuildDevDoorsSection(Control parent)
	{
		DevHeader(parent, "Portas de recompensa");
		var forceRow = DevRow(parent, "Forçar portas");
		_devDoorsCheck = DevCheck(forceRow, "Ligado (vale em toda saída ainda não sorteada)", RunState.DevForceDoors, on =>
		{
			RunState.DevSetForceDoors(on);
			RefreshDevDoorsInfo();
			DevActionStatus(on ? "Forçar portas ligado: as próximas saídas seguem o override." : "Forçar portas desligado: sorteio normal.");
		});

		var countRow = DevRow(parent, "Quantidade");
		_devDoorsCountPick = DevDoorsOption(countRow, DevDoorCountNames, RunState.DevForcedDoorCount, i =>
		{
			RunState.DevSetForcedDoorCount(i);
			RefreshDevDoorsInfo();
		});

		for (var slot = 0; slot < 2; slot++)
		{
			var captured = slot;
			var typeRow = DevRow(parent, slot == 0 ? "Porta 1 (esquerda)" : "Porta 2 (direita)");
			_devDoorsTypePicks[slot] = DevDoorsOption(typeRow, DevDoorTypeNames, DevDoorTypeToIndex(RunState.DevForcedType(slot)), i =>
			{
				RunState.DevSetForcedType(captured, DevDoorTypeFromIndex(i));
				RefreshDevDoorsInfo();
			});
		}

		var presetRow = DevRow(parent, "Atalhos");
		DevButton(presetRow, "Bênção + Bigorna", () => DevApplyDoorPreset(2, RoomRewardType.Blessing, RoomRewardType.Anvil));
		DevButton(presetRow, "Bênção + Fruta", () => DevApplyDoorPreset(2, RoomRewardType.Blessing, RoomRewardType.DevilsFruit));
		DevButton(presetRow, "Bênção + Moedas", () => DevApplyDoorPreset(2, RoomRewardType.Blessing, RoomRewardType.Coins));
		DevButton(presetRow, "Só Bênção", () => DevApplyDoorPreset(1, RoomRewardType.Blessing, null));

		var actionRow = DevRow(parent, "Agora");
		DevButton(actionRow, "Rerrolar portas desta sala", DevRerollDoors);
		DevButton(actionRow, "Bênção agora", DevBlessingNow);
		DevButton(actionRow, "Bigorna agora", DevAnvilNow);

		_devDoorsInfo = UiComponents.MakeLabel("", UiVariation.SecondaryLabel, true);
		parent.AddChild(_devDoorsInfo);
		RefreshDevDoorsInfo();
	}

	private void DevApplyDoorPreset(int count, RoomRewardType? first, RoomRewardType? second)
	{
		RunState.DevSetForceDoors(true);
		RunState.DevSetForcedDoorCount(count);
		RunState.DevSetForcedType(0, first);
		RunState.DevSetForcedType(1, second);
		_devDoorsCheck?.SetPressedNoSignal(true);
		_devDoorsCountPick?.Select(count);
		_devDoorsTypePicks[0]?.Select(DevDoorTypeToIndex(first));
		_devDoorsTypePicks[1]?.Select(DevDoorTypeToIndex(second));
		RefreshDevDoorsInfo();
		DevActionStatus("Forçar portas ligado. Vale nas próximas saídas ainda não sorteadas; use \"Rerrolar portas desta sala\" para a sala atual.");
	}

	private void RefreshDevDoorsInfo()
	{
		if (_devDoorsInfo == null || !IsInstanceValid(_devDoorsInfo))
			return;

		var map = Mathf.Clamp(RunState.CurrentMapIndex, 0, 3);
		var origin = DevDoorsOrigin();
		var text = new StringBuilder();

		var current = RunState.GetRoomRewardSet(map, origin);
		text.Append($"Mapa {map + 1}, saída da sala {origin}: ");
		if (current == null)
			text.Append(RoomRewardCatalog.HasRoomRewardSet(map, origin) ? "conjunto ainda não sorteado." : "sem conjunto de portas (boss ou sala N).");
		else
			text.Append(string.Join(" | ", current.Doors.Select(d => DevDoorTypeName(d.Type))) + (RunState.DevIsForcedSet(current) ? " (forçado)." : " (sorteio normal)."));

		if (!RunState.DevForceDoors)
		{
			text.Append("\nOverride desligado: sorteio normal (pesos, pity e tetos).");
			_devDoorsInfo.Text = text.ToString();
			return;
		}

		var count = RunState.DevForcedDoorCount;
		var first = RunState.DevForcedType(0);
		var second = RunState.DevForcedType(1);
		string Describe(RoomRewardType? t) => t.HasValue ? DevDoorTypeName(t.Value) : "Auto";
		text.Append("\nAplicará nas saídas ainda não sorteadas (origens 0 a N-1, qualquer mapa): ");
		text.Append(count == 1 ? $"1 porta ({Describe(first)})." : count == 2 || second.HasValue ? $"2 portas (esquerda {Describe(first)}, direita {Describe(second)})." : $"portas pelo sorteio normal; porta 1 = {Describe(first)}.");
		text.Append(" Ignora pesos, pity, teto de Fruta e teto de Bigorna.");

		var twoDoors = count == 2 || (count == 0 && second.HasValue);
		var types = new List<RoomRewardType>();
		if (first.HasValue) types.Add(first.Value);
		if (twoDoors && second.HasValue) types.Add(second.Value);
		var allKnown = twoDoors ? types.Count == 2 : types.Count == 1;
		var outOfNormal = false;
		if (twoDoors && types.Count == 2 && types[0] == types[1])
			outOfNormal = true;
		else if (allKnown && !types.Contains(RoomRewardType.Blessing))
			outOfNormal = true;
		if (outOfNormal)
			text.Append("\nAVISO: combinação fora do normal do jogo (toda saída tem Bênção e duas portas nunca repetem tipo).");
		if (types.Contains(RoomRewardType.Anvil) && !RunState.DevAnvilPoolAvailable())
			text.Append($"\nAVISO: Bigorna sem modificação livre para a arma equipada: a porta pagará o fallback de {DevAnvilFallbackCoins()} Moedas.");
		if (!RunState.IsRunActive)
			text.Append("\nAVISO: sem run ativa. Iniciar uma run zera o override: ligue depois de entrar na run.");

		_devDoorsInfo.Text = text.ToString();
	}

	private static int DevAnvilFallbackCoins() => CompendiumTuning.GetInt("economy.coins.blessing_fallback", EconomyDefaults.BlessingFallbackCoins);

	private void DevRerollDoors()
	{
		var runManager = DevRunManager();
		if (runManager == null)
		{
			DevActionStatus("Rerrolar só funciona nas salas da arena (nesta cena as portas já foram montadas).", true);
			return;
		}
		if (RunState.IsBossRoom)
		{
			DevActionStatus("A sala do boss não tem conjunto de portas.", true);
			return;
		}
		var set = runManager.DevRerollRewardDoors();
		RefreshDevDoorsInfo();
		if (set == null)
		{
			DevActionStatus("Esta saída não tem conjunto de portas (sala N leva ao boss).", true);
			return;
		}
		var names = string.Join(" | ", set.Doors.Select(d => DevDoorTypeName(d.Type)));
		DevActionStatus(runManager.IsRoomCleared
			? $"Portas recriadas: {names}."
			: $"Conjunto sorteado: {names}. As portas aparecem ao limpar a sala.");
	}

	private void DevBlessingNow()
	{
		var plan = RunState.DevRollBlessingPlan();
		if (plan == null)
		{
			DevActionStatus("Não há bênção para oferecer.", true);
			return;
		}
		RunState.DevSetDeliveredBlessingPlan(plan);
		CloseDevPanel();
		Callable.From(OpenBloodBlessingOffer).CallDeferred();
	}

	private void DevAnvilNow()
	{
		if (!RunState.DevAnvilPoolAvailable())
		{
			DevActionStatus($"Sem modificação livre para a arma equipada: a Bigorna pagaria {DevAnvilFallbackCoins()} Moedas de fallback (nada aberto).", true);
			return;
		}
		CloseDevPanel();
		Callable.From(OpenAnvilOffer).CallDeferred();
	}

	private static string DevTimeText() => Engine.TimeScale.ToString("0.0", CultureInfo.InvariantCulture) + "×";

	// Aviso fixo na tela (fora do painel) enquanto a escala de tempo != 1.
	private void RefreshDevTimeWarning()
	{
		if (_devTimeScaleWarning == null)
			return;
		var changed = !Mathf.IsEqualApprox((float)Engine.TimeScale, 1.0f);
		_devTimeScaleWarning.Visible = changed;
		if (changed)
			_devTimeScaleWarning.Text = "TEMPO " + DevTimeText() + " (painel de dev)";
	}

	private void BuildDevTimeWarning(Control root)
	{
		_devTimeScaleWarning = new Label
		{
			Name = "DevTimeScaleWarning",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			AnchorLeft = 0.5f, AnchorRight = 0.5f,
			OffsetLeft = -200.0f, OffsetRight = 200.0f, OffsetTop = UiMetrics.SafeMargin + 24.0f, OffsetBottom = UiMetrics.SafeMargin + 48.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_devTimeScaleWarning.AddThemeColorOverride("font_color", UiPalette.Warn);
		root.AddChild(_devTimeScaleWarning);
		RefreshDevTimeWarning();
	}

	// ---------- Implementação das ações ----------

	private void DevGiveCoins()
	{
		var amount = (int)_devCoinInput;
		if (amount <= 0)
			return;
		if (DevRunManager() is { } runManager)
			runManager.AddBloodCoins(amount);
		else
		{
			RunState.AddRunBloodCoins(amount);
			SyncBloodCoins(RunState.RunBloodCoins);
		}
		DevActionStatus($"+{amount} Moedas de Sangue (total {RunState.RunBloodCoins}).");
	}

	private void DevSetCoins()
	{
		var target = Math.Max((int)_devCoinInput, 0);
		RunState.AddRunBloodCoins(target - RunState.RunBloodCoins);
		SyncBloodCoins(RunState.RunBloodCoins);
		DevActionStatus($"Moedas de Sangue definidas para {RunState.RunBloodCoins}.");
	}

	private void DevZeroCoins()
	{
		_devCoinInput = 0;
		DevSetCoins();
	}

	private void DevGiveCursedBlood()
	{
		var amount = (int)_devCursedInput;
		if (amount <= 0)
			return;
		CharacterSheet.AddCursedBlood(amount);
		RefreshResourceIndicator();
		DevActionStatus($"+{amount} Sangue Amaldiçoado (total {CharacterSheet.CursedBlood}).");
	}

	private void DevSetCursedBlood()
	{
		var target = Math.Max((int)_devCursedInput, 0);
		CharacterSheet.AddCursedBlood(target - CharacterSheet.CursedBlood);
		RefreshResourceIndicator();
		DevActionStatus($"Sangue Amaldiçoado definido para {CharacterSheet.CursedBlood}.");
	}

	private void DevHealFull()
	{
		var health = DevPlayerHealth();
		if (health == null || health.IsDead)
		{
			DevActionStatus("Sem jogador vivo nesta cena.", true);
			return;
		}
		health.SetCurrentHealth(health.MaxHealth);
		DevActionStatus($"Vida cheia ({Mathf.RoundToInt(health.MaxHealth)}).");
	}

	private void DevSetHealth()
	{
		var health = DevPlayerHealth();
		if (health == null || health.IsDead)
		{
			DevActionStatus("Sem jogador vivo nesta cena.", true);
			return;
		}
		health.SetCurrentHealth((float)_devHealthInput);
		DevActionStatus($"Vida atual: {Mathf.RoundToInt(health.CurrentHealth)}/{Mathf.RoundToInt(health.MaxHealth)}.");
	}

	private void DevFillFury()
	{
		var combat = DevPlayer()?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
		if (combat == null)
		{
			DevActionStatus("Sem jogador nesta cena.", true);
			return;
		}
		if (!combat.IsFuryUnlocked)
		{
			DevActionStatus("Fúria bloqueada nesta run (falta a habilidade que a libera).", true);
			return;
		}
		combat.AddFury(combat.MaxFury * 2.0f); // AddFury aplica o multiplicador de Bloodcraft e limita no máximo
		DevActionStatus("Fúria cheia.");
	}

	private void DevKillEnemies()
	{
		var runManager = DevRunManager();
		if (runManager == null)
		{
			DevActionStatus("Esta cena não tem RunManager (use na arena).", true);
			return;
		}
		DevActionStatus($"{runManager.DevKillAllEnemies()} inimigos mortos.");
	}

	private void DevCompleteRoom()
	{
		var runManager = DevRunManager();
		if (runManager == null)
		{
			DevActionStatus("Esta cena não tem RunManager (use na arena).", true);
			return;
		}
		DevActionStatus(runManager.DevCompleteRoom()
			? "Sala completada: recompensas e portas liberadas conforme o fluxo normal."
			: "Uma wave está surgindo; tente de novo em instantes.", !runManager.IsRoomCleared);
	}

	private void DevJump(int mapIndex, int room)
	{
		if (_isGameOver)
			return;
		RunState.DevJumpTo(mapIndex, room);
		CloseDevPanel();
		UiModalStack.Clear();
		UiModalStack.ApplyPause(false);
		GetTree().ChangeSceneToFile("res://scenes/game/TestArena.tscn");
	}

	private void DevAfterRewardGiven()
	{
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		RefreshCharacterSheet();
		RefreshBlessingHud();
	}

	private void DevGiveBlessing()
	{
		var blessings = BloodBlessingCatalog.All;
		var rarities = BloodBlessingCatalog.AllRarities;
		if (blessings.Count == 0)
			return;
		var definition = blessings[Math.Clamp(_devBlessingIndex, 0, blessings.Count - 1)];
		var rarity = rarities.Count > 0 ? rarities[Math.Clamp(_devRarityIndex, 0, rarities.Count - 1)] : null;
		var isStirps = definition.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
		var rarityName = isStirps ? "Stirps" : rarity?.DisplayName ?? "Common";
		var power = isStirps ? 1.0f : rarity?.PowerMultiplier ?? 1.0f;
		if (!RunState.AddBloodBlessing(new BloodBlessingOffer(definition, rarityName, power)))
		{
			DevActionStatus($"{definition.DisplayName}: já está no máximo de acúmulos.", true);
			return;
		}
		DevAfterRewardGiven();
		DevActionStatus($"Bênção: {definition.DisplayName} [{rarityName}].");
	}

	private void DevGiveArtifact()
	{
		var artifacts = ArtifactCatalog.All;
		if (artifacts.Count == 0)
			return;
		var definition = artifacts[Math.Clamp(_devArtifactIndex, 0, artifacts.Count - 1)];
		RunState.AddTemporaryArtifact(ArtifactCatalog.CreateRecord(definition));
		DevAfterRewardGiven();
		DevActionStatus($"Artefato: {definition.DisplayName}.");
	}

	private void DevGiveFruit()
	{
		var maxHealthGain = RunState.AddDevilsFruit();
		var player = DevPlayer() as PlayerController;
		player?.ApplyRuntimeTuning();
		DevPlayerHealth()?.Heal(Math.Max(RunState.DevilsFruitHealAmount - maxHealthGain, 0.0f));
		DevActionStatus($"Fruta do Diabo: +{Mathf.RoundToInt(maxHealthGain)} de vida máxima (frutas na run: {RunState.DevilsFruitCount}).");
	}

	private void DevOpenBlessingOffer()
	{
		CloseDevPanel();
		Callable.From(OpenBloodBlessingOffer).CallDeferred();
	}

	private void DevOpenDataFolder()
	{
		var path = ProjectSettings.GlobalizePath("user://");
		var error = OS.ShellOpen(path);
		DevActionStatus(error == Error.Ok ? $"Pasta aberta: {path}" : $"Não foi possível abrir {path} ({error}).", error != Error.Ok);
	}

	private void DevExportOverrides()
	{
		CompendiumTuning.FlushPendingSave();
		var path = ProjectSettings.GlobalizePath("user://dev_tuning.json");
		var exists = FileAccess.FileExists("user://dev_tuning.json");
		DisplayServer.ClipboardSet(path);
		DevActionStatus(exists
			? $"Overrides gravados em: {path} (caminho copiado)."
			: $"Ainda não há overrides (nenhum parâmetro alterado). Caminho: {path} (copiado).", !exists);
	}

	// ---------- Pré-visualização (UiPreview) ----------

	private bool OpenDevPanelPreview(bool actionsTab = false)
	{
		if (!DevPanelAvailable)
			return false;
		if (actionsTab)
			_devSelectedGroup = DevActionsGroup;
		OpenDevPanel();
		return true;
	}
}

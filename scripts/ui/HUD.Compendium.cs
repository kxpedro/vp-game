using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Dicionário / compêndio (guia de UI §8.12, decisão D2): diário de descobertas.
// Release: o que não foi descoberto aparece bloqueado ("???"). Debug: tudo visível, com ID e editores de tuning numa seção DEV.
// Preview do modo release em debug: --ui-preview=compendium --ui-codex-locked (ou --ui-codex-locked=sample, que finge
// algumas descobertas sem tocar no save).
public partial class HUD
{
	// IDs de aba (persistem em --ui-tab e no _selectedCompendiumTab; não renomear).
	private static readonly string[] CompendiumTabIds = { "Artifacts", "Blessings", "Skills", "Attacks", "Enemies", "Weapons" };
	private const float CompendiumDetailWidth = 560.0f;
	private const float CompendiumRowHeight = 64.0f;
	private const double CompendiumApplyDelay = 0.2;
	private const ulong CompendiumHoverMotionMs = 150;

	private sealed class CodexEntry
	{
		public string Category;
		public string Id;
		public string Title;
		public string IconPath;
		public string IconText;
		public string RarityId;
		public string BadgeText;
		public Color? BadgeColor;
		public string Meta;
		public string Body;
		public string Flavor;
		public string LockedHint;
		public string Section;
		public bool PreviewSample;
		public Action<VBoxContainer> BuildDev;
	}

	// Selo "Em breve" com fundo visível sobre a linha (PanelInset sumia no PanelRaised; §3.13).
	private static readonly Color CompendiumSoonBadgeColor = UiPalette.Border;

	private UiWindow _compendiumWindow;
	private UiTabs _compendiumTabs;
	private UiDetailPanel _compendiumDetail;
	private Label _compendiumCounterLabel;
	private UiDevSection _compendiumDevSection;
	private readonly List<CodexEntry> _compendiumEntries = new();
	private readonly List<Button> _compendiumRows = new();
	private readonly List<SpinBox> _compendiumEditors = new();
	private readonly Dictionary<string, Texture2D> _compendiumIconCache = new();
	private int _compendiumSelectedIndex = -1;
	private int _compendiumApplyGeneration;
	private bool _compendiumForceLocked;
	private bool _compendiumPreviewSample;

	// Release (ou preview --ui-codex-locked): respeita as descobertas. Debug: tudo visível.
	private bool CompendiumReleaseView => _compendiumForceLocked || !CodexProgress.DebugUnlockAll;
	private bool CompendiumShowDev => OS.IsDebugBuild() && !_compendiumForceLocked;

	private void BuildCompendiumOverlay(Control root)
	{
		ReadCompendiumPreviewArguments();

		_compendiumWindow = UiComponents.Window(root, "CompendiumOverlay", WindowSize.Full, Localization.Text("codex.title"));
		_compendiumOverlay = _compendiumWindow.Root;
		_compendiumOverlay.Visible = false;
		_compendiumStatusLabel = _compendiumWindow.Status;
		// Atalhos (L1/R1, analógico direito): HUD.RegisterModalShortcuts → HandleCompendiumShortcut.

		if (CompendiumShowDev)
		{
			var devBadge = UiComponents.Badge("DEV: tudo visível", UiPalette.PanelInset, UiPalette.TextSecondary);
			devBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			_compendiumWindow.HeaderRight.AddChild(devBadge);
		}
		else if (_compendiumForceLocked)
		{
			var previewBadge = UiComponents.Badge("Preview: modo release", UiPalette.PanelInset, UiPalette.TextSecondary);
			previewBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			_compendiumWindow.HeaderRight.AddChild(previewBadge);
		}

		var body = _compendiumWindow.Body;

		_compendiumTabs = UiComponents.Tabs(new[]
		{
			Localization.Text("codex.tab.artifacts"), Localization.Text("codex.tab.blessings"), Localization.Text("codex.tab.skills"),
			Localization.Text("codex.tab.attacks"), Localization.Text("codex.tab.enemies"), Localization.Text("codex.tab.weapons")
		}, OnCompendiumTabChanged);
		_compendiumTabs.Root.Name = "CompendiumTabs";
		_compendiumTabs.Root.Alignment = BoxContainer.AlignmentMode.Center;
		body.AddChild(_compendiumTabs.Root);
		_compendiumTabButtons.Clear();
		for (var i = 0; i < CompendiumTabIds.Length && i < _compendiumTabs.Buttons.Count; i++)
		{
			_compendiumTabs.Buttons[i].Name = $"Compendium{CompendiumTabIds[i]}Tab";
			_compendiumTabButtons[CompendiumTabIds[i]] = _compendiumTabs.Buttons[i];
		}

		_compendiumCounterLabel = UiComponents.MakeLabel("", UiVariation.SecondaryLabel);
		_compendiumCounterLabel.Name = "CompendiumCounter";
		body.AddChild(_compendiumCounterLabel);

		var split = new HBoxContainer
		{
			Name = "CompendiumSplit",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		split.AddThemeConstantOverride("separation", UiMetrics.S5);
		body.AddChild(split);

		var scroll = UiComponents.ScrollList(320.0f, out _compendiumList);
		scroll.Name = "CompendiumScroll";
		_compendiumList.Name = "CompendiumList";
		_compendiumList.AddThemeConstantOverride("separation", UiMetrics.S1);
		// Margem interna: o anel de foco (expand 3 + brilho) das linhas não é cortado pelas bordas do ScrollContainer.
		scroll.RemoveChild(_compendiumList);
		var listMargin = CreateCompendiumMargin(UiMetrics.FocusExpand + 3);
		listMargin.AddChild(_compendiumList);
		scroll.AddChild(listMargin);
		split.AddChild(scroll);

		_compendiumDetail = UiComponents.DetailPanel(DetailPanelDock.Right);
		_compendiumDetail.Root.Name = "CompendiumDetail";
		_compendiumDetail.Root.CustomMinimumSize = new Vector2(CompendiumDetailWidth, 0.0f);
		// Editores DEV dentro do painel: rolar até o campo focado.
		_compendiumDetail.Scroll.FollowFocus = true;
		split.AddChild(_compendiumDetail.Root);

		var footer = _compendiumWindow.Footer;
		footer.Add(UiAction.Cancel, Localization.Text("ui.common.close"), CloseCompendium, left: true);
		footer.Add(UiAction.Scroll, Localization.Text("sheet.hint.scroll_detail"));
		if (CompendiumShowDev)
		{
			var devTools = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			devTools.AddThemeConstantOverride("separation", UiMetrics.S2);
			var resetButton = UiComponents.MenuButton("Redefinir valores originais", ConfirmResetCompendiumValues, ButtonVariant.Secondary, ButtonSize.Small);
			resetButton.Name = "ResetCompendiumValuesButton";
			resetButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			devTools.AddChild(resetButton);
			var badge = UiComponents.Badge("DEV", UiPalette.PanelInset, UiPalette.TextSecondary);
			badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			devTools.AddChild(badge);
			footer.Right.AddChild(devTools);
		}
	}

	// --ui-codex-locked[=sample]: só preview em build de debug (força a visão de release).
	private void ReadCompendiumPreviewArguments()
	{
		_compendiumForceLocked = false;
		_compendiumPreviewSample = false;
		if (!OS.IsDebugBuild())
			return;

		foreach (var argument in OS.GetCmdlineUserArgs())
		{
			if (argument == "--ui-codex-locked")
				_compendiumForceLocked = true;
			else if (argument.StartsWith("--ui-codex-locked=", StringComparison.Ordinal))
			{
				_compendiumForceLocked = true;
				_compendiumPreviewSample = argument.EndsWith("=sample", StringComparison.OrdinalIgnoreCase);
			}
		}
	}

	private void OpenCompendium()
	{
		if (_compendiumOverlay == null)
			return;

		var tabIndex = Array.IndexOf(CompendiumTabIds, _selectedCompendiumTab);
		if (tabIndex < 0)
		{
			tabIndex = 0;
			_selectedCompendiumTab = CompendiumTabIds[0];
		}
		_compendiumTabs.Select(tabIndex, false);
		_compendiumSelectedIndex = -1;
		RefreshCompendium();

		Control initialFocus = _compendiumRows.Count > 0 ? _compendiumRows[0] : _compendiumTabs.Buttons[tabIndex];
		UiModalStack.Push(_compendiumOverlay, true, initialFocus, CloseCompendium);
		_compendiumTabs.Arm();
		_compendiumWindow.FadeIn();
	}

	private void CloseCompendium()
	{
		FlushCompendiumApply();
		if (_compendiumOverlay != null)
			UiModalStack.Pop(_compendiumOverlay);
	}

	private void OnCompendiumTabChanged(int index)
	{
		if (index < 0 || index >= CompendiumTabIds.Length)
			return;
		SelectCompendiumTab(CompendiumTabIds[index]);
		FocusCompendiumRow(0);
	}

	private void SelectCompendiumTab(string tabId)
	{
		_selectedCompendiumTab = tabId;
		_compendiumSelectedIndex = -1;
		var index = Array.IndexOf(CompendiumTabIds, tabId);
		if (index >= 0 && _compendiumTabs != null && _compendiumTabs.Selected != index)
			_compendiumTabs.Select(index, false);
		RefreshCompendium();
	}

	// L1/R1 (Q/E) e analógico direito com o foco em qualquer lugar da tela. Chamado pelo HUD._UnhandledInput com o
	// Dicionário no topo da pilha (no Godot 4 tecla/controle vai só ao Control focado; o GuiInput da raiz não recebe).
	private bool HandleCompendiumShortcut(InputEvent input)
	{
		if (!IsVisible(_compendiumOverlay) || input is InputEventMouse)
			return false;

		if (_compendiumTabs != null && _compendiumTabs.HandleInput(input))
			return true;

		if (_compendiumDetail == null)
			return false;
		if (InputMap.HasAction("ui_scroll_down") && input.IsActionPressed("ui_scroll_down", true))
		{
			_compendiumDetail.ScrollBy(UiMetrics.S5);
			return true;
		}
		if (InputMap.HasAction("ui_scroll_up") && input.IsActionPressed("ui_scroll_up", true))
		{
			_compendiumDetail.ScrollBy(-UiMetrics.S5);
			return true;
		}
		return false;
	}

	private void RefreshCompendium()
	{
		if (_compendiumList == null)
			return;

		_compendiumEntries.Clear();
		switch (_selectedCompendiumTab)
		{
			case "Skills":
				BuildSkillCompendium();
				break;
			case "Attacks":
				BuildAttackCompendium();
				break;
			case "Enemies":
				BuildEnemyCompendium();
				break;
			case "Blessings":
				BuildBlessingCompendium();
				break;
			case "Weapons":
				BuildWeaponCompendium();
				break;
			default:
				_selectedCompendiumTab = "Artifacts";
				BuildArtifactCompendium();
				break;
		}

		if (_compendiumPreviewSample)
			for (var i = 0; i < _compendiumEntries.Count; i++)
				_compendiumEntries[i].PreviewSample = i % 3 == 0;

		RebuildCompendiumRows();
		UpdateCompendiumCounter();

		if (_compendiumRows.Count == 0)
		{
			_compendiumSelectedIndex = -1;
			RemoveCompendiumDevSection();
			_compendiumDetail.Clear();
			return;
		}
		SelectCompendiumEntry(Mathf.Clamp(_compendiumSelectedIndex, 0, _compendiumRows.Count - 1));
	}

	// ---------- Lista ----------

	private void RebuildCompendiumRows()
	{
		var hadFocus = ControllerUiSupport.IsFocusInside(_compendiumList);
		foreach (var child in _compendiumList.GetChildren())
		{
			_compendiumList.RemoveChild(child);
			child.QueueFree();
		}
		_compendiumRows.Clear();

		string section = null;
		for (var i = 0; i < _compendiumEntries.Count; i++)
		{
			var entry = _compendiumEntries[i];
			if (!string.IsNullOrEmpty(entry.Section) && entry.Section != section)
			{
				section = entry.Section;
				var header = UiComponents.MakeLabel(section, UiVariation.SectionGoldLabel);
				header.Name = $"Section{_compendiumList.GetChildCount()}";
				if (_compendiumList.GetChildCount() > 0)
					header.CustomMinimumSize = new Vector2(0.0f, 40.0f);
				header.VerticalAlignment = VerticalAlignment.Bottom;
				_compendiumList.AddChild(header);
			}
			var row = CreateCompendiumRow(entry, i);
			_compendiumList.AddChild(row);
			_compendiumRows.Add(row);
		}

		if (_compendiumRows.Count == 0)
			_compendiumList.AddChild(UiComponents.MakeLabel(Localization.Text("codex.empty_tab"), UiVariation.SecondaryLabel));

		LinkCompendiumRowFocus();
		if (hadFocus)
			FocusCompendiumRow(Mathf.Clamp(_compendiumSelectedIndex, 0, Math.Max(0, _compendiumRows.Count - 1)));
	}

	private Button CreateCompendiumRow(CodexEntry entry, int index)
	{
		var discovered = IsCompendiumEntryVisible(entry);
		var row = new Button
		{
			Name = $"Entry{index}",
			ThemeTypeVariation = UiVariation.SelectButton,
			ToggleMode = true,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, CompendiumRowHeight)
		};

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", UiMetrics.S3);
		margin.AddThemeConstantOverride("margin_right", UiMetrics.S3);
		margin.AddThemeConstantOverride("margin_top", UiMetrics.S1);
		margin.AddThemeConstantOverride("margin_bottom", UiMetrics.S1);
		row.AddChild(margin);

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", UiMetrics.S3);
		margin.AddChild(line);

		var icon = discovered
			? UppercaseCompendiumMonogram(UiComponents.IconOrMonogram(LoadCompendiumIcon(entry.IconPath), entry.IconText ?? entry.Title, UiMetrics.IconM))
			: CreateCompendiumLockedIcon(UiMetrics.IconM);
		icon.MouseFilter = Control.MouseFilterEnum.Ignore;
		line.AddChild(icon);

		var text = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		text.AddThemeConstantOverride("separation", 0);
		line.AddChild(text);

		var name = UiComponents.MakeLabel(discovered ? entry.Title : "???", UiVariation.BodyLabel);
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// "???" no tom secundário (não mais apagado que a linha de meta).
		if (!discovered)
			name.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
		else if (!string.IsNullOrEmpty(entry.RarityId))
			name.AddThemeColorOverride("font_color", UiPalette.Rarity(entry.RarityId));
		text.AddChild(name);

		var meta = UiComponents.MakeLabel(discovered ? entry.Meta : Localization.Text("codex.locked.meta"), UiVariation.SecondaryLabel);
		meta.ClipText = true;
		meta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		meta.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		meta.Visible = !string.IsNullOrEmpty(meta.Text);
		text.AddChild(meta);

		if (discovered && !string.IsNullOrEmpty(entry.BadgeText))
		{
			var badge = UiComponents.Badge(entry.BadgeText, entry.BadgeColor ?? UiPalette.Rarity(entry.RarityId));
			badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			line.AddChild(badge);
		}

		row.FocusEntered += () => SelectCompendiumEntry(index);
		row.MouseEntered += () =>
		{
			// Hover = foco, só com mouse em movimento real: com controle, ou com a lista rolando sob o cursor parado,
			// o detalhe segue o item focado (sem duas linhas destacadas). Nunca rouba o foco de um editor do painel (DEV).
			if (UiInput.LastDevice != InputDevice.KeyboardMouse || Time.GetTicksMsec() - _lastMouseMotionMs > CompendiumHoverMotionMs)
				return;
			if (ControllerUiSupport.IsFocusInside(_compendiumDetail.Root))
				return;
			if (row.HasFocus())
				SelectCompendiumEntry(index);
			else
				row.GrabFocus();
		};
		row.Pressed += () => SelectCompendiumEntry(index, true);
		return row;
	}

	// Monograma sempre em caixa alta (o helper comum gera "Fa" para palavra única).
	private static Control UppercaseCompendiumMonogram(Control icon)
	{
		if (icon is Panel)
			foreach (var child in icon.GetChildren())
				if (child is Label label)
					label.Uppercase = true;
		return icon;
	}

	private static Control CreateCompendiumLockedIcon(int size)
	{
		var slot = new PanelContainer
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		slot.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 0, 0));
		var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		center.AddChild(UiComponents.LockGlyph(Math.Max(UiMetrics.IconInline, size / 2)));
		slot.AddChild(center);
		return slot;
	}

	// Cima/baixo explícitos; cima no primeiro vai à aba ativa; direita entra nos editores DEV (quando houver).
	private void LinkCompendiumRowFocus()
	{
		var tabButton = _compendiumTabs != null && _compendiumTabs.Buttons.Count > 0 ? _compendiumTabs.Buttons[_compendiumTabs.Selected] : null;
		for (var i = 0; i < _compendiumRows.Count; i++)
		{
			var row = _compendiumRows[i];
			row.FocusNeighborTop = i > 0 ? row.GetPathTo(_compendiumRows[i - 1]) : tabButton != null ? row.GetPathTo(tabButton) : row.GetPathTo(row);
			row.FocusNeighborBottom = row.GetPathTo(i < _compendiumRows.Count - 1 ? _compendiumRows[i + 1] : row);
			row.FocusNeighborLeft = row.GetPathTo(row);
			row.FocusNeighborRight = row.GetPathTo(row);
			row.FocusPrevious = row.FocusNeighborTop;
			row.FocusNext = row.FocusNeighborBottom;
		}
		if (_compendiumTabs != null && _compendiumRows.Count > 0)
			foreach (var button in _compendiumTabs.Buttons)
				button.FocusNeighborBottom = button.GetPathTo(_compendiumRows[0]);
	}

	private void FocusCompendiumRow(int index)
	{
		if (index < 0 || index >= _compendiumRows.Count)
			return;
		_compendiumRows[index].CallDeferred(Control.MethodName.GrabFocus);
	}

	private void SelectCompendiumEntry(int index, bool force = false)
	{
		if (index < 0 || index >= _compendiumEntries.Count || index >= _compendiumRows.Count)
			return;

		for (var i = 0; i < _compendiumRows.Count; i++)
			_compendiumRows[i].SetPressedNoSignal(i == index);

		if (index == _compendiumSelectedIndex && !force && _compendiumDetail.Header.Visible)
			return;
		_compendiumSelectedIndex = index;
		ShowCompendiumDetail(_compendiumEntries[index]);

		var row = _compendiumRows[index];
		var firstEditor = _compendiumEditors.Count > 0 ? _compendiumEditors[0].GetLineEdit() : null;
		row.FocusNeighborRight = row.GetPathTo(firstEditor != null ? firstEditor : row);
	}

	// ---------- Detalhe ----------

	private void ShowCompendiumDetail(CodexEntry entry)
	{
		RemoveCompendiumDevSection();

		if (IsCompendiumEntryVisible(entry))
		{
			_compendiumDetail.Show(new DetailData
			{
				Icon = LoadCompendiumIcon(entry.IconPath),
				IconText = entry.IconText ?? entry.Title,
				Title = entry.Title,
				RarityId = entry.RarityId,
				BadgeText = entry.BadgeText,
				BadgeColor = entry.BadgeColor,
				Meta = entry.Meta,
				BodyBbcode = entry.Body,
				Flavor = entry.Flavor
			});
			foreach (var child in _compendiumDetail.IconSlot.GetChildren())
				if (child is Control iconControl)
					UppercaseCompendiumMonogram(iconControl);
		}
		else
		{
			_compendiumDetail.Show(new DetailData
			{
				Title = "???",
				BadgeText = Localization.Text("codex.locked.badge"),
				BadgeColor = UiPalette.PanelInset,
				Meta = CompendiumCategoryLabel(entry.Category),
				BodyBbcode = Localization.Text("codex.locked.body") + "\n\n" + EscapeCompendiumBbcode(entry.LockedHint ?? Localization.Text("codex.hint.default"))
			});
			foreach (var child in _compendiumDetail.IconSlot.GetChildren())
			{
				_compendiumDetail.IconSlot.RemoveChild(child);
				child.QueueFree();
			}
			_compendiumDetail.IconSlot.AddChild(CreateCompendiumLockedIcon(UiMetrics.IconL));
			_compendiumDetail.Title.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
		}

		if (CompendiumShowDev)
			AddCompendiumDevSection(entry);
	}

	private void AddCompendiumDevSection(CodexEntry entry)
	{
		var content = _compendiumDetail.Scroll.GetChildCount() > 0 ? _compendiumDetail.Scroll.GetChild(0) as VBoxContainer : null;
		if (content == null)
			return;

		_compendiumDevSection = UiComponents.DevSection();
		_compendiumDevSection.Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var section = _compendiumDevSection.Body;
		var discoveredRaw = CodexProgress.IsDiscoveredRaw(entry.Category, entry.Id);
		section.AddChild(MakeCompendiumDevLine($"ID: {entry.Category}/{entry.Id}"));
		section.AddChild(MakeCompendiumDevLine($"Descoberto no save: {(discoveredRaw ? "sim" : "não")}"));
		if (!string.IsNullOrEmpty(entry.IconPath))
			section.AddChild(MakeCompendiumDevLine($"Ícone: {entry.IconPath}"));
		entry.BuildDev?.Invoke(section);
		// Margem: a borda tracejada e o anel de foco dos editores não encostam no recorte do painel.
		var wrap = CreateCompendiumMargin(UiMetrics.FocusExpand + 3);
		wrap.AddChild(_compendiumDevSection.Root);
		content.AddChild(wrap);
	}

	private static MarginContainer CreateCompendiumMargin(int margin)
	{
		var container = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		container.AddThemeConstantOverride("margin_left", margin);
		container.AddThemeConstantOverride("margin_right", margin);
		container.AddThemeConstantOverride("margin_top", margin);
		container.AddThemeConstantOverride("margin_bottom", margin);
		return container;
	}

	private void RemoveCompendiumDevSection()
	{
		_compendiumEditors.Clear();
		if (_compendiumDevSection == null)
			return;
		var root = _compendiumDevSection.Root;
		_compendiumDevSection = null;
		if (!IsInstanceValid(root))
			return;
		// Remove o invólucro de margem junto (pai direto da seção).
		var node = root.GetParent() is MarginContainer wrap ? (Node)wrap : root;
		node.GetParent()?.RemoveChild(node);
		node.QueueFree();
	}

	private static Label MakeCompendiumDevLine(string text)
	{
		var label = UiComponents.MakeLabel(text, UiVariation.SecondaryLabel, true);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return label;
	}

	private void UpdateCompendiumCounter()
	{
		if (_compendiumCounterLabel == null)
			return;
		var total = _compendiumEntries.Count;
		var known = _compendiumEntries.Count(IsCompendiumEntryKnown);
		var text = Localization.TextFormat("codex.counter", known, total);
		if (!CompendiumReleaseView)
			text += "  ·  contagem real do save; tudo aparece desbloqueado por ser build de debug";
		else if (_compendiumPreviewSample)
			text += "  ·  amostra fictícia de preview";
		_compendiumCounterLabel.Text = text;
	}

	// Entrada que o jogador realmente conhece (save) ou amostra de preview.
	private static bool IsCompendiumEntryKnown(CodexEntry entry)
		=> entry.PreviewSample || CodexProgress.IsDiscoveredRaw(entry.Category, entry.Id);

	private bool IsCompendiumEntryVisible(CodexEntry entry)
		=> !CompendiumReleaseView || IsCompendiumEntryKnown(entry);

	private bool IsCompendiumIdVisible(string category, string id)
		=> !CompendiumReleaseView || CodexProgress.IsDiscoveredRaw(category, id);

	private static string CompendiumCategoryLabel(string category) => category switch
	{
		CodexCategory.Artifact => Localization.Text("codex.category.artifact"),
		CodexCategory.Blessing => Localization.Text("codex.category.blessing"),
		CodexCategory.Skill => Localization.Text("codex.category.skill"),
		CodexCategory.Enemy => Localization.Text("codex.category.enemy"),
		CodexCategory.Boss => Localization.Text("codex.category.boss"),
		CodexCategory.Attack => Localization.Text("codex.category.attack"),
		CodexCategory.Weapon => Localization.Text("codex.category.weapon"),
		_ => ""
	};

	private Texture2D LoadCompendiumIcon(string path)
	{
		// generic.png é o ícone provisório dos artefatos: o guia pede monograma no lugar (§3.4).
		if (string.IsNullOrWhiteSpace(path) || path.EndsWith("/generic.png", StringComparison.OrdinalIgnoreCase))
			return null;
		if (_compendiumIconCache.TryGetValue(path, out var cached))
			return cached;
		var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
		_compendiumIconCache[path] = texture;
		return texture;
	}

	private static string EscapeCompendiumBbcode(string text) => (text ?? "").Replace("[", "[lb]");

	private void SetCompendiumStatus(string text)
	{
		if (_compendiumWindow != null)
			_compendiumWindow.SetStatus(text);
		else if (_compendiumStatusLabel != null)
			_compendiumStatusLabel.Text = text;
	}

	// ---------- Abas ----------

	private void BuildArtifactCompendium()
	{
		SetCompendiumStatus(Localization.Text("codex.status.artifacts"));
		foreach (var artifact in ArtifactCatalog.All)
		{
			var definition = artifact;
			_compendiumEntries.Add(new CodexEntry
			{
				Category = CodexCategory.Artifact,
				Id = definition.Id,
				Title = definition.DisplayName,
				IconPath = definition.IconPath,
				RarityId = definition.Rarity,
				BadgeText = UiComponents.RarityLabel(definition.Rarity),
				Meta = $"{UiComponents.RarityLabel(definition.Rarity)} · {FormatCompendiumSlot(definition.Slot)}",
				Body = EscapeCompendiumBbcode(definition.Description)
					+ (HasFriendlyEffectNames(definition.Effects.Select(e => e.Type))
						? $"\n\n[color=#{UiPalette.Gold.ToHtml(false)}]{Localization.Text("ui.common.effects")}[/color]\n{EscapeCompendiumBbcode(FormatArtifactEffects(definition))}"
						: ""),
				Flavor = definition.FlavorText,
				LockedHint = Localization.Text("codex.hint.artifact"),
				BuildDev = section =>
				{
					section.AddChild(MakeCompendiumDevLine($"Raridade: {definition.Rarity} · Slot: {definition.Slot} · Tags: {FormatTags(definition.Tags)}"));
					AddCompendiumNumberEditor(section, "Peso na loja", $"{definition.Id}.weight", definition.Weight);
					for (var i = 0; i < definition.Effects.Length; i++)
					{
						var effect = definition.Effects[i];
						AddCompendiumNumberEditor(section, $"{effect.Type} ({effect.Operation})", $"{definition.Id}.effect.{i}.value", effect.Value, 0.01, 999.0);
					}
				}
			});
		}
	}

	private void BuildBlessingCompendium()
	{
		SetCompendiumStatus(Localization.TextFormat("codex.status.blessings", BloodBlessingCatalog.AllFamilies.Count));
		var rarityText = string.Join(" · ", BloodBlessingCatalog.AllRarities.Select(r => $"{UiComponents.RarityLabel(r.Id)} ×{FormatLocalizedNumber(r.PowerMultiplier)}"));
		foreach (var blessing in BloodBlessingCatalog.All.OrderBy(b => CompendiumFamilyOrder(b.Family)).ThenBy(b => b.DisplayName, StringComparer.CurrentCulture))
		{
			var definition = blessing;
			var isStirps = definition.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
			// Mesmo texto da oferta (valores do tuning em destaque, palavras-chave coloridas) no poder base (×1).
			// A lista "Efeitos" usa os nomes amigáveis da ficha e só aparece se todos os efeitos tiverem nome em português.
			var body = FormatEffectKeywords(FormatBloodBlessingDescription(definition, 1.0f, true));
			if (HasFriendlyEffectNames(definition.Effects.Select(e => e.Type)))
				body += $"\n\n[color=#{UiPalette.Gold.ToHtml(false)}]{(isStirps ? Localization.Text("codex.blessing.stirps_power") : Localization.Text("codex.blessing.common_effects"))}[/color]\n"
					+ FormatSheetBlessingEffects(definition, 1.0f);
			body += "\n\n" + EscapeCompendiumBbcode(isStirps ? Localization.Text("codex.blessing.stirps_condition") : Localization.TextFormat("codex.blessing.rarities", rarityText));
			_compendiumEntries.Add(new CodexEntry
			{
				Category = CodexCategory.Blessing,
				Id = definition.Id,
				Title = definition.DisplayName,
				IconPath = definition.IconPath,
				RarityId = isStirps ? "stirps" : null,
				BadgeText = isStirps ? UiComponents.RarityLabel("stirps") : null,
				Meta = isStirps
					? Localization.TextFormat("codex.blessing.meta_stirps", FormatCompendiumLineage(definition))
					: Localization.TextFormat("codex.blessing.meta_all", FormatCompendiumLineage(definition)),
				Body = body,
				Flavor = definition.FlavorText,
				Section = definition.Family,
				LockedHint = Localization.Text("codex.hint.blessing"),
				BuildDev = section =>
				{
					section.AddChild(MakeCompendiumDevLine($"Raridade: {definition.Rarity} · Peso: {definition.Weight} · Máx. acúmulos: {definition.MaxStacks}"));
					section.AddChild(MakeCompendiumDevLine($"Efeitos: {string.Join(", ", definition.Effects.Select(e => $"{e.Type} {e.Operation} {e.Value:0.###}"))}"));
					if (definition.Tags.Length > 0)
						section.AddChild(MakeCompendiumDevLine($"Tags: {FormatTags(definition.Tags)}"));
				}
			});
		}
	}

	private void BuildSkillCompendium()
	{
		var skills = new List<SkillDefinition>(SkillCatalog.All);
		SetCompendiumStatus(Localization.Text("codex.status.skills"));
		foreach (var skill in skills.OrderBy(s => s.Tree).ThenBy(s => s.Tier).ThenBy(s => s.GridRow).ThenBy(s => s.GridColumn))
		{
			var definition = skill;
			var cost = GetCompendiumNumber($"{definition.Id}.cost", definition.Cost);
			var maxRank = GetCompendiumNumber($"{definition.Id}.max_rank", definition.MaxRank);
			_compendiumEntries.Add(new CodexEntry
			{
				Category = CodexCategory.Skill,
				Id = definition.Id,
				Title = definition.DisplayName,
				IconText = string.IsNullOrWhiteSpace(definition.IconText) ? definition.DisplayName : definition.IconText,
				BadgeText = definition.IsImplemented ? null : Localization.Text("ui.common.coming_soon"),
				BadgeColor = definition.IsImplemented ? null : CompendiumSoonBadgeColor,
				Meta = Localization.TextFormat("codex.skill.meta", SkillTreeName(definition.Tree), cost, maxRank),
				Body = $"{EscapeCompendiumBbcode(definition.Description)}\n\n{EscapeCompendiumBbcode(Localization.TextFormat("codex.skill.prerequisites", FormatCompendiumPrerequisites(definition)))}",
				Flavor = definition.FlavorText,
				Section = SkillTreeName(definition.Tree),
				LockedHint = Localization.Text("codex.hint.skill"),
				BuildDev = section =>
				{
					section.AddChild(MakeCompendiumDevLine($"Tier {definition.Tier} · grade {definition.GridColumn},{definition.GridRow} · {definition.ImplementationStatus}"));
					AddCompendiumNumberEditor(section, "Custo base (Ícor)", $"{definition.Id}.cost", definition.Cost);
					AddCompendiumNumberEditor(section, "Rank máximo", $"{definition.Id}.max_rank", definition.MaxRank);
				}
			});
		}
	}

	private void BuildAttackCompendium()
	{
		SetCompendiumStatus(Localization.Text("codex.status.attacks"));
		AddAttackEntry(CodexAttackId.Light1, Localization.Text("codex.attack.light_1.title"), Localization.Text("codex.attack.light_1.meta"), Localization.Text("codex.attack.light_1.body"), 32.0, 0.68, 0.32, 1.55, 1.65, 4.2);
		AddAttackEntry(CodexAttackId.Light2, Localization.Text("codex.attack.light_2.title"), Localization.Text("codex.attack.light_2.meta"), Localization.Text("codex.attack.light_2.body"), 38.0, 0.70, 0.30, 1.75, 1.75, 4.8);
		AddAttackEntry(CodexAttackId.Light3, Localization.Text("codex.attack.light_3.title"), Localization.Text("codex.attack.light_3.meta"), Localization.Text("codex.attack.light_3.body"), 52.0, 0.72, 0.28, 2.05, 1.95, 6.4);
		AddAttackEntry(CodexAttackId.Special, Localization.Text("codex.attack.special.title"), Localization.Text("codex.attack.special.meta"), Localization.Text("codex.attack.special.body"), 56.0, 0.28, 0.42, 3.15, 3.15, 11.0);
		AddAttackEntry(CodexAttackId.Dash, Localization.Text("codex.attack.dash.title"), Localization.Text("codex.attack.dash.meta"), Localization.Text("codex.attack.dash.body"), 68.0, 0.12, 0.18, 2.25, 2.05, 7.4,
			section => AddCompendiumNumberEditor(section, "Multiplicador de dano", "attack.dash.damage_multiplier", 1.35, 0.05, 20.0));

		_compendiumEntries.Add(new CodexEntry
		{
			Category = CodexCategory.Attack,
			Id = CodexAttackId.Fury,
			Title = Localization.Text("codex.attack.fury.title"),
			Meta = Localization.Text("codex.attack.fury.meta"),
			Body = Localization.Text("codex.attack.fury.body"),
			LockedHint = Localization.Text("codex.attack.fury.hint"),
			BuildDev = section =>
			{
				AddCompendiumNumberEditor(section, "Multiplicador de dano", "attack.fury.damage_multiplier", 1.75, 0.05, 20.0);
				AddCompendiumNumberEditor(section, "Consumo por segundo", "attack.fury.drain_per_second", 10.0, 0.1, 999.0);
				AddCompendiumNumberEditor(section, "Fúria máxima", "attack.fury.max_fury", 100.0);
			}
		});
	}

	private void AddAttackEntry(string attackId, string title, string meta, string body, double damage, double hitDelay, double recovery, double range, double width, double knockback, Action<VBoxContainer> extraDev = null)
	{
		_compendiumEntries.Add(new CodexEntry
		{
			Category = CodexCategory.Attack,
			Id = attackId,
			Title = title,
			IconText = attackId.StartsWith("light_", StringComparison.Ordinal) ? $"L{attackId.Substring(6)}" : null,
			Meta = meta,
			Body = EscapeCompendiumBbcode(body),
			LockedHint = Localization.Text("codex.hint.attack"),
			BuildDev = section =>
			{
				section.AddChild(MakeCompendiumDevLine($"Chaves attack.{attackId}.* (o PlayerCombat atual lê weapon.<arma>.*; ver aba Armas)."));
				AddAttackEditors(section, $"attack.{attackId}", damage, hitDelay, recovery, range, width, knockback);
				extraDev?.Invoke(section);
			}
		});
	}

	private void AddAttackEditors(VBoxContainer entry, string keyPrefix, double damage, double hitDelay, double recovery, double range, double width, double knockback)
	{
		AddCompendiumNumberEditor(entry, "Dano", $"{keyPrefix}.damage", damage, 1.0, 9999.0);
		AddCompendiumNumberEditor(entry, "Recuperação", $"{keyPrefix}.recovery", recovery, 0.01, 1.0);
		AddCompendiumNumberEditor(entry, "Alcance", $"{keyPrefix}.range", range, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Largura", $"{keyPrefix}.width", width, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Knockback", $"{keyPrefix}.knockback", knockback, 0.1, 999.0);
	}

	private void BuildEnemyCompendium()
	{
		SetCompendiumStatus(Localization.Text("codex.status.enemies"));
		// Nomes dos arquétipos via EnemyTuningCatalog.ArchetypeName (world.json); papel e descrição do Dicionário em ui.json (codex.enemy.*).
		AddEnemyEntry(EnemyArchetype.Melee, EnemyTuningCatalog.ArchetypeName(EnemyArchetype.Melee), Localization.Text("codex.enemy.melee.role"),
			Localization.Text("codex.enemy.melee.body"));
		AddEnemyEntry(EnemyArchetype.Ranged, EnemyTuningCatalog.ArchetypeName(EnemyArchetype.Ranged), Localization.Text("codex.enemy.ranged.role"),
			Localization.Text("codex.enemy.ranged.body"));
		AddEnemyEntry(EnemyArchetype.Tank, EnemyTuningCatalog.ArchetypeName(EnemyArchetype.Tank), Localization.Text("codex.enemy.tank.role"),
			Localization.Text("codex.enemy.tank.body"));

		var seen = new HashSet<string>();
		for (var mapIndex = 0; mapIndex < CampaignCatalog.Count; mapIndex++)
		{
			var boss = BossCatalog.Get(mapIndex);
			if (!seen.Add(boss.Id))
				continue;
			var map = CampaignCatalog.Get(mapIndex);
			var mapNumber = mapIndex + 1;
			var definition = boss;
			var moves = string.Join("\n", definition.Moves.Select(m => m.MinimumPhase > 1
				? $"• {EscapeCompendiumBbcode(Localization.TextFormat("codex.boss.move_phase", m.DisplayName, m.MinimumPhase))}"
				: $"• {EscapeCompendiumBbcode(m.DisplayName)}"));
			_compendiumEntries.Add(new CodexEntry
			{
				Category = CodexCategory.Boss,
				Id = definition.Id,
				Title = definition.DisplayName,
				RarityId = "legendary",
				BadgeText = Localization.Text("codex.category.boss"),
				Meta = Localization.TextFormat("codex.boss.meta", map.Name),
				Body = $"{EscapeCompendiumBbcode(Localization.TextFormat("codex.boss.body", map.Name, definition.EstimatedMaxHealth))}\n\n[color=#{UiPalette.Gold.ToHtml(false)}]{Localization.Text("codex.moves")}[/color]\n{moves}",
				Flavor = Localization.Text("codex.boss.flavor"),
				Section = Localization.Text("codex.section.bosses"),
				LockedHint = Localization.Text("codex.hint.boss"),
				BuildDev = section =>
				{
					section.AddChild(MakeCompendiumDevLine($"Mapa {mapNumber}: {map.Id} · duração alvo {definition.TargetDurationSeconds:0}s × DPS {definition.ExpectedPlayerDps:0.##}"));
					section.AddChild(MakeCompendiumDevLine($"Golpes: {string.Join(", ", definition.Moves.Select(m => m.Id))}"));
					section.AddChild(MakeCompendiumDevLine("Base compartilhada por todos os chefes (RunManager); a vida vem do BossCatalog."));
					AddCompendiumNumberEditor(section, "Dano", "enemy.blood_mage.damage", 30.0);
					AddCompendiumNumberEditor(section, "Velocidade", "enemy.blood_mage.move_speed", 2.7, 0.1, 100.0);
					AddCompendiumNumberEditor(section, "Alcance", "enemy.blood_mage.attack_range", 2.25, 0.05, 100.0);
					var rewardIndex = Mathf.Clamp(mapNumber - 1, 0, 3);
					AddCompendiumNumberEditor(section, "Moedas da run (fixo)", $"economy.coins.boss.{rewardIndex}", EconomyDefaults.BossCoins[rewardIndex]);
					AddCompendiumNumberEditor(section, "Ícor (fixo)", $"economy.cursed_blood.boss.{rewardIndex}", EconomyDefaults.BossCursedBlood[rewardIndex]);
				}
			});
		}
	}

	private void AddEnemyEntry(EnemyArchetype archetype, string title, string role, string description)
	{
		var id = CodexProgress.EnemyId(archetype);
		var variants = new List<string>();
		foreach (EnemyRarity rarity in Enum.GetValues(typeof(EnemyRarity)))
		{
			var tuning = EnemyTuningCatalog.Get(rarity);
			variants.Add(IsCompendiumIdVisible(CodexCategory.Enemy, CodexProgress.EnemyId(archetype, rarity))
				? Localization.TextFormat("codex.enemy.variant", tuning.DisplayName, tuning.Health, tuning.Damage)
					+ (tuning.ModifierCount == 1 ? Localization.Text("codex.enemy.modifiers_one")
						: tuning.ModifierCount > 1 ? Localization.TextFormat("codex.enemy.modifiers_other", tuning.ModifierCount) : "")
				: Localization.Text("codex.enemy.variant_unknown"));
		}

		_compendiumEntries.Add(new CodexEntry
		{
			Category = CodexCategory.Enemy,
			Id = id,
			Title = title,
			Meta = Localization.TextFormat("codex.enemy.meta", role),
			Body = $"{EscapeCompendiumBbcode(description)}\n\n[color=#{UiPalette.Gold.ToHtml(false)}]{Localization.Text("codex.enemy.variants")}[/color]\n{string.Join("\n", variants)}",
			Section = Localization.Text("codex.section.common_enemies"),
			LockedHint = Localization.Text("codex.hint.enemy"),
			BuildDev = section =>
			{
				var raw = string.Join(", ", Enum.GetValues(typeof(EnemyRarity)).Cast<EnemyRarity>()
					.Select(r => $"{CodexProgress.EnemyId(archetype, r)}={(CodexProgress.IsDiscoveredRaw(CodexCategory.Enemy, CodexProgress.EnemyId(archetype, r)) ? "sim" : "não")}"));
				section.AddChild(MakeCompendiumDevLine($"Variantes no save: {raw}"));
				section.AddChild(MakeCompendiumDevLine("Base compartilhada por todos os arquétipos (RunManager)."));
				AddCompendiumNumberEditor(section, "Vida base", "enemy.basic.health", 110.0);
				AddCompendiumNumberEditor(section, "Dano base", "enemy.basic.damage", 22.0);
				AddCompendiumNumberEditor(section, "Velocidade", "enemy.basic.move_speed", 3.2, 0.1, 100.0);
				AddCompendiumNumberEditor(section, "Moedas da run: sorteio mínimo", "economy.coins.enemy.base_min", EconomyDefaults.EnemyCoinsMin);
				AddCompendiumNumberEditor(section, "Moedas da run: sorteio máximo", "economy.coins.enemy.base_max", EconomyDefaults.EnemyCoinsMax);
				AddCompendiumNumberEditor(section, "Fúria ao morrer", "enemy.basic.fury_on_death", 12.0);
			}
		});
	}

	private void BuildWeaponCompendium()
	{
		SetCompendiumStatus(Localization.Text("codex.status.weapons"));
		foreach (var weapon in WeaponCatalog.All)
		{
			var definition = weapon;
			var placeholder = IsCompendiumPlaceholderWeapon(definition);
			var equipped = definition.Id == WeaponLoadout.EquippedWeaponId;
			var combo = string.Join(" · ", definition.BasicCombo.Select(a => $"{EscapeCompendiumBbcode(a.Name)} ({a.Damage:0})"));
			var body = EscapeCompendiumBbcode(definition.Description);
			if (!placeholder)
				body += $"\n\n[color=#{UiPalette.Gold.ToHtml(false)}]{Localization.Text("codex.moves")}[/color]\n"
					+ Localization.TextFormat("codex.weapon.moves", combo, EscapeCompendiumBbcode(definition.QuickAttack.Name), definition.QuickAttack.Damage,
						EscapeCompendiumBbcode(definition.SpecialAttack.Name), definition.SpecialAttack.Damage);
			_compendiumEntries.Add(new CodexEntry
			{
				Category = CodexCategory.Weapon,
				Id = definition.Id,
				Title = definition.DisplayName,
				BadgeText = placeholder ? Localization.Text("ui.common.coming_soon") : equipped ? Localization.Text("ui.common.equipped") : null,
				BadgeColor = placeholder ? CompendiumSoonBadgeColor : UiPalette.Gold,
				Meta = placeholder ? Localization.Text("bench.meta_placeholder") : Localization.TextFormat("codex.weapon.meta", definition.BasicCombo.Length),
				Body = body,
				LockedHint = Localization.Text("codex.hint.weapon"),
				BuildDev = placeholder ? null : section =>
				{
					section.AddChild(MakeCompendiumDevLine($"Chaves lidas pelo PlayerCombat: weapon.{definition.Id}.<golpe>.*"));
					for (var i = 0; i < definition.BasicCombo.Length; i++)
						AddWeaponAttackEditors(section, $"{definition.BasicCombo[i].Name}", $"weapon.{definition.Id}.basic_{i + 1}", definition.BasicCombo[i]);
					AddWeaponAttackEditors(section, $"Rápido: {definition.QuickAttack.Name}", $"weapon.{definition.Id}.quick", definition.QuickAttack);
					AddWeaponAttackEditors(section, $"Especial: {definition.SpecialAttack.Name}", $"weapon.{definition.Id}.special", definition.SpecialAttack);
				}
			});
		}
	}

	private void AddWeaponAttackEditors(VBoxContainer section, string title, string keyPrefix, WeaponAttackDefinition attack)
	{
		var header = UiComponents.MakeLabel(title, UiVariation.BodyLabel);
		header.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
		section.AddChild(header);
		AddCompendiumNumberEditor(section, "Dano", $"{keyPrefix}.damage", attack.Damage, 1.0, 9999.0);
		AddCompendiumNumberEditor(section, "Atraso do acerto", $"{keyPrefix}.hit_delay", attack.HitDelay, 0.01, 5.0);
		AddCompendiumNumberEditor(section, "Recuperação", $"{keyPrefix}.recovery", attack.Recovery, 0.01, 5.0);
		AddCompendiumNumberEditor(section, "Alcance", $"{keyPrefix}.range", attack.Range, 0.05, 100.0);
		AddCompendiumNumberEditor(section, "Largura", $"{keyPrefix}.width", attack.Width, 0.05, 100.0);
		AddCompendiumNumberEditor(section, "Knockback", $"{keyPrefix}.knockback", attack.Knockback, 0.1, 999.0);
	}

	private static bool IsCompendiumPlaceholderWeapon(WeaponDefinition weapon)
		=> weapon.BasicCombo.Length == 1 && ReferenceEquals(weapon.BasicCombo[0], weapon.SpecialAttack);

	// ---------- Editores de valores (só debug) ----------

	private void AddCompendiumNumberEditor(VBoxContainer parent, string label, string key, double originalValue, double step = 1.0, double maxValue = 9999.0)
	{
		var row = new HBoxContainer
		{
			Name = $"{key.Replace(".", "")}Editor",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 36.0f)
		};
		row.AddThemeConstantOverride("separation", UiMetrics.S2);
		parent.AddChild(row);

		var name = UiComponents.MakeLabel(label, UiVariation.BodyLabel);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.TooltipText = key;
		row.AddChild(name);

		var valueInput = new SpinBox
		{
			MinValue = 0.0,
			MaxValue = maxValue,
			Step = step,
			Value = GetCompendiumNumber(key, originalValue),
			CustomMinimumSize = new Vector2(132.0f, 32.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
			TooltipText = key
		};
		valueInput.ValueChanged += value =>
		{
			// Chave registrada passa pelo registro (clamp, inteiro, "igual ao padrão não é override"); as demais vão direto.
			if (DevParameterRegistry.Find(key) != null)
				DevParameterRegistry.Set(key, value);
			else
				CompendiumTuning.SetNumber(key, value);
			SetCompendiumStatus($"Valor customizado: {label} = {value:0.##}");
			ScheduleCompendiumApply();
		};
		var editor = valueInput.GetLineEdit();
		editor.GuiInput += input => HandleCompendiumEditorInput(valueInput, input);
		row.AddChild(valueInput);
		_compendiumEditors.Add(valueInput);
	}

	// Controle (d-pad/analógico) no campo: cima/baixo navegam entre os editores (cima no primeiro volta à lista),
	// esquerda/direita diminuem/aumentam um passo. Teclado e mouse mantêm o comportamento padrão do SpinBox.
	private void HandleCompendiumEditorInput(SpinBox spin, InputEvent input)
	{
		if (input is not (InputEventJoypadButton or InputEventJoypadMotion))
			return;

		var editor = spin.GetLineEdit();
		var index = _compendiumEditors.IndexOf(spin);
		if (input.IsActionPressed("ui_up", true))
		{
			if (index > 0)
				_compendiumEditors[index - 1].GetLineEdit().GrabFocus();
			else
				FocusCompendiumRow(_compendiumSelectedIndex);
		}
		else if (input.IsActionPressed("ui_down", true))
		{
			if (index >= 0 && index < _compendiumEditors.Count - 1)
				_compendiumEditors[index + 1].GetLineEdit().GrabFocus();
		}
		else if (input.IsActionPressed("ui_left", true))
			spin.Value -= spin.Step;
		else if (input.IsActionPressed("ui_right", true))
			spin.Value += spin.Step;
		else
			return;
		editor.AcceptEvent();
	}

	// Recalcular bônus e reaplicar o tuning custa caro: agrupa os passos do SpinBox (debounce de 0,2 s).
	private void ScheduleCompendiumApply()
	{
		var generation = ++_compendiumApplyGeneration;
		var timer = GetTree().CreateTimer(CompendiumApplyDelay, true, false, true);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(this) || generation != _compendiumApplyGeneration)
				return;
			ApplyCompendiumTuning();
		};
	}

	private void FlushCompendiumApply()
	{
		if (_compendiumApplyGeneration == 0)
			return;
		_compendiumApplyGeneration = 0;
		ApplyCompendiumTuning();
	}

	private void ApplyCompendiumTuning()
	{
		_compendiumApplyGeneration = 0;
		RunState.RecalculateArtifactBonuses();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		RefreshCharacterSheet();
	}

	private double GetCompendiumNumber(string key, double originalValue)
	{
		return CompendiumTuning.GetNumber(key, originalValue);
	}

	private void ConfirmResetCompendiumValues()
	{
		var parent = _compendiumOverlay?.GetParent() as Control;
		if (parent == null)
			return;
		// Rodapé do Dicionário ("Fechar") some enquanto a confirmação está aberta.
		SetCompendiumFooterInert(true);
		var confirm = UiComponents.Confirm(parent, "Redefinir valores originais?",
			"Todos os valores customizados no Dicionário voltam ao original. Isto também apaga TODOS os valores do painel de dev (F1).", "Redefinir",
			() => { SetCompendiumFooterInert(false); ResetCompendiumValues(); },
			ConfirmKind.Destructive, () => SetCompendiumFooterInert(false));
		UiModalStack.Push(confirm.Window.Root, true, confirm.CancelButton, confirm.Cancel);
	}

	private void SetCompendiumFooterInert(bool inert)
	{
		if (_compendiumWindow != null)
			_compendiumWindow.Footer.Root.Modulate = inert ? Colors.Transparent : Colors.White;
	}

	private void ResetCompendiumValues()
	{
		CompendiumTuning.Reset();
		ApplyCompendiumTuning();
		SetCompendiumStatus("Valores originais restaurados.");
		var selected = _compendiumSelectedIndex;
		RefreshCompendium();
		if (selected >= 0)
			SelectCompendiumEntry(Mathf.Clamp(selected, 0, Math.Max(0, _compendiumRows.Count - 1)), true);
	}

	// ---------- Formatação ----------

	// Valores dos efeitos de artefato com o poder da Loja (ArtifactCatalog.ShopPowerMultiplier), como RunState aplica.
	private string FormatArtifactEffects(ArtifactDefinition artifact)
	{
		if (artifact.Effects.Length == 0)
			return Localization.Text("ui.common.none");

		var text = "";
		for (var i = 0; i < artifact.Effects.Length; i++)
		{
			var effect = artifact.Effects[i];
			var value = GetCompendiumNumber($"{artifact.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.ShopPowerMultiplier;
			text += $"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {FormatArtifactOperation(effect.Operation, value)}";

			if (i < artifact.Effects.Length - 1)
				text += ", ";
		}

		return text;
	}

	// Usado também pela oferta de bênçãos (HUD.BlessingOffer).
	private static string FormatArtifactOperation(string operation, double value)
	{
		if (operation.Contains("Multiplier")) return $"{value * 100:+0;-0;0}%";
		if (operation is "Set" or "SetBool") return value > 0 ? Localization.Text("ui.common.active") : Localization.Text("ui.common.inactive");
		return $"{value:+0.##;-0.##;0}";
	}

	private string FormatTags(string[] tags)
	{
		return tags.Length == 0 ? "nenhuma" : string.Join(", ", tags);
	}

	private static string FormatCompendiumSlot(string slot) => (slot ?? "").ToLowerInvariant() switch
	{
		"passive" => Localization.Text("ui.artifact_slot.passive"),
		"active" => Localization.Text("ui.artifact_slot.active"),
		_ => slot ?? ""
	};

	private static int CompendiumFamilyOrder(string family) => (family ?? "").ToLowerInvariant() switch
	{
		"bathory" => 0,
		"dracul" => 1,
		"karnstein" => 2,
		"orlok" => 3,
		"stirps" => 9,
		_ => 5
	};

	private static string FormatCompendiumLineage(BloodBlessingDefinition blessing)
	{
		return blessing.ParentFamilies.Length > 0
			? $"{blessing.Family}: {string.Join(" + ", blessing.ParentFamilies)}"
			: blessing.Family;
	}

	// Nomes de efeito sem tradução no ArtifactEffectRegistry saem em inglês ("Blood Pool Area"): nesse caso a lista é omitida
	// fora da seção DEV (a descrição já traz os valores). FriendlyName devolve o tipo separado por espaços quando não conhece.
	private static bool HasFriendlyEffectNames(IEnumerable<string> types)
		=> types.All(ArtifactEffectRegistry.HasFriendlyName);

	private static string FormatCompendiumPrerequisites(SkillDefinition definition)
	{
		if (definition.Prerequisites.Length == 0)
			return Localization.Text("ui.common.none");
		return string.Join(", ", definition.Prerequisites.Select(id => SkillCatalog.TryGet(id, out var prerequisite) ? prerequisite.DisplayName : id));
	}
}

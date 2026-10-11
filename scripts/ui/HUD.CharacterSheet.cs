using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Ficha do personagem (guia de UI §8.13): Window(Full) com abas Atributos · Bênçãos · Artefatos · Habilidades,
// lista rolável de linhas focáveis e painel de detalhes à direita que segue foco e hover.
// Controle: cima/baixo nas linhas; esquerda/direita (ou L1/R1, Q/E) troca de aba; analógico direito rola o detalhe;
// ui_cancel / C / View fecham. Pausa pela UiModalStack (comportamento anterior: a ficha pausa o jogo).
// Preview (debug): --ui-preview=character_sheet [--ui-tab=Attributes|Blessings|Artifacts|Skills] usa dados fictícios
// só de exibição (nada entra em RunState, CharacterSheet ou no save).
public partial class HUD
{
	// IDs de aba (usados em --ui-tab; não renomear).
	private static readonly string[] CharacterSheetTabIds = { "Attributes", "Blessings", "Artifacts", "Skills" };
	// Atributos ficam invisíveis ao jogador em release (continuam computados): a aba 0 só existe em debug.
	// _characterSheetTab e _characterSheetSelected usam o índice lógico (CharacterSheetTabIds); os botões, o índice - offset.
	private static int CharacterSheetTabOffset => OS.IsDebugBuild() ? 0 : 1;
	private const float CharacterSheetDetailWidth = 520.0f;
	private const float CharacterSheetRowHeight = 64.0f;

	private sealed class CharacterSheetEntry
	{
		public string Section;
		public Func<Control> BuildIcon;
		public string Title;
		public Color? TitleColor;
		public string Meta;
		public string ValueText;
		public string BadgeText;
		public Color? BadgeColor;
		public DetailData Detail;
		public bool Note;           // linha secundária não focável (seção vazia)
	}

	// Dados exibidos (reais ou fictícios do preview).
	private sealed class CharacterSheetData
	{
		public int Balance;               // Moedas da run, ou Sangue Amaldiçoado quando CursedBalance
		public bool CursedBalance;        // hub: o saldo do cabeçalho é o permanente
		public List<BloodBlessingRecord> Blessings = new();
		public List<ArtifactRecord> RunArtifacts = new();   // artefatos comprados na Loja nesta run (não há coleção nem slots)
		public List<(string Id, int Rank)> Skills = new();
	}

	private UiWindow _characterSheetWindow;
	private UiTabs _characterSheetTabs;
	private UiDetailPanel _characterSheetDetail;
	private VBoxContainer _characterSheetList;
	private Label _characterSheetCoinsLabel;
	private Label _characterSheetCurrencyName;
	private Control _characterSheetCoinGlyph;
	private Control _characterSheetCursedGlyph;
	private readonly List<CharacterSheetEntry> _characterSheetEntries = new();
	private readonly List<CharacterSheetEntry> _characterSheetRowEntries = new();
	private readonly List<Button> _characterSheetRows = new();
	private readonly int[] _characterSheetSelected = { 0, 0, 0, 0 };
	private int _characterSheetTab = 1;
	private bool _characterSheetPreview;
	// "Rolar detalhes" só existe no analógico direito: no teclado/mouse a roda já rola e a dica some.
	private UiHint _characterSheetScrollHint;
	private Action<InputDevice> _characterSheetDeviceHandler;

	private void BuildCharacterSheetOverlay(Control root)
	{
		_characterSheetWindow = UiComponents.Window(root, "CharacterSheetOverlay", WindowSize.Full, Localization.Text("sheet.title"));
		_characterSheetOverlay = _characterSheetWindow.Root;
		_characterSheetOverlay.Visible = false;

		// Saldo no cabeçalho (§3.1 HeaderRight): Moedas de Sangue da run (gota dourada) ou, no hub, Sangue Amaldiçoado
		// (losango violeta). O par glifo/rótulo é alternado em RebuildCharacterSheet.
		var coins = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		coins.AddThemeConstantOverride("separation", UiMetrics.S2);
		_characterSheetCoinGlyph = UiComponents.CoinGlyph(UiMetrics.IconGlyph);
		coins.AddChild(_characterSheetCoinGlyph);
		_characterSheetCursedGlyph = UiComponents.CursedBloodGlyph(UiMetrics.IconGlyph);
		_characterSheetCursedGlyph.Visible = false;
		coins.AddChild(_characterSheetCursedGlyph);
		_characterSheetCoinsLabel = UiComponents.MakeLabel("0", UiVariation.SectionGoldLabel);
		_characterSheetCoinsLabel.Name = "CharacterSheetCoins";
		coins.AddChild(_characterSheetCoinsLabel);
		_characterSheetCurrencyName = UiComponents.MakeLabel(Localization.Text("hud.coins_label"), UiVariation.SecondaryLabel);
		_characterSheetCurrencyName.VerticalAlignment = VerticalAlignment.Center;
		coins.AddChild(_characterSheetCurrencyName);
		_characterSheetWindow.HeaderRight.AddChild(coins);

		var body = _characterSheetWindow.Body;
		var tabOffset = CharacterSheetTabOffset;
		var tabLabels = new List<string>();
		if (tabOffset == 0)
			tabLabels.Add(Localization.Text("sheet.tab.attributes"));
		tabLabels.Add(Localization.Text("sheet.tab.blessings"));
		tabLabels.Add(Localization.Text("sheet.tab.artifacts"));
		tabLabels.Add(Localization.Text("sheet.tab.skills"));
		_characterSheetTabs = UiComponents.Tabs(tabLabels.ToArray(), index => OnCharacterSheetTabChanged(index + tabOffset));
		_characterSheetTabs.Root.Name = "CharacterSheetTabs";
		_characterSheetTabs.Root.Alignment = BoxContainer.AlignmentMode.Center;
		for (var i = 0; i < _characterSheetTabs.Buttons.Count; i++)
		{
			var tabButton = _characterSheetTabs.Buttons[i];
			tabButton.Name = $"CharacterSheet{CharacterSheetTabIds[i + tabOffset]}Tab";
			tabButton.GuiInput += input => OnCharacterSheetItemInput(tabButton, input, false);
		}
		body.AddChild(_characterSheetTabs.Root);

		var split = new HBoxContainer
		{
			Name = "CharacterSheetSplit",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		split.AddThemeConstantOverride("separation", UiMetrics.S5);
		body.AddChild(split);

		var scroll = UiComponents.ScrollList(320.0f, out _characterSheetList);
		scroll.Name = "CharacterSheetScroll";
		_characterSheetList.Name = "CharacterSheetList";
		_characterSheetList.AddThemeConstantOverride("separation", UiMetrics.S1);
		// Margem interna: o anel de foco das linhas (expand 3 + brilho) não é cortado pelo ScrollContainer.
		scroll.RemoveChild(_characterSheetList);
		var listMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			listMargin.AddThemeConstantOverride(side, UiMetrics.FocusExpand + 3);
		listMargin.AddChild(_characterSheetList);
		scroll.AddChild(listMargin);
		split.AddChild(scroll);

		_characterSheetDetail = UiComponents.DetailPanel(DetailPanelDock.Right);
		_characterSheetDetail.Root.Name = "CharacterSheetDetail";
		_characterSheetDetail.Root.CustomMinimumSize = new Vector2(CharacterSheetDetailWidth, 0.0f);
		split.AddChild(_characterSheetDetail.Root);

		var footer = _characterSheetWindow.Footer;
		footer.Add(UiAction.Cancel, Localization.Text("ui.common.close"), CloseCharacterSheet, left: true);
		_characterSheetScrollHint = footer.Add(UiAction.Scroll, Localization.Text("sheet.hint.scroll_detail"));
		_characterSheetDeviceHandler = RefreshCharacterSheetScrollHint;
		UiInput.DeviceChanged += _characterSheetDeviceHandler;
		_characterSheetOverlay.TreeExiting += () => UiInput.DeviceChanged -= _characterSheetDeviceHandler;
		RefreshCharacterSheetScrollHint(UiInput.LastDevice);
	}

	private void RefreshCharacterSheetScrollHint(InputDevice device)
	{
		if (_characterSheetScrollHint != null && IsInstanceValid(_characterSheetScrollHint.Root))
			_characterSheetScrollHint.Visible = device != InputDevice.KeyboardMouse;
	}

	// ---------- Abrir / fechar (nome mantido: chamado pelo input "character_sheet") ----------

	private void ToggleCharacterSheet()
	{
		if (_characterSheetOverlay == null)
			return;
		if (UiModalStack.IsOpen(_characterSheetOverlay) || _characterSheetOverlay.Visible)
			CloseCharacterSheet();
		else
			OpenCharacterSheet(null);
	}

	private void OpenCharacterSheet(string tabId)
	{
		if (_characterSheetOverlay == null)
			return;

		var data = GetCharacterSheetData();
		var tab = Array.FindIndex(CharacterSheetTabIds, id => string.Equals(id, tabId, StringComparison.OrdinalIgnoreCase));
		if (tab < 0)
			tab = data.Blessings.Count > 0 ? 1 : 0;
		tab = Math.Max(tab, CharacterSheetTabOffset);
		_characterSheetTab = tab;
		_characterSheetTabs.Select(tab - CharacterSheetTabOffset, false);
		RebuildCharacterSheet(data, false);

		Control initialFocus = _characterSheetRows.Count > 0
			? _characterSheetRows[Mathf.Clamp(_characterSheetSelected[tab], 0, _characterSheetRows.Count - 1)]
			: _characterSheetTabs.Buttons[tab - CharacterSheetTabOffset];
		UiModalStack.Push(_characterSheetOverlay, true, initialFocus, CloseCharacterSheet);
		_characterSheetTabs.Arm();
		_characterSheetWindow.FadeIn();
	}

	private void CloseCharacterSheet()
	{
		if (_characterSheetOverlay != null)
			UiModalStack.Pop(_characterSheetOverlay);
		_characterSheetPreview = false;
	}

	// Preview (debug): dados fictícios só de exibição.
	internal void OpenCharacterSheetPreview(string tabId)
	{
		_characterSheetPreview = true;
		OpenCharacterSheet(tabId);
	}

	// Chamado por várias telas após mudanças (bênção, artefato, skill). Com a ficha fechada não faz nada:
	// ela é reconstruída ao abrir (guia §8.13, bug "refresh com a ficha fechada").
	private void RefreshCharacterSheet()
	{
		if (!IsVisible(_characterSheetOverlay) || _characterSheetList == null)
			return;
		RebuildCharacterSheet(GetCharacterSheetData(), true);
	}

	private void OnCharacterSheetTabChanged(int index)
	{
		if (index < CharacterSheetTabOffset || index >= CharacterSheetTabIds.Length)
			return;
		_characterSheetTab = index;
		RebuildCharacterSheet(GetCharacterSheetData(), false);
		if (_characterSheetRows.Count > 0)
			FocusCharacterSheetRow(Mathf.Clamp(_characterSheetSelected[index], 0, _characterSheetRows.Count - 1));
		else
			_characterSheetTabs.Buttons[index - CharacterSheetTabOffset].CallDeferred(Control.MethodName.GrabFocus);
	}

	// L1/R1 (Q/E) e esquerda/direita nas linhas trocam de aba; analógico direito rola o detalhe.
	// Ligado nas linhas e nas abas: no Godot 4 eventos de teclado/controle só chegam ao controle focado (não sobem à raiz).
	// AcceptEvent antes de trocar a aba: a troca remove a linha que recebeu o evento.
	private void OnCharacterSheetItemInput(Control source, InputEvent input, bool isRow)
	{
		if (!IsVisible(_characterSheetOverlay) || input is InputEventMouse || !source.IsInsideTree())
			return;

		if ((InputMap.HasAction("ui_tab_prev") && input.IsActionPressed("ui_tab_prev", true))
			|| (InputMap.HasAction("ui_tab_next") && input.IsActionPressed("ui_tab_next", true)))
		{
			// Echo e o quadro de abertura são ignorados pelo HandleInput, mas o evento é consumido do mesmo jeito.
			source.AcceptEvent();
			_characterSheetTabs.HandleInput(input);
			return;
		}
		if (isRow && (input.IsActionPressed("ui_left") || input.IsActionPressed("ui_right")))
		{
			var next = input.IsActionPressed("ui_right");
			source.AcceptEvent();
			if (next)
				_characterSheetTabs.Next();
			else
				_characterSheetTabs.Previous();
			return;
		}

		if (InputMap.HasAction("ui_scroll_down") && input.IsActionPressed("ui_scroll_down", true))
		{
			source.AcceptEvent();
			_characterSheetDetail.ScrollBy(UiMetrics.S5);
		}
		else if (InputMap.HasAction("ui_scroll_up") && input.IsActionPressed("ui_scroll_up", true))
		{
			source.AcceptEvent();
			_characterSheetDetail.ScrollBy(-UiMetrics.S5);
		}
	}

	// Fallback pela pilha (HUD.RouteModalShortcut) quando o foco não está numa linha/aba (ex.: após clique fora da lista):
	// abas por L1/R1 (Q/E) e rolagem do detalhe pelo analógico direito.
	private bool HandleCharacterSheetShortcut(InputEvent input)
	{
		if (!IsVisible(_characterSheetOverlay) || input is InputEventMouse)
			return false;
		if (_characterSheetTabs != null && _characterSheetTabs.HandleInput(input))
			return true;
		if (_characterSheetDetail == null)
			return false;
		if (InputMap.HasAction("ui_scroll_down") && input.IsActionPressed("ui_scroll_down", true))
		{
			_characterSheetDetail.ScrollBy(UiMetrics.S5);
			return true;
		}
		if (InputMap.HasAction("ui_scroll_up") && input.IsActionPressed("ui_scroll_up", true))
		{
			_characterSheetDetail.ScrollBy(-UiMetrics.S5);
			return true;
		}
		return false;
	}

	// ---------- Lista ----------

	private void RebuildCharacterSheet(CharacterSheetData data, bool keepFocus)
	{
		_characterSheetCoinsLabel.Text = data.Balance.ToString();
		_characterSheetCoinGlyph.Visible = !data.CursedBalance;
		_characterSheetCursedGlyph.Visible = data.CursedBalance;
		_characterSheetCurrencyName.Text = Localization.Text(data.CursedBalance ? "hud.cursed_blood_name" : "hud.coins_label");
		if (data.CursedBalance)
		{
			_characterSheetCoinsLabel.AddThemeColorOverride("font_color", UiPalette.CursedBlood);
			_characterSheetCurrencyName.AddThemeColorOverride("font_color", UiPalette.CursedBlood);
		}
		else
		{
			_characterSheetCoinsLabel.RemoveThemeColorOverride("font_color");
			_characterSheetCurrencyName.RemoveThemeColorOverride("font_color");
		}

		var hadFocus = keepFocus && ControllerUiSupport.IsFocusInside(_characterSheetList);
		foreach (var child in _characterSheetList.GetChildren())
		{
			_characterSheetList.RemoveChild(child);
			child.QueueFree();
		}
		_characterSheetRows.Clear();
		_characterSheetEntries.Clear();
		_characterSheetRowEntries.Clear();

		switch (_characterSheetTab)
		{
			case 0: BuildCharacterSheetAttributes(data); break;
			case 1: BuildCharacterSheetBlessings(data); break;
			case 2: BuildCharacterSheetArtifacts(data); break;
			default: BuildCharacterSheetSkills(data); break;
		}

		string section = null;
		foreach (var entry in _characterSheetEntries)
		{
			if (!string.IsNullOrEmpty(entry.Section) && entry.Section != section)
			{
				section = entry.Section;
				AddCharacterSheetHeader(section);
			}
			if (entry.Note)
			{
				AddCharacterSheetNote(entry.Title);
				continue;
			}
			var row = CreateCharacterSheetRow(entry, _characterSheetRows.Count);
			_characterSheetList.AddChild(row);
			_characterSheetRows.Add(row);
			_characterSheetRowEntries.Add(entry);
		}

		LinkCharacterSheetFocus();

		if (_characterSheetRows.Count == 0)
		{
			_characterSheetDetail.Clear();
			return;
		}
		var selected = Mathf.Clamp(_characterSheetSelected[_characterSheetTab], 0, _characterSheetRows.Count - 1);
		SelectCharacterSheetEntry(selected);
		if (hadFocus)
			FocusCharacterSheetRow(selected);
	}

	private void AddCharacterSheetHeader(string text)
	{
		var header = UiComponents.MakeLabel(text, UiVariation.SectionGoldLabel);
		header.Name = $"Section{_characterSheetList.GetChildCount()}";
		if (_characterSheetList.GetChildCount() > 0)
			header.CustomMinimumSize = new Vector2(0.0f, 44.0f);
		header.VerticalAlignment = VerticalAlignment.Bottom;
		_characterSheetList.AddChild(header);
	}

	private void AddCharacterSheetNote(string text)
	{
		var note = UiComponents.MakeLabel(text, UiVariation.SecondaryLabel, true);
		note.CustomMinimumSize = new Vector2(0.0f, 36.0f);
		note.VerticalAlignment = VerticalAlignment.Center;
		_characterSheetList.AddChild(note);
	}

	private void AddCharacterSheetEmptyNote(string section, string text)
	{
		_characterSheetEntries.Add(new CharacterSheetEntry { Section = section, Title = text, Note = true });
	}

	private Button CreateCharacterSheetRow(CharacterSheetEntry entry, int index)
	{
		var row = new Button
		{
			Name = $"Entry{index}",
			ThemeTypeVariation = UiVariation.SelectButton,
			ToggleMode = true,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, CharacterSheetRowHeight)
		};

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", UiMetrics.S3);
		margin.AddThemeConstantOverride("margin_right", UiMetrics.S4);
		margin.AddThemeConstantOverride("margin_top", UiMetrics.S1);
		margin.AddThemeConstantOverride("margin_bottom", UiMetrics.S1);
		row.AddChild(margin);

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", UiMetrics.S3);
		margin.AddChild(line);

		if (entry.BuildIcon != null)
		{
			var icon = entry.BuildIcon();
			icon.MouseFilter = Control.MouseFilterEnum.Ignore;
			icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			line.AddChild(icon);
		}

		var text = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		text.AddThemeConstantOverride("separation", 0);
		line.AddChild(text);

		var name = UiComponents.MakeLabel(entry.Title, UiVariation.BodyLabel);
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (entry.TitleColor.HasValue)
			name.AddThemeColorOverride("font_color", entry.TitleColor.Value);
		text.AddChild(name);

		var meta = UiComponents.MakeLabel(entry.Meta, UiVariation.SecondaryLabel);
		meta.ClipText = true;
		meta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		meta.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		meta.Visible = !string.IsNullOrEmpty(entry.Meta);
		text.AddChild(meta);

		if (!string.IsNullOrEmpty(entry.BadgeText))
		{
			var badge = UiComponents.Badge(entry.BadgeText, entry.BadgeColor ?? UiPalette.Gold);
			badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			line.AddChild(badge);
		}
		if (!string.IsNullOrEmpty(entry.ValueText))
		{
			var value = UiComponents.MakeLabel(entry.ValueText, UiVariation.SectionGoldLabel);
			value.HorizontalAlignment = HorizontalAlignment.Right;
			value.VerticalAlignment = VerticalAlignment.Center;
			value.CustomMinimumSize = new Vector2(56.0f, 0.0f);
			value.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			line.AddChild(value);
		}

		row.FocusEntered += () => SelectCharacterSheetEntry(index);
		row.MouseEntered += () => SelectCharacterSheetEntry(index);
		row.Pressed += () => SelectCharacterSheetEntry(index, true);
		row.GuiInput += input => OnCharacterSheetItemInput(row, input, true);
		return row;
	}

	// Cima/baixo explícitos; cima na primeira linha vai à aba ativa; esquerda/direita ficam na linha (o GuiInput troca a aba).
	private void LinkCharacterSheetFocus()
	{
		var tabButton = _characterSheetTabs.Buttons[_characterSheetTab - CharacterSheetTabOffset];
		for (var i = 0; i < _characterSheetRows.Count; i++)
		{
			var row = _characterSheetRows[i];
			row.FocusNeighborTop = row.GetPathTo(i > 0 ? _characterSheetRows[i - 1] : tabButton);
			row.FocusNeighborBottom = row.GetPathTo(i < _characterSheetRows.Count - 1 ? _characterSheetRows[i + 1] : row);
			row.FocusNeighborLeft = row.GetPathTo(row);
			row.FocusNeighborRight = row.GetPathTo(row);
			row.FocusPrevious = row.FocusNeighborTop;
			row.FocusNext = row.FocusNeighborBottom;
		}
		foreach (var button in _characterSheetTabs.Buttons)
			button.FocusNeighborBottom = button.GetPathTo(_characterSheetRows.Count > 0 ? _characterSheetRows[0] : button);
	}

	private void FocusCharacterSheetRow(int index)
	{
		if (index < 0 || index >= _characterSheetRows.Count)
			return;
		_characterSheetRows[index].CallDeferred(Control.MethodName.GrabFocus);
	}

	private void SelectCharacterSheetEntry(int index, bool force = false)
	{
		if (index < 0 || index >= _characterSheetRowEntries.Count || index >= _characterSheetRows.Count)
			return;
		for (var i = 0; i < _characterSheetRows.Count; i++)
			_characterSheetRows[i].SetPressedNoSignal(i == index);
		if (index == _characterSheetSelected[_characterSheetTab] && !force && _characterSheetDetail.Header.Visible)
			return;
		_characterSheetSelected[_characterSheetTab] = index;
		_characterSheetDetail.Show(_characterSheetRowEntries[index].Detail);
	}

	// ---------- Abas ----------

	private void BuildCharacterSheetAttributes(CharacterSheetData data)
	{
		var section = Localization.Text("sheet.section.attributes");
		AddCharacterSheetAttribute(section, CharacterAttribute.Strength, Localization.Text("sheet.attr.strength"),
			Localization.TextFormat("sheet.attr.strength.effect", FormatSheetPercent(CharacterSheet.GetDamageMultiplier())),
			Localization.Text("sheet.attr.strength.desc"));
		AddCharacterSheetAttribute(section, CharacterAttribute.Dexterity, Localization.Text("sheet.attr.dexterity"),
			Localization.TextFormat("sheet.attr.dexterity.effect", FormatSheetPercent(CharacterSheet.GetMoveSpeedMultiplier()), CharacterSheet.GetMaxStaminaBonus()),
			Localization.Text("sheet.attr.dexterity.desc"));
		AddCharacterSheetAttribute(section, CharacterAttribute.Vitality, Localization.Text("sheet.attr.vitality"),
			Localization.TextFormat("sheet.attr.vitality.effect", CharacterSheet.GetMaxHealthBonus()),
			Localization.Text("sheet.attr.vitality.desc"));
		AddCharacterSheetAttribute(section, CharacterAttribute.Resolve, Localization.Text("sheet.attr.resolve"),
			Localization.TextFormat("sheet.attr.resolve.effect", FormatSheetPercent(CharacterSheet.GetFuryMultiplier())),
			Localization.Text("sheet.attr.resolve.desc"));
		AddCharacterSheetAttribute(section, CharacterAttribute.Bloodcraft, Localization.Text("sheet.attr.bloodcraft"),
			Localization.TextFormat("sheet.attr.bloodcraft.effect", FormatSheetPercent(CharacterSheet.GetBloodcraftMultiplier())),
			Localization.Text("sheet.attr.bloodcraft.desc"));

		var stats = Localization.Text("sheet.section.stats");
		AddCharacterSheetStat(stats, Localization.Text("sheet.stat.damage"), FormatSheetMultiplier(CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier),
			Localization.TextFormat("sheet.stat.damage.meta", FormatSheetMultiplier(CharacterSheet.GetDamageMultiplier()), FormatSheetMultiplier(RunState.DamageMultiplier)),
			Localization.Text("sheet.stat.damage.desc"));
		AddCharacterSheetStat(stats, Localization.Text("sheet.stat.max_health"), $"+{CharacterSheet.GetMaxHealthBonus() + RunState.MaxHealthBonus:0}",
			Localization.TextFormat("sheet.stat.max_health.meta", CharacterSheet.GetMaxHealthBonus(), RunState.MaxHealthBonus),
			Localization.Text("sheet.stat.max_health.desc"));
		AddCharacterSheetStat(stats, Localization.Text("sheet.stat.speed"), FormatSheetMultiplier(CharacterSheet.GetMoveSpeedMultiplier()), null,
			Localization.Text("sheet.stat.speed.desc"));
		AddCharacterSheetStat(stats, Localization.Text("sheet.stat.fury"), FormatSheetMultiplier(CharacterSheet.GetFuryMultiplier()), null,
			Localization.Text("sheet.stat.fury.desc"));
		if (OS.IsDebugBuild())
			AddCharacterSheetStat("DEV", "Dano tuning", FormatSheetMultiplier(GameTuning.GetDamageMultiplier()), null,
				"Multiplicador global de dano do tuning (só em build de debug).");
	}

	private void AddCharacterSheetAttribute(string section, CharacterAttribute attribute, string name, string effect, string description)
	{
		var value = CharacterSheet.GetAttribute(attribute);
		_characterSheetEntries.Add(new CharacterSheetEntry
		{
			Section = section,
			Title = name,
			Meta = effect,
			ValueText = value.ToString(),
			Detail = new DetailData
			{
				Title = name,
				BadgeText = value.ToString(),
				BadgeColor = UiPalette.Gold,
				Meta = effect,
				BodyBbcode = EscapeSheetBbcode(description)
			}
		});
	}

	private void AddCharacterSheetStat(string section, string name, string value, string meta, string description)
	{
		_characterSheetEntries.Add(new CharacterSheetEntry
		{
			Section = section,
			Title = name,
			Meta = meta,
			ValueText = value,
			Detail = new DetailData
			{
				Title = name,
				BadgeText = value,
				BadgeColor = UiPalette.Gold,
				Meta = meta,
				BodyBbcode = EscapeSheetBbcode(description)
			}
		});
	}

	private void BuildCharacterSheetBlessings(CharacterSheetData data)
	{
		var section = Localization.TextFormat("sheet.section.blessings", data.Blessings.Count);
		if (data.Blessings.Count == 0)
			AddCharacterSheetEmptyNote(section, Localization.Text("sheet.empty.blessings"));
		foreach (var record in data.Blessings)
		{
			var hasDefinition = BloodBlessingCatalog.TryGet(record.Id, out var blessing);
			var name = hasDefinition && !string.IsNullOrEmpty(blessing.DisplayName) ? blessing.DisplayName : (string.IsNullOrEmpty(record.Name) ? record.Id : record.Name);
			var rarityLabel = UiComponents.RarityLabel(record.Rarity);
			var level = Localization.TextFormat("ui.common.level", record.Level);
			var lineage = hasDefinition ? Localization.TextFormat("offer.blessing.lineage", FormatBlessingLineage(blessing)) : null;
			var captured = record;
			_characterSheetEntries.Add(new CharacterSheetEntry
			{
				Section = section,
				BuildIcon = () => UiComponents.BlessingIcon(captured, UiMetrics.IconM),
				Title = name,
				TitleColor = UiPalette.Rarity(record.Rarity),
				Meta = lineage != null ? $"{lineage}  ·  {level}" : level,
				BadgeText = rarityLabel,
				BadgeColor = UiPalette.Rarity(record.Rarity),
				Detail = BuildBlessingSheetDetail(record, hasDefinition ? blessing : null, name, rarityLabel, level, lineage)
			});
		}
	}

	private DetailData BuildBlessingSheetDetail(BloodBlessingRecord record, BloodBlessingDefinition blessing, string name, string rarityLabel, string level, string lineage)
	{
		if (blessing == null)
			return new DetailData { Title = name, RarityId = record.Rarity, BadgeText = rarityLabel, Meta = level };

		var metaParts = new List<string> { lineage, level };
		var attacks = AffectedAttackLabels(blessing);
		if (attacks.Count > 0)
			metaParts.Add(string.Join(", ", attacks));

		var body = new StringBuilder();
		body.Append(FormatEffectKeywords(FormatBloodBlessingDescription(blessing, record.EffectivePowerMultiplier, true)));
		var effects = FormatSheetBlessingEffects(blessing, record.EffectivePowerMultiplier);
		if (effects.Length > 0)
		{
			body.Append("\n\n[color=#").Append(UiPalette.Gold.ToHtml(false)).Append(']')
				.Append(Localization.Text("sheet.detail.effects")).Append("[/color]\n").Append(effects);
		}

		return new DetailData
		{
			Icon = LoadBlessingIcon(blessing),
			IconText = name,
			Title = name,
			RarityId = record.Rarity,
			BadgeText = rarityLabel,
			Meta = string.Join("  ·  ", metaParts),
			BodyBbcode = body.ToString(),
			Flavor = blessing.FlavorText
		};
	}

	// Uma linha por efeito, com o mesmo valor efetivo (raridade × nível × tuning) usado na descrição.
	private static string FormatSheetBlessingEffects(BloodBlessingDefinition blessing, float effectivePowerMultiplier)
	{
		var lines = new List<string>();
		for (var i = 0; i < blessing.Effects.Length; i++)
		{
			var effect = blessing.Effects[i];
			var baseValue = CompendiumTuning.GetFloat($"blessing.{blessing.Id}.effect.{i}.value", effect.Value);
			var value = effect.BooleanValue ? 1.0f : GetDisplayedBlessingValue(effect, baseValue * effectivePowerMultiplier);
			lines.Add($"• {EscapeSheetBbcode(ArtifactEffectRegistry.FormatEffect(effect.Type, effect.Operation, value))}");
		}
		return string.Join("\n", lines);
	}

	// Aba Artefatos: só os da run atual (comprados na Loja da Câmara de Hemomancia). Não há coleção permanente nem slots.
	private void BuildCharacterSheetArtifacts(CharacterSheetData data)
	{
		var runSection = Localization.TextFormat("sheet.section.run_artifacts", data.RunArtifacts.Count);
		if (data.RunArtifacts.Count == 0)
			AddCharacterSheetEmptyNote(runSection, Localization.Text("sheet.empty.run_artifacts"));
		foreach (var record in data.RunArtifacts)
			AddCharacterSheetArtifact(runSection, record, Localization.Text("sheet.artifact.origin.shop"));
	}

	private void AddCharacterSheetArtifact(string section, ArtifactRecord record, string origin)
	{
		var definition = FindSheetArtifactDefinition(record);
		var name = definition != null ? definition.DisplayName : ArtifactCatalog.DisplayNameFor(record);
		var rarity = definition?.Rarity;
		var slot = FormatSheetArtifactSlot(definition != null ? definition.Slot : record.Slot);
		var meta = string.IsNullOrEmpty(slot) ? origin : $"{slot}  ·  {origin}";
		var icon = definition != null ? LoadArtifactIcon(definition) : null;

		var body = new StringBuilder();
		if (definition != null && !string.IsNullOrWhiteSpace(definition.Description))
			body.Append(EscapeSheetBbcode(definition.Description)).Append("\n\n");
		body.Append("[color=#").Append(UiPalette.Gold.ToHtml(false)).Append(']')
			.Append(Localization.Text("sheet.detail.effects")).Append("[/color]\n");
		// "Impacto dos artefatos" da ficha antiga fica fundido aqui. Com definição, usa o mesmo cálculo da Loja
		// (valor × ShopPowerMultiplier, como RunState aplica); o Amount do registro não tem esse multiplicador.
		if (definition != null && definition.Effects != null && definition.Effects.Length > 0)
		{
			for (var i = 0; i < definition.Effects.Length; i++)
			{
				var effect = definition.Effects[i];
				var value = CompendiumTuning.GetNumber($"{definition.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.ShopPowerMultiplier;
				if (i > 0)
					body.Append('\n');
				body.Append("• ").Append(EscapeSheetBbcode(ArtifactEffectRegistry.FormatEffect(effect.Type, effect.Operation, value)));
			}
		}
		else if (!string.IsNullOrEmpty(record.Effect))
			body.Append("• ").Append(EscapeSheetBbcode(FormatArtifactRecordEffect(record)));
		else
			body.Append(Localization.Text("sheet.detail.unknown_effects"));

		_characterSheetEntries.Add(new CharacterSheetEntry
		{
			Section = section,
			BuildIcon = () => UiComponents.IconOrMonogram(icon, name, UiMetrics.IconM),
			Title = name,
			TitleColor = rarity != null ? UiPalette.Rarity(rarity) : null,
			Meta = meta,
			BadgeText = rarity != null ? UiComponents.RarityLabel(rarity) : null,
			BadgeColor = rarity != null ? UiPalette.Rarity(rarity) : null,
			Detail = new DetailData
			{
				Icon = icon,
				IconText = name,
				Title = name,
				RarityId = rarity,
				BadgeText = rarity != null ? UiComponents.RarityLabel(rarity) : null,
				Meta = meta,
				BodyBbcode = body.ToString(),
				Flavor = definition?.FlavorText
			}
		});
	}

	private void BuildCharacterSheetSkills(CharacterSheetData data)
	{
		var ordered = data.Skills
			.Select(skill => (skill.Id, skill.Rank, Definition: SkillCatalog.TryGet(skill.Id, out var definition) ? definition : null))
			.OrderBy(skill => skill.Definition == null ? 2 : skill.Definition.Tree == SkillTree.Bestialis ? 0 : 1)
			.ThenBy(skill => skill.Definition?.Tier ?? 0)
			.ToList();
		if (ordered.Count == 0)
			AddCharacterSheetEmptyNote(Localization.Text("sheet.section.skills"),
				Localization.Text("sheet.empty.skills"));

		foreach (var skill in ordered)
		{
			var definition = skill.Definition;
			if (definition == null)
			{
				_characterSheetEntries.Add(new CharacterSheetEntry
				{
					Section = Localization.Text("sheet.section.skills_other"),
					BuildIcon = () => UiComponents.Monogram(skill.Id, UiMetrics.IconM),
					Title = skill.Id,
					Meta = Localization.TextFormat("sheet.skill.rank", skill.Rank),
					Detail = new DetailData { Title = skill.Id, Meta = Localization.TextFormat("sheet.skill.rank", skill.Rank) }
				});
				continue;
			}

			var treeName = SkillTreeName(definition.Tree);
			var isMax = skill.Rank >= definition.MaxRank;
			var rankText = isMax ? Localization.Text("ui.common.max") : $"{skill.Rank}/{definition.MaxRank}";
			var meta = Localization.TextFormat("sheet.skill.meta", treeName, skill.Rank, definition.MaxRank);
			var iconText = string.IsNullOrWhiteSpace(definition.IconText) ? definition.DisplayName : definition.IconText;
			_characterSheetEntries.Add(new CharacterSheetEntry
			{
				Section = treeName,
				BuildIcon = () => UiComponents.Monogram(iconText, UiMetrics.IconM),
				Title = definition.DisplayName,
				Meta = meta,
				BadgeText = rankText,
				BadgeColor = isMax ? UiPalette.GoldBright : UiPalette.Gold,
				Detail = new DetailData
				{
					IconText = iconText,
					Title = definition.DisplayName,
					BadgeText = rankText,
					BadgeColor = isMax ? UiPalette.GoldBright : UiPalette.Gold,
					Meta = meta,
					BodyBbcode = EscapeSheetBbcode(definition.Description),
					Flavor = definition.FlavorText
				}
			});
		}
	}

	// ---------- Dados ----------

	private CharacterSheetData GetCharacterSheetData()
	{
		if (_characterSheetPreview && OS.IsDebugBuild())
			return BuildCharacterSheetPreviewData();

		var data = new CharacterSheetData
		{
			// Mesmo critério do indicador do HUD: hub = Sangue Amaldiçoado permanente; run = Moedas de Sangue da run.
			CursedBalance = ShowsCursedBloodIndicator(),
			Blessings = RunState.RunBlessings.ToList(),
			RunArtifacts = RunState.RunArtifacts.ToList()
		};
		data.Balance = data.CursedBalance ? CharacterSheet.CursedBlood : RunState.RunBloodCoins;
		foreach (var skillId in CharacterSheet.AcquiredSkills)
			data.Skills.Add((skillId, CharacterSheet.GetSkillRank(skillId)));
		return data;
	}

	// Anexo A: 8 bênçãos (todas as raridades de bênção, níveis 1–3), artefatos Common/Epic/Mythic da run, 500 moedas.
	// Só exibição: não chama RunState.AddBloodBlessing/AddTemporaryArtifact (que marcariam descobertas no save).
	private CharacterSheetData BuildCharacterSheetPreviewData()
	{
		var data = new CharacterSheetData { CursedBalance = ShowsCursedBloodIndicator() };
		data.Balance = data.CursedBalance ? Math.Max(CharacterSheet.CursedBlood, 120) : 500;

		var rarities = new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Stirps", "Common", "Rare" };
		var levels = new[] { 1, 2, 3, 1, 2, 1, 3, 2 };
		var blessings = BloodBlessingCatalog.All;
		for (var i = 0; i < rarities.Length && blessings.Count > 0; i++)
		{
			var definition = blessings[(i * 5) % blessings.Count];
			var power = BloodBlessingCatalog.AllRarities.FirstOrDefault(rarity => rarity.Id.Equals(rarities[i], StringComparison.OrdinalIgnoreCase)
				|| rarity.DisplayName.Equals(rarities[i], StringComparison.OrdinalIgnoreCase))?.PowerMultiplier ?? 1.0f;
			data.Blessings.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, 1, rarities[i], power, levels[i]));
		}

		foreach (var rarity in new[] { "common", "epic", "mythic" })
		{
			var definition = ArtifactCatalog.All.FirstOrDefault(artifact => string.Equals(artifact.Rarity, rarity, StringComparison.OrdinalIgnoreCase));
			if (definition != null)
				data.RunArtifacts.Add(ArtifactCatalog.CreateRecord(definition));
		}

		var skills = SkillCatalog.All.Where(skill => skill.IsImplemented).ToList();
		var bestialis = skills.Where(skill => skill.Tree == SkillTree.Bestialis).Take(2).ToList();
		var humanitas = skills.Where(skill => skill.Tree == SkillTree.Humanitas).Take(2).ToList();
		for (var i = 0; i < bestialis.Count; i++)
			data.Skills.Add((bestialis[i].Id, i == 0 ? bestialis[i].MaxRank : 1));
		for (var i = 0; i < humanitas.Count; i++)
			data.Skills.Add((humanitas[i].Id, Math.Min(2, humanitas[i].MaxRank)));
		return data;
	}

	// ---------- Formatação ----------

	private static ArtifactDefinition FindSheetArtifactDefinition(ArtifactRecord record)
	{
		if (!string.IsNullOrWhiteSpace(record.Id) && ArtifactCatalog.TryGet(record.Id, out var byId))
			return byId;
		// Saves antigos guardam só o SaveName; resolve by SaveName ou exibe o nome gravado.
		return string.IsNullOrWhiteSpace(record.Name)
			? null
			: ArtifactCatalog.TryGetByRecordName(record.Name, out var found) ? found : null;
	}

	// ---------- Texto e ícone de artefato (ficha e Loja) ----------

	private const string ArtifactGenericIconPath = "res://images/generic.png";

	// generic.png é o placeholder do catálogo: o card usa monograma (guia §3.4).
	private static Texture2D LoadArtifactIcon(ArtifactDefinition artifact)
	{
		if (artifact == null || string.IsNullOrEmpty(artifact.IconPath) || artifact.IconPath == ArtifactGenericIconPath)
			return null;
		return ResourceLoader.Exists(artifact.IconPath) ? ResourceLoader.Load<Texture2D>(artifact.IconPath) : null;
	}

	// Descrição + efeitos (valores com o mesmo cálculo do compêndio: ajuste salvo × poder da Loja).
	private static string ArtifactBodyBbcode(ArtifactDefinition artifact)
	{
		var gold = UiPalette.Gold.ToHtml(false);
		var effects = FormatArtifactEffectList(artifact);
		return $"{EscapeArtifactBbcode(artifact.Description)}\n[color=#{gold}]{Localization.Text("ui.common.effects")}:[/color] {EscapeArtifactBbcode(effects)}";
	}

	// Formato único de efeito ("Nome: +20%") na Loja e na ficha: ArtifactEffectRegistry.FormatEffect.
	private static string FormatArtifactEffectList(ArtifactDefinition artifact)
	{
		if (artifact.Effects == null || artifact.Effects.Length == 0)
			return Localization.Text("ui.common.none");

		var parts = new List<string>();
		for (var i = 0; i < artifact.Effects.Length; i++)
		{
			var effect = artifact.Effects[i];
			var value = CompendiumTuning.GetNumber($"{artifact.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.ShopPowerMultiplier;
			parts.Add(ArtifactEffectRegistry.FormatEffect(effect.Type, effect.Operation, value));
		}
		return string.Join(", ", parts);
	}

	// Registro antigo sem definição no catálogo: só tipo e valor (operação desconhecida, tratada como soma).
	private static string FormatArtifactRecordEffect(ArtifactRecord record)
	{
		return ArtifactEffectRegistry.FormatEffect(record.Effect, "AddFlat", record.Amount);
	}

	private static string EscapeArtifactBbcode(string text)
	{
		return (text ?? "").Replace("[", "[lb]");
	}

	private static string FormatSheetArtifactSlot(string slot) => (slot ?? "").ToLowerInvariant() switch
	{
		"passive" => Localization.Text("ui.artifact_slot.passive"),
		"active" => Localization.Text("ui.artifact_slot.active"),
		_ => slot ?? ""
	};

	// Nome exibido da árvore de skills (ficha, Fonte de Sangue e Dicionário).
	private static string SkillTreeName(SkillTree tree) => tree == SkillTree.Bestialis
		? Localization.Text("ui.skill_tree.bestialis")
		: Localization.Text("ui.skill_tree.humanitas");

	private static string FormatSheetPercent(float multiplier) => $"{(multiplier - 1.0f) * 100.0f:+0.#;-0.#;+0}%";

	private static string FormatSheetMultiplier(float multiplier) => $"×{multiplier:0.00}";

	private static string EscapeSheetBbcode(string text) => (text ?? "").Replace("[", "[lb]");
}

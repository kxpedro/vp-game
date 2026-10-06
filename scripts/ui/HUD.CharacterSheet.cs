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
		public int Coins;
		public List<BloodBlessingRecord> Blessings = new();
		public List<ArtifactRecord> RunArtifacts = new();
		public List<ArtifactRecord> Collection = new();
		public string[] EquippedSlots = Array.Empty<string>();
		public List<(string Id, int Rank)> Skills = new();
	}

	private UiWindow _characterSheetWindow;
	private UiTabs _characterSheetTabs;
	private UiDetailPanel _characterSheetDetail;
	private VBoxContainer _characterSheetList;
	private Label _characterSheetCoinsLabel;
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
		_characterSheetWindow = UiComponents.Window(root, "CharacterSheetOverlay", WindowSize.Full, UiComponents.TextOr("sheet.title", "Ficha do personagem"));
		_characterSheetOverlay = _characterSheetWindow.Root;
		_characterSheetOverlay.Visible = false;

		// Moedas de Sangue no cabeçalho (§3.1 HeaderRight).
		var coins = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		coins.AddThemeConstantOverride("separation", UiMetrics.S2);
		coins.AddChild(UiComponents.CoinGlyph(UiMetrics.IconGlyph));
		_characterSheetCoinsLabel = UiComponents.MakeLabel("0", UiVariation.SectionGoldLabel);
		_characterSheetCoinsLabel.Name = "CharacterSheetCoins";
		coins.AddChild(_characterSheetCoinsLabel);
		_characterSheetWindow.HeaderRight.AddChild(coins);

		var body = _characterSheetWindow.Body;
		_characterSheetTabs = UiComponents.Tabs(new[]
		{
			UiComponents.TextOr("sheet.tab.attributes", "Atributos"),
			UiComponents.TextOr("sheet.tab.blessings", "Bênçãos"),
			UiComponents.TextOr("sheet.tab.artifacts", "Artefatos"),
			UiComponents.TextOr("sheet.tab.skills", "Habilidades")
		}, OnCharacterSheetTabChanged);
		_characterSheetTabs.Root.Name = "CharacterSheetTabs";
		_characterSheetTabs.Root.Alignment = BoxContainer.AlignmentMode.Center;
		for (var i = 0; i < _characterSheetTabs.Buttons.Count; i++)
		{
			var tabButton = _characterSheetTabs.Buttons[i];
			tabButton.Name = $"CharacterSheet{CharacterSheetTabIds[i]}Tab";
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
		footer.Add(UiAction.Cancel, UiComponents.TextOr("ui.common.close", "Fechar"), CloseCharacterSheet, left: true);
		_characterSheetScrollHint = footer.Add(UiAction.Scroll, UiComponents.TextOr("sheet.hint.scroll_detail", "Rolar detalhes"));
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
		_characterSheetTab = tab;
		_characterSheetTabs.Select(tab, false);
		RebuildCharacterSheet(data, false);

		Control initialFocus = _characterSheetRows.Count > 0
			? _characterSheetRows[Mathf.Clamp(_characterSheetSelected[tab], 0, _characterSheetRows.Count - 1)]
			: _characterSheetTabs.Buttons[tab];
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
		if (index < 0 || index >= CharacterSheetTabIds.Length)
			return;
		_characterSheetTab = index;
		RebuildCharacterSheet(GetCharacterSheetData(), false);
		if (_characterSheetRows.Count > 0)
			FocusCharacterSheetRow(Mathf.Clamp(_characterSheetSelected[index], 0, _characterSheetRows.Count - 1));
		else
			_characterSheetTabs.Buttons[index].CallDeferred(Control.MethodName.GrabFocus);
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
		_characterSheetCoinsLabel.Text = data.Coins.ToString();

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
		var tabButton = _characterSheetTabs.Buttons[_characterSheetTab];
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
		var section = UiComponents.TextOr("sheet.section.attributes", "Atributos");
		AddCharacterSheetAttribute(section, CharacterAttribute.Strength, UiComponents.TextOr("sheet.attr.strength", "Força"),
			string.Format(UiComponents.TextOr("sheet.attr.strength.effect", "Dano {0}"), FormatSheetPercent(CharacterSheet.GetDamageMultiplier())),
			UiComponents.TextOr("sheet.attr.strength.desc", "Aumenta o dano de todos os ataques em 3% por ponto."));
		AddCharacterSheetAttribute(section, CharacterAttribute.Dexterity, UiComponents.TextOr("sheet.attr.dexterity", "Destreza"),
			string.Format(UiComponents.TextOr("sheet.attr.dexterity.effect", "Velocidade {0} · Vigor +{1:0}"), FormatSheetPercent(CharacterSheet.GetMoveSpeedMultiplier()), CharacterSheet.GetMaxStaminaBonus()),
			UiComponents.TextOr("sheet.attr.dexterity.desc", "Aumenta a velocidade de movimento em 1,5% e o vigor máximo em 5 por ponto."));
		AddCharacterSheetAttribute(section, CharacterAttribute.Vitality, UiComponents.TextOr("sheet.attr.vitality", "Vitalidade"),
			string.Format(UiComponents.TextOr("sheet.attr.vitality.effect", "Vida máxima +{0:0}"), CharacterSheet.GetMaxHealthBonus()),
			UiComponents.TextOr("sheet.attr.vitality.desc", "Aumenta a vida máxima em 6 por ponto."));
		AddCharacterSheetAttribute(section, CharacterAttribute.Resolve, UiComponents.TextOr("sheet.attr.resolve", "Determinação"),
			string.Format(UiComponents.TextOr("sheet.attr.resolve.effect", "Fúria {0}"), FormatSheetPercent(CharacterSheet.GetFuryMultiplier())),
			UiComponents.TextOr("sheet.attr.resolve.desc", "Aumenta o ganho de Fúria em 3% por ponto."));
		AddCharacterSheetAttribute(section, CharacterAttribute.Bloodcraft, UiComponents.TextOr("sheet.attr.bloodcraft", "Sangue"),
			string.Format(UiComponents.TextOr("sheet.attr.bloodcraft.effect", "Artes de sangue {0}"), FormatSheetPercent(CharacterSheet.GetBloodcraftMultiplier())),
			UiComponents.TextOr("sheet.attr.bloodcraft.desc", "Fortalece as artes de sangue em 3% por ponto."));

		var stats = UiComponents.TextOr("sheet.section.stats", "Totais da run");
		AddCharacterSheetStat(stats, UiComponents.TextOr("sheet.stat.damage", "Dano"), FormatSheetMultiplier(CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier),
			string.Format(UiComponents.TextOr("sheet.stat.damage.meta", "Atributos {0} · Run {1}"), FormatSheetMultiplier(CharacterSheet.GetDamageMultiplier()), FormatSheetMultiplier(RunState.DamageMultiplier)),
			UiComponents.TextOr("sheet.stat.damage.desc", "Multiplicador de dano combinado: atributos permanentes × bônus desta run (artefatos e bênçãos)."));
		AddCharacterSheetStat(stats, UiComponents.TextOr("sheet.stat.max_health", "Vida máxima extra"), $"+{CharacterSheet.GetMaxHealthBonus() + RunState.MaxHealthBonus:0}",
			string.Format(UiComponents.TextOr("sheet.stat.max_health.meta", "Atributos +{0:0} · Artefatos +{1:0}"), CharacterSheet.GetMaxHealthBonus(), RunState.MaxHealthBonus),
			UiComponents.TextOr("sheet.stat.max_health.desc", "Vida extra somada à vida base de Virgil."));
		AddCharacterSheetStat(stats, UiComponents.TextOr("sheet.stat.speed", "Velocidade"), FormatSheetMultiplier(CharacterSheet.GetMoveSpeedMultiplier()), null,
			UiComponents.TextOr("sheet.stat.speed.desc", "Multiplicador de velocidade de movimento vindo dos atributos."));
		AddCharacterSheetStat(stats, UiComponents.TextOr("sheet.stat.fury", "Fúria"), FormatSheetMultiplier(CharacterSheet.GetFuryMultiplier()), null,
			UiComponents.TextOr("sheet.stat.fury.desc", "Multiplicador de ganho de Fúria vindo dos atributos."));
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
		var section = string.Format(UiComponents.TextOr("sheet.section.blessings", "Bênçãos de Sangue ({0})"), data.Blessings.Count);
		if (data.Blessings.Count == 0)
			AddCharacterSheetEmptyNote(section, UiComponents.TextOr("sheet.empty.blessings", "Nenhuma Bênção de Sangue nesta run. Elas são oferecidas ao limpar salas."));
		foreach (var record in data.Blessings)
		{
			var hasDefinition = BloodBlessingCatalog.TryGet(record.Id, out var blessing);
			var name = hasDefinition && !string.IsNullOrEmpty(blessing.DisplayName) ? blessing.DisplayName : (string.IsNullOrEmpty(record.Name) ? record.Id : record.Name);
			var rarityLabel = UiComponents.RarityLabel(record.Rarity);
			var level = string.Format(UiComponents.TextOr("sheet.blessing.level", "Nível {0}"), record.Level);
			var lineage = hasDefinition ? string.Format(UiComponents.TextOr("offer.blessing.lineage", "Linhagem {0}"), FormatBlessingLineage(blessing)) : null;
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
				.Append(UiComponents.TextOr("sheet.detail.effects", "Efeitos")).Append("[/color]\n").Append(effects);
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

	private void BuildCharacterSheetArtifacts(CharacterSheetData data)
	{
		var equippedCount = data.EquippedSlots.Count(slot => !string.IsNullOrWhiteSpace(slot));
		var equippedSection = string.Format(UiComponents.TextOr("sheet.section.equipped", "Equipados ({0}/{1})"), equippedCount, data.EquippedSlots.Length);
		for (var i = 0; i < data.EquippedSlots.Length; i++)
		{
			var slotLabel = string.Format(UiComponents.TextOr("sheet.artifact.slot", "Slot {0}"), i + 1);
			var artifactName = data.EquippedSlots[i];
			if (string.IsNullOrWhiteSpace(artifactName))
			{
				_characterSheetEntries.Add(new CharacterSheetEntry
				{
					Section = equippedSection,
					BuildIcon = () => CreateSheetEmptySlotIcon(UiMetrics.IconM),
					Title = UiComponents.TextOr("sheet.artifact.empty", "Vazio"),
					TitleColor = UiPalette.TextDisabled,
					Meta = slotLabel,
					Detail = new DetailData
					{
						Title = slotLabel,
						BadgeText = UiComponents.TextOr("sheet.artifact.empty", "Vazio"),
						BadgeColor = UiPalette.PanelInset,
						BodyBbcode = EscapeSheetBbcode(UiComponents.TextOr("sheet.artifact.empty.desc", "Equipe artefatos da coleção permanente no Baú de Artefatos, no Ninho do Corvo."))
					}
				});
				continue;
			}
			var record = data.Collection.FirstOrDefault(artifact => artifact.Name == artifactName);
			if (string.IsNullOrEmpty(record.Name))
				record = new ArtifactRecord("", artifactName, "", "", 0.0f);
			AddCharacterSheetArtifact(equippedSection, record, slotLabel);
		}

		var runSection = string.Format(UiComponents.TextOr("sheet.section.run_artifacts", "Desta run ({0})"), data.RunArtifacts.Count);
		if (data.RunArtifacts.Count == 0)
			AddCharacterSheetEmptyNote(runSection, UiComponents.TextOr("sheet.empty.run_artifacts", "Nenhum artefato coletado nesta run."));
		foreach (var record in data.RunArtifacts)
			AddCharacterSheetArtifact(runSection, record, UiComponents.TextOr("sheet.artifact.origin.run", "Desta run"));

		var collectionSection = string.Format(UiComponents.TextOr("sheet.section.collection", "Coleção permanente ({0})"), data.Collection.Count);
		if (data.Collection.Count == 0)
			AddCharacterSheetEmptyNote(collectionSection, UiComponents.TextOr("sheet.empty.collection", "Nenhum artefato na coleção."));
		foreach (var record in data.Collection)
			AddCharacterSheetArtifact(collectionSection, record, UiComponents.TextOr("sheet.artifact.origin.collection", "Coleção permanente"));
	}

	private void AddCharacterSheetArtifact(string section, ArtifactRecord record, string origin)
	{
		var definition = FindSheetArtifactDefinition(record);
		var name = definition != null ? definition.DisplayName : record.Name;
		var rarity = definition?.Rarity;
		var slot = FormatSheetArtifactSlot(definition != null ? definition.Slot : record.Slot);
		var meta = string.IsNullOrEmpty(slot) ? origin : $"{slot}  ·  {origin}";
		var icon = definition != null ? LoadSheetArtifactIcon(definition) : null;

		var body = new StringBuilder();
		if (definition != null && !string.IsNullOrWhiteSpace(definition.Description))
			body.Append(EscapeSheetBbcode(definition.Description)).Append("\n\n");
		body.Append("[color=#").Append(UiPalette.Gold.ToHtml(false)).Append(']')
			.Append(UiComponents.TextOr("sheet.detail.effects", "Efeitos")).Append("[/color]\n");
		// "Impacto dos artefatos" da ficha antiga fica fundido aqui. Com definição, usa o mesmo formatador do baú/oferta
		// (valor × BossDropPowerMultiplier, como RunState aplica); o Amount do registro não tem esse multiplicador.
		if (definition != null && definition.Effects != null && definition.Effects.Length > 0)
		{
			for (var i = 0; i < definition.Effects.Length; i++)
			{
				var effect = definition.Effects[i];
				var value = CompendiumTuning.GetNumber($"{definition.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.BossDropPowerMultiplier;
				if (i > 0)
					body.Append('\n');
				body.Append("• ").Append(EscapeSheetBbcode(ArtifactEffectRegistry.FormatEffect(effect.Type, effect.Operation, value)));
			}
		}
		else if (!string.IsNullOrEmpty(record.Effect))
			body.Append("• ").Append(EscapeSheetBbcode(FormatArtifactRecordEffect(record)));
		else
			body.Append(UiComponents.TextOr("sheet.detail.unknown_effects", "Sem dados de efeito (registro antigo)."));

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
			AddCharacterSheetEmptyNote(UiComponents.TextOr("sheet.section.skills", "Habilidades passivas"),
				UiComponents.TextOr("sheet.empty.skills", "Nenhuma habilidade adquirida. Compre habilidades na Fonte de Sangue."));

		foreach (var skill in ordered)
		{
			var definition = skill.Definition;
			if (definition == null)
			{
				_characterSheetEntries.Add(new CharacterSheetEntry
				{
					Section = UiComponents.TextOr("sheet.section.skills_other", "Outras"),
					BuildIcon = () => UiComponents.Monogram(skill.Id, UiMetrics.IconM),
					Title = skill.Id,
					Meta = string.Format(UiComponents.TextOr("sheet.skill.rank", "Rank {0}"), skill.Rank),
					Detail = new DetailData { Title = skill.Id, Meta = string.Format(UiComponents.TextOr("sheet.skill.rank", "Rank {0}"), skill.Rank) }
				});
				continue;
			}

			var treeName = definition.Tree == SkillTree.Bestialis ? "Bestialis" : "Humanitas";
			var isMax = skill.Rank >= definition.MaxRank;
			var rankText = isMax ? UiComponents.TextOr("ui.common.max", "Máx") : $"{skill.Rank}/{definition.MaxRank}";
			var meta = string.Format(UiComponents.TextOr("sheet.skill.meta", "{0} · Rank {1}/{2}"), treeName, skill.Rank, definition.MaxRank);
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
			Coins = CharacterSheet.BloodCoins,
			Blessings = RunState.RunBlessings.ToList(),
			RunArtifacts = RunState.RunArtifacts.ToList(),
			Collection = CharacterSheet.FoundArtifacts.ToList(),
			EquippedSlots = new string[CharacterSheet.EquippedArtifactSlotCount]
		};
		for (var i = 0; i < data.EquippedSlots.Length; i++)
			data.EquippedSlots[i] = CharacterSheet.GetEquippedArtifactSlot(i);
		foreach (var skillId in CharacterSheet.AcquiredSkills)
			data.Skills.Add((skillId, CharacterSheet.GetSkillRank(skillId)));
		return data;
	}

	// Anexo A: 8 bênçãos (todas as raridades de bênção, níveis 1–3), artefatos Common/Epic/Mythic, 500 moedas.
	// Só exibição: não chama RunState.AddBloodBlessing/AddTemporaryArtifact (que marcariam descobertas no save).
	private static CharacterSheetData BuildCharacterSheetPreviewData()
	{
		var data = new CharacterSheetData { Coins = 500, EquippedSlots = new string[CharacterSheet.EquippedArtifactSlotCount] };

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
				data.Collection.Add(ArtifactCatalog.CreateRecord(definition));
		}
		if (data.Collection.Count > 0)
			data.RunArtifacts.Add(data.Collection[data.Collection.Count - 1]);
		if (data.Collection.Count > 1)
			data.RunArtifacts.Add(data.Collection[1]);
		for (var i = 0; i < data.EquippedSlots.Length; i++)
			data.EquippedSlots[i] = i < 2 && i < data.Collection.Count ? data.Collection[i].Name : "";

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
		// Saves antigos guardam só o nome.
		return string.IsNullOrWhiteSpace(record.Name)
			? null
			: ArtifactCatalog.All.FirstOrDefault(artifact => artifact.DisplayName == record.Name);
	}

	// Ícone genérico vira monograma (guia §3.4).
	private static Texture2D LoadSheetArtifactIcon(ArtifactDefinition definition)
	{
		var path = definition?.IconPath;
		if (string.IsNullOrWhiteSpace(path) || path.EndsWith("generic.png", StringComparison.OrdinalIgnoreCase) || !ResourceLoader.Exists(path))
			return null;
		return ResourceLoader.Load<Texture2D>(path);
	}

	private static Control CreateSheetEmptySlotIcon(int size)
	{
		var slot = new Panel
		{
			CustomMinimumSize = new Vector2(size, size),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		slot.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 0, 0));
		return slot;
	}

	private static string FormatSheetArtifactSlot(string slot) => (slot ?? "").ToLowerInvariant() switch
	{
		"passive" => UiComponents.TextOr("sheet.artifact.passive", "Passivo"),
		"active" => UiComponents.TextOr("sheet.artifact.active", "Ativo"),
		_ => slot ?? ""
	};

	private static string FormatSheetPercent(float multiplier) => $"{(multiplier - 1.0f) * 100.0f:+0.#;-0.#;+0}%";

	private static string FormatSheetMultiplier(float multiplier) => $"×{multiplier:0.00}";

	private static string EscapeSheetBbcode(string text) => (text ?? "").Replace("[", "[lb]");
}

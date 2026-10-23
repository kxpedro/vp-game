using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// Menu principal (docs/design/ui-guia.md §8.9). Páginas: main, slots, options, credits.
// Coluna de páginas a partir de y≈330; rodapé com dicas por dispositivo à esquerda e versão à direita.
public partial class MainMenu : Control
{
	private const float MainColumnWidth = 440;
	private const float WideColumnWidth = 640;
	private const float ColumnTop = 330;
	private const float ColumnBottomMargin = 96; // rodapé (52) + margem segura (24) + folga
	private const float SlotRowHeight = 72;
	private const float CreditsScrollSpeed = 520; // px/s no analógico direito

	private sealed class SlotRow
	{
		public int Slot;
		public Button Play;
		public Label Title;
		public Label Date;
		public HBoxContainer Summary;
		public Button Delete;
		public bool Exists;
		public bool Corrupted;
		public bool HasFile => Exists || Corrupted;
	}

	private VBoxContainer _main;
	private VBoxContainer _slots;
	private VBoxContainer _options;
	private OptionsPanelView _optionsPanel;
	private VBoxContainer _credits;
	private Control _currentPage;

	private Button _playButton;
	private Button _optionsButton;
	private Button _creditsButton;
	private Button _slotsBackButton;
	private ScrollContainer _creditsScroll;
	private float _creditsScrollRemainder;
	private readonly List<SlotRow> _slotRows = new();

	private UiHintBar _hints;
	private UiHint _backHint;
	private UiHint _scrollHint;
	private UiConfirm _confirm;

	public override void _Ready()
	{
		// Debug: --ui-preview/--ui-lang/--ui-device; antes de assinar LanguageChanged (idioma já entra no build).
		UiPreview.Attach(this);
		ControllerUiSupport.EnsureMappings();
		Theme = UIThemeFactory.Create();
		if (Localization.Instance != null) Localization.Instance.LanguageChanged += OnLanguageChanged;
		BuildInterface();
		ShowPage(_main, _playButton);
	}

	public override void _ExitTree()
	{
		if (Localization.Instance != null) Localization.Instance.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Input(InputEvent @event)
	{
		// Créditos: o analógico direito rola de forma contínua em _Process (com foco em qualquer controle da página).
		if (_credits?.Visible == true && IsScrollAction(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!@event.IsActionPressed("ui_cancel")) return;
		// Camada modal (confirmação) primeiro.
		if (UiModalStack.CancelTop())
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		// Na página principal, ui_cancel não faz nada (não sai do jogo sem ação explícita).
		if (_currentPage == null || _currentPage == _main) return;
		GoBack();
		GetViewport().SetInputAsHandled();
	}

	public override void _Process(double delta)
	{
		if (_credits?.Visible != true || _creditsScroll == null || UiModalStack.AnyOpen) return;
		if (!InputMap.HasAction("ui_scroll_up") || !InputMap.HasAction("ui_scroll_down")) return;
		var axis = Input.GetAxis("ui_scroll_up", "ui_scroll_down");
		if (Mathf.Abs(axis) < 0.01f)
		{
			_creditsScrollRemainder = 0;
			return;
		}
		_creditsScrollRemainder += axis * CreditsScrollSpeed * (float)delta;
		var step = (int)_creditsScrollRemainder;
		if (step == 0) return;
		_creditsScrollRemainder -= step;
		_creditsScroll.ScrollVertical += step;
	}

	private static bool IsScrollAction(InputEvent @event) =>
		InputMap.HasAction("ui_scroll_up") && (@event.IsAction("ui_scroll_up") || @event.IsAction("ui_scroll_down"));

	// ---------- Montagem ----------

	private void BuildInterface()
	{
		BuildIdentity();
		BuildMainPage();
		BuildSlotsPage();
		BuildOptionsPage();
		BuildCreditsPage();
		BuildFooter();
	}

	// Identidade (§9): fundo preto, glow carmesim, emblema, título dourado com sombra carmesim, subtítulo e divisor.
	private void BuildIdentity()
	{
		AddChild(new ColorRect { Color = UiPalette.ScreenBg, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
		AddChild(BuildBackdropGlow());
		AddChild(new TextureRect
		{
			Texture = ResourceLoader.Load<Texture2D>("res://icon.png"),
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -48, OffsetTop = 36, OffsetRight = 48, OffsetBottom = 132,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(Colors.White, 0.8f), MouseFilter = MouseFilterEnum.Ignore
		});
		AddChild(new Label
		{
			Text = Localization.Text("game.title"), ThemeTypeVariation = UiVariation.DisplayLabel,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -400, OffsetTop = 138, OffsetRight = 400, OffsetBottom = 206,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
		});
		var subtitle = new Label
		{
			Text = Localization.Text("game.subtitle"), ThemeTypeVariation = UiVariation.BodyLabel,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -400, OffsetTop = 206, OffsetRight = 400, OffsetBottom = 234,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		subtitle.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
		AddChild(subtitle);
		AddChild(new TextureRect
		{
			Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimpleDividerPath),
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -220, OffsetTop = 246, OffsetRight = 220, OffsetBottom = 250,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
			SelfModulate = new Color(UiPalette.DecorCrimson.Lightened(0.25f), 0.8f), MouseFilter = MouseFilterEnum.Ignore
		});
	}

	private static TextureRect BuildBackdropGlow()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.34f, 0.02f, 0.05f, 0.55f));
		gradient.SetColor(1, new Color(0.0f, 0.0f, 0.0f, 0.0f));
		return new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = gradient, Width = 512, Height = 512,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.42f), FillTo = new Vector2(1.05f, 0.95f)
			},
			AnchorRight = 1, AnchorBottom = 1,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
	}

	private VBoxContainer CreatePage(float width)
	{
		var page = new VBoxContainer
		{
			Visible = false,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0, AnchorBottom = 1,
			OffsetLeft = -width / 2, OffsetRight = width / 2, OffsetTop = ColumnTop, OffsetBottom = -ColumnBottomMargin,
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		page.AddThemeConstantOverride("separation", UiMetrics.S3);
		AddChild(page);
		return page;
	}

	private static void AddPageHeader(VBoxContainer page, string title)
	{
		page.AddChild(new Label { Text = title, ThemeTypeVariation = UiVariation.TitleLabel, HorizontalAlignment = HorizontalAlignment.Center });
		page.AddChild(UiComponents.Divider());
	}

	private Button AddPageButton(VBoxContainer page, string text, Action action, ButtonVariant variant = ButtonVariant.Secondary)
	{
		var button = UiComponents.MenuButton(text, action, variant, ButtonSize.Large);
		page.AddChild(button);
		return button;
	}

	private Button AddBackButton(VBoxContainer page)
	{
		page.AddChild(UiComponents.Separator());
		return AddPageButton(page, Localization.Text("menu.back"), GoBack);
	}

	private void BuildMainPage()
	{
		_main = CreatePage(MainColumnWidth);
		// Dois jogos no mesmo projeto (GameMode): cada um com as próprias cenas e os próprios saves.
		_playButton = AddPageButton(_main, Localization.Text("menu.play_3d"), () => OpenSlotsForMode(GameMode.Kind.ThreeD), ButtonVariant.Primary);
		AddPageButton(_main, Localization.Text("menu.play_pixel"), () => OpenSlotsForMode(GameMode.Kind.PixelArt), ButtonVariant.Primary);
		_optionsButton = AddPageButton(_main, Localization.Text("menu.options"), () => ShowPage(_options, _optionsPanel?.PreferredFocus));
		_creditsButton = AddPageButton(_main, Localization.Text("menu.credits"), () => ShowPage(_credits, _creditsScroll));
		_main.AddChild(UiComponents.Separator());
		AddPageButton(_main, Localization.Text("menu.quit"), () => GetTree().Quit());
	}

	private void OpenSlotsForMode(GameMode.Kind mode)
	{
		// Grava o que estiver pendente antes de trocar de modo: os saves de cada modo ficam em arquivos próprios.
		SaveGameManager.Instance?.FlushPendingSave();
		GameMode.Current = mode;
		foreach (var row in _slotRows)
			UpdateSlotRow(row);
		ShowPage(_slots, PreferredSlotFocus());
	}

	// ---------- Slots ----------

	private void BuildSlotsPage()
	{
		_slots = CreatePage(WideColumnWidth);
		AddPageHeader(_slots, Localization.Text("menu.choose_slot"));
		_slotRows.Clear();
		for (var slot = 1; slot <= 3; slot++)
		{
			var row = BuildSlotRow(slot);
			_slotRows.Add(row);
			UpdateSlotRow(row);
		}
		_slotsBackButton = AddBackButton(_slots);
		RefreshSlotNeighbors();
	}

	private SlotRow BuildSlotRow(int slot)
	{
		var row = new SlotRow { Slot = slot };
		var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, SlotRowHeight) };
		line.AddThemeConstantOverride("separation", UiMetrics.S3);
		_slots.AddChild(line);

		row.Play = UiComponents.MenuButton("", () => RequestLoadSlot(row), ButtonVariant.Secondary, ButtonSize.Large);
		row.Play.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.Play.CustomMinimumSize = new Vector2(0, SlotRowHeight);
		line.AddChild(row.Play);

		// Conteúdo do botão: "Slot N" + data à direita; abaixo, resumo (moedas, ranks, artefatos) ou "Novo jogo".
		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", UiMetrics.PadPanelX);
		margin.AddThemeConstantOverride("margin_right", UiMetrics.PadPanelX);
		margin.AddThemeConstantOverride("margin_top", UiMetrics.S2);
		margin.AddThemeConstantOverride("margin_bottom", UiMetrics.S2);
		row.Play.AddChild(margin);
		var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 2);
		margin.AddChild(column);
		var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		top.AddThemeConstantOverride("separation", UiMetrics.S3);
		column.AddChild(top);
		row.Title = UiComponents.MakeLabel(Localization.TextFormat("menu.slot_title", slot), UiVariation.SectionLabel);
		top.AddChild(row.Title);
		row.Date = UiComponents.MakeLabel("", UiVariation.SecondaryLabel);
		row.Date.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.Date.HorizontalAlignment = HorizontalAlignment.Right;
		row.Date.VerticalAlignment = VerticalAlignment.Center;
		row.Date.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		top.AddChild(row.Date);
		row.Summary = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.Summary.AddThemeConstantOverride("separation", UiMetrics.S1);
		column.AddChild(row.Summary);

		row.Delete = UiComponents.MenuButton(Localization.Text("menu.delete"), () => RequestDeleteSlot(row), ButtonVariant.Destructive, ButtonSize.Small);
		row.Delete.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		line.AddChild(row.Delete);
		return row;
	}

	private void UpdateSlotRow(SlotRow row)
	{
		var info = SaveGameManager.Instance?.GetSlotInfo(row.Slot);
		row.Exists = info != null && info.Exists;
		row.Corrupted = info != null && info.Corrupted;
		foreach (var child in row.Summary.GetChildren())
		{
			row.Summary.RemoveChild(child);
			child.QueueFree();
		}

		if (row.Corrupted)
		{
			row.Date.Text = "";
			var unreadable = UiComponents.MakeLabel(Localization.Text("menu.slot_unreadable"), UiVariation.SecondaryLabel);
			unreadable.AddThemeColorOverride("font_color", UiPalette.Warn);
			row.Summary.AddChild(unreadable);
		}
		else if (!row.Exists)
		{
			row.Date.Text = "";
			var empty = UiComponents.MakeLabel(Localization.Text("menu.new_game"), UiVariation.SecondaryLabel);
			empty.AddThemeColorOverride("font_color", UiPalette.Gold);
			row.Summary.AddChild(empty);
		}
		else
		{
			row.Date.Text = info.LastPlayedLocal.Year < 2000 ? "" : FormatDate(info.LastPlayedLocal);
			// Sangue Amaldiçoado do slot (recurso permanente, losango violeta); as Moedas de Sangue só existem na run.
			var glyph = UiComponents.CursedBloodGlyph(18);
			row.Summary.AddChild(glyph);
			var cursedBlood = UiComponents.MakeLabel(Localization.TextFormat("menu.slot_cursed_blood", info.CursedBlood), UiVariation.SecondaryLabel);
			cursedBlood.AddThemeColorOverride("font_color", UiPalette.CursedBlood);
			row.Summary.AddChild(cursedBlood);
			var details = new List<string>
			{
				Localization.TextFormat("menu.slot_skill_ranks", info.SkillRanks)
			};
			var rest = UiComponents.MakeLabel("· " + string.Join(" · ", details), UiVariation.SecondaryLabel);
			rest.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rest.ClipText = true;
			rest.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			row.Summary.AddChild(rest);
		}

		// Slot vazio: Excluir some, mas guarda o espaço (as três linhas ficam alinhadas). Save ilegível pode ser excluído.
		row.Delete.Disabled = !row.HasFile;
		row.Delete.Modulate = row.HasFile ? Colors.White : Colors.Transparent;
		row.Delete.MouseFilter = row.HasFile ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		row.Delete.FocusMode = row.HasFile ? FocusModeEnum.All : FocusModeEnum.None;
		row.Delete.MouseDefaultCursorShape = row.HasFile ? CursorShape.PointingHand : CursorShape.Arrow;
	}

	// Data/hora local curta no formato do idioma atual.
	private static string FormatDate(DateTime value)
	{
		var language = Localization.Instance?.CurrentLanguage ?? "pt-BR";
		CultureInfo culture;
		try
		{
			culture = new CultureInfo(language == "en" ? "en-US" : language == "es" ? "es-ES" : "pt-BR");
		}
		catch (CultureNotFoundException)
		{
			culture = CultureInfo.InvariantCulture;
		}
		return value.ToString("g", culture);
	}

	// Cima/baixo entre os slots e o Voltar; direita no slot vai a Excluir (se houver save), esquerda volta.
	private void RefreshSlotNeighbors()
	{
		for (var i = 0; i < _slotRows.Count; i++)
		{
			var row = _slotRows[i];
			var up = i > 0 ? _slotRows[i - 1].Play : (Control)_slotsBackButton;
			var down = i < _slotRows.Count - 1 ? _slotRows[i + 1].Play : (Control)_slotsBackButton;
			row.Play.FocusNeighborTop = row.Play.GetPathTo(up);
			row.Play.FocusNeighborBottom = row.Play.GetPathTo(down);
			row.Play.FocusNeighborRight = row.Play.GetPathTo(row.HasFile ? row.Delete : row.Play);
			row.Play.FocusNeighborLeft = row.Play.GetPathTo(row.Play);
			row.Delete.FocusNeighborLeft = row.Delete.GetPathTo(row.Play);
			row.Delete.FocusNeighborRight = row.Delete.GetPathTo(row.Delete);
			var upDelete = i > 0 ? NeighborTarget(_slotRows[i - 1]) : _slotsBackButton;
			var downDelete = i < _slotRows.Count - 1 ? NeighborTarget(_slotRows[i + 1]) : _slotsBackButton;
			row.Delete.FocusNeighborTop = row.Delete.GetPathTo(upDelete);
			row.Delete.FocusNeighborBottom = row.Delete.GetPathTo(downDelete);
		}
		if (_slotRows.Count > 0)
		{
			_slotsBackButton.FocusNeighborTop = _slotsBackButton.GetPathTo(_slotRows[_slotRows.Count - 1].Play);
			_slotsBackButton.FocusNeighborBottom = _slotsBackButton.GetPathTo(_slotRows[0].Play);
		}
	}

	private static Control NeighborTarget(SlotRow row) => row.HasFile ? row.Delete : row.Play;

	// Foco inicial: o slot jogado por último; sem saves, o Slot 1.
	private Control PreferredSlotFocus()
	{
		SlotRow best = null;
		var bestDate = DateTime.MinValue;
		foreach (var row in _slotRows)
		{
			if (!row.Exists) continue;
			var info = SaveGameManager.Instance?.GetSlotInfo(row.Slot);
			if (info == null || best != null && info.LastPlayedLocal <= bestDate) continue;
			best = row;
			bestDate = info.LastPlayedLocal;
		}
		return (best ?? (_slotRows.Count > 0 ? _slotRows[0] : null))?.Play;
	}

	private void RequestDeleteSlot(SlotRow row)
	{
		if (_confirm != null && _confirm.IsOpen) return;
		var title = Localization.TextFormat("menu.delete_confirm_title", row.Slot);
		var body = Localization.Text("menu.delete_confirm_body");
		OpenMenuConfirm(title, body, Localization.Text("menu.delete"), () => OnDeleteConfirmed(row));
	}

	private void OnDeleteConfirmed(SlotRow row)
	{
		SaveGameManager.Instance?.DeleteSlot(row.Slot);
		// Depois do retorno de foco adiado do Confirm (vai para Excluir, que fica desabilitado): refoca o slot.
		Callable.From(() =>
		{
			if (!IsInstanceValid(row.Play) || !row.Play.IsInsideTree()) return;
			UpdateSlotRow(row);
			RefreshSlotNeighbors();
			row.Play.GrabFocus();
		}).CallDeferred();
	}

	// ---------- Opções ----------

	private void BuildOptionsPage()
	{
		_options = CreatePage(WideColumnWidth);
		AddPageHeader(_options, Localization.Text("options.title"));
		// Mesmo painel da pausa (UiComponents.OptionsPanel): abas Vídeo/Áudio/Jogo; rótulo 240 · controle · valor 64.
		_optionsPanel = UiComponents.OptionsPanel(true);
		_optionsPanel.SetScrollMinHeight(340);
		_options.AddChild(_optionsPanel);
		_optionsPanel.SetFooterTarget(AddBackButton(_options));
	}

	// ---------- Créditos ----------

	private void BuildCreditsPage()
	{
		_credits = CreatePage(WideColumnWidth);
		AddPageHeader(_credits, Localization.Text("credits.title"));
		_creditsScroll = UiComponents.ScrollList(240, out var list);
		UiComponents.MakeScrollFocusable(_creditsScroll);
		// Moldura que acende com o foco da lista: no 4.7 o "focus" do ScrollContainer não aparece (nem com DrawFocusBorder).
		var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		var idle = UIThemeFactory.ControlBox(Colors.Transparent, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S2);
		var focused = UIThemeFactory.ControlBox(UiPalette.PanelRaised, UiPalette.Focus, UiMetrics.BorderFocus, UiMetrics.S2, UiMetrics.S2);
		focused.ShadowColor = UiPalette.FocusGlow;
		focused.ShadowSize = 6;
		frame.AddThemeStyleboxOverride("panel", idle);
		_creditsScroll.FocusEntered += () => frame.AddThemeStyleboxOverride("panel", focused);
		_creditsScroll.FocusExited += () => frame.AddThemeStyleboxOverride("panel", idle);
		frame.AddChild(_creditsScroll);
		_credits.AddChild(frame);
		var text = new RichTextLabel
		{
			BbcodeEnabled = true, FitContent = true, ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Pass,
			Text = BuildCreditsText()
		};
		list.AddChild(text);
		AddBackButton(_credits);
	}

	private static string BuildCreditsText()
	{
		var gold = UiPalette.Gold.ToHtml(false);
		var secondary = UiPalette.TextSecondary.ToHtml(false);
		var text = $"[center][font_size={UiFont.Section}][color=#{gold}]{Localization.Text("credits.created_by")}[/color][/font_size]\n" +
			"Pedro Henrique Rech Sarmento\n\n" +
			$"[font_size={UiFont.Section}][color=#{gold}]{Localization.Text("credits.third_party")}[/color][/font_size]\n";
		foreach (var (work, detail) in ThirdPartyCredits)
			text += $"{work}\n[font_size={UiFont.Secondary}][color=#{secondary}]{detail}[/color][/font_size]\n\n";
		return text.TrimEnd('\n') + "[/center]";
	}

	// Atribuições exigidas pelas licenças (CC BY 3.0 e OGA-BY 3.0) e cortesia aos pacotes CC0. Detalhes em CREDITS.md.
	private static readonly (string Work, string Detail)[] ThirdPartyCredits =
	{
		("Whip", "Poly by Google (poly.pizza) — CC BY 3.0"),
		("Additional Sound FX by Will Leamon — \"Fleshy Fight Sounds\"", "Will Leamon (OpenGameArt) — OGA-BY 3.0"),
		("Swishes Sound Pack", "artisticdude (OpenGameArt) — CC0"),
		("20 Sword Sound Effects", "StarNinjas (OpenGameArt) — CC0"),
		("Ultimate Animated Character, Ultimate Monsters, Modular Medieval Dungeon, Modular Weapons, Revolver", "Quaternius — CC0"),
		("Modular Dungeon Kit, UI Pack, Cursor Pack", "Kenney — CC0")
	};

	// ---------- Rodapé ----------

	private void BuildFooter()
	{
		_hints = UiComponents.HintBar();
		var root = _hints.Root;
		root.AnchorLeft = 0; root.AnchorRight = 1; root.AnchorTop = 1; root.AnchorBottom = 1;
		root.OffsetLeft = UiMetrics.SafeMargin; root.OffsetRight = -UiMetrics.SafeMargin;
		root.OffsetTop = -UiMetrics.SafeMargin - UiMetrics.FooterHeight; root.OffsetBottom = -UiMetrics.SafeMargin;
		AddChild(root);
		_backHint = _hints.Add(UiAction.Cancel, Localization.Text("menu.back"), GoBack, true);
		_scrollHint = _hints.Add(UiAction.Scroll, Localization.Text("ui.common.scroll"), null, true);
		_hints.Add(UiAction.Accept, Localization.Text("ui.common.select"), null, true);
		var version = UiComponents.MakeLabel(GameVersionText(), UiVariation.SecondaryLabel);
		version.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_hints.Right.AddChild(version);
	}

	private static string GameVersionText()
	{
		var version = ProjectSettings.GetSetting("application/config/version", "").AsString();
		var name = ProjectSettings.GetSetting("application/config/name", "VP0.1").AsString();
		var text = string.IsNullOrEmpty(version) ? name : $"{name} v{version}";
		return OS.IsDebugBuild() ? text + " · debug" : text;
	}

	// ---------- Navegação ----------

	private void ShowPage(Control page, Control focus = null)
	{
		_currentPage = page;
		_main.Visible = page == _main;
		_slots.Visible = page == _slots;
		_options.Visible = page == _options;
		_credits.Visible = page == _credits;
		_creditsScrollRemainder = 0;
		if (page == _credits) _creditsScroll.ScrollVertical = 0;
		if (_backHint != null) _backHint.Visible = page != _main;
		if (_scrollHint != null) _scrollHint.Visible = page == _credits;
		ControllerUiSupport.FocusInitial(focus, page);
	}

	// Volta à página principal com foco no botão que abriu a subpágina.
	private void GoBack()
	{
		if (_currentPage == null || _currentPage == _main) return;
		var opener = _currentPage == _slots ? _playButton : _currentPage == _options ? _optionsButton : _creditsButton;
		ShowPage(_main, opener);
	}

	// Pré-visualização (debug, ver UiPreview.cs): mostra a página pelo nome. False = nome desconhecido.
	internal bool ShowPreviewPage(string page)
	{
		if (page == "menu_confirm_delete")
		{
			ShowPage(_slots, PreferredSlotFocus());
			if (_slotRows.Count > 0) RequestDeleteSlot(_slotRows[0]);
			return true;
		}
		var target = page switch
		{
			"menu_main" => _main,
			"menu_slots" => _slots,
			"menu_options" or "menu_options_audio" or "menu_options_game" => _options,
			"menu_credits" => _credits,
			_ => null
		};
		if (target == null) return false;
		if (target == _options)
			UiComponents.FindOptionsPanel(_options)?.SelectTab(page == "menu_options_audio" ? OptionsPanelView.TabAudio : page == "menu_options_game" ? OptionsPanelView.TabGame : OptionsPanelView.TabVideo);
		ShowPage(target, target == _main ? _playButton : target == _slots ? PreferredSlotFocus() : target == _credits ? _creditsScroll : _optionsPanel?.PreferredFocus);
		return true;
	}

	// Save ilegível: jogar começa um jogo novo no slot (o arquivo é copiado e sobrescrito), então pede confirmação.
	private void RequestLoadSlot(SlotRow row)
	{
		if (_confirm != null && _confirm.IsOpen) return;
		var info = SaveGameManager.Instance?.GetSlotInfo(row.Slot);
		if (info == null || !info.Corrupted)
		{
			LoadSlot(row.Slot);
			return;
		}
		var title = Localization.TextFormat("menu.unreadable_confirm_title", row.Slot);
		var body = Localization.Text("menu.unreadable_confirm_body");
		OpenMenuConfirm(title, body, Localization.Text("menu.new_game"), () => LoadSlot(row.Slot));
	}

	// Confirmação na pilha modal; o rodapé do menu ("Voltar") some enquanto ela está aberta (ela tem o próprio rodapé).
	private void OpenMenuConfirm(string title, string body, string confirmText, Action onConfirm)
	{
		SetMenuHintsHidden(true);
		_confirm = UiComponents.Confirm(this, title, body, confirmText, () =>
		{
			SetMenuHintsHidden(false);
			onConfirm();
		}, ConfirmKind.Destructive, () => SetMenuHintsHidden(false));
		UiModalStack.Push(_confirm.Window.Root, false, _confirm.CancelButton, _confirm.Cancel);
	}

	private void SetMenuHintsHidden(bool hidden)
	{
		if (_hints != null && IsInstanceValid(_hints.Root))
			_hints.Root.Modulate = hidden ? Colors.Transparent : Colors.White;
	}

	private void LoadSlot(int slot)
	{
		SaveGameManager.Instance.StartSlot(slot); RunState.FailRun(); GetTree().ChangeSceneToFile(GameMode.Scene("res://scenes/game/CrowsNest.tscn"));
	}

	// ---------- Idioma ----------

	private void OnLanguageChanged(string language)
	{
		var page = _currentPage == _options ? "options" : _currentPage == _slots ? "slots" : _currentPage == _credits ? "credits" : "main";
		Callable.From(() => RebuildForLanguage(page)).CallDeferred();
	}

	private void RebuildForLanguage(string page)
	{
		_confirm = null; // a confirmação aberta sai junto (UiModalStack esquece a camada no TreeExiting)
		foreach (var child in GetChildren())
		{
			if (child is UiPreview) continue;
			RemoveChild(child);
			child.Free();
		}
		BuildInterface();
		switch (page)
		{
			case "options": ShowPage(_options, _optionsPanel?.PreferredFocus); break;
			case "slots": ShowPage(_slots, PreferredSlotFocus()); break;
			case "credits": ShowPage(_credits, _creditsScroll); break;
			default: ShowPage(_main, _playButton); break;
		}
	}
}

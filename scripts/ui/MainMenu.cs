using Godot;

public partial class MainMenu : Control
{
	private VBoxContainer _main;
	private VBoxContainer _slots;
	private VBoxContainer _options;
	private VBoxContainer _credits;

	private bool _showingOptions;

	public override void _Ready()
	{
		ControllerUiSupport.EnsureMappings();
		Theme = UIThemeFactory.Create();
		if (Localization.Instance != null) Localization.Instance.LanguageChanged += OnLanguageChanged;
		BuildInterface(); ShowPage(_main);
	}

	public override void _ExitTree()
	{
		if (Localization.Instance != null) Localization.Instance.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		if (_main?.Visible == true) return;
		ShowPage(_main);
		GetViewport().SetInputAsHandled();
	}

	private void BuildInterface()
	{
		AddChild(new ColorRect { Color = new Color(0.006f, 0.002f, 0.005f), AnchorRight = 1, AnchorBottom = 1 });
		AddChild(BuildBackdropGlow());
		var emblem = new TextureRect
		{
			Texture = ResourceLoader.Load<Texture2D>("res://icon.png"),
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -64, OffsetTop = 48, OffsetRight = 64, OffsetBottom = 176,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(1, 1, 1, 0.92f)
		};
		AddChild(emblem);
		var title = new Label { Text = Localization.Text("game.title"), AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -350, OffsetTop = 184, OffsetRight = 350, OffsetBottom = 250, HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 54); title.Modulate = new Color(0.9f, 0.72f, 0.38f);
		title.AddThemeColorOverride("font_shadow_color", new Color(0.35f, 0.0f, 0.03f, 0.85f));
		title.AddThemeConstantOverride("shadow_offset_x", 0); title.AddThemeConstantOverride("shadow_offset_y", 4); title.AddThemeConstantOverride("shadow_outline_size", 10);
		AddChild(title);
		var subtitle = new Label { Text = Localization.Text("game.subtitle"), AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -350, OffsetTop = 248, OffsetRight = 350, OffsetBottom = 280, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(0.68f, 0.57f, 0.5f) }; AddChild(subtitle);
		var divider = new TextureRect
		{
			Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimpleDividerPath),
			AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -220, OffsetTop = 288, OffsetRight = 220, OffsetBottom = 292,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
			Modulate = new Color(0.62f, 0.2f, 0.18f, 0.7f)
		};
		AddChild(divider);
		_main = CreatePage(); _slots = CreatePage(); _options = CreatePage(); _credits = CreatePage();
		AddMenuButton(_main, Localization.Text("menu.play"), () => ShowPage(_slots));
		AddMenuButton(_main, Localization.Text("menu.options"), () => ShowPage(_options));
		AddMenuButton(_main, Localization.Text("menu.credits"), () => ShowPage(_credits));
		AddMenuButton(_main, Localization.Text("menu.quit"), () => GetTree().Quit());
		BuildSlotsPage();
		BuildCreditsPage();
		_options.AddChild(new Label { Text = Localization.Text("options.title"), HorizontalAlignment = HorizontalAlignment.Center });
		AddLanguageOption(); AddResolutionOption(); AddVolumeOption("options.volume_music", true); AddVolumeOption("options.volume_effects", false);
		AddMenuButton(_options, Localization.Text("menu.back"), () => ShowPage(_main));
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

	private void BuildCreditsPage()
	{
		_credits.AddChild(new Label { Text = Localization.Text("credits.title"), HorizontalAlignment = HorizontalAlignment.Center });
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		var text = new RichTextLabel
		{
			BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Text = $"[center][b]{Localization.Text("credits.created_by")}[/b]\nPedro Henrique Rech Sarmento\n\n[b]{Localization.Text("credits.third_party")}[/b][/center]\n" + ThirdPartyCredits
		};
		scroll.AddChild(text);
		_credits.AddChild(scroll);
		AddMenuButton(_credits, Localization.Text("menu.back"), () => ShowPage(_main));
	}

	// Atribuicoes exigidas pelas licencas (CC BY 3.0 e OGA-BY 3.0) e cortesia aos pacotes CC0. Detalhes em CREDITS.md.
	private const string ThirdPartyCredits =
		"• Whip by Poly by Google (poly.pizza) — CC BY 3.0\n" +
		"• Additional Sound FX by Will Leamon — \"Fleshy Fight Sounds\" (OpenGameArt) — OGA-BY 3.0\n" +
		"• Swishes Sound Pack by artisticdude (OpenGameArt) — CC0\n" +
		"• 20 Sword Sound Effects by StarNinjas (OpenGameArt) — CC0\n" +
		"• Ultimate Animated Character, Ultimate Monsters, Modular Medieval Dungeon, Modular Weapons e Revolver by Quaternius — CC0\n" +
		"• Modular Dungeon Kit, UI Pack e Cursor Pack by Kenney — CC0";

	private VBoxContainer CreatePage()
	{
		var page = new VBoxContainer { AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f, OffsetLeft = -300, OffsetTop = -140, OffsetRight = 300, OffsetBottom = 260, Alignment = BoxContainer.AlignmentMode.Center };
		AddChild(page); return page;
	}

	private void AddMenuButton(VBoxContainer page, string text, System.Action action)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(500, 54) }; button.Pressed += action; page.AddChild(button);
	}

	private void BuildSlotsPage()
	{
		_slots.AddChild(new Label { Text = Localization.Text("menu.choose_slot"), HorizontalAlignment = HorizontalAlignment.Center });
		for (var slot = 1; slot <= 3; slot++)
		{
			var captured = slot;
			var row = new HBoxContainer { CustomMinimumSize = new Vector2(600, 54) };
			var play = new Button { Text = Localization.TextFormat("menu.slot", slot, SaveGameManager.Instance.GetSlotSummary(slot)), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			play.Pressed += () => LoadSlot(captured); row.AddChild(play);
			var delete = new Button { Text = Localization.Text("menu.delete"), Disabled = !SaveGameManager.Instance.SlotExists(slot), CustomMinimumSize = new Vector2(95, 54) };
			delete.Pressed += () => { SaveGameManager.Instance.DeleteSlot(captured); RebuildInterface(); };
			row.AddChild(delete); _slots.AddChild(row);
		}
		AddMenuButton(_slots, Localization.Text("menu.back"), () => ShowPage(_main));
	}

	private void AddLanguageOption()
	{
		_options.AddChild(new Label { Text = Localization.Text("options.language") });
		var choice = new OptionButton { CustomMinimumSize = new Vector2(500, 48) };
		choice.AddItem(Localization.Text("options.language.pt_br"));
		choice.AddItem(Localization.Text("options.language.en"));
		choice.AddItem(Localization.Text("options.language.es"));
		var language = GlobalSettings.Instance?.Data.Language ?? "pt-BR";
		choice.Selected = language == "en" ? 1 : language == "es" ? 2 : 0;
		choice.ItemSelected += index => GlobalSettings.Instance?.SetLanguage(index == 1 ? "en" : index == 2 ? "es" : "pt-BR");
		_options.AddChild(choice);
	}

	private void AddResolutionOption()
	{
		var choice = new OptionButton { CustomMinimumSize = new Vector2(500, 48) };
		choice.AddItem("1280 x 720"); choice.AddItem("1600 x 900"); choice.AddItem("1920 x 1080");
		choice.Selected = GlobalSettings.Instance?.Data.ResolutionIndex ?? 1;
		choice.ItemSelected += index => GlobalSettings.Instance?.SetResolution((int)index); _options.AddChild(choice);
		var fullscreen = new CheckButton { Text = Localization.Text("options.fullscreen"), ButtonPressed = GlobalSettings.Instance?.Data.Fullscreen ?? false };
		fullscreen.Toggled += enabled => GlobalSettings.Instance?.SetFullscreen(enabled); _options.AddChild(fullscreen);
	}

	private void AddVolumeOption(string labelKey, bool music)
	{
		_options.AddChild(new Label { Text = Localization.Text(labelKey) });
		var initial = music ? GlobalSettings.Instance?.Data.MusicVolume ?? 70 : GlobalSettings.Instance?.Data.EffectsVolume ?? 85;
		var slider = new HSlider { MinValue = 0, MaxValue = 100, Value = initial, Step = 1, CustomMinimumSize = new Vector2(500, 36) };
		slider.ValueChanged += value => { if (music) GlobalSettings.Instance?.SetMusicVolume((float)value); else GlobalSettings.Instance?.SetEffectsVolume((float)value); }; _options.AddChild(slider);
	}

	private void ShowPage(Control page)
	{
		_showingOptions = page == _options;
		_main.Visible = page == _main;
		_slots.Visible = page == _slots;
		_options.Visible = page == _options;
		_credits.Visible = page == _credits;
		ControllerUiSupport.FocusFirst(page);
	}

	private void LoadSlot(int slot)
	{
		SaveGameManager.Instance.StartSlot(slot); RunState.FailRun(); GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
	}

	private void RebuildInterface()
	{
		foreach (var child in GetChildren()) child.QueueFree();
		BuildInterface(); ShowPage(_slots);
	}

	private void OnLanguageChanged(string language)
	{
		CallDeferred(nameof(RebuildForLanguage), _showingOptions);
	}

	private void RebuildForLanguage(bool keepOptionsOpen)
	{
		foreach (var child in GetChildren()) child.Free();
		BuildInterface();
		ShowPage(keepOptionsOpen ? _options : _main);
	}
}

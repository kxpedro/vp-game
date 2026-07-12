using Godot;

public partial class MainMenu : Control
{
    private VBoxContainer _main;
    private VBoxContainer _slots;
    private VBoxContainer _options;

    public override void _Ready() { Theme = UIThemeFactory.Create(); BuildInterface(); ShowPage(_main); }

    private void BuildInterface()
    {
        var background = new ColorRect { Color = new Color(0.012f, 0.004f, 0.009f), AnchorRight = 1, AnchorBottom = 1 }; AddChild(background);
        var veil = new ColorRect { Color = new Color(0.18f, 0.0f, 0.025f, 0.18f), AnchorLeft = 0.18f, AnchorRight = 0.82f, AnchorBottom = 1 }; AddChild(veil);
        var title = new Label { Text = "VAMPIRE'S PREY", AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -350, OffsetTop = 90, OffsetRight = 350, OffsetBottom = 160, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 54); title.Modulate = new Color(0.9f, 0.72f, 0.38f); AddChild(title);
        var subtitle = new Label { Text = "UMA CRÔNICA DE SANGUE E RUÍNA", AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -350, OffsetTop = 158, OffsetRight = 350, OffsetBottom = 194, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(0.68f, 0.57f, 0.5f) }; AddChild(subtitle);
        _main = CreatePage(); _slots = CreatePage(); _options = CreatePage();
        AddMenuButton(_main, "Jogar", () => ShowPage(_slots));
        AddMenuButton(_main, "Opcoes", () => ShowPage(_options));
        AddMenuButton(_main, "Sair", () => GetTree().Quit());
        BuildSlotsPage();
        _options.AddChild(new Label { Text = "Opcoes", HorizontalAlignment = HorizontalAlignment.Center });
        AddResolutionOption(); AddVolumeOption("Musica", true); AddVolumeOption("Efeitos", false);
        AddMenuButton(_options, "Voltar", () => ShowPage(_main));
    }

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
        _slots.AddChild(new Label { Text = "Escolha um slot", HorizontalAlignment = HorizontalAlignment.Center });
        for (var slot = 1; slot <= 3; slot++)
        {
            var captured = slot;
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(600, 54) };
            var play = new Button { Text = $"Slot {slot} — {SaveGameManager.Instance.GetSlotSummary(slot)}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            play.Pressed += () => LoadSlot(captured); row.AddChild(play);
            var delete = new Button { Text = "Excluir", Disabled = !SaveGameManager.Instance.SlotExists(slot), CustomMinimumSize = new Vector2(95, 54) };
            delete.Pressed += () => { SaveGameManager.Instance.DeleteSlot(captured); RebuildInterface(); };
            row.AddChild(delete); _slots.AddChild(row);
        }
        AddMenuButton(_slots, "Voltar", () => ShowPage(_main));
    }

    private void AddResolutionOption()
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(500, 48) };
        choice.AddItem("1280 x 720"); choice.AddItem("1600 x 900"); choice.AddItem("1920 x 1080");
        choice.Selected = GlobalSettings.Instance?.Data.ResolutionIndex ?? 1;
        choice.ItemSelected += index => GlobalSettings.Instance?.SetResolution((int)index); _options.AddChild(choice);
        var fullscreen = new CheckButton { Text = "Tela cheia", ButtonPressed = GlobalSettings.Instance?.Data.Fullscreen ?? false };
        fullscreen.Toggled += enabled => GlobalSettings.Instance?.SetFullscreen(enabled); _options.AddChild(fullscreen);
    }

    private void AddVolumeOption(string label, bool music)
    {
        _options.AddChild(new Label { Text = $"Volume: {label}" });
        var initial = music ? GlobalSettings.Instance?.Data.MusicVolume ?? 70 : GlobalSettings.Instance?.Data.EffectsVolume ?? 85;
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Value = initial, Step = 1, CustomMinimumSize = new Vector2(500, 36) };
        slider.ValueChanged += value => { if (music) GlobalSettings.Instance?.SetMusicVolume((float)value); else GlobalSettings.Instance?.SetEffectsVolume((float)value); }; _options.AddChild(slider);
    }

    private void ShowPage(Control page) { _main.Visible = page == _main; _slots.Visible = page == _slots; _options.Visible = page == _options; }

    private void LoadSlot(int slot)
    {
        SaveGameManager.Instance.StartSlot(slot); RunState.FailRun(); GetTree().ChangeSceneToFile("res://scenes/game/CrowsNest.tscn");
    }

    private void RebuildInterface()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        BuildInterface(); ShowPage(_slots);
    }
}

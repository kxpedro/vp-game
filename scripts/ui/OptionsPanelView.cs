using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Painel de opções compartilhado pelo menu principal e pela pausa (docs/design/ui-guia.md §3.12):
/// abas Vídeo / Áudio / Jogo (L1/R1 trocam), linhas OptionRow/ToggleRow/LabeledSlider numa lista rolável,
/// presets que atualizam os controles (<see cref="RefreshAll"/>) e confirmação "Manter estas configurações?" com
/// reversão automática em 10 s para modo de janela, resolução e monitor.
/// Criado por <see cref="UiComponents.OptionsPanel"/>; a aba atual é lembrada (a pausa se reconstrói ao trocar de idioma).
/// </summary>
public partial class OptionsPanelView : VBoxContainer
{
    public const int TabVideo = 0, TabAudio = 1, TabGame = 2;
    public static int RememberedTab;

    private const float KeepSeconds = 10.0f;
    private const float ScrollMinHeight = 420.0f; // padrão da pausa (a janela cresce); o menu usa menos (SetScrollMinHeight)

    private UiTabs _tabs;
    private ScrollContainer _scroll;
    private readonly VBoxContainer[] _pages = new VBoxContainer[3];
    private readonly List<UiOptionRow>[] _rows = { new(), new(), new() };
    private readonly List<Action> _refreshers = new();
    private Control _footerTarget;
    private UiOptionRow _resolutionRow;
    private UiOptionRow _monitorRow;
    private List<Vector2I> _resolutions = new();

    public override void _Ready() => RelinkFocus();

    public void SetScrollMinHeight(float height)
    {
        if (_scroll != null) _scroll.CustomMinimumSize = new Vector2(0, height);
    }

    // Foco inicial ao abrir a página: o botão da aba atual (não a primeira aba).
    public Control PreferredFocus => _tabs != null && _tabs.Buttons.Count > 0 ? _tabs.Buttons[_tabs.Selected] : null;

    // L1/R1 (Q/E) com o foco em qualquer linha. Só com o painel visível e sem outra camada modal por cima (a confirmação).
    public override void _Input(InputEvent @event)
    {
        if (_tabs == null || !IsVisibleInTree() || UiModalStack.Top != null) return;
        if (_tabs.HandleInput(@event)) GetViewport().SetInputAsHandled();
    }

    public void Build(bool showLanguage)
    {
        Name = "OptionsPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", UiMetrics.S2);
        var settings = GlobalSettings.Instance;
        if (settings == null) return;

        _tabs = UiComponents.Tabs(new[] { Localization.Text("options.tab.video"), Localization.Text("options.tab.audio"), Localization.Text("options.tab.game") }, OnTabChanged);
        _tabs.Root.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_tabs.Root);

        _scroll = UiComponents.ScrollList(ScrollMinHeight, out var list);
        list.AddThemeConstantOverride("separation", 0);
        AddChild(_scroll);
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i] = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
            _pages[i].AddThemeConstantOverride("separation", UiMetrics.S1);
            list.AddChild(_pages[i]);
        }

        BuildVideoTab(settings);
        BuildAudioTab(settings);
        BuildGameTab(settings, showLanguage);

        RememberedTab = Math.Clamp(RememberedTab, 0, 2);
        _tabs.Select(RememberedTab, false);
        ShowTab(RememberedTab, false);
        RefreshAll();
    }

    // ---------- Montagem das abas ----------

    private void BuildVideoTab(GlobalSettings settings)
    {
        var d = settings.Data;
        string Off() => Localization.Text("ui.common.off");
        string Quality(int level) => level switch { 1 => Localization.Text("options.quality.low"), 2 => Localization.Text("options.quality.medium"), _ => Localization.Text("options.quality.high") };
        string Percent(double v) => $"{Mathf.RoundToInt((float)v)}%";

        AddChoice(TabVideo, Localization.Text("options.preset"),
            new[] { Localization.Text("options.preset.low"), Localization.Text("options.preset.medium"), Localization.Text("options.preset.high"), Localization.Text("options.preset.custom") },
            () => GraphicsSettings.DetectPreset(d),
            i => { if (i <= GraphicsSettings.PresetHigh) settings.ApplyPreset(i); RefreshAll(); });

        AddChoice(TabVideo, Localization.Text("options.display_mode"),
            new[] { Localization.Text("options.display_mode.windowed"), Localization.Text("options.display_mode.borderless"), Localization.Text("options.display_mode.exclusive") },
            () => d.DisplayMode,
            i => ChangeDisplay(() => settings.SetDisplayMode(i)));

        _resolutionRow = AddChoice(TabVideo, Localization.Text("options.resolution"), Array.Empty<string>(),
            () => Math.Max(0, _resolutions.IndexOf(new Vector2I(d.ResolutionWidth, d.ResolutionHeight))),
            i => { if (i >= 0 && i < _resolutions.Count) ChangeDisplay(() => settings.SetResolution(_resolutions[i])); });

        _monitorRow = AddChoice(TabVideo, Localization.Text("options.monitor"), Array.Empty<string>(),
            () => GraphicsSettings.ResolveScreen(d),
            i => ChangeDisplay(() => settings.SetMonitor(i)));

        AddChoice(TabVideo, Localization.Text("options.vsync"),
            new[] { Off(), Localization.Text("ui.common.on"), Localization.Text("options.vsync.adaptive") },
            () => d.VsyncMode,
            i => { settings.SetVsync(i); RefreshAll(); });

        var fpsLabels = new string[GraphicsSettings.FpsLimits.Length];
        for (var i = 0; i < fpsLabels.Length; i++)
            fpsLabels[i] = GraphicsSettings.FpsLimits[i] == 0 ? Localization.Text("options.fps_limit.unlimited") : GraphicsSettings.FpsLimits[i].ToString();
        AddChoice(TabVideo, Localization.Text("options.fps_limit"), fpsLabels,
            () => NearestFpsIndex(d.MaxFps),
            i => { settings.SetMaxFps(GraphicsSettings.FpsLimits[Math.Clamp(i, 0, GraphicsSettings.FpsLimits.Length - 1)]); RefreshAll(); });

        AddSlider(TabVideo, Localization.Text("options.render_scale"), 50, 100, 5, () => d.RenderScalePercent, v => { settings.SetRenderScale((int)v); RefreshAll(); }, Percent);

        AddChoice(TabVideo, Localization.Text("options.upscaler"),
            new[] { Localization.Text("options.upscaler.bilinear"), Localization.Text("options.upscaler.fsr") },
            () => d.Upscaler,
            i => { settings.SetUpscaler(i); RefreshAll(); });

        AddChoice(TabVideo, Localization.Text("options.antialiasing"),
            new[] { Off(), Localization.Text("options.antialiasing.fxaa"), Localization.Text("options.antialiasing.msaa2"), Localization.Text("options.antialiasing.msaa4") },
            () => d.AntiAliasing,
            i => { settings.SetAntiAliasing(i); RefreshAll(); });

        AddChoice(TabVideo, Localization.Text("options.shadows"),
            new[] { Off(), Quality(1), Quality(2), Quality(3) },
            () => d.ShadowQuality,
            i => { settings.SetShadowQuality(i); RefreshAll(); });

        AddChoice(TabVideo, Localization.Text("options.ssao"),
            new[] { Off(), Quality(1), Quality(2), Quality(3) },
            () => d.SsaoQuality,
            i => { settings.SetSsaoQuality(i); RefreshAll(); });

        AddToggle(TabVideo, Localization.Text("options.glow"), () => d.Glow, on => { settings.SetGlow(on); RefreshAll(); });

        AddSlider(TabVideo, Localization.Text("options.brightness"), 50, 150, 5, () => d.BrightnessPercent, v => settings.SetBrightness((int)v), Percent);
    }

    private void BuildAudioTab(GlobalSettings settings)
    {
        var d = settings.Data;
        AddSlider(TabAudio, Localization.Text("options.volume_music"), 0, 100, 1, () => (int)d.MusicVolume, v => settings.SetMusicVolume((float)v), null);
        AddSlider(TabAudio, Localization.Text("options.volume_effects"), 0, 100, 1, () => (int)d.EffectsVolume, v => settings.SetEffectsVolume((float)v), null);
    }

    private void BuildGameTab(GlobalSettings settings, bool showLanguage)
    {
        var d = settings.Data;
        if (showLanguage)
        {
            AddChoice(TabGame, Localization.Text("options.language"),
                new[] { Localization.Text("options.language.pt_br"), Localization.Text("options.language.en"), Localization.Text("options.language.es") },
                () => (Localization.Instance?.CurrentLanguage ?? d.Language) switch { "en" => 1, "es" => 2, _ => 0 },
                i => settings.SetLanguage(i == 1 ? "en" : i == 2 ? "es" : "pt-BR"));
        }
        var steps = new[] { "0%", "50%", "100%" };
        AddChoice(TabGame, Localization.Text("options.camera_shake"), steps,
            () => Math.Clamp((int)Math.Round(d.CameraShakeReduction / 50.0), 0, 2),
            i => settings.SetCameraShakeReduction(i * 50));
        AddChoice(TabGame, Localization.Text("options.damage_flash"), steps,
            () => Math.Clamp((int)Math.Round(d.DamageFlashPercent / 50.0), 0, 2),
            i => settings.SetDamageFlash(i * 50));
    }

    private UiOptionRow AddChoice(int tab, string label, IReadOnlyList<string> items, Func<int> get, Action<int> set)
    {
        var row = UiComponents.OptionRow(label, items, get(), set);
        // Largura fixa: o texto longo (ex.: "Monitor 1 (1920 x 1080)") é cortado em vez de alargar a janela.
        row.Options.FitToLongestItem = false;
        row.Options.ClipText = true;
        _pages[tab].AddChild(row.Root);
        _rows[tab].Add(row);
        _refreshers.Add(() =>
        {
            if (row.Options.ItemCount > 0) row.Options.Select(Mathf.Clamp(get(), 0, row.Options.ItemCount - 1));
        });
        return row;
    }

    private UiOptionRow AddSlider(int tab, string label, double min, double max, double step, Func<int> get, Action<double> set, Func<double, string> format)
    {
        var row = UiComponents.LabeledSlider(label, min, max, get(), set, step, format);
        row.Slider.Scrollable = false; // a roda do mouse rola a lista, não muda o valor
        row.Value.CustomMinimumSize = new Vector2(64, 0);
        _pages[tab].AddChild(row.Root);
        _rows[tab].Add(row);
        _refreshers.Add(() =>
        {
            var value = get();
            row.Slider.SetValueNoSignal(value);
            row.Value.Text = format != null ? format(value) : value.ToString();
        });
        return row;
    }

    private UiOptionRow AddToggle(int tab, string label, Func<bool> get, Action<bool> set)
    {
        var row = UiComponents.ToggleRow(label, get(), set);
        _pages[tab].AddChild(row.Root);
        _rows[tab].Add(row);
        _refreshers.Add(() => UiComponents.SetToggleRow(row, get()));
        return row;
    }

    private static int NearestFpsIndex(int fps)
    {
        var best = 0;
        for (var i = 0; i < GraphicsSettings.FpsLimits.Length; i++)
        {
            var candidate = GraphicsSettings.FpsLimits[i];
            if (candidate == fps) return i;
            // 0 (ilimitado) é o extremo superior.
            var distance = Math.Abs((candidate == 0 ? 100000 : candidate) - (fps == 0 ? 100000 : fps));
            var bestValue = GraphicsSettings.FpsLimits[best];
            if (distance < Math.Abs((bestValue == 0 ? 100000 : bestValue) - (fps == 0 ? 100000 : fps))) best = i;
        }
        return best;
    }

    // ---------- Atualização dos controles ----------

    // Lê o GlobalSettings de novo e atualiza todas as linhas sem disparar os handlers (presets, reversão, monitor novo).
    public void RefreshAll()
    {
        var settings = GlobalSettings.Instance;
        if (settings == null || _resolutionRow == null) return;
        var d = settings.Data;
        var screen = GraphicsSettings.ResolveScreen(d);
        _resolutions = GraphicsSettings.ResolutionOptions(screen, d.DisplayMode == GraphicsSettings.ModeWindowed);
        FillOptions(_resolutionRow.Options, _resolutions.ConvertAll(r => $"{r.X} x {r.Y}"));
        _resolutionRow.Options.Disabled = d.DisplayMode != GraphicsSettings.ModeWindowed;

        var screens = GraphicsSettings.ScreenCount;
        _monitorRow.Root.Visible = screens > 1;
        var monitors = new List<string>();
        for (var i = 0; i < screens; i++)
        {
            var size = DisplayServer.ScreenGetSize(i);
            monitors.Add(Localization.TextFormat("options.monitor_item", i + 1, size.X, size.Y));
        }
        FillOptions(_monitorRow.Options, monitors);

        foreach (var refresh in _refreshers) refresh();
        RelinkFocus();
    }

    private static void FillOptions(OptionButton button, List<string> items)
    {
        button.Clear();
        foreach (var item in items) button.AddItem(item);
    }

    // ---------- Abas e foco ----------

    private void OnTabChanged(int index)
    {
        RememberedTab = index;
        ShowTab(index, true);
    }

    public void SelectTab(int index)
    {
        if (_tabs == null) return;
        index = Math.Clamp(index, 0, 2);
        RememberedTab = index;
        _tabs.Select(index, false);
        ShowTab(index, false);
    }

    private void ShowTab(int index, bool moveFocus)
    {
        var owner = GetViewport()?.GuiGetFocusOwner();
        var focusWasInList = moveFocus && owner != null && _scroll.IsAncestorOf(owner);
        var focusWasOnTab = moveFocus && owner is Button tabOwner && _tabs.Buttons.Contains(tabOwner);
        for (var i = 0; i < _pages.Length; i++) _pages[i].Visible = i == index;
        _scroll.ScrollVertical = 0;
        RelinkFocus();
        if (focusWasOnTab) _tabs.Buttons[index].GrabFocus();
        else if (focusWasInList)
        {
            var first = FocusableControls(index);
            if (first.Count > 0) first[0].GrabFocus();
        }
    }

    private List<Control> FocusableControls(int tab)
    {
        var items = new List<Control>();
        foreach (var row in _rows[tab])
            if (row.Root.Visible && row.Control != null && row.Control is not BaseButton { Disabled: true })
                items.Add(row.Control);
        return items;
    }

    // Botão logo abaixo do painel ("Voltar"): vira o próximo da última linha e o anterior da primeira aba.
    public void SetFooterTarget(Control target)
    {
        _footerTarget = target;
        RelinkFocus();
    }

    // Vizinhança explícita da aba atual (as outras abas estão ocultas; o ciclo geométrico do Godot não as conhece).
    private void RelinkFocus()
    {
        if (_tabs == null || !IsInsideTree()) return;
        var tab = _tabs.Selected;
        var items = FocusableControls(tab);
        var footer = _footerTarget != null && IsInstanceValid(_footerTarget) && _footerTarget.IsInsideTree() ? _footerTarget : null;
        var selectedTab = _tabs.Buttons[tab];

        foreach (var button in _tabs.Buttons)
        {
            button.FocusNeighborTop = button.GetPathTo(footer ?? button);
            button.FocusNeighborBottom = button.GetPathTo(items.Count > 0 ? items[0] : button);
        }
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.FocusNeighborLeft = item.GetPathTo(item);
            item.FocusNeighborRight = item.GetPathTo(item);
            item.FocusNeighborTop = item.GetPathTo(i == 0 ? selectedTab : items[i - 1]);
            item.FocusNeighborBottom = item.GetPathTo(i < items.Count - 1 ? items[i + 1] : footer ?? item);
        }
        if (footer != null)
        {
            footer.FocusNeighborTop = footer.GetPathTo(items.Count > 0 ? items[items.Count - 1] : selectedTab);
            footer.FocusNeighborBottom = footer.GetPathTo(selectedTab);
        }
    }

    // ---------- Confirmação "Manter estas configurações?" ----------

    private void ChangeDisplay(Action change)
    {
        var settings = GlobalSettings.Instance;
        if (settings == null) return;
        var before = settings.BeginDisplayTrial();
        change();
        RefreshAll();
        if (settings.CaptureDisplay() == before)
        {
            settings.CancelDisplayTrial();
            return;
        }
        OpenKeepConfirm(before, false);
    }

    // Só debug (UiPreview): rola a lista até o fim (mostra as últimas linhas da aba Vídeo na captura).
    public void PreviewScrollToEnd()
    {
        GetTree().CreateTimer(0.3, true, false, true).Timeout += () =>
        {
            if (IsInstanceValid(_scroll)) _scroll.ScrollVertical = 100000;
        };
    }

    // Só debug (UiPreview): abre a confirmação sem alterar nada.
    public void PreviewKeepConfirm()
    {
        var settings = GlobalSettings.Instance;
        if (settings != null) OpenKeepConfirm(settings.CaptureDisplay(), true);
    }

    private void OpenKeepConfirm(DisplaySnapshot before, bool preview)
    {
        var settings = GlobalSettings.Instance;
        // A janela da confirmação cobre a tela toda: sobe até o Control mais alto (HUD ou menu).
        Control host = this;
        while (host.GetParent() is Control parent) host = parent;

        var startedAt = Time.GetTicksMsec();
        Godot.Timer ticker = null;
        var confirm = UiComponents.Confirm(host,
            Localization.Text("options.confirm_title"),
            Localization.TextFormat("options.confirm_body", (int)KeepSeconds),
            Localization.Text("options.confirm_keep"),
            () => { if (!preview) settings?.CommitDisplay(); },
            ConfirmKind.Neutral,
            () => { if (!preview) { settings?.RestoreDisplay(before); if (IsInsideTree()) RefreshAll(); } });
        confirm.CancelButton.Text = Localization.Text("options.confirm_revert");
        confirm.Window.Footer.Clear();
        confirm.Window.Footer.Add(UiAction.Cancel, Localization.Text("options.confirm_revert"), confirm.Cancel, left: true);
        confirm.Window.Footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"));
        ControllerUiSupport.SetHorizontalCycle(new Control[] { confirm.CancelButton, confirm.ConfirmButton });

        var bodyLabel = confirm.Window.Body.GetChildCount() > 0 ? confirm.Window.Body.GetChild(0) as Label : null;
        // Tempo real (OS): a contagem continua com a árvore pausada e não depende do time scale.
        ticker = new Godot.Timer { WaitTime = 0.25, OneShot = false, Autostart = true, IgnoreTimeScale = true };
        ticker.Timeout += () =>
        {
            var remaining = KeepSeconds - (Time.GetTicksMsec() - startedAt) / 1000.0f;
            if (remaining <= 0.0f)
            {
                if (confirm.IsOpen) confirm.Cancel();
                return;
            }
            if (bodyLabel != null && GodotObject.IsInstanceValid(bodyLabel))
                bodyLabel.Text = Localization.TextFormat("options.confirm_body", Mathf.CeilToInt(remaining));
        };
        confirm.Window.Root.AddChild(ticker);
        UiModalStack.Push(confirm.Window.Root, pauses: false, initialFocus: confirm.CancelButton, onCancel: confirm.Cancel);
    }
}

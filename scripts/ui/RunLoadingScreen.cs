using Godot;

// Tela de carregamento curta do início da run (RunStartGate): escurece a tela, mostra o nome e a regra do mapa
// atual com "Carregando..." e uma barra fina, carrega a cena em segundo plano e clareia já na cena nova.
// Fica na raiz da árvore (sobrevive à troca de cena) e se apaga sozinha. Não pausa o jogo (a pausa é só do
// ControllerUiSupport): o jogador é desligado enquanto a tela está escura.
public partial class RunLoadingScreen : CanvasLayer
{
    private const float FadeInSeconds = 0.35f;
    private const float FadeOutSeconds = 0.45f;
    // Tempo mínimo com a tela cheia, para a passagem não piscar quando a cena carrega rápido.
    private const float MinimumHoldSeconds = 1.1f;
    private const float BarWidth = 360.0f;

    private string _scenePath;
    private Control _root;
    private Label _loadingLabel;
    private ColorRect _barFill;
    private bool _requested;
    private bool _changed;
    private double _held;
    private double _dotsTime;

    public static void Start(SceneTree tree, string scenePath)
    {
        if (tree?.Root == null || string.IsNullOrWhiteSpace(scenePath))
            return;
        tree.Root.AddChild(new RunLoadingScreen { Name = "RunLoadingScreen", _scenePath = scenePath });
    }

    public override void _Ready()
    {
        Layer = 120;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Control { Theme = UIThemeFactory.Create(), MouseFilter = Control.MouseFilterEnum.Stop, Modulate = new Color(1, 1, 1, 0) };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var background = new ColorRect { Color = UiPalette.ScreenBg, MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(background);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", UiMetrics.S3);
        center.AddChild(column);

        var map = RunState.CurrentMap;
        column.AddChild(new Label { Text = map.Name, ThemeTypeVariation = UiVariation.BannerLabel, HorizontalAlignment = HorizontalAlignment.Center });
        column.AddChild(new Label { Text = map.SpecialRule, ThemeTypeVariation = UiVariation.SecondaryLabel, HorizontalAlignment = HorizontalAlignment.Center });

        var track = new ColorRect { Color = UiPalette.PanelInset, CustomMinimumSize = new Vector2(BarWidth, 3.0f), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _barFill = new ColorRect { Color = UiPalette.GoldDim, Size = new Vector2(0.0f, 3.0f) };
        track.AddChild(_barFill);
        var spacer = new Control { CustomMinimumSize = new Vector2(0.0f, UiMetrics.S5) };
        column.AddChild(spacer);
        column.AddChild(track);

        _loadingLabel = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_loadingLabel);
        UpdateLoadingText();

        var tween = CreateTween();
        tween.TweenProperty(_root, "modulate:a", 1.0f, FadeInSeconds);
        tween.TweenCallback(Callable.From(OnCovered));
    }

    // Tela coberta: desliga o jogador e pede a cena nova.
    private void OnCovered()
    {
        foreach (var node in GetTree().GetNodesInGroup("player"))
            node.ProcessMode = ProcessModeEnum.Disabled;
        var error = ResourceLoader.LoadThreadedRequest(_scenePath);
        if (error != Error.Ok)
        {
            GD.PushError($"RunLoadingScreen: carga da cena falhou ({error}): {_scenePath}");
            Finish(null);
            return;
        }
        _requested = true;
    }

    public override void _Process(double delta)
    {
        _dotsTime += delta;
        UpdateLoadingText();
        if (!_requested || _changed)
            return;

        _held += delta;
        var progress = new Godot.Collections.Array();
        var status = ResourceLoader.LoadThreadedGetStatus(_scenePath, progress);
        var ratio = progress.Count > 0 ? (float)progress[0] : 0.0f;
        // A barra anda pelo menor entre o progresso real e o tempo mínimo: enche num ritmo regular mesmo com carga instantânea.
        var shown = Mathf.Min(ratio, (float)(_held / MinimumHoldSeconds));
        _barFill.Size = new Vector2(BarWidth * Mathf.Clamp(shown, 0.0f, 1.0f), 3.0f);

        if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
        {
            GD.PushError($"RunLoadingScreen: cena nao carregada: {_scenePath}");
            Finish(null);
            return;
        }
        if (status == ResourceLoader.ThreadLoadStatus.Loaded && _held >= MinimumHoldSeconds)
            Finish(ResourceLoader.LoadThreadedGet(_scenePath) as PackedScene);
    }

    private void Finish(PackedScene scene)
    {
        _changed = true;
        _barFill.Size = new Vector2(BarWidth, 3.0f);
        if (scene != null)
            GetTree().ChangeSceneToPacked(scene);
        else
            GetTree().ChangeSceneToFile(_scenePath);

        // Clareia depois de a cena nova desenhar o primeiro quadro.
        var timer = GetTree().CreateTimer(0.15, true);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this))
                return;
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate:a", 0.0f, FadeOutSeconds);
            tween.TweenCallback(Callable.From(QueueFree));
        };
    }

    private void UpdateLoadingText()
    {
        var dots = (int)(_dotsTime * 3.0) % 4;
        _loadingLabel.Text = Localization.Text("loading.run") + new string('.', dots);
    }
}

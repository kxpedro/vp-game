using Godot;

// Passagem curta entre salas por um portão (ScenePortal): o jogador segue andando para dentro do arco enquanto a tela
// escurece, a cena nova carrega em segundo plano com um "Carregando..." discreto e a tela clareia já na sala nova, onde
// o RoomGenerator põe o jogador saindo de outro portão (EntryWalk). Fica na raiz da árvore (sobrevive à troca de cena)
// e se apaga sozinha. Diferente da RunLoadingScreen (início da run), não mostra o nome do mapa.
public partial class RoomTransition : CanvasLayer
{
    private const float FadeOutSeconds = 0.4f;
    private const float FadeInSeconds = 0.45f;
    // Tempo mínimo com a tela preta, para a passagem ler como "atravessar" mesmo com carga instantânea.
    private const float MinimumHoldSeconds = 0.3f;
    // Atraso antes de clarear na cena nova (o primeiro quadro e os primeiros passos para fora do portão).
    private const float RevealDelaySeconds = 0.12f;

    /// <summary>True da escolha do portão até a cena nova começar a clarear: o jogador não ataca nem anda por conta própria.</summary>
    public static bool IsBusy { get; private set; }

    private string _scenePath;
    private Control _root;
    private Label _loadingLabel;
    private bool _requested;
    private bool _changed;
    private double _held;
    private double _dotsTime;

    public static void Start(SceneTree tree, string scenePath, PlayerController player, Vector3 walkDirection)
    {
        if (tree?.Root == null || string.IsNullOrWhiteSpace(scenePath))
            return;
        if (tree.Root.GetNodeOrNull("RoomTransition") != null)
            return;
        IsBusy = true;
        player ??= tree.GetFirstNodeInGroup("player") as PlayerController;
        if (player != null && walkDirection.LengthSquared() > 0.001f)
            player.StartScriptedWalk(walkDirection, FadeOutSeconds + MinimumHoldSeconds);
        tree.Root.AddChild(new RoomTransition { Name = "RoomTransition", _scenePath = scenePath });
    }

    public override void _Ready()
    {
        Layer = 120;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Modulate = new Color(1, 1, 1, 0) };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var background = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(background);

        _loadingLabel = new Label
        {
            Theme = UIThemeFactory.Create(),
            ThemeTypeVariation = UiVariation.SecondaryLabel,
            HorizontalAlignment = HorizontalAlignment.Right,
            Modulate = new Color(1, 1, 1, 0.7f)
        };
        _loadingLabel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _loadingLabel.GrowHorizontal = Control.GrowDirection.Begin;
        _loadingLabel.GrowVertical = Control.GrowDirection.Begin;
        _loadingLabel.OffsetRight = -UiMetrics.S5;
        _loadingLabel.OffsetBottom = -UiMetrics.S5;
        _root.AddChild(_loadingLabel);
        UpdateLoadingText();

        var tween = CreateTween();
        tween.TweenProperty(_root, "modulate:a", 1.0f, FadeOutSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(OnCovered));
    }

    public override void _ExitTree()
    {
        IsBusy = false;
    }

    private void OnCovered()
    {
        var error = ResourceLoader.LoadThreadedRequest(_scenePath);
        if (error != Error.Ok)
        {
            GD.PushError($"RoomTransition: carga da cena falhou ({error}): {_scenePath}");
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
        var status = ResourceLoader.LoadThreadedGetStatus(_scenePath);
        if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
        {
            GD.PushError($"RoomTransition: cena nao carregada: {_scenePath}");
            Finish(null);
            return;
        }
        if (status == ResourceLoader.ThreadLoadStatus.Loaded && _held >= MinimumHoldSeconds)
            Finish(ResourceLoader.LoadThreadedGet(_scenePath) as PackedScene);
    }

    private void Finish(PackedScene scene)
    {
        _changed = true;
        if (scene != null)
            GetTree().ChangeSceneToPacked(scene);
        else
            GetTree().ChangeSceneToFile(_scenePath);

        var timer = GetTree().CreateTimer(RevealDelaySeconds, true);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this))
                return;
            IsBusy = false;
            _loadingLabel.Visible = false;
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate:a", 0.0f, FadeInSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenCallback(Callable.From(QueueFree));
        };
    }

    private void UpdateLoadingText()
    {
        // Só depois de a tela fechar, para não piscar numa troca rápida.
        _loadingLabel.Visible = _requested && !_changed && _held > 0.15;
        var dots = (int)(_dotsTime * 3.0) % 4;
        _loadingLabel.Text = Localization.Text("loading.run") + new string('.', dots);
    }
}

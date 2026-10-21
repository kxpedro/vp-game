using Godot;

public partial class CombatFeedback : Node
{
    public static CombatFeedback Instance { get; private set; }

    [Export] public float LightHitStopSeconds { get; set; } = 0.022f;
    [Export] public float HeavyHitStopSeconds { get; set; } = 0.038f;
    [Export] public float CriticalHitStopBonus { get; set; } = 0.010f;
    [Export] public float HitStopTimeScale { get; set; } = 0.18f;

    // Ao receber dano: hit-stop curto (limite de seguranca 50 ms) e flash de tela.
    [Export] public float PlayerDamagedHitStopSeconds { get; set; } = 0.045f;
    [Export] public float PlayerDamagedFlashAlpha { get; set; } = 0.20f;
    // Multiplicador global de flash (0 desliga); o shake tem IsoCamera.ShakeIntensity.
    public static float FlashIntensity { get; set; } = 1.0f;

    private ulong _hitStopUntilMilliseconds;
    private bool _hitStopActive;
    private double _timeScaleBeforeHitStop = 1.0;
    private CanvasLayer _flashLayer;
    private ColorRect _flashRect;
    private Tween _flashTween;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _flashLayer = new CanvasLayer { Name = "DamageFlashLayer", Layer = 40 };
        _flashRect = new ColorRect
        {
            Name = "DamageFlash",
            Color = new Color(1.0f, 0.45f, 0.40f, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _flashRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _flashLayer.AddChild(_flashRect);
        AddChild(_flashLayer);
    }

    public override void _Process(double delta)
    {
        if (_hitStopActive && Time.GetTicksMsec() >= _hitStopUntilMilliseconds)
            EndHitStop();
    }

    public void PlayPlayerHit(int targetsHit, bool isCritical, bool isHeavy)
    {
        if (targetsHit <= 0)
            return;

        var duration = isHeavy ? HeavyHitStopSeconds : LightHitStopSeconds;
        if (isCritical)
            duration += CriticalHitStopBonus;
        duration += Mathf.Min(targetsHit - 1, 3) * 0.003f;

        RequestHitStop(duration);
        GetTree().CallGroup("combat_camera", "Shake", isHeavy || isCritical ? 0.060f : 0.035f, isHeavy ? 0.10f : 0.07f);
    }

    // weight: 0 = golpe leve, 1 = finalizador/rapido, 1.5 = especial.
    public void PlayPlayerHitWeighted(int targetsHit, bool isCritical, float weight)
    {
        if (targetsHit <= 0)
            return;

        weight = Mathf.Clamp(weight, 0.0f, 1.5f);
        var duration = Mathf.Lerp(LightHitStopSeconds, 0.045f, Mathf.Min(weight, 1.0f)) + weight * 0.006f;
        if (isCritical)
            duration += CriticalHitStopBonus;
        duration += Mathf.Min(targetsHit - 1, 3) * 0.003f;

        RequestHitStop(Mathf.Min(duration, 0.058f));
        var strength = Mathf.Min(Mathf.Lerp(0.035f, 0.065f, Mathf.Min(weight, 1.0f)) + (isCritical ? 0.012f : 0.0f), 0.08f);
        GetTree().CallGroup("combat_camera", "Shake", strength, 0.07f + weight * 0.03f);
    }

    public void PlayPlayerDamaged(float amount)
    {
        if (amount <= 0.0f)
            return;

        RequestHitStop(Mathf.Min(PlayerDamagedHitStopSeconds, 0.05f));
        GetTree().CallGroup("combat_camera", "Shake", 0.075f, 0.14f);

        if (_flashRect == null || FlashIntensity <= 0.0f)
            return;

        _flashTween?.Kill();
        _flashRect.Visible = true;
        _flashRect.Color = new Color(1.0f, 0.45f, 0.40f, PlayerDamagedFlashAlpha * FlashIntensity);
        _flashTween = CreateTween().SetIgnoreTimeScale(true);
        _flashTween.TweenProperty(_flashRect, "color:a", 0.0f, 0.20f);
        _flashTween.TweenCallback(Callable.From(() => _flashRect.Visible = false));
    }

    private void RequestHitStop(float duration)
    {
        var requestedEnd = Time.GetTicksMsec() + (ulong)Mathf.RoundToInt(duration * 1000.0f);
        _hitStopUntilMilliseconds = System.Math.Max(_hitStopUntilMilliseconds, requestedEnd);

        if (_hitStopActive)
            return;

        _hitStopActive = true;
        _timeScaleBeforeHitStop = Engine.TimeScale;
        Engine.TimeScale = HitStopTimeScale;
    }

    private void EndHitStop()
    {
        if (!_hitStopActive)
            return;

        _hitStopActive = false;
        Engine.TimeScale = _timeScaleBeforeHitStop;
    }

    // Escala base de tempo do painel de dev (so debug). Durante um hit-stop altera so o valor a restaurar, para EndHitStop nao desfazer a mudanca.
    public static void DevSetBaseTimeScale(float scale)
    {
        if (!OS.IsDebugBuild())
            return;

        if (Instance != null && Instance._hitStopActive)
            Instance._timeScaleBeforeHitStop = scale;
        else
            Engine.TimeScale = scale;
    }

    public override void _ExitTree()
    {
        EndHitStop();
        if (Instance == this)
            Instance = null;
    }
}

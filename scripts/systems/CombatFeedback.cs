using Godot;

public partial class CombatFeedback : Node
{
    public static CombatFeedback Instance { get; private set; }

    [Export] public float LightHitStopSeconds { get; set; } = 0.022f;
    [Export] public float HeavyHitStopSeconds { get; set; } = 0.038f;
    [Export] public float CriticalHitStopBonus { get; set; } = 0.010f;
    [Export] public float HitStopTimeScale { get; set; } = 0.18f;

    private ulong _hitStopUntilMilliseconds;
    private bool _hitStopActive;
    private double _timeScaleBeforeHitStop = 1.0;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
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

    public override void _ExitTree()
    {
        EndHitStop();
        if (Instance == this)
            Instance = null;
    }
}

using Godot;

public partial class IsoCamera : Camera3D
{
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    [Export] public float CameraYawDegrees { get; set; } = 45.0f;
    [Export] public float CameraPitchDegrees { get; set; } = 60.0f;
    [Export] public float StartZoomDistance { get; set; } = 15.0f;
    [Export] public float FollowSpeed { get; set; } = 18.0f;
    [Export] public float ShakeFrequency { get; set; } = 32.0f;

    private Node3D _target;
    private Vector3 _offsetDirection;
    private Vector3 _fixedRotation;
    private float _zoomDistance;
    private float _shakeStrength;
    private float _shakeDuration;
    private float _shakeRemaining;
    private float _shakePhase;

    public override void _Ready()
    {
        AddToGroup("combat_camera");
        _target = GetNodeOrNull<Node3D>(TargetPath);
        GameTuning.Changed += ApplyRuntimeTuning;
        ApplyRuntimeTuning();
        _offsetDirection = BuildOffsetDirection();
        GlobalRotation = BuildCameraRotation();

        if (_target != null)
        {
            GlobalPosition = _target.GlobalPosition + _offsetDirection * _zoomDistance;
            LookAt(_target.GlobalPosition + Vector3.Up * 0.35f, Vector3.Up);
            _fixedRotation = GlobalRotation;
        }

        Current = true;
    }

    public override void _ExitTree()
    {
        GameTuning.Changed -= ApplyRuntimeTuning;
    }

    private void ApplyRuntimeTuning()
    {
        _zoomDistance = StartZoomDistance * GameTuning.GetCameraZoomDistanceMultiplier();
    }

    public override void _Process(double delta)
    {
        if (_target == null)
            return;

        var desiredPosition = _target.GlobalPosition + _offsetDirection * _zoomDistance;
        GlobalPosition = GlobalPosition.Lerp(desiredPosition, 1.0f - Mathf.Exp(-FollowSpeed * (float)delta));
        ApplyShake((float)delta);
        GlobalRotation = _fixedRotation;
    }

    // Multiplicador global do shake (0 desliga; reducao de movimento).
    public static float ShakeIntensity { get; set; } = 1.0f;

    public void Shake(float strength, float duration)
    {
        strength *= ShakeIntensity;
        if (strength <= 0.0f)
            return;
        _shakeStrength = Mathf.Max(_shakeStrength, strength);
        _shakeDuration = Mathf.Max(_shakeDuration, duration);
        _shakeRemaining = Mathf.Max(_shakeRemaining, duration);
    }

    private void ApplyShake(float delta)
    {
        if (_shakeRemaining <= 0.0f)
            return;

        _shakeRemaining = Mathf.Max(_shakeRemaining - delta, 0.0f);
        _shakePhase += delta * ShakeFrequency;
        var falloff = _shakeDuration > 0.0f ? _shakeRemaining / _shakeDuration : 0.0f;
        var offset = new Vector3(
            Mathf.Sin(_shakePhase * 1.37f),
            Mathf.Cos(_shakePhase * 1.91f) * 0.55f,
            Mathf.Sin(_shakePhase * 2.23f)
        ) * _shakeStrength * falloff;
        GlobalPosition += offset;

        if (_shakeRemaining <= 0.0f)
        {
            _shakeStrength = 0.0f;
            _shakeDuration = 0.0f;
        }
    }

    private Vector3 BuildOffsetDirection()
    {
        var yaw = Mathf.DegToRad(CameraYawDegrees);
        var pitch = Mathf.DegToRad(CameraPitchDegrees);
        var horizontal = Mathf.Cos(pitch);
        return new Vector3(
            Mathf.Sin(yaw) * horizontal,
            Mathf.Sin(pitch),
            Mathf.Cos(yaw) * horizontal
        ).Normalized();
    }

    private Vector3 BuildCameraRotation()
    {
        return new Vector3(
            Mathf.DegToRad(-CameraPitchDegrees),
            Mathf.DegToRad(CameraYawDegrees),
            0.0f
        );
    }
}

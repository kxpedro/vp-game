using Godot;

public partial class IsoCamera : Camera3D
{
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    public const float DefaultYawDegrees = 45.0f;
    public const float DefaultPitchDegrees = 60.0f;
    public const float DefaultStartZoomDistance = 15.0f;
    public const float DefaultFollowSpeed = 18.0f;
    public const float DefaultShakeFrequency = 32.0f;

    [Export] public float CameraYawDegrees { get; set; } = DefaultYawDegrees;
    [Export] public float CameraPitchDegrees { get; set; } = DefaultPitchDegrees;
    [Export] public float StartZoomDistance { get; set; } = DefaultStartZoomDistance;
    [Export] public float FollowSpeed { get; set; } = DefaultFollowSpeed;
    [Export] public float ShakeFrequency { get; set; } = DefaultShakeFrequency;

    // Travas usadas pelo hub em pixel art (PixelArtHub.cs): o ângulo e a distância ignoram o tuning da câmera.
    public float? LockedYawDegrees { get; set; }
    public float? LockedPitchDegrees { get; set; }
    public float? LockedZoomDistance { get; set; }

    private float Yaw => LockedYawDegrees ?? CompendiumTuning.GetFloat("camera.yaw", CameraYawDegrees);
    private float Pitch => LockedPitchDegrees ?? CompendiumTuning.GetFloat("camera.pitch", CameraPitchDegrees);

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
        CompendiumTuning.Changed += OnCompendiumTuningChanged;
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
        CompendiumTuning.Changed -= OnCompendiumTuningChanged;
    }

    private void ApplyRuntimeTuning()
    {
        _zoomDistance = LockedZoomDistance ?? CompendiumTuning.GetFloat("camera.zoom_distance", StartZoomDistance) * GameTuning.GetCameraZoomDistanceMultiplier();
    }

    private void OnCompendiumTuningChanged(string key)
    {
        if (key != null && !key.StartsWith("camera."))
            return;

        ApplyRuntimeTuning();
        if (_target == null)
            return;

        // Yaw/pitch mudaram: refaz a direcao do offset e a rotacao fixa (so apos edicao; sem override nada muda).
        _offsetDirection = BuildOffsetDirection();
        _fixedRotation = BuildCameraRotation();
    }

    public override void _Process(double delta)
    {
        if (_target == null)
            return;

        var desiredPosition = _target.GlobalPosition + _offsetDirection * _zoomDistance * TestZoomMultiplier;
        GlobalPosition = GlobalPosition.Lerp(desiredPosition, 1.0f - Mathf.Exp(-CompendiumTuning.GetFloat("camera.follow_speed", FollowSpeed) * (float)delta));
        ApplyShake((float)delta);
        GlobalRotation = _fixedRotation;
    }

    // TESTE: aproximacao da camera no modo pixel (PixelRenderTest.cs); 1 = normal.
    public static float TestZoomMultiplier { get; set; } = 1.0f;

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
        _shakePhase += delta * CompendiumTuning.GetFloat("camera.shake_frequency", ShakeFrequency);
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
        var yaw = Mathf.DegToRad(Yaw);
        var pitch = Mathf.DegToRad(Pitch);
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
            Mathf.DegToRad(-Pitch),
            Mathf.DegToRad(Yaw),
            0.0f
        );
    }
}

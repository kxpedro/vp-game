using Godot;

public partial class IsoCamera : Camera3D
{
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    [Export] public float CameraYawDegrees { get; set; } = 45.0f;
    [Export] public float CameraPitchDegrees { get; set; } = 60.0f;
    [Export] public float StartZoomDistance { get; set; } = 15.0f;
    [Export] public float FollowSpeed { get; set; } = 18.0f;
    [Export] public float MinZoomDistance { get; set; } = 10.0f;
    [Export] public float MaxZoomDistance { get; set; } = 24.0f;
    [Export] public float ZoomStep { get; set; } = 1.2f;

    private Node3D _target;
    private Vector3 _offsetDirection;
    private Vector3 _fixedRotation;
    private float _zoomDistance;

    public override void _Ready()
    {
        _target = GetNodeOrNull<Node3D>(TargetPath);
        _zoomDistance = Mathf.Clamp(StartZoomDistance, MinZoomDistance, MaxZoomDistance);
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            SetZoomDistance(_zoomDistance - ZoomStep);

        if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            SetZoomDistance(_zoomDistance + ZoomStep);
    }

    public override void _Process(double delta)
    {
        if (_target == null)
            return;

        var desiredPosition = _target.GlobalPosition + _offsetDirection * _zoomDistance;
        GlobalPosition = GlobalPosition.Lerp(desiredPosition, 1.0f - Mathf.Exp(-FollowSpeed * (float)delta));
        GlobalRotation = _fixedRotation;
    }

    private void SetZoomDistance(float value)
    {
        _zoomDistance = Mathf.Clamp(value, MinZoomDistance, MaxZoomDistance);
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

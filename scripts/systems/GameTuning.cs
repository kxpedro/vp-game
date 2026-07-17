using Godot;
using System;

public static class GameTuning
{
    private const string SavePath = "user://game_tuning.cfg";
    private const int MinPercent = 1;
    private const int MaxPercent = 250;

    private static int _walkSpeedPercent = 100;
    private static int _runSpeedPercent = 100;
    private static int _dashSpeedPercent = 100;
    private static int _dashDistancePercent = 70;
    private static int _cameraZoomDistancePercent = 100;
    private static int _damagePercent = 100;
    private static int _healthPercent = 100;
    private static int _animationSpeedPercent = 100;
    private static int _attackSpeedPercent = 100;
    private static int _attackDelayPercent = 100;
    private static int _attackRangePercent = 65;

    static GameTuning()
    {
        Load();
    }

    public static event Action Changed;

    public static int WalkSpeedPercent => _walkSpeedPercent;
    public static int RunSpeedPercent => _runSpeedPercent;
    public static int DashSpeedPercent => _dashSpeedPercent;
    public static int DashDistancePercent => _dashDistancePercent;
    public static int CameraZoomDistancePercent => _cameraZoomDistancePercent;
    public static int DamagePercent => _damagePercent;
    public static int HealthPercent => _healthPercent;
    public static int AnimationSpeedPercent => _animationSpeedPercent;
    public static int AttackSpeedPercent => _attackSpeedPercent;
    public static int AttackDelayPercent => _attackDelayPercent;
    public static int AttackRangePercent => _attackRangePercent;

    public static void SetWalkSpeedPercent(int value) => SetPercent(ref _walkSpeedPercent, value);
    public static void SetRunSpeedPercent(int value) => SetPercent(ref _runSpeedPercent, value);
    public static void SetDashSpeedPercent(int value) => SetPercent(ref _dashSpeedPercent, value);
    public static void SetDashDistancePercent(int value) => SetPercent(ref _dashDistancePercent, value);
    public static void SetCameraZoomDistancePercent(int value) => SetPercent(ref _cameraZoomDistancePercent, value);
    public static void SetDamagePercent(int value) => SetPercent(ref _damagePercent, value);
    public static void SetHealthPercent(int value) => SetPercent(ref _healthPercent, value);
    public static void SetAnimationSpeedPercent(int value) => SetPercent(ref _animationSpeedPercent, value);
    public static void SetAttackSpeedPercent(int value) => SetPercent(ref _attackSpeedPercent, value);
    public static void SetAttackDelayPercent(int value) => SetPercent(ref _attackDelayPercent, value);
    public static void SetAttackRangePercent(int value) => SetPercent(ref _attackRangePercent, value);

    public static float GetWalkSpeedMultiplier() => _walkSpeedPercent / 100.0f;
    public static float GetRunSpeedMultiplier() => _runSpeedPercent / 100.0f;
    public static float GetDashSpeedMultiplier() => _dashSpeedPercent / 100.0f;
    public static float GetDashDistanceMultiplier() => _dashDistancePercent / 100.0f;
    public static float GetCameraZoomDistanceMultiplier() => _cameraZoomDistancePercent / 100.0f;
    public static float GetDamageMultiplier() => _damagePercent / 100.0f;
    public static float GetHealthMultiplier() => _healthPercent / 100.0f;
    public static float GetAnimationSpeedMultiplier() => _animationSpeedPercent / 100.0f;
    public static float GetAttackSpeedMultiplier() => _attackSpeedPercent / 100.0f;
    public static float GetAttackDelayMultiplier() => _attackDelayPercent / 100.0f;
    public static float GetAttackRangeMultiplier() => _attackRangePercent / 50.0f;

    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(SavePath) != Error.Ok)
            return;

        _walkSpeedPercent = ReadPercent(config, "tuning", "walk_speed", _walkSpeedPercent);
        _runSpeedPercent = ReadPercent(config, "tuning", "run_speed", _runSpeedPercent);
        _dashSpeedPercent = ReadPercent(config, "tuning", "dash_speed", _dashSpeedPercent);
        _dashDistancePercent = ReadPercent(config, "tuning", "dash_distance", _dashDistancePercent);
        _cameraZoomDistancePercent = ReadPercent(config, "tuning", "camera_zoom_distance", _cameraZoomDistancePercent);
        _damagePercent = ReadPercent(config, "tuning", "damage", _damagePercent);
        _healthPercent = ReadPercent(config, "tuning", "health", _healthPercent);
        _animationSpeedPercent = ReadPercent(config, "tuning", "animation_speed", _animationSpeedPercent);
        _attackSpeedPercent = ReadPercent(config, "tuning", "attack_speed", _attackSpeedPercent);
        _attackDelayPercent = ReadPercent(config, "tuning", "attack_delay", _attackDelayPercent);
        _attackRangePercent = ReadPercent(config, "tuning", "attack_range", _attackRangePercent);

        if (_dashDistancePercent == 100)
            _dashDistancePercent = 70;

        if (_attackRangePercent == 100)
            _attackRangePercent = 65;
    }

    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("tuning", "walk_speed", _walkSpeedPercent);
        config.SetValue("tuning", "run_speed", _runSpeedPercent);
        config.SetValue("tuning", "dash_speed", _dashSpeedPercent);
        config.SetValue("tuning", "dash_distance", _dashDistancePercent);
        config.SetValue("tuning", "camera_zoom_distance", _cameraZoomDistancePercent);
        config.SetValue("tuning", "damage", _damagePercent);
        config.SetValue("tuning", "health", _healthPercent);
        config.SetValue("tuning", "animation_speed", _animationSpeedPercent);
        config.SetValue("tuning", "attack_speed", _attackSpeedPercent);
        config.SetValue("tuning", "attack_delay", _attackDelayPercent);
        config.SetValue("tuning", "attack_range", _attackRangePercent);
        config.Save(SavePath);
    }

    private static void SetPercent(ref int field, int value)
    {
        var clamped = Math.Clamp(value, MinPercent, MaxPercent);
        if (field == clamped)
            return;

        field = clamped;
        Changed?.Invoke();
    }

    private static int ReadPercent(ConfigFile config, StringName section, StringName key, int fallback)
    {
        var value = config.GetValue(section, key, fallback);
        return Math.Clamp(value.AsInt32(), MinPercent, MaxPercent);
    }
}

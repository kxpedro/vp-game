using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Autoload: cursor temático e detecção do último dispositivo (guia de UI §5.5).
/// Joypad (botão ou eixo > 0,3) esconde o cursor; mouse (> 2 px ou clique) mostra; teclado não mexe no cursor.
/// </summary>
public partial class GameCursor : Node
{
    private const string PointerPath = "res://assets/ui/kenney_hud/cursor_pointer.png";
    private const string PointingHandPath = "res://assets/ui/kenney_hud/cursor_gauntlet.png";
    private const float JoyAxisThreshold = 0.3f;
    private const float MouseMoveThreshold = 2.0f;
    private const ulong MouseTravelWindowMs = 250;

    // Tipo de cada joypad por id, para não chamar GetJoyName (aloca string) a cada evento.
    private readonly Dictionary<int, InputDevice> _joyKinds = new();
    private float _mouseTravel;
    private ulong _mouseTravelStartMs;
    private Window _window;

    // Último dispositivo usado (estado em UiInput).
    public static InputDevice LastDevice => UiInput.LastDevice;

    // Disparado quando o dispositivo muda (repassa UiInput.DeviceChanged).
    public static event Action<InputDevice> DeviceChanged
    {
        add => UiInput.DeviceChanged += value;
        remove => UiInput.DeviceChanged -= value;
    }

    public static bool IsUsingJoypad => UiInput.LastDevice != InputDevice.KeyboardMouse;

    public override void _Ready()
    {
        // Precisa receber input com o jogo pausado (menus).
        ProcessMode = ProcessModeEnum.Always;
        ControllerUiSupport.EnsureMappings();
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
        // WindowInput chega antes da propagação: vê também eventos consumidos pela cena (ex.: ui_cancel do HUD).
        _window = GetTree().Root;
        _window.WindowInput += OnWindowInput;

        var pointer = ResourceLoader.Load<Texture2D>(PointerPath);
        var pointingHand = ResourceLoader.Load<Texture2D>(PointingHandPath);
        if (pointer != null)
        {
            Input.SetCustomMouseCursor(pointer, Input.CursorShape.Arrow, new Vector2(5.0f, 4.0f));
            Input.SetCustomMouseCursor(pointer, Input.CursorShape.Help, new Vector2(5.0f, 4.0f));
        }
        if (pointingHand != null)
            Input.SetCustomMouseCursor(pointingHand, Input.CursorShape.PointingHand, new Vector2(14.0f, 5.0f));
    }

    public override void _ExitTree()
    {
        Input.JoyConnectionChanged -= OnJoyConnectionChanged;
        if (_window != null && GodotObject.IsInstanceValid(_window))
            _window.WindowInput -= OnWindowInput;
    }

    private void OnWindowInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventJoypadButton button when button.Pressed:
                UseJoypad(button.Device);
                break;
            case InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) > JoyAxisThreshold:
                UseJoypad(motion.Device);
                break;
            case InputEventMouseMotion mouseMotion:
                if (UiInput.LastDevice == InputDevice.KeyboardMouse && Input.MouseMode != Input.MouseModeEnum.Hidden) break;
                // Acumula numa janela curta para ignorar tremidas pequenas enquanto o controle está em uso.
                var now = Time.GetTicksMsec();
                if (now - _mouseTravelStartMs > MouseTravelWindowMs)
                {
                    _mouseTravel = 0.0f;
                    _mouseTravelStartMs = now;
                }
                _mouseTravel += mouseMotion.Relative.Length();
                if (_mouseTravel > MouseMoveThreshold) UseMouse();
                break;
            case InputEventMouseButton mouseButton when mouseButton.Pressed:
                UseMouse();
                break;
            case InputEventKey key when key.Pressed && !key.Echo:
                _mouseTravel = 0.0f;
                UiInput.Report(InputDevice.KeyboardMouse);
                break;
        }
    }

    private void UseJoypad(int device)
    {
        _mouseTravel = 0.0f;
        UiInput.Report(GetJoyKind(device));
        if (Input.MouseMode == Input.MouseModeEnum.Visible)
            Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    private void UseMouse()
    {
        _mouseTravel = 0.0f;
        UiInput.Report(InputDevice.KeyboardMouse);
        if (Input.MouseMode == Input.MouseModeEnum.Hidden)
            Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private InputDevice GetJoyKind(int device)
    {
        if (_joyKinds.TryGetValue(device, out var kind)) return kind;
        kind = IsPlayStationName(Input.GetJoyName(device)) ? InputDevice.PlayStation : InputDevice.Xbox;
        _joyKinds[device] = kind;
        return kind;
    }

    private static bool IsPlayStationName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // "PS" exato (PS4/PS5); os demais sem diferenciar maiúsculas.
        return name.Contains("PS", StringComparison.Ordinal)
            || name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DualShock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PlayStation", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Sony", StringComparison.OrdinalIgnoreCase);
    }

    private void OnJoyConnectionChanged(long device, bool connected)
    {
        _joyKinds.Remove((int)device);
    }
}

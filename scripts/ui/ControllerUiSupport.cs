using Godot;

public static class ControllerUiSupport
{
    private static bool _configured;

    public static void EnsureMappings()
    {
        if (_configured) return;
        _configured = true;
        AddButton("ui_accept", 0);
        AddButton("ui_cancel", 1);
        AddButton("ui_up", 11);
        AddButton("ui_down", 12);
        AddButton("ui_left", 13);
        AddButton("ui_right", 14);
        AddAxis("ui_up", 1, -1.0f);
        AddAxis("ui_down", 1, 1.0f);
        AddAxis("ui_left", 0, -1.0f);
        AddAxis("ui_right", 0, 1.0f);
    }

    public static void FocusFirst(Control root)
    {
        if (root == null) return;
        foreach (var child in root.FindChildren("*", "Control", true, false))
        {
            if (child is not Control control || control.FocusMode == Control.FocusModeEnum.None || !control.IsVisibleInTree()) continue;
            if (control is BaseButton { Disabled: true }) continue;
            control.GrabFocus();
            return;
        }
    }

    private static void AddButton(string action, int buttonIndex)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var input in InputMap.ActionGetEvents(action))
            if (input is InputEventJoypadButton button && (int)button.ButtonIndex == buttonIndex) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { Device = -1, ButtonIndex = (JoyButton)buttonIndex });
    }

    private static void AddAxis(string action, int axisIndex, float value)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var input in InputMap.ActionGetEvents(action))
            if (input is InputEventJoypadMotion motion && (int)motion.Axis == axisIndex && Mathf.IsEqualApprox(motion.AxisValue, value)) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Device = -1, Axis = (JoyAxis)axisIndex, AxisValue = value });
    }
}

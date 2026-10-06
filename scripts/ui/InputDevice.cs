using System;

/// <summary>Dispositivo de entrada usado por último; define glifos de botão e visibilidade do cursor.</summary>
public enum InputDevice
{
	KeyboardMouse,
	Xbox,
	PlayStation
}

/// <summary>
/// Estado compartilhado do último dispositivo. GameCursor detecta a troca e chama <see cref="Report"/>;
/// componentes (ex.: HintBar) leem <see cref="LastDevice"/> e escutam <see cref="DeviceChanged"/>.
/// </summary>
public static class UiInput
{
	public static InputDevice LastDevice { get; private set; } = InputDevice.KeyboardMouse;

	public static event Action<InputDevice> DeviceChanged;

	public static void Report(InputDevice device)
	{
		if (device == LastDevice)
			return;
		LastDevice = device;
		DeviceChanged?.Invoke(device);
	}
}

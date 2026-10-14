#nullable enable
using System.Windows.Input;
using Volumey.Controls;

namespace Volumey.DataProvider.Settings;

/// <summary>
/// Volume hotkeys registered for an application, matched by its process name.
/// </summary>
public sealed class AppHotkeysEntry
{
	public string ProcessName { get; set; } = string.Empty;
	public HotKey? VolumeUp { get; set; }
	public HotKey? VolumeDown { get; set; }
}

/// <summary>
/// A hotkey that makes the device the default output device. The device is matched by its id or its name.
/// </summary>
public sealed class DeviceHotkeyEntry
{
	public HotKey? Hotkey { get; set; }
	public string? DeviceId { get; set; }
	public string? DeviceName { get; set; }
}

/// <summary>
/// A hotkey that simulates pressing a system (media) key.
/// </summary>
public sealed class SystemHotkeyEntry
{
	public Key SystemKey { get; set; }
	public HotKey? Replacement { get; set; }
}

#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Nucs.JsonSettings.NotifyChanges;
using Volumey.Controls;

namespace Volumey.DataProvider.Settings;

[NotifyChanges]
public sealed class GeneralSettings : SettingsSection
{
	public AppTheme Theme { get; set; } = AppTheme.System;
	/// <summary>Culture name of the UI language. Null means the system language.</summary>
	public string? Language { get; set; }
	public int VolumeStep { get; set; } = 1;
	public bool VolumeLimitEnabled { get; set; }
	public int VolumeLimit { get; set; } = 50;
	public bool AppVolumeSyncEnabled { get; set; }
	/// <summary>Localized name of the system sounds session and the system language it was resolved for.</summary>
	public string? SystemSoundsName { get; set; }
	public string? SystemSoundsLanguage { get; set; }
}

[NotifyChanges]
public sealed class WindowSettings : SettingsSection
{
	public bool PopupMode { get; set; }
	public bool AlwaysOnTop { get; set; }
	public bool DeviceViewAtTheBottom { get; set; }
	/// <summary>The mixer's output device picker; without it the mixer shows the default device</summary>
	public bool ShowDevicePicker { get; set; } = true;
	public TrayIconKind TrayIcon { get; set; }
	/// <summary>Files written before <see cref="TrayIcon"/> had this switch; it is read once and never written back</summary>
	[JsonProperty("DisplayDeviceIconAtTray")]
	private bool LegacyDisplayDeviceIconAtTray
	{
		set
		{
			if(value)
				TrayIcon = TrayIconKind.DeviceIcon;
		}
	}
	public TrayMiddleClickAction TrayMiddleClickAction { get; set; } = TrayMiddleClickAction.None;
	/// <summary>The mixer opens where it was last (window mode: <see cref="Left"/>/<see cref="Top"/>, popup: <see cref="PopupLeft"/>/<see cref="PopupTop"/>), not next to the tray</summary>
	public bool RememberPosition { get; set; }
	/// <summary>Where the popup was last dragged to, in physical pixels (WPF's Left/Top depend on the screen the window is on); null = never</summary>
	public double? PopupLeft { get; set; }
	public double? PopupTop { get; set; }
	/// <summary>Window mode's position, in physical pixels (see <see cref="PopupLeft"/>)</summary>
	public double Left { get; set; }
	public double Top { get; set; }
	/// <summary>Width of the mixer in window mode (not in popup mode); 0 = not saved yet. The height fits the apps each time it opens</summary>
	public double Width { get; set; }
	/// <summary>Device name of the screen the window opens on (e.g. \\.\DISPLAY2). Null means the primary screen.</summary>
	public string? ScreenName { get; set; }
}

/// <summary>
/// Lists are only saved when the whole list is replaced, so always assign a new list instead of changing items.
/// </summary>
[NotifyChanges]
public sealed class HotkeySettings : SettingsSection
{
	public HotKey? OpenMixer { get; set; }
	public HotKey? DeviceVolumeUp { get; set; }
	public HotKey? DeviceVolumeDown { get; set; }
	public HotKey? DeviceMute { get; set; }
	public HotKey? ForegroundVolumeUp { get; set; }
	public HotKey? ForegroundVolumeDown { get; set; }
	public HotKey? ForegroundMute { get; set; }
	public bool PreventResettingVolumeBalance { get; set; }
	/// <summary>
	/// True: hotkeys are registered with Windows and other apps don't get the keys (RegisterHotKey).
	/// False: a keyboard hook only listens (SharpHook), so the keys still reach other apps.
	/// </summary>
	public bool BlockInSystem { get; set; } = true;
	public IReadOnlyList<AppHotkeysEntry> Apps { get; set; } = Array.Empty<AppHotkeysEntry>();
	public IReadOnlyList<DeviceHotkeyEntry> DefaultDevices { get; set; } = Array.Empty<DeviceHotkeyEntry>();
	public IReadOnlyList<SystemHotkeyEntry> System { get; set; } = Array.Empty<SystemHotkeyEntry>();
}

[NotifyChanges]
public sealed class NotificationSettings : SettingsSection
{
	/// <summary>The range of the OSD's distance from the screen edges, in pixels at 100% scaling</summary>
	public const int MinIndent = 15;
	public const int MaxIndent = 500;

	public bool Enabled { get; set; }
	public int VerticalIndent { get; set; } = MinIndent;
	public int HorizontalIndent { get; set; } = MinIndent;
	public NotificationPosition Position { get; set; } = NotificationPosition.TopLeft;
	public int DisplayTimeSeconds { get; set; } = 3;
	public bool ReactToAllVolumeChanges { get; set; }
	/// <summary>The OSD shows the new default output device when it changes</summary>
	public bool ShowDefaultDeviceChanges { get; set; } = true;
	/// <summary>Device name of the screen the notifications show on (e.g. \\.\DISPLAY2). Null means the primary screen.</summary>
	public string? ScreenName { get; set; }
}

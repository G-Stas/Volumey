using System;
using System.Collections.Generic;
using Notification.Wpf.Controls;
using Nucs.JsonSettings.NotifyChanges;
using Volumey.Controls;
using Volumey.Helper;

namespace Volumey.DataProvider.Settings;

[NotifyChanges]
public sealed class GeneralSettings : SettingsSection
{
	public AppTheme Theme { get; set; } = AppTheme.System;
	/// <summary>Culture name of the UI language. Null means the system language.</summary>
	public string Language { get; set; }
	public int VolumeStep { get; set; } = 1;
	public bool VolumeLimitEnabled { get; set; }
	public int VolumeLimit { get; set; } = 50;
	public bool AppVolumeSyncEnabled { get; set; }
	/// <summary>Localized name of the system sounds session and the system language it was resolved for.</summary>
	public string SystemSoundsName { get; set; }
	public string SystemSoundsLanguage { get; set; }
}

[NotifyChanges]
public sealed class WindowSettings : SettingsSection
{
	public bool PopupMode { get; set; }
	public bool AlwaysOnTop { get; set; }
	public bool DeviceViewAtTheBottom { get; set; }
	public bool DisplayDeviceIconAtTray { get; set; }
	public double Left { get; set; }
	public double Top { get; set; }
	/// <summary>Device name of the screen the window opens on (e.g. \\.\DISPLAY2). Null means the primary screen.</summary>
	public string ScreenName { get; set; }
}

/// <summary>
/// Lists are only saved when the whole list is replaced, so always assign a new list instead of changing items.
/// </summary>
[NotifyChanges]
public sealed class HotkeySettings : SettingsSection
{
	public HotKey OpenMixer { get; set; }
	public HotKey DeviceVolumeUp { get; set; }
	public HotKey DeviceVolumeDown { get; set; }
	public HotKey DeviceMute { get; set; }
	public HotKey ForegroundVolumeUp { get; set; }
	public HotKey ForegroundVolumeDown { get; set; }
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
	public bool Enabled { get; set; }
	public int VerticalIndent { get; set; } = NotificationManagerHelper.MinIndent;
	public int HorizontalIndent { get; set; } = NotificationManagerHelper.MinIndent;
	public NotificationPosition Position { get; set; } = NotificationPosition.TopLeft;
	public int DisplayTimeSeconds { get; set; } = 3;
	public bool ReactToAllVolumeChanges { get; set; }
	/// <summary>Device name of the screen the notifications show on (e.g. \\.\DISPLAY2). Null means the primary screen.</summary>
	public string ScreenName { get; set; }
}

#nullable enable
namespace Volumey.DataProvider.Settings;

/// <summary>What the notification area icon shows</summary>
public enum TrayIconKind
{
	/// <summary>A glyph for the default device's volume, mute or no device, in the taskbar's colors</summary>
	VolumeLevel,
	/// <summary>The default device's own icon; the volume glyph while the device has none</summary>
	DeviceIcon,
	/// <summary>The app icon, whatever the device's state</summary>
	AppIcon,
	/// <summary>The glyphs of Windows' own volume icon, read from the system file that holds them (so a patched set shows too)</summary>
	SystemIcons
}

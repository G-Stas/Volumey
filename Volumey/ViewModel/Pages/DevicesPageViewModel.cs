using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The Devices settings page: the default device's volume hotkeys, then each device's hotkey and settings
/// </summary>
internal sealed class DevicesPageViewModel
{
	/// <summary>For the page's hotkey boxes</summary>
	public IHotkeyService Hotkeys { get; }

	/// <summary>For the hotkey mode note</summary>
	public HotkeyModeViewModel HotkeyMode { get; }

	public DeviceVolumeHotkeysViewModel DeviceVolume { get; }
	public DefaultDeviceHotkeysViewModel DefaultDevice { get; }

	internal DevicesPageViewModel(IHotkeyService hotkeys, HotkeyModeViewModel hotkeyMode, DeviceVolumeHotkeysViewModel deviceVolume,
		DefaultDeviceHotkeysViewModel defaultDevice)
	{
		Hotkeys = hotkeys;
		HotkeyMode = hotkeyMode;
		DeviceVolume = deviceVolume;
		DefaultDevice = defaultDevice;
	}
}

using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The Devices settings page: the default device's volume hotkeys, then each device's hotkey and settings
/// </summary>
internal sealed class DevicesPageViewModel
{
	public DeviceVolumeHotkeysViewModel DeviceVolume { get; }
	public DefaultDeviceHotkeysViewModel DefaultDevice { get; }

	internal DevicesPageViewModel(DeviceVolumeHotkeysViewModel deviceVolume, DefaultDeviceHotkeysViewModel defaultDevice)
	{
		DeviceVolume = deviceVolume;
		DefaultDevice = defaultDevice;
	}
}

using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The Volume mixer settings page: the open-mixer hotkey, the window's behavior, the volume limit and app volume sync
/// </summary>
internal sealed class MixerPageViewModel
{
	public AppBehaviorViewModel Window { get; }
	public OpenHotkeyViewModel OpenHotkey { get; }
	public VolumeLimitViewModel VolumeLimit { get; }
	public AppVolumeSyncViewModel AppVolumeSync { get; }

	internal MixerPageViewModel(AppBehaviorViewModel window, OpenHotkeyViewModel openHotkey, VolumeLimitViewModel volumeLimit,
		AppVolumeSyncViewModel appVolumeSync)
	{
		Window = window;
		OpenHotkey = openHotkey;
		VolumeLimit = volumeLimit;
		AppVolumeSync = appVolumeSync;
	}
}

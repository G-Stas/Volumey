#nullable enable
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The Volume mixer settings page: the open-mixer hotkey, the window's behavior, the volume limit and app volume sync
/// </summary>
internal sealed class MixerPageViewModel
{
	/// <summary>For the page's hotkey boxes</summary>
	public IHotkeyService Hotkeys { get; }

	/// <summary>For the hotkey mode note</summary>
	public HotkeyModeViewModel HotkeyMode { get; }

	public AppBehaviorViewModel Window { get; }
	public OpenHotkeyViewModel OpenHotkey { get; }
	public VolumeLimitViewModel VolumeLimit { get; }
	public AppVolumeSyncViewModel AppVolumeSync { get; }

	internal MixerPageViewModel(IHotkeyService hotkeys, HotkeyModeViewModel hotkeyMode, AppBehaviorViewModel window, OpenHotkeyViewModel openHotkey,
		VolumeLimitViewModel volumeLimit, AppVolumeSyncViewModel appVolumeSync)
	{
		Hotkeys = hotkeys;
		HotkeyMode = hotkeyMode;
		Window = window;
		OpenHotkey = openHotkey;
		VolumeLimit = volumeLimit;
		AppVolumeSync = appVolumeSync;
	}
}

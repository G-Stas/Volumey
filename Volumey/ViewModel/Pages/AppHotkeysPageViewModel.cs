using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The App hotkeys settings page: the foreground app's hotkeys and the per-app list
/// </summary>
internal sealed class AppHotkeysPageViewModel
{
	/// <summary>For the page's hotkey boxes</summary>
	public IHotkeyService Hotkeys { get; }

	/// <summary>For the hotkey mode note</summary>
	public HotkeyModeViewModel HotkeyMode { get; }

	public ForegroundWindowVolumeViewModel Foreground { get; }
	public AppsHotkeysViewModel AppHotkeys { get; }

	internal AppHotkeysPageViewModel(IHotkeyService hotkeys, HotkeyModeViewModel hotkeyMode, ForegroundWindowVolumeViewModel foreground,
		AppsHotkeysViewModel appHotkeys)
	{
		Hotkeys = hotkeys;
		HotkeyMode = hotkeyMode;
		Foreground = foreground;
		AppHotkeys = appHotkeys;
	}
}

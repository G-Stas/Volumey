using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The App hotkeys settings page: the foreground app's hotkeys and the per-app list
/// </summary>
internal sealed class AppHotkeysPageViewModel
{
	public ForegroundWindowVolumeViewModel Foreground { get; }
	public AppsHotkeysViewModel AppHotkeys { get; }

	internal AppHotkeysPageViewModel(ForegroundWindowVolumeViewModel foreground, AppsHotkeysViewModel appHotkeys)
	{
		Foreground = foreground;
		AppHotkeys = appHotkeys;
	}
}

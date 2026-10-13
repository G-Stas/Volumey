using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The General settings page: language, theme, tray icon, startup, hotkey mode and volume step
/// </summary>
internal sealed class GeneralPageViewModel
{
	public HotkeyModeViewModel HotkeyMode { get; }
	public ThemeViewModel Theme { get; }
	public AppBehaviorViewModel Window { get; }
	public LangSettings Language { get; }

	/// <summary>Holds the volume step</summary>
	public AppsHotkeysViewModel AppHotkeys { get; }

	internal GeneralPageViewModel(HotkeyModeViewModel hotkeyMode, ThemeViewModel theme, AppBehaviorViewModel window, LangSettings language,
		AppsHotkeysViewModel appHotkeys)
	{
		HotkeyMode = hotkeyMode;
		Theme = theme;
		Window = window;
		Language = language;
		AppHotkeys = appHotkeys;
	}
}

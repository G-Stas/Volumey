using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel.Pages;

/// <summary>
/// The Media keys settings page: a hotkey for each media key
/// </summary>
internal sealed class MediaKeysPageViewModel
{
	/// <summary>For the page's hotkey boxes</summary>
	public IHotkeyService Hotkeys { get; }

	/// <summary>For the hotkey mode note</summary>
	public HotkeyModeViewModel HotkeyMode { get; }

	public SystemHotkeysViewModel MediaKeys { get; }

	internal MediaKeysPageViewModel(IHotkeyService hotkeys, HotkeyModeViewModel hotkeyMode, SystemHotkeysViewModel mediaKeys)
	{
		Hotkeys = hotkeys;
		HotkeyMode = hotkeyMode;
		MediaKeys = mediaKeys;
	}
}

#nullable enable
namespace Volumey.ViewModel.Settings;

/// <summary>
/// Who holds a key combination (<see cref="IHotkeyService.FindOwner"/>)
/// </summary>
public enum HotkeyOwner
{
	Nobody,
	/// <summary>Registered by Volumey's blocking hotkey manager</summary>
	Volumey,
	/// <summary>Registered by another app with RegisterHotKey</summary>
	AnotherApp
}

#nullable enable
using System;
using System.Collections.Generic;
using Volumey.Controls;
using Volumey.Helper;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// Registers the app's hotkeys and raises them when they are pressed. See <see cref="HotkeyService"/>.
/// </summary>
public interface IHotkeyService : IDisposable
{
	/// <summary>
	/// Raised when the first hotkey manager is set and hotkeys can be registered
	/// </summary>
	event Action Activated;

	/// <summary>
	/// Raised on the UI thread for every registered hotkey that is pressed
	/// </summary>
	event Action<HotKey> HotkeyPressed;

	event Action OpenHotkeyPressed;

	/// <summary>
	/// A hotkey manager is set and hotkeys can be registered
	/// </summary>
	bool IsActive { get; }

	/// <summary>
	/// Set while the user types a hotkey into a hotkey box, so the typed combination doesn't trigger a registered hotkey
	/// </summary>
	bool IsEditingHotkey { get; set; }

	/// <summary>
	/// Who holds a combination that Windows delivered as a hotkey instead of as keys (see <see cref="HotkeyService.FindOwner"/>)
	/// </summary>
	HotkeyOwner FindOwner(HotKey hotkey);

	int VolumeStep { get; }

	void SetVolumeStep(int newValue);

	void SetHotkeyManager(IHotkeyManager manager);

	bool RegisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2);

	/// <param name="failed">The hotkey that couldn't be registered, null on success</param>
	bool RegisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2, out HotKey? failed);

	void UnregisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2);

	/// <summary>
	/// Registers the hotkeys that are set (nulls are skipped), or none (see <see cref="HotkeyService.RegisterHotkeys"/>)
	/// </summary>
	/// <param name="failed">The hotkey that couldn't be registered, null on success</param>
	bool RegisterHotkeys(IReadOnlyList<HotKey?> hotkeys, out HotKey? failed);

	void UnregisterHotkeys(IReadOnlyList<HotKey?> hotkeys);

	bool RegisterHotkey(HotKey? hotkey);

	void UnregisterHotkey(HotKey? hotkey);

	bool RegisterOpenMixerHotkey(HotKey? hotkey);

	void UnregisterOpenMixerHotkey(HotKey? hotkey);

	ErrorMessageType HotkeyIsValid(HotKey? hotkey);

	ErrorMessageType HotkeysAreValid(HotKey? up, HotKey? down);

	/// <summary>
	/// Checks a volume up/down pair hotkey by hotkey (see <see cref="HotkeyService.CheckHotkeysPair"/>)
	/// </summary>
	(ErrorMessageType up, ErrorMessageType down) CheckHotkeysPair(HotKey? up, HotKey? down);

	/// <summary>
	/// Checks an app's or the foreground app's optional hotkeys (see <see cref="HotkeyService.CheckAppHotkeys"/>)
	/// </summary>
	(ErrorMessageType up, ErrorMessageType down, ErrorMessageType mute) CheckAppHotkeys(HotKey? up, HotKey? down, HotKey? mute);
}

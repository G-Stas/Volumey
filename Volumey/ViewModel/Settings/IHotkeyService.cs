using System;
using Volumey.Controls;
using Volumey.Helper;

namespace Volumey.ViewModel.Settings
{
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

		int VolumeStep { get; }

		void SetVolumeStep(int newValue);

		void SetHotkeyManager(IHotkeyManager manager);

		bool RegisterHotkeysPair(HotKey hotkey1, HotKey hotkey2);

		void UnregisterHotkeysPair(HotKey hotkey1, HotKey hotkey2);

		bool RegisterHotkey(HotKey hotkey);

		void UnregisterHotkey(HotKey hotkey);

		bool RegisterOpenMixerHotkey(HotKey hotkey);

		void UnregisterOpenMixerHotkey(HotKey hotkey);

		ErrorMessageType HotkeyIsValid(HotKey hotkey);

		ErrorMessageType HotkeysAreValid(HotKey up, HotKey down);
	}
}

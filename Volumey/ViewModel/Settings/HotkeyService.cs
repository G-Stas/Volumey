using System;
using System.Collections.Generic;
using System.Windows.Input;
using log4net;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.DataProvider;

namespace Volumey.ViewModel.Settings
{
	/// <summary>
	/// The app's hotkeys: holds the hotkey manager, counts the users of each hotkey so several features can share one,
	/// raises the pressed hotkeys and validates new ones. One instance for the app (<see cref="AppServices"/>).
	/// </summary>
	public sealed class HotkeyService : IHotkeyService
	{
		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(HotkeyService));

		/// <summary>
		/// Raised when the first hotkey manager is set and hotkeys can be registered
		/// </summary>
		public event Action Activated;

		private Action<HotKey> _hotkeyPressed;
		public event Action<HotKey> HotkeyPressed
		{
			add
			{
				//Prevent multiple subscribing of the same event handler
				_hotkeyPressed -= value;
				_hotkeyPressed += value;
			}
			remove => _hotkeyPressed -= value;
		}
		public event Action OpenHotkeyPressed;
		/// <summary>
		/// Indicates whether a hotkey manager instance is set and hotkeys can be registered.
		/// </summary>
		public bool IsActive { get; private set; }

		public int VolumeStep { get; private set; } = 1;

		private HotKey openMixer;
		private IHotkeyManager hotkeyManager;

		/// <summary>
		/// Keeps track of hotkeys and amount of its users to prevent early unregistering of hotkeys.
		/// </summary>
		private readonly Dictionary<HotKey, int> HotkeysUserCount = new Dictionary<HotKey, int>();

		private void IncrementHotkeyUserCounter(HotKey hotkey)
		{
			if(HotkeysUserCount.ContainsKey(hotkey))
				HotkeysUserCount[hotkey]++;
			else
				HotkeysUserCount.Add(hotkey, 1);
		}

		/// <param name="hotkey"></param>
		/// <param name="lastUser">Flag to check if the call was for the last user and hotkey can be unregistered.</param>
		private void DecrementHotkeyUserCounter(HotKey hotkey, out bool lastUser)
		{
			if(HotkeysUserCount.TryGetValue(hotkey, out var userCount))
			{
				if(userCount == 1)
				{
					HotkeysUserCount.Remove(hotkey);
					lastUser = true;
					return;
				}
				HotkeysUserCount[hotkey] = --userCount;
				lastUser = false;
				return;
			}
			lastUser = true;
		}

		public bool RegisterHotkeysPair(HotKey hotkey1, HotKey hotkey2)
		{
			if(hotkeyManager == null)
				throw new NullReferenceException("Hotkey manager is not set");
			try
			{
				hotkeyManager.RegisterHotkey(hotkey1);
				IncrementHotkeyUserCounter(hotkey1);
				hotkeyManager.RegisterHotkey(hotkey2);
				IncrementHotkeyUserCounter(hotkey2);
				return true;
			}
			catch
			{
				DecrementHotkeyUserCounter(hotkey1, out bool lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey1);
				
				DecrementHotkeyUserCounter(hotkey2, out lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey2);
				throw;
			}
		}

		public void UnregisterHotkeysPair(HotKey hotkey1, HotKey hotkey2)
		{
			if(hotkeyManager == null)
				throw new NullReferenceException("Hotkey manager is not set");
			try
			{
				DecrementHotkeyUserCounter(hotkey1, out bool lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey1);
				DecrementHotkeyUserCounter(hotkey2, out lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey2);
			}
			catch { }
		}

		public bool RegisterHotkey(HotKey hotkey)
		{
			try
			{
				IncrementHotkeyUserCounter(hotkey);
				hotkeyManager.RegisterHotkey(hotkey);
				return true;
			}
			catch {}
			return false;
		}

		public void UnregisterHotkey(HotKey hotkey)
		{
			try
			{
				DecrementHotkeyUserCounter(hotkey, out bool lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey);
			}
			catch { }
		}

		public bool RegisterOpenMixerHotkey(HotKey hotkey)
		{
			if(hotkeyManager == null)
				return false;
			if(hotkey == null)
				return false;
			try
			{
				openMixer = hotkey;
				IncrementHotkeyUserCounter(hotkey);
				hotkeyManager.RegisterHotkey(hotkey);
				return true;
			}
			catch { }
			return false;
		}

		public void UnregisterOpenMixerHotkey(HotKey hotkey)
		{
			if(hotkeyManager == null)
				return;
			if(hotkey == null)
				return;
			try
			{
				openMixer = null;
				DecrementHotkeyUserCounter(hotkey, out bool lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey);
			}
			catch { }
		}

		public ErrorMessageType HotkeyIsValid(HotKey hotkey)
		{
			if(hotkey == null)
				return ErrorMessageType.OpenReg;
			if(hotkey.ModifierKeys == ModifierKeys.None && hotkey.Key == Key.F12)
				return ErrorMessageType.F12;
			if(SettingsProvider.HotkeyExists(hotkey)) //!SettingsProvider.Settings.AllowDuplicates && 
				return ErrorMessageType.HotkeyExists;
			return ErrorMessageType.None;
		}

		public ErrorMessageType HotkeysAreValid(HotKey up, HotKey down)
		{
			if(up == null || down == null)
				return ErrorMessageType.VolumeReg;
			if((up.ModifierKeys == ModifierKeys.None && up.Key == Key.F12) ||
			   (down.ModifierKeys == ModifierKeys.None && down.Key == Key.F12))
				return ErrorMessageType.F12;
			if(up.Equals(down))
				return ErrorMessageType.Diff;
			if(SettingsProvider.HotkeysExist(up, down)) //!SettingsProvider.Settings.AllowDuplicates && 
				return ErrorMessageType.HotkeyExists;
			return ErrorMessageType.None;
		}

		public void SetVolumeStep(int newValue)
		{
			VolumeStep = newValue;
		}

		/// <summary>
		/// Sets the hotkey manager. On a later call the registered hotkeys move from the previous manager to the new one,
		/// which is then the only one, and the previous manager is disposed. The user counts stay as they are.
		/// </summary>
		public void SetHotkeyManager(IHotkeyManager hm)
		{
			if(hotkeyManager == hm)
				return;
			var previous = hotkeyManager;
			hotkeyManager = hm;
			hotkeyManager.HotkeyPressed += OnHotkeyPressed;
			if(previous == null)
			{
				IsActive = true;
				Activated?.Invoke();
				return;
			}

			previous.HotkeyPressed -= OnHotkeyPressed;
			foreach(var hotkey in previous.GetRegisteredHotkeys() ?? Array.Empty<HotKey>())
			{
				try { previous.UnregisterHotkey(hotkey); }
				catch { }
				try { hm.RegisterHotkey(hotkey); }
				catch(Exception e)
				{
					//e.g. another app uses the combination when moving to the blocking manager
					Logger.Error($"Failed to move hotkey [{hotkey}] to the new hotkey manager", e);
				}
			}
			previous.Dispose();
		}

		/// <summary>
		/// Set while the user types a hotkey into a hotkey box, so the typed combination doesn't trigger a registered hotkey.
		/// The blocking manager doesn't need it (the box swallows WM_HOTKEY), the non-blocking one does.
		/// </summary>
		public bool IsEditingHotkey { get; set; }
		private void OnHotkeyPressed(HotKey hotkey)
		{
			if(IsEditingHotkey)
				return;
			if(openMixer != null && hotkey.Equals(openMixer))
				OpenHotkeyPressed?.Invoke();
			_hotkeyPressed?.Invoke(hotkey);
		}

		public void Dispose()
		{
			hotkeyManager?.Dispose();
		}
	}
}
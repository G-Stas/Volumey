using System;
using System.Collections.Generic;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.DataProvider;

namespace Volumey.ViewModel.Settings
{
	public static class HotkeysControl
	{
		/// <summary>
		/// Invokes when a hotkey manager is set and is ready to register hotkeys
		/// </summary>
		internal static event Action Activated;

		private static Action<HotKey> _hotkeyPressed;
		internal static event Action<HotKey> HotkeyPressed
		{
			add
			{
				//Prevent multiple subscribing of the same event handler
				_hotkeyPressed -= value;
				_hotkeyPressed += value;
			}
			remove => _hotkeyPressed -= value;
		}
		internal static event Action OpenHotkeyPressed;
		/// <summary>
		/// Indicates whether a hotkey manager instance is set and hotkeys can be registered.
		/// </summary>
		internal static bool IsActive { get; private set; }

		private static int volumeStep = 1;
		public static int VolumeStep
		{
			get => volumeStep;
			private set => volumeStep = value;
		}

		private static HotKey openMixer;
		private static IHotkeyManager hotkeyManager;

		/// <summary>
		/// Keeps track of hotkeys and amount of its users to prevent early unregistering of hotkeys.
		/// </summary>
		private static readonly Dictionary<HotKey, int> HotkeysUserCount = new Dictionary<HotKey, int>();

		private static void IncrementHotkeyUserCounter(HotKey hotkey)
		{
			if(HotkeysUserCount.ContainsKey(hotkey))
				HotkeysUserCount[hotkey]++;
			else
				HotkeysUserCount.Add(hotkey, 1);
		}

		/// <param name="hotkey"></param>
		/// <param name="lastUser">Flag to check if the call was for the last user and hotkey can be unregistered.</param>
		private static void DecrementHotkeyUserCounter(HotKey hotkey, out bool lastUser)
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

		public static bool RegisterHotkeysPair(HotKey hotkey1, HotKey hotkey2)
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

		public static void UnregisterHotkeysPair(HotKey hotkey1, HotKey hotkey2)
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

		public static bool RegisterHotkey(HotKey hotkey)
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

		public static void UnregisterHotkey(HotKey hotkey)
		{
			try
			{
				DecrementHotkeyUserCounter(hotkey, out bool lastUser);
				if(lastUser)
					hotkeyManager.UnregisterHotkey(hotkey);
			}
			catch { }
		}

		public static bool RegisterOpenMixerHotkey(HotKey hotkey)
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

		public static void UnregisterOpenMixerHotkey(HotKey hotkey)
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

		public static ErrorMessageType HotkeyIsValid(HotKey hotkey)
		{
			if(hotkey == null)
				return ErrorMessageType.OpenReg;
			if(hotkey.ModifierKeys == ModifierKeys.None && hotkey.Key == Key.F12)
				return ErrorMessageType.F12;
			if(SettingsProvider.Settings.HotkeyExists(hotkey)) //!SettingsProvider.Settings.AllowDuplicates && 
				return ErrorMessageType.HotkeyExists;
			return ErrorMessageType.None;
		}

		public static ErrorMessageType HotkeysAreValid(HotKey up, HotKey down)
		{
			if(up == null || down == null)
				return ErrorMessageType.VolumeReg;
			if((up.ModifierKeys == ModifierKeys.None && up.Key == Key.F12) ||
			   (down.ModifierKeys == ModifierKeys.None && down.Key == Key.F12))
				return ErrorMessageType.F12;
			if(up.Equals(down))
				return ErrorMessageType.Diff;
			if(SettingsProvider.Settings.HotkeysExist(up, down)) //!SettingsProvider.Settings.AllowDuplicates && 
				return ErrorMessageType.HotkeyExists;
			return ErrorMessageType.None;
		}

		internal static void SetVolumeStep(int newValue)
		{
			VolumeStep = newValue;
		}

		public static void SetHotkeyManager(IHotkeyManager hm)
		{
			if(hotkeyManager == hm)
				return;
			// var prevManager = hotkeyManager;
			hotkeyManager = hm;
			hotkeyManager.HotkeyPressed += OnHotkeyPressed;
			// if(prevManager == null)
			// {
			IsActive = true;
			Activated?.Invoke();
			// }
			// else
			// {
			// 	prevManager.HotkeyPressed -= OnHotkeyPressed;
			// 	ConvertHotkeysToNewManager(prevManager: prevManager, newManager: hm);
			// 	prevManager.Dispose();
			// }
		}

		// private static void ConvertHotkeysToNewManager(IHotkeyManager prevManager, IHotkeyManager newManager)
		// {
		// 	var hotkeysCount = prevManager.RegisteredHotkeysCount;
		// 	var hotkeysList = prevManager.GetRegisteredHotkeys();
		// 	for(int i = hotkeysCount - 1; i >= 0; i--)
		// 	{
		// 		var hotkey = hotkeysList[i];
		// 		prevManager.UnregisterHotkey(hotkey);
		// 		newManager.RegisterHotkey(hotkey);
		// 	}
		// }

		private static void OnHotkeyPressed(HotKey hotkey)
		{
			if(openMixer != null && hotkey.Equals(openMixer))
				OpenHotkeyPressed?.Invoke();
			_hotkeyPressed?.Invoke(hotkey);
		}

		public static void Dispose()
		{
			hotkeyManager.Dispose();
		}
	}
}
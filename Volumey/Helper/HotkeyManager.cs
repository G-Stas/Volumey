using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using log4net;
using Volumey.Controls;

namespace Volumey.Helper
{
	/// <summary>
	/// Provides global hotkeys functionality by using native hotkeys functionality from Windows API.
	/// Blocks registered hotkeys combinations input system-wide.
	/// </summary>
	internal class HotkeyManager : IHotkeyManager
	{
		public event Action<HotKey> HotkeyPressed;

		private readonly Dictionary<HotKey, int> RegisteredHotkeys = new Dictionary<HotKey, int>();
		private readonly IntPtr handle;

		private int keyId;
		private ILog logger;
		private ILog Logger => logger ??= LogManager.GetLogger(typeof(HotkeyManager));

		private readonly SynchronizationContext syncContext;

		/// <summary></summary>
		/// <param name="handle">A handle to the window which will be receiving a WM_HOTKEY message</param>
		/// <exception cref="NullReferenceException">A handle points to zero</exception>
		internal HotkeyManager(IntPtr handle)
		{
			if(handle == IntPtr.Zero)
				throw new NullReferenceException("Handle ptr is zero");
			this.handle = handle;
			this.syncContext = SynchronizationContext.Current;
		}

		public IReadOnlyList<HotKey> GetRegisteredHotkeys() => this.RegisteredHotkeys.Keys.ToArray();
		
		internal Action<int> GetMessageHandler() => OnWindowHotkeyMessage;

		/// <summary>
		/// Registers a system-wide hotkey.
		/// </summary>
		/// <param name="hotkey"></param>
		/// <exception cref="NullReferenceException">Hotkey is null</exception>
		/// <exception cref="Exception">Failed to register hotkey</exception>
		public void RegisterHotkey(HotKey hotkey)
		{
			if(hotkey == null)
				throw new NullReferenceException("Hotkey is null");
			if(!RegisteredHotkeys.ContainsKey(hotkey))
			{
				var id = keyId;
				var virtualKey = KeyInterop.VirtualKeyFromKey(hotkey.Key);
				if(this.RegisterHotkey((int) hotkey.ModifierKeys, (uint) virtualKey))
				{
					RegisteredHotkeys.Add(hotkey, id);
					keyId++;
					return;
				}
				var error = Marshal.GetLastWin32Error();
				Logger.Error($"Failed to register hotkey: [{hotkey}], id: [{id.ToString()}], error code: [{error.ToString()}]");
				throw new Win32Exception(error);
			}
		}

		/// <summary>
		/// Unregisters a previously registered hotkey
		/// </summary>
		/// <param name="hotkey">A hotkey to unregister</param>
		/// <exception cref="NullReferenceException">Hotkey is null</exception>
		public void UnregisterHotkey(HotKey hotkey)
		{
			if(hotkey == null)
				throw new NullReferenceException("Hotkey is null");
			if(RegisteredHotkeys.TryGetValue(hotkey, out int id))
			{
				if(UnregisterHotkey(id))
				{
					RegisteredHotkeys.Remove(hotkey);
					return;
				}
				var errCode = Marshal.GetLastWin32Error();
				Logger.Error($"Failed to unregister hotkey: [{hotkey}], id: [{id.ToString()}], error code: [{errCode.ToString()}]");
			}
		}

		public int RegisteredHotkeysCount => this.RegisteredHotkeys.Count;

		private bool RegisterHotkey(int modifierKeys, uint virtualKey)
		{
			bool result = false;
			this.syncContext.Send((o) =>
			{
				result = NativeMethods.RegisterHotKey(this.handle, this.keyId, modifierKeys, virtualKey);
			}, null);
			return result;
		}

		private bool UnregisterHotkey(int id)
		{
			bool result = false;
			this.syncContext.Send((o) =>
			{
				result = NativeMethods.UnregisterHotKey(handle, id);
			}, null);
			return result;
		}

		private void OnWindowHotkeyMessage(int lParam)
		{
			//"The low-order word specifies the keys that were to be pressed
			//in combination with the key specified by the high-order word to generate the WM_HOTKEY message.
			//The high-order word specifies the virtual key code of the hot key."
			int low = lParam & 0xFF; //modifier key
			int high = lParam >> 16; //key
			var hotkey = new HotKey(KeyInterop.KeyFromVirtualKey(high), (ModifierKeys) low);
			if(RegisteredHotkeys.ContainsKey(hotkey))
				HotkeyPressed?.Invoke(hotkey);
		}

		public void Dispose()
		{
			foreach(var hotkeyId in RegisteredHotkeys.Values)
				this.UnregisterHotkey(hotkeyId);
		}
	}
}
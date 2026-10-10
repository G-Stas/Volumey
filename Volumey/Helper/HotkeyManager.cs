using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using log4net;
using Volumey.Controls;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Volumey.Helper;

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
	private static readonly ILog Logger = LogManager.GetLogger(typeof(HotkeyManager));

	private readonly SynchronizationContext syncContext;
	private readonly IWin32 win32;

	/// <summary></summary>
	/// <param name="handle">A handle to the window which will be receiving a WM_HOTKEY message</param>
	/// <exception cref="NullReferenceException">A handle points to zero</exception>
	internal HotkeyManager(IntPtr handle) : this(handle, Win32.Instance) { }

	internal HotkeyManager(IntPtr handle, IWin32 win32)
	{
		if(handle == IntPtr.Zero)
			throw new NullReferenceException("Handle ptr is zero");
		this.handle = handle;
		this.win32 = win32;
		//the hotkeys belong to the thread that registers them: the window's (UI) thread
		syncContext = SynchronizationContext.Current;
	}

	public IReadOnlyList<HotKey> GetRegisteredHotkeys() => RegisteredHotkeys.Keys.ToArray();

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
			//WPF's ModifierKeys have the values of the MOD_ flags (Alt 1, Control 2, Shift 4, Windows 8)
			if(RegisterHotkey((HOT_KEY_MODIFIERS)hotkey.ModifierKeys, (uint)virtualKey, out int error))
			{
				RegisteredHotkeys.Add(hotkey, id);
				keyId++;
				return;
			}
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
			if(UnregisterHotkey(id, out int errCode))
			{
				RegisteredHotkeys.Remove(hotkey);
				return;
			}
			Logger.Error($"Failed to unregister hotkey: [{hotkey}], id: [{id.ToString()}], error code: [{errCode.ToString()}]");
		}
	}

	public int RegisteredHotkeysCount => RegisteredHotkeys.Count;

	//the error is read right after the call, on the thread that made it (it's per thread)
	private bool RegisterHotkey(HOT_KEY_MODIFIERS modifiers, uint virtualKey, out int error)
	{
		bool result = false;
		int lastError = 0;
		OnOwnerThread(() =>
		{
			result = win32.RegisterHotKey(handle, keyId, modifiers, virtualKey);
			if(!result)
				lastError = win32.GetLastError();
		});
		error = lastError;
		return result;
	}

	private bool UnregisterHotkey(int id, out int error)
	{
		bool result = false;
		int lastError = 0;
		OnOwnerThread(() =>
		{
			result = win32.UnregisterHotKey(handle, id);
			if(!result)
				lastError = win32.GetLastError();
		});
		error = lastError;
		return result;
	}

	private void OnOwnerThread(Action action)
	{
		if(syncContext == null)
			action();
		else
			syncContext.Send(_ => action(), null);
	}

	private void OnWindowHotkeyMessage(int lParam)
	{
		//"The low-order word specifies the keys that were to be pressed
		//in combination with the key specified by the high-order word to generate the WM_HOTKEY message.
		//The high-order word specifies the virtual key code of the hot key."
		int low = lParam & 0xFF; //modifier key
		int high = lParam >> 16; //key
		var hotkey = new HotKey(KeyInterop.KeyFromVirtualKey(high), (ModifierKeys)low);
		if(RegisteredHotkeys.ContainsKey(hotkey))
			HotkeyPressed?.Invoke(hotkey);
	}

	public void Dispose()
	{
		foreach(var hotkeyId in RegisteredHotkeys.Values)
			UnregisterHotkey(hotkeyId, out _);
	}
}

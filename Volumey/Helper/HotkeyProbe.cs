#nullable enable
using System;
using System.Windows.Input;
using Volumey.Controls;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Volumey.Helper;

/// <summary>
/// Tells whether another app holds a key combination through RegisterHotKey: Volumey tries to register it for a moment.
/// </summary>
internal static class HotkeyProbe
{
	/// <summary>Outside the IDs <see cref="HotkeyManager"/> hands out, which count up from 0</summary>
	private const int ProbeId = 0xBFFE;

	private const int ErrorHotkeyAlreadyRegistered = 1409;

	/// <summary>
	/// True if Windows refuses the combination because it's registered already (by another app, or by Volumey's blocking manager:
	/// the caller checks Volumey's own first). On the UI thread: the probe is registered to the calling thread.
	/// </summary>
	internal static bool IsRegisteredElsewhere(IWin32 win32, HotKey hotkey)
	{
		var modifiers = (HOT_KEY_MODIFIERS)hotkey.ModifierKeys | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
		uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);
		if(win32.RegisterHotKey(IntPtr.Zero, ProbeId, modifiers, virtualKey))
		{
			win32.UnregisterHotKey(IntPtr.Zero, ProbeId);
			return false;
		}
		return win32.GetLastError() == ErrorHotkeyAlreadyRegistered;
	}
}

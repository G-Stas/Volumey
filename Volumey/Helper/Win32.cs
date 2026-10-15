#nullable enable
using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Recovery;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.Helper;

/// <summary>
/// The Win32 calls behind the hotkeys, the window menu, the restart registration and the second-instance message,
/// so tests can check that the app makes them with the right arguments (<see cref="Win32"/> makes the real calls)
/// </summary>
internal interface IWin32
{
	bool RegisterHotKey(IntPtr hwnd, int id, HOT_KEY_MODIFIERS modifiers, uint virtualKey);

	bool UnregisterHotKey(IntPtr hwnd, int id);

	/// <summary>
	/// The error of the last failed call on this thread
	/// </summary>
	int GetLastError();

	/// <summary>
	/// The window's own system menu (not a copy, so it's never destroyed)
	/// </summary>
	IntPtr GetSystemMenu(IntPtr hwnd);

	void EnableMenuItem(IntPtr menu, uint item, MENU_ITEM_FLAGS flags);

	void RegisterApplicationRestart(string commandLine, REGISTER_APPLICATION_RESTART_FLAGS flags);

	void UnregisterApplicationRestart();

	void PostMessage(IntPtr hwnd, uint message);
}

/// <inheritdoc cref="IWin32"/>
internal sealed class Win32 : IWin32
{
	public static Win32 Instance { get; } = new Win32();

	public bool RegisterHotKey(IntPtr hwnd, int id, HOT_KEY_MODIFIERS modifiers, uint virtualKey)
		=> PInvoke.RegisterHotKey((HWND)hwnd, id, modifiers, virtualKey);

	public bool UnregisterHotKey(IntPtr hwnd, int id) => PInvoke.UnregisterHotKey((HWND)hwnd, id);

	public int GetLastError() => Marshal.GetLastPInvokeError();

	public IntPtr GetSystemMenu(IntPtr hwnd) => PInvoke.GetSystemMenu((HWND)hwnd, false);

	public void EnableMenuItem(IntPtr menu, uint item, MENU_ITEM_FLAGS flags) => PInvoke.EnableMenuItem((HMENU)menu, item, flags);

	public void RegisterApplicationRestart(string commandLine, REGISTER_APPLICATION_RESTART_FLAGS flags)
		=> PInvoke.RegisterApplicationRestart(commandLine, flags);

	public void UnregisterApplicationRestart() => PInvoke.UnregisterApplicationRestart();

	public void PostMessage(IntPtr hwnd, uint message) => PInvoke.PostMessage((HWND)hwnd, message, default, default);
}

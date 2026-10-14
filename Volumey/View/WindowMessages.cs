#nullable enable
using System;
using Volumey.Helper;
using Windows.Win32;
using Windows.Win32.System.Recovery;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.View;

/// <summary>
/// The main window's message handling that calls Windows: the restart registration, the title bar menu and the hotkey messages
/// </summary>
internal sealed class WindowMessages
{
	internal const int WM_INITMENU = 0x0116;
	internal const int WM_CLOSE = 0x0010;
	internal const int WM_ACTIVATEAPP = 0x001C;
	internal const int WM_HOTKEY = 0x0312;

	private readonly IWin32 win32;
	private readonly IntPtr hwnd;
	private int previousMessage;

	/// <summary>
	/// Gets the lParam of WM_HOTKEY (the blocking hotkey manager's handler)
	/// </summary>
	public Action<int>? HotkeyMessageHandler { get; set; }

	public WindowMessages(IWin32 win32, IntPtr hwnd)
	{
		this.win32 = win32;
		this.hwnd = hwnd;
	}

	/// <summary>
	/// Lets Windows restart the app (minimized) when it's closed for an update while running
	/// </summary>
	public void RegisterRestart()
		=> win32.RegisterApplicationRestart(Startup.MinimizedArg, REGISTER_APPLICATION_RESTART_FLAGS.RESTART_NO_REBOOT);

	public void Handle(int message, IntPtr wParam, IntPtr lParam)
	{
		switch(message)
		{
			//the window can't be minimized or maximized: grey out the items of the title bar menu every time it opens
			case WM_INITMENU:
				IntPtr menu = win32.GetSystemMenu(hwnd);
				win32.EnableMenuItem(menu, PInvoke.SC_MINIMIZE, MENU_ITEM_FLAGS.MF_GRAYED | MENU_ITEM_FLAGS.MF_BYCOMMAND);
				win32.EnableMenuItem(menu, PInvoke.SC_MAXIMIZE, MENU_ITEM_FLAGS.MF_GRAYED | MENU_ITEM_FLAGS.MF_BYCOMMAND);
				break;

			//The hidden window doesn't get WM_QUERYENDSESSION when Windows closes the app for an update, so the restart is registered
			//at startup and unregistered when the app is closed the normal way: by the tray menu's Exit item, which is always
			//preceded by WM_ACTIVATEAPP (other messages come first when it's closed by an update or a crash)
			case WM_CLOSE:
				if(previousMessage == WM_ACTIVATEAPP)
					win32.UnregisterApplicationRestart();
				break;

			case WM_HOTKEY:
				HotkeyDiagnostics.WmHotkeyReceived(wParam.ToInt32(), HotkeyDiagnostics.GetMessageTime());
				HotkeyMessageHandler?.Invoke(lParam.ToInt32());
				break;
		}
		previousMessage = message;
	}

	/// <summary>
	/// Asks the running instance to show itself (a second start broadcasts the instance's registered message)
	/// </summary>
	public static void ShowRunningInstance(IWin32 win32, int showMessage)
		=> win32.PostMessage(HWND_BROADCAST, (uint)showMessage);

	internal static readonly IntPtr HWND_BROADCAST = 0xffff;
}

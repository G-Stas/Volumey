using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.View;
using Windows.Win32.System.Recovery;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.Tests;

/// <summary>
/// The Win32 calls behind the blocking hotkeys, the window menu, the restart registration and the second start,
/// checked against a fake <see cref="IWin32"/>. The values are the documented Win32 constants, written out on purpose.
/// </summary>
[TestClass]
public class Win32CallsTests
{
	private static readonly IntPtr Window = 0x1234;
	private static readonly HotKey CtrlShiftF5 = new HotKey(Key.F5, ModifierKeys.Control | ModifierKeys.Shift);
	private static readonly HotKey WinAltA = new HotKey(Key.A, ModifierKeys.Windows | ModifierKeys.Alt);

	private readonly Mock<IWin32> win32 = new Mock<IWin32>();

	private HotkeyManager CreateHotkeyManager(bool windowsAccepts = true)
	{
		win32.Setup(m => m.RegisterHotKey(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
			 .Returns(windowsAccepts);
		win32.Setup(m => m.UnregisterHotKey(It.IsAny<IntPtr>(), It.IsAny<int>())).Returns(true);
		return new HotkeyManager(Window, win32.Object);
	}

	[TestMethod]
	public void AHotkeyIsRegisteredWithItsModifierFlagsAndVirtualKey()
	{
		var manager = CreateHotkeyManager();

		manager.RegisterHotkey(CtrlShiftF5);
		manager.RegisterHotkey(WinAltA);

		//MOD_CONTROL 0x2 | MOD_SHIFT 0x4, VK_F5 0x74
		win32.Verify(m => m.RegisterHotKey(Window, 0, (HOT_KEY_MODIFIERS)0x6, 0x74u), Times.Once);
		//MOD_WIN 0x8 | MOD_ALT 0x1, 'A' 0x41
		win32.Verify(m => m.RegisterHotKey(Window, 1, (HOT_KEY_MODIFIERS)0x9, 0x41u), Times.Once);
	}

	[TestMethod]
	public void AHotkeyIsRegisteredOnce()
	{
		var manager = CreateHotkeyManager();

		manager.RegisterHotkey(CtrlShiftF5);
		manager.RegisterHotkey(CtrlShiftF5);

		win32.Verify(m => m.RegisterHotKey(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()), Times.Once);
		Assert.AreEqual(1, manager.RegisteredHotkeysCount);
	}

	[TestMethod]
	public void AHotkeyIsUnregisteredWithTheIdItWasRegisteredWith()
	{
		var manager = CreateHotkeyManager();
		manager.RegisterHotkey(CtrlShiftF5);
		manager.RegisterHotkey(WinAltA);

		manager.UnregisterHotkey(WinAltA);

		win32.Verify(m => m.UnregisterHotKey(Window, 1), Times.Once);
		win32.Verify(m => m.UnregisterHotKey(Window, 0), Times.Never);
		Assert.AreEqual(1, manager.RegisteredHotkeysCount);
	}

	[TestMethod]
	public void ARefusedHotkeyThrowsWindowsErrorAndIsNotKept()
	{
		var manager = CreateHotkeyManager(windowsAccepts: false);
		//ERROR_HOTKEY_ALREADY_REGISTERED: another app has the combination
		win32.Setup(m => m.GetLastError()).Returns(1409);

		var error = Assert.ThrowsExactly<Win32Exception>(() => manager.RegisterHotkey(CtrlShiftF5));

		Assert.AreEqual(1409, error.NativeErrorCode);
		Assert.AreEqual(0, manager.RegisteredHotkeysCount);
	}

	[TestMethod]
	public void TheIdOfARefusedHotkeyIsUsedForTheNextOne()
	{
		var manager = CreateHotkeyManager(windowsAccepts: false);
		Assert.ThrowsExactly<Win32Exception>(() => manager.RegisterHotkey(CtrlShiftF5));
		win32.Setup(m => m.RegisterHotKey(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>())).Returns(true);

		manager.RegisterHotkey(WinAltA);

		win32.Verify(m => m.RegisterHotKey(Window, 0, (HOT_KEY_MODIFIERS)0x9, 0x41u), Times.Once);
	}

	[TestMethod]
	public void DisposingUnregistersEveryHotkey()
	{
		var manager = CreateHotkeyManager();
		manager.RegisterHotkey(CtrlShiftF5);
		manager.RegisterHotkey(WinAltA);

		manager.Dispose();

		win32.Verify(m => m.UnregisterHotKey(Window, 0), Times.Once);
		win32.Verify(m => m.UnregisterHotKey(Window, 1), Times.Once);
	}

	[TestMethod]
	public void AWmHotkeyMessageRaisesItsHotkey()
	{
		var manager = CreateHotkeyManager();
		manager.RegisterHotkey(CtrlShiftF5);
		HotKey pressed = null;
		manager.HotkeyPressed += h => pressed = h;

		//lParam: the virtual key in the high word, the modifiers in the low word
		manager.GetMessageHandler()(0x74 << 16 | 0x6);

		Assert.AreEqual(CtrlShiftF5, pressed);
	}

	[TestMethod]
	public void TheRestartIsRegisteredToStartMinimized()
	{
		new WindowMessages(win32.Object, Window).RegisterRestart();

		//RESTART_NO_REBOOT 0x8
		win32.Verify(m => m.RegisterApplicationRestart("-minimized", (REGISTER_APPLICATION_RESTART_FLAGS)0x8), Times.Once);
	}

	[TestMethod]
	public void TheRestartKeepsTheSettingsFolderOverride()
	{
		var environmentArgs = EnvironmentArgs.Build(name => name == EnvironmentArgs.SettingsDirVariable ? @"C:\Temp\Volumey dev" : null);

		new WindowMessages(win32.Object, Window).RegisterRestart(environmentArgs);

		win32.Verify(m => m.RegisterApplicationRestart("-minimized \"-env:VOLUMEY_SETTINGS_DIR=C:\\Temp\\Volumey dev\"",
			(REGISTER_APPLICATION_RESTART_FLAGS)0x8), Times.Once);
	}

	[TestMethod]
	public void OpeningTheTitleBarMenuGreysOutMinimizeAndMaximize()
	{
		var menu = (IntPtr)0x5678;
		win32.Setup(m => m.GetSystemMenu(Window)).Returns(menu);

		new WindowMessages(win32.Object, Window).Handle(WindowMessages.WM_INITMENU, default, default);

		//SC_MINIMIZE 0xF020, SC_MAXIMIZE 0xF030; MF_GRAYED 0x1 | MF_BYCOMMAND 0x0
		win32.Verify(m => m.EnableMenuItem(menu, 0xF020u, (MENU_ITEM_FLAGS)0x1), Times.Once);
		win32.Verify(m => m.EnableMenuItem(menu, 0xF030u, (MENU_ITEM_FLAGS)0x1), Times.Once);
	}

	[TestMethod]
	public void ANormalExitUnregistersTheRestart()
	{
		var messages = new WindowMessages(win32.Object, Window);

		messages.Handle(WindowMessages.WM_ACTIVATEAPP, default, default);
		messages.Handle(WindowMessages.WM_CLOSE, default, default);

		win32.Verify(m => m.UnregisterApplicationRestart(), Times.Once);
	}

	[TestMethod]
	public void AnUpdateOrCrashKeepsTheRestart()
	{
		var messages = new WindowMessages(win32.Object, Window);

		//WM_QUERYENDSESSION, then the close
		messages.Handle(0x0011, default, default);
		messages.Handle(WindowMessages.WM_CLOSE, default, default);

		win32.Verify(m => m.UnregisterApplicationRestart(), Times.Never);
	}

	[TestMethod]
	public void AWmHotkeyMessageGoesToTheHotkeyManager()
	{
		int received = 0;
		var messages = new WindowMessages(win32.Object, Window) { HotkeyMessageHandler = lParam => received = lParam };

		messages.Handle(WindowMessages.WM_HOTKEY, 7, 0x740006);

		Assert.AreEqual(0x740006, received);
	}

	[TestMethod]
	public void ASecondStartBroadcastsTheShowMessage()
	{
		WindowMessages.ShowRunningInstance(win32.Object, 0xC123);

		//HWND_BROADCAST 0xFFFF
		win32.Verify(m => m.PostMessage(0xFFFF, 0xC123u), Times.Once);
	}

	[TestMethod]
	public void AShortcutPointsToItsTarget()
	{
		string folder = Path.Combine(Path.GetTempPath(), "Volumey-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(folder);
		try
		{
			string link = Path.Combine(folder, "Volumey.lnk");
			string firstTarget = Path.Combine(Environment.SystemDirectory, "notepad.exe");
			string secondTarget = Path.Combine(Environment.SystemDirectory, "calc.exe");

			SystemIntegrationHelper.SaveShortcut(link, firstTarget);
			Assert.AreEqual(firstTarget, SystemIntegrationHelper.ReadShortcutTarget(link), ignoreCase: true);

			//pointing an existing shortcut somewhere else, as a moved exe does
			SystemIntegrationHelper.SaveShortcut(link, secondTarget);
			Assert.AreEqual(secondTarget, SystemIntegrationHelper.ReadShortcutTarget(link), ignoreCase: true);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}

using System.Reflection;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

[TestClass]
public class HotkeysValidationTests
{
	private readonly HotkeyService hotkeys = new HotkeyService();

	[TestMethod]
	public void HotkeysValidationShouldReturn_DiffError()
	{
		//arrange
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.A);

		//act
		var errorType = hotkeys.HotkeysAreValid(upHotkey, downHotkey);

		//assert
		Assert.IsTrue(errorType == ErrorMessageType.Diff);
	}

	[TestMethod]
	public void NullsShouldNotPassHotkeysValidation()
	{
		//arrange
		var hotkey = new HotKey(Key.A);

		//act and assert
		Assert.IsTrue(hotkeys.HotkeysAreValid(null, hotkey) != ErrorMessageType.None);
		Assert.IsTrue(hotkeys.HotkeysAreValid(hotkey, null) != ErrorMessageType.None);
		Assert.IsTrue(hotkeys.HotkeyIsValid(null) != ErrorMessageType.None);
	}

	[TestMethod]
	public void HotkeysValidationShouldReturn_ExistsError()
	{
		var existingHotkey = new HotKey(Key.F1, ModifierKeys.Control);
		SettingsProvider.Settings.Hotkeys.DeviceMute = existingHotkey;

		//act and assert
		Assert.IsTrue(hotkeys.HotkeyIsValid(existingHotkey) == ErrorMessageType.HotkeyExists);
		Assert.IsTrue(hotkeys.HotkeysAreValid(existingHotkey, new HotKey(Key.Down)) ==
					ErrorMessageType.HotkeyExists);
	}

	[TestMethod]
	public void SystemHotkeyShouldNotPassHotkeysValidation()
	{
		//arrange
		var systemHotkey = new HotKey(Key.F12);

		//act and assert
		Assert.IsTrue(hotkeys.HotkeyIsValid(systemHotkey) == ErrorMessageType.F12);
		Assert.IsTrue(hotkeys.HotkeysAreValid(systemHotkey, new HotKey(Key.A)) == ErrorMessageType.F12);
	}

	[TestMethod]
	public void PairCheckReportsEachHotkeysOwnError()
	{
		var existingHotkey = new HotKey(Key.F1, ModifierKeys.Control);
		SettingsProvider.Settings.Hotkeys.DeviceMute = existingHotkey;
		var free = new HotKey(Key.A, ModifierKeys.Control);

		Assert.AreEqual((ErrorMessageType.None, ErrorMessageType.HotkeyExists), hotkeys.CheckHotkeysPair(free, existingHotkey));
		Assert.AreEqual((ErrorMessageType.F12, ErrorMessageType.None), hotkeys.CheckHotkeysPair(new HotKey(Key.F12), free));
		Assert.AreEqual((ErrorMessageType.Missing, ErrorMessageType.None), hotkeys.CheckHotkeysPair(null, free));
		//two equal hotkeys: the second one gets the error
		Assert.AreEqual((ErrorMessageType.None, ErrorMessageType.Diff), hotkeys.CheckHotkeysPair(free, free));
		Assert.AreEqual((ErrorMessageType.None, ErrorMessageType.None), hotkeys.CheckHotkeysPair(free, new HotKey(Key.B, ModifierKeys.Control)));
	}
}

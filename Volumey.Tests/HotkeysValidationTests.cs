using System.Reflection;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.ViewModel.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
	[TestClass]
	public class HotkeysValidationTests
	{
		[TestMethod]
		public void HotkeysValidationShouldReturn_DiffError()
		{
			//arrange
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.A);

			//act
			var errorType = HotkeysControl.HotkeysAreValid(upHotkey, downHotkey);

			//assert
			Assert.IsTrue(errorType == ErrorMessageType.Diff);
		}

		[TestMethod]
		public void NullsShouldNotPassHotkeysValidation()
		{
			//arrange
			var hotkey = new HotKey(Key.A);

			//act and assert
			Assert.IsTrue(HotkeysControl.HotkeysAreValid(null, hotkey) != ErrorMessageType.None);
			Assert.IsTrue(HotkeysControl.HotkeysAreValid(hotkey, null) != ErrorMessageType.None);
			Assert.IsTrue(HotkeysControl.HotkeyIsValid(null) != ErrorMessageType.None);
		}

		[TestMethod]
		public void HotkeysValidationShouldReturn_ExistsError()
		{
			var existingHotkey = new HotKey(Key.F1, ModifierKeys.Control);
			SettingsProvider.Settings.Hotkeys.DeviceMute = existingHotkey;

			//act and assert
			Assert.IsTrue(HotkeysControl.HotkeyIsValid(existingHotkey) == ErrorMessageType.HotkeyExists);
			Assert.IsTrue(HotkeysControl.HotkeysAreValid(existingHotkey, new HotKey(Key.Down)) ==
			            ErrorMessageType.HotkeyExists);
		}

		[TestMethod]
		public void SystemHotkeyShouldNotPassHotkeysValidation()
		{
			//arrange
			var systemHotkey = new HotKey(Key.F12);

			//act and assert
			Assert.IsTrue(HotkeysControl.HotkeyIsValid(systemHotkey) == ErrorMessageType.F12);
			Assert.IsTrue(HotkeysControl.HotkeysAreValid(systemHotkey, new HotKey(Key.A)) == ErrorMessageType.F12);
		}
	}
}
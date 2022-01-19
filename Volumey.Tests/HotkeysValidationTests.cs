using System.Reflection;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.ViewModel.Settings;
using Xunit;

namespace Volumey.Tests
{
	public class HotkeysValidationTests
	{
		[Fact]
		public void HotkeysValidationShouldReturn_DiffError()
		{
			//arrange
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.A);

			//act
			var errorType = HotkeysControl.HotkeysAreValid(upHotkey, downHotkey);

			//assert
			Assert.True(errorType == ErrorMessageType.Diff);
		}

		[Fact]
		public void NullsShouldNotPassHotkeysValidation()
		{
			//arrange
			var hotkey = new HotKey(Key.A);

			//act and assert
			Assert.True(HotkeysControl.HotkeysAreValid(null, hotkey) != ErrorMessageType.None);
			Assert.True(HotkeysControl.HotkeysAreValid(hotkey, null) != ErrorMessageType.None);
			Assert.True(HotkeysControl.HotkeyIsValid(null) != ErrorMessageType.None);
		}

		[Fact]
		public void HotkeysValidationShouldReturn_ExistsError()
		{
			var existingHotkey = new HotKey(Key.F1, ModifierKeys.Control);
			var settings = SettingsProvider.Settings.HotkeysSettings;
			var muteKey = settings.GetType().GetField("DeviceMuteKey", BindingFlags.NonPublic | BindingFlags.Instance);
			var muteModeKeys = settings.GetType().GetField("DeviceMuteModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
			
			muteKey.SetValue(settings, existingHotkey.Key);
			muteModeKeys.SetValue(settings, existingHotkey.ModifierKeys);
			
			//act and assert
			Assert.True(HotkeysControl.HotkeyIsValid(existingHotkey) == ErrorMessageType.HotkeyExists);
			Assert.True(HotkeysControl.HotkeysAreValid(existingHotkey, new HotKey(Key.Down)) ==
			            ErrorMessageType.HotkeyExists);
		}

		[Fact]
		public void SystemHotkeyShouldNotPassHotkeysValidation()
		{
			//arrange
			var systemHotkey = new HotKey(Key.F12);

			//act and assert
			Assert.True(HotkeysControl.HotkeyIsValid(systemHotkey) == ErrorMessageType.F12);
			Assert.True(HotkeysControl.HotkeysAreValid(systemHotkey, new HotKey(Key.A)) == ErrorMessageType.F12);
		}
	}
}
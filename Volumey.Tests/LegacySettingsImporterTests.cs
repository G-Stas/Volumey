using System.IO;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Notification.Wpf.Controls;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.Tests
{
	/// <summary>
	/// The TestData files were written by Volumey 1.5.4's AppSettings class with BinaryFormatter, using synthetic values.
	/// </summary>
	[TestClass]
	public class LegacySettingsImporterTests
	{
		private static VolumeySettings Import(string fileName)
		{
			var settings = new VolumeySettings();
			using(var file = File.OpenRead(Path.Combine(System.AppContext.BaseDirectory, "TestData", fileName)))
				LegacySettingsImporter.Import(file, settings);
			return settings;
		}

		[TestMethod]
		public void ShouldImportGeneralSettings()
		{
			var settings = Import("legacy-appconfig-1.5.4");

			Assert.AreEqual(AppTheme.Dark, settings.General.Theme);
			Assert.AreEqual("de", settings.General.Language);
			Assert.AreEqual(3, settings.General.VolumeStep);
			Assert.IsTrue(settings.General.VolumeLimitEnabled);
			Assert.AreEqual(40, settings.General.VolumeLimit);
			Assert.IsTrue(settings.General.AppVolumeSyncEnabled);
			Assert.AreEqual("Systemsounds", settings.General.SystemSoundsName);
			Assert.AreEqual("de", settings.General.SystemSoundsLanguage);
			Assert.IsTrue(settings.Window.PopupMode);
			Assert.IsTrue(settings.Window.AlwaysOnTop);
			Assert.IsTrue(settings.Window.DeviceViewAtTheBottom);
			Assert.IsTrue(settings.Window.DisplayDeviceIconAtTray);
			Assert.AreEqual(100.5, settings.Window.Left);
			Assert.AreEqual(200.25, settings.Window.Top);
			//turned into a screen name by the settings migration (SettingsProviderTests)
			Assert.AreEqual(1, settings.Window.TakeOtherValue<int?>(VolumeySettings.LegacyScreenIndex));
			Assert.AreEqual(1, settings.Version);
		}

		[TestMethod]
		public void ShouldImportNotificationSettings()
		{
			var settings = Import("legacy-appconfig-1.5.4");

			Assert.IsTrue(settings.Notifications.Enabled);
			Assert.AreEqual(20, settings.Notifications.VerticalIndent);
			Assert.AreEqual(30, settings.Notifications.HorizontalIndent);
			Assert.AreEqual(NotificationPosition.BottomRight, settings.Notifications.Position);
			Assert.AreEqual(5, settings.Notifications.DisplayTimeSeconds);
			Assert.IsTrue(settings.Notifications.ReactToAllVolumeChanges);
			Assert.AreEqual(2, settings.Notifications.TakeOtherValue<int?>(VolumeySettings.LegacyScreenIndex));
		}

		[TestMethod]
		public void ShouldImportHotkeys()
		{
			var settings = Import("legacy-appconfig-1.5.4");
			var ctrl = ModifierKeys.Control;

			Assert.AreEqual(new HotKey(Key.Up, ctrl | ModifierKeys.Alt), settings.Hotkeys.DeviceVolumeUp);
			Assert.AreEqual(new HotKey(Key.Down, ctrl | ModifierKeys.Alt), settings.Hotkeys.DeviceVolumeDown);
			Assert.AreEqual(new HotKey(Key.F1, ctrl), settings.Hotkeys.OpenMixer);
			Assert.AreEqual(new HotKey(Key.M, ctrl | ModifierKeys.Shift), settings.Hotkeys.DeviceMute);
			Assert.AreEqual(new HotKey(Key.PageUp, ctrl), settings.Hotkeys.ForegroundVolumeUp);
			Assert.AreEqual(new HotKey(Key.PageDown, ctrl), settings.Hotkeys.ForegroundVolumeDown);
			Assert.IsTrue(settings.Hotkeys.PreventResettingVolumeBalance);

			var apps = settings.Hotkeys.Apps;
			Assert.HasCount(2, apps);
			var spotify = Assert.ContainsSingle(a => a.ProcessName == "Spotify", apps);
			Assert.AreEqual(new HotKey(Key.Up, ctrl), spotify.VolumeUp);
			Assert.AreEqual(new HotKey(Key.Down, ctrl), spotify.VolumeDown);
			var chrome = Assert.ContainsSingle(a => a.ProcessName == "Google Chrome", apps);
			Assert.AreEqual(new HotKey(Key.Up, ctrl | ModifierKeys.Shift), chrome.VolumeUp);

			var device = Assert.ContainsSingle(settings.Hotkeys.DefaultDevices);
			Assert.AreEqual(new HotKey(Key.F1, ModifierKeys.Alt), device.Hotkey);
			Assert.AreEqual("{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}", device.DeviceId);
			Assert.AreEqual("Speakers (Test Audio)", device.DeviceName);

			var system = Assert.ContainsSingle(settings.Hotkeys.System);
			Assert.AreEqual(Key.MediaPlayPause, system.SystemKey);
			Assert.AreEqual(new HotKey(Key.P, ctrl | ModifierKeys.Alt), system.Replacement);
		}

		[TestMethod]
		public void ShouldConvertOldestSingleAppHotkeys()
		{
			var settings = Import("legacy-appconfig-music-app");

			Assert.AreEqual(2, settings.General.VolumeStep);
			var app = Assert.ContainsSingle(settings.Hotkeys.Apps);
			Assert.AreEqual("Spotify", app.ProcessName);
			Assert.AreEqual(new HotKey(Key.Add, ModifierKeys.Control), app.VolumeUp);
			Assert.AreEqual(new HotKey(Key.Subtract, ModifierKeys.Control), app.VolumeDown);
			//in that format VolumeUp/VolumeDown belonged to the app, not to the device
			Assert.IsNull(settings.Hotkeys.DeviceVolumeUp);
			Assert.IsNull(settings.Hotkeys.DeviceVolumeDown);
		}
	}
}

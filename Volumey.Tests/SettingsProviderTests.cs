using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.Tests
{
	[TestClass]
	public class SettingsProviderTests
	{
		private string dir;

		[TestInitialize]
		public void Initialize()
		{
			this.dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
		}

		[TestCleanup]
		public void Cleanup()
		{
			if(Directory.Exists(this.dir))
				Directory.Delete(this.dir, true);
		}

		private string SettingsFile => Path.Combine(this.dir, "settings.json");

		[TestMethod]
		public void FirstLaunchCreatesTheFileWithDefaults()
		{
			var settings = SettingsProvider.LoadSettings(this.dir);

			Assert.IsTrue(File.Exists(this.SettingsFile));
			Assert.AreEqual(VolumeySettings.CurrentVersion, settings.Version);
			Assert.AreEqual(new VolumeySettings().General.VolumeStep, settings.General.VolumeStep);
			Assert.IsTrue(settings.Hotkeys.BlockInSystem, "hotkeys block by default, as in 1.x");
		}

		[TestMethod]
		public void ChangesAreSavedAndLoadedAgain()
		{
			var settings = SettingsProvider.LoadSettings(this.dir);
			settings.General.VolumeStep = 7;

			var reloaded = SettingsProvider.LoadSettings(this.dir);

			Assert.AreEqual(7, reloaded.General.VolumeStep);
		}

		[TestMethod]
		public void FirstLaunchImportsTheLegacySettingsAndKeepsThem()
		{
			Directory.CreateDirectory(this.dir);
			string legacy = Path.Combine(this.dir, "appconfig");
			File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "legacy-appconfig-1.5.4"), legacy);
			byte[] legacyContent = File.ReadAllBytes(legacy);

			var settings = SettingsProvider.LoadSettings(this.dir, () => ThreeScreens);

			Assert.AreEqual(AppTheme.Dark, settings.General.Theme);
			Assert.AreEqual(3, settings.General.VolumeStep);
			Assert.IsTrue(settings.Hotkeys.BlockInSystem, "upgrading from 1.x keeps blocking hotkeys");
			//the 1.x file stores screen indexes 1 and 2
			Assert.AreEqual(@"\\.\DISPLAY2", settings.Window.ScreenName);
			Assert.AreEqual(@"\\.\DISPLAY3", settings.Notifications.ScreenName);
			Assert.AreEqual(VolumeySettings.CurrentVersion, settings.Version);
			CollectionAssert.AreEqual(legacyContent, File.ReadAllBytes(legacy));
		}

		private static readonly ScreenInfo[] ThreeScreens =
		{
			new ScreenInfo(@"\\.\DISPLAY1", true, 1920, 1040, 1920, 1080, 0, 1920, 0, 1040, 1),
			new ScreenInfo(@"\\.\DISPLAY2", false, 1920, 1040, 1920, 1080, 1920, 3840, 0, 1040, 1),
			new ScreenInfo(@"\\.\DISPLAY3", false, 1920, 1040, 1920, 1080, 3840, 5760, 0, 1040, 1)
		};

		[TestMethod]
		public void Version1ScreenIndexesBecomeScreenNames()
		{
			Directory.CreateDirectory(this.dir);
			File.WriteAllText(this.SettingsFile, """
				{
				  "Version": 1,
				  "Window": { "PopupMode": true, "ScreenIndex": 2 },
				  "Notifications": { "Enabled": true, "ScreenIndex": 7 }
				}
				""");

			var settings = SettingsProvider.LoadSettings(this.dir, () => ThreeScreens);

			Assert.AreEqual(@"\\.\DISPLAY3", settings.Window.ScreenName);
			Assert.IsTrue(settings.Window.PopupMode);
			//an index of a screen that isn't there anymore means the primary screen
			Assert.IsNull(settings.Notifications.ScreenName);
			Assert.AreEqual(VolumeySettings.CurrentVersion, settings.Version);
			string saved = File.ReadAllText(this.SettingsFile);
			Assert.DoesNotContain("ScreenIndex", saved);
			Assert.Contains("ScreenName", saved);
		}

		[TestMethod]
		public void CurrentFilesDontReadTheScreens()
		{
			bool screensRead = false;
			ScreenInfo[] GetScreens()
			{
				screensRead = true;
				return ThreeScreens;
			}
			var settings = SettingsProvider.LoadSettings(this.dir, GetScreens);
			settings.Window.ScreenName = @"\\.\DISPLAY2";

			var reloaded = SettingsProvider.LoadSettings(this.dir, GetScreens);

			Assert.AreEqual(@"\\.\DISPLAY2", reloaded.Window.ScreenName);
			Assert.IsFalse(screensRead);
		}

		[TestMethod]
		public void SettingsOfANewerVersionAreKept()
		{
			//a downgrade from a later version keeps the settings only that version knows
			Directory.CreateDirectory(this.dir);
			File.WriteAllText(this.SettingsFile, $$"""
				{
				  "Version": {{VolumeySettings.CurrentVersion}},
				  "Window": { "PopupMode": true, "SomeFutureSetting": { "Value": 5 } }
				}
				""");

			var settings = SettingsProvider.LoadSettings(this.dir);
			settings.Window.AlwaysOnTop = true;

			string saved = File.ReadAllText(this.SettingsFile);
			Assert.Contains("SomeFutureSetting", saved);
			Assert.IsTrue(SettingsProvider.LoadSettings(this.dir).Window.AlwaysOnTop);
		}

		[TestMethod]
		public void LegacySettingsAreImportedOnlyOnce()
		{
			var first = SettingsProvider.LoadSettings(this.dir);
			first.General.VolumeStep = 9;
			File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "legacy-appconfig-1.5.4"), Path.Combine(this.dir, "appconfig"));

			var second = SettingsProvider.LoadSettings(this.dir);

			Assert.AreEqual(9, second.General.VolumeStep);
		}

		[TestMethod]
		public void CorruptLegacyFileStillGivesDefaults()
		{
			Directory.CreateDirectory(this.dir);
			File.WriteAllText(Path.Combine(this.dir, "appconfig"), "not a BinaryFormatter file");

			var settings = SettingsProvider.LoadSettings(this.dir);

			Assert.AreEqual(new VolumeySettings().General.VolumeStep, settings.General.VolumeStep);
			Assert.IsTrue(File.Exists(this.SettingsFile));
		}
	}
}

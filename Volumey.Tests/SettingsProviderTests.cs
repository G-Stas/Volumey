using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

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

			var settings = SettingsProvider.LoadSettings(this.dir);

			Assert.AreEqual(AppTheme.Dark, settings.General.Theme);
			Assert.AreEqual(3, settings.General.VolumeStep);
			Assert.IsTrue(settings.Hotkeys.BlockInSystem, "upgrading from 1.x keeps blocking hotkeys");
			CollectionAssert.AreEqual(legacyContent, File.ReadAllBytes(legacy));
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

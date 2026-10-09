using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.Tests;

[TestClass]
public class SettingsProviderTests
{
	private string dir;

	[TestInitialize]
	public void Initialize()
	{
		dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
	}

	[TestCleanup]
	public void Cleanup()
	{
		if(Directory.Exists(dir))
			Directory.Delete(dir, true);
	}

	private string SettingsFile => Path.Combine(dir, "settings.json");

	[TestMethod]
	public void FirstLaunchCreatesTheFileWithDefaults()
	{
		var settings = SettingsProvider.LoadSettings(dir);

		Assert.IsTrue(File.Exists(SettingsFile));
		Assert.AreEqual(VolumeySettings.CurrentVersion, settings.Version);
		Assert.AreEqual(new VolumeySettings().General.VolumeStep, settings.General.VolumeStep);
		Assert.IsTrue(settings.Hotkeys.BlockInSystem, "hotkeys block by default, as in 1.x");
	}

	[TestMethod]
	public void ChangesAreSavedAndLoadedAgain()
	{
		var settings = SettingsProvider.LoadSettings(dir);
		settings.General.VolumeStep = 7;
		settings.Flush();

		var reloaded = SettingsProvider.LoadSettings(dir);

		Assert.AreEqual(7, reloaded.General.VolumeStep);
	}

	[TestMethod]
	public void FirstLaunchImportsTheLegacySettingsAndKeepsThem()
	{
		Directory.CreateDirectory(dir);
		string legacy = Path.Combine(dir, "appconfig");
		File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "legacy-appconfig-1.5.4"), legacy);
		byte[] legacyContent = File.ReadAllBytes(legacy);

		var settings = SettingsProvider.LoadSettings(dir, () => ThreeScreens);

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
	public void ScreensAreOnlyReadToImportLegacySettings()
	{
		bool screensRead = false;
		ScreenInfo[] GetScreens()
		{
			screensRead = true;
			return ThreeScreens;
		}
		var settings = SettingsProvider.LoadSettings(dir, GetScreens);
		settings.Window.ScreenName = @"\\.\DISPLAY2";
		settings.Flush();

		var reloaded = SettingsProvider.LoadSettings(dir, GetScreens);

		Assert.AreEqual(@"\\.\DISPLAY2", reloaded.Window.ScreenName);
		Assert.IsFalse(screensRead);
	}

	[TestMethod]
	public void LegacySettingsAreImportedWhenTheScreensCantBeRead()
	{
		Directory.CreateDirectory(dir);
		File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "legacy-appconfig-1.5.4"), Path.Combine(dir, "appconfig"));

		var settings = SettingsProvider.LoadSettings(dir, () => throw new InvalidOperationException("no screens"));

		Assert.AreEqual(3, settings.General.VolumeStep);
		Assert.IsNull(settings.Window.ScreenName);
	}

	[TestMethod]
	public void LegacySettingsAreImportedOnlyOnce()
	{
		var first = SettingsProvider.LoadSettings(dir);
		first.General.VolumeStep = 9;
		first.Flush();
		File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "legacy-appconfig-1.5.4"), Path.Combine(dir, "appconfig"));

		var second = SettingsProvider.LoadSettings(dir);

		Assert.AreEqual(9, second.General.VolumeStep);
	}

	[TestMethod]
	public void CorruptLegacyFileStillGivesDefaults()
	{
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "appconfig"), "not a BinaryFormatter file");

		var settings = SettingsProvider.LoadSettings(dir);

		Assert.AreEqual(new VolumeySettings().General.VolumeStep, settings.General.VolumeStep);
		Assert.IsTrue(File.Exists(SettingsFile));
	}
}

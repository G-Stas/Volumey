using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Autosave;
using Nucs.JsonSettings.Modulation.Recovery;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.Tests;

[TestClass]
public class VolumeySettingsTests
{
	private string dir;
	private string file;

	[TestInitialize]
	public void Initialize()
	{
		dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		file = Path.Combine(dir, "settings.json");
	}

	[TestCleanup]
	public void Cleanup() => Directory.Delete(dir, true);

	private VolumeySettings Load()
		=> JsonSettings.Configure<VolumeySettings>(file).WithRecovery(RecoveryAction.RenameAndLoadDefault).LoadNow();

	[TestMethod]
	public void NestedSectionChangesShouldBeAutosaved()
	{
		var hotkey = new HotKey(Key.F1, ModifierKeys.Control | ModifierKeys.Shift);
		var settings = Load().EnableAutosave();

		settings.General.VolumeStep = 5;
		settings.Notifications.Position = NotificationPosition.BottomRight;
		settings.Hotkeys.OpenMixer = hotkey;
		settings.Hotkeys.Apps = new List<AppHotkeysEntry> { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = hotkey } };
		settings.Flush();

		var reloaded = Load();
		Assert.AreEqual(5, reloaded.General.VolumeStep);
		Assert.AreEqual(NotificationPosition.BottomRight, reloaded.Notifications.Position);
		Assert.AreEqual(hotkey, reloaded.Hotkeys.OpenMixer);
		var app = Assert.ContainsSingle(reloaded.Hotkeys.Apps);
		Assert.AreEqual("Spotify", app.ProcessName);
		Assert.AreEqual(hotkey, app.VolumeUp);
		Assert.IsNull(app.VolumeDown);
	}

	[TestMethod]
	public void FileShouldStoreEnumsAndHotkeysByName()
	{
		var settings = Load().EnableAutosave();

		settings.General.Theme = AppTheme.Dark;
		settings.Hotkeys.DeviceMute = new HotKey(Key.M, ModifierKeys.Control);
		settings.Flush();

		string json = File.ReadAllText(file);
		StringAssert.Contains(json, "\"Theme\": \"Dark\"");
		StringAssert.Contains(json, "\"Key\": \"M\"");
		StringAssert.Contains(json, "\"ModifierKeys\": \"Control\"");
		Assert.IsFalse(json.Contains("$type"), "no .NET type names should be stored");
	}

	[TestMethod]
	public void ABurstOfChangesIsSavedOnceAfterTheDelay()
	{
		var settings = Load().EnableAutosave();
		var before = File.GetLastWriteTimeUtc(file);

		for(int volume = 10; volume <= 60; volume++)
			settings.General.VolumeLimit = volume;

		//nothing is written while the burst goes on
		Assert.AreEqual(before, File.GetLastWriteTimeUtc(file));
		System.Threading.Thread.Sleep(VolumeySettings.SaveDelay * 4);
		Assert.AreEqual(60, Load().General.VolumeLimit);
	}

	[TestMethod]
	public void SectionShouldNotifyOnlyOnChange()
	{
		var general = new GeneralSettings();
		var changes = new List<string>();
		general.PropertyChanged += (s, e) => changes.Add(e.PropertyName);

		general.VolumeStep = 5;
		general.VolumeStep = 5;

		CollectionAssert.AreEqual(new[] { nameof(GeneralSettings.VolumeStep) }, changes);
	}

	[TestMethod]
	public void CorruptFileShouldLoadDefaults()
	{
		File.WriteAllText(file, "{ not json");

		var settings = Load();

		Assert.AreEqual(new GeneralSettings().VolumeStep, settings.General.VolumeStep);
		Assert.AreEqual(VolumeySettings.CurrentVersion, settings.Version);
	}
}

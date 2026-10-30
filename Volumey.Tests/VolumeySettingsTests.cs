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
	public void MouseHotkeysAreSavedAndKeyboardHotkeysStayAsBefore()
	{
		var settings = Load().EnableAutosave();
		var mouse = HotKey.Mouse(MouseButtons.XButton2, ModifierKeys.Control);
		settings.Hotkeys.DeviceVolumeUp = mouse;
		settings.Hotkeys.DeviceMute = new HotKey(Key.M, ModifierKeys.Control);
		settings.Flush();

		string json = File.ReadAllText(file);
		StringAssert.Contains(json, "\"Source\": \"Mouse\"");
		StringAssert.Contains(json, "\"Button\": \"XButton2\"");
		//a keyboard hotkey has only Key and ModifierKeys, as before mouse hotkeys
		Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(json, "\"Source\"").Count);
		var reloaded = Load();
		Assert.AreEqual(mouse, reloaded.Hotkeys.DeviceVolumeUp);
		Assert.AreEqual(new HotKey(Key.M, ModifierKeys.Control), reloaded.Hotkeys.DeviceMute);
	}

	// U5
	[TestMethod]
	[DataRow("\"DisplayDeviceIconAtTray\": true", TrayIconKind.DeviceIcon)]
	[DataRow("\"DisplayDeviceIconAtTray\": false", TrayIconKind.VolumeLevel)]
	[DataRow("\"PopupMode\": true", TrayIconKind.VolumeLevel)]
	[DataRow("\"TrayIcon\": \"AppIcon\"", TrayIconKind.AppIcon)]
	public void TheTrayIconChoiceIsReadFromOldAndNewFiles(string window, TrayIconKind expected)
	{
		File.WriteAllText(file, "{ \"Window\": { " + window + " } }");

		Assert.AreEqual(expected, Load().Window.TrayIcon);
	}

	// U5
	[TestMethod]
	public void TheOldTrayIconKeyIsNotWrittenBack()
	{
		File.WriteAllText(file, """{ "Window": { "DisplayDeviceIconAtTray": true } }""");

		Load().SaveNow();

		string saved = File.ReadAllText(file);
		StringAssert.Contains(saved, "\"TrayIcon\": \"DeviceIcon\"");
		Assert.IsFalse(saved.Contains("DisplayDeviceIconAtTray"), saved);
	}

	[TestMethod]
	public void AHotkeySavedWithoutASourceIsAKeyboardHotkey()
	{
		//how every hotkey was saved before mouse hotkeys
		File.WriteAllText(file, """
			{
			  "Hotkeys": {
			    "DeviceMute": { "Key": "F1", "ModifierKeys": "Control, Shift" }
			  }
			}
			""");

		var hotkey = Load().Hotkeys.DeviceMute;

		Assert.AreEqual(new HotKey(Key.F1, ModifierKeys.Control | ModifierKeys.Shift), hotkey);
		Assert.AreEqual(InputSource.Keyboard, hotkey!.Source);
		Assert.IsNull(hotkey.Button);
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
	public void FlushWaitsForAWriteThatIsAlreadyRunning()
	{
		//the timer's write has taken the change and is still writing when the app exits and flushes
		var settings = Load().EnableAutosave();
		settings.General.VolumeStep = 7;
		//nothing pending anymore: the timer's write has taken it
		settings.Flush();
		var writeLock = typeof(VolumeySettings).GetField("writeLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
		                                      .GetValue(settings)!;
		using var writing = new System.Threading.ManualResetEventSlim();
		using var finish = new System.Threading.ManualResetEventSlim();
		var write = System.Threading.Tasks.Task.Run(() =>
		{
			lock(writeLock)
			{
				writing.Set();
				finish.Wait();
			}
		});
		writing.Wait();

		var flush = System.Threading.Tasks.Task.Run(settings.Flush);
		Assert.IsFalse(flush.Wait(300), "Flush returned while the file was still being written");

		finish.Set();
		Assert.IsTrue(flush.Wait(5000), "Flush didn't return after the write");
		write.Wait();
	}

	[TestMethod]
	public void ASaveWaitsForAnotherProcessToLetGoOfTheFile()
	{
		var settings = Load().EnableAutosave();
		settings.SaveNow();
		System.Threading.Tasks.Task flush;
		using(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
		{
			settings.General.Theme = AppTheme.Dark;
			flush = System.Threading.Tasks.Task.Run(settings.Flush);
			System.Threading.Thread.Sleep(VolumeySettings.SaveRetryDelay * 2);
			Assert.IsFalse(flush.IsCompleted, "Flush gave up while the file was in use");
		}

		Assert.IsTrue(flush.Wait(5000), "Flush didn't return after the file was released");
		Assert.AreEqual(AppTheme.Dark, Load().General.Theme);
	}

	[TestMethod]
	public void ASaveGivesUpAndLogsWhenTheFileStaysInUse()
	{
		var repository = (log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository(typeof(VolumeySettings).Assembly);
		bool wasConfigured = repository.Configured;
		var log = new log4net.Appender.MemoryAppender();
		log4net.Config.BasicConfigurator.Configure(repository, log);
		try
		{
			var settings = Load().EnableAutosave();
			settings.SaveNow();
			string before = File.ReadAllText(file);
			using(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				settings.General.Theme = AppTheme.Dark;
				var flush = System.Threading.Tasks.Task.Run(settings.Flush);
				Assert.IsTrue(flush.Wait(VolumeySettings.SaveRetryDelay * VolumeySettings.SaveAttempts * 5), "Flush didn't give up");
			}

			Assert.AreEqual(before, File.ReadAllText(file));
			var error = Assert.ContainsSingle(log.GetEvents());
			Assert.AreEqual("Failed to save the settings", error.RenderedMessage);
			Assert.IsInstanceOfType<IOException>(error.ExceptionObject.InnerException ?? error.ExceptionObject);
		}
		finally
		{
			repository.Root.RemoveAppender(log);
			repository.Configured = wasConfigured;
		}
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

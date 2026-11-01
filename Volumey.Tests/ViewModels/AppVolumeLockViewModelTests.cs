using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class AppVolumeLockViewModelTests : ViewModelTestBase
{
	private Mock<IAppRouting> routing;

	[TestInitialize]
	public void Initialize()
	{
		routing = new Mock<IAppRouting>();
		//the same app on all devices
		routing.Setup(m => m.GetModels(It.IsAny<AudioProcessModel>()))
			.Returns<AudioProcessModel>(app => ActiveDevices.SelectMany(d => d.Processes).Where(p => p.Name == app.Name).ToList());
	}

	private AppVolumeLockViewModel CreateLocks() => new AppVolumeLockViewModel(DeviceProvider.Object, routing.Object);

	//a process with an ID: the model of the System Sounds has none
	private AudioProcessModel CreateApp(string name, int volume, uint processId = 100)
		=> new AudioProcessModel(volume, false, name, processId, string.Empty, null, null, Hotkeys);

	private static void Press(AudioProcessModel app, int times = 1)
	{
		for(int i = 0; i < times; i++)
			app.ToggleVolumeLockCommand.Execute(null);
	}

	private static void SaveLocked(VolumeySettings settings, string name, int volume)
		=> settings.General.LockedAppVolumes = new[] { new AppVolumeLockEntry { AppName = name, Volume = volume } };

	[TestMethod]
	public void TheFirstPressLocksTheAppForTheSessionAndSavesNothing()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Press(spotify);

		Assert.AreEqual(VolumeLockMode.Session, spotify.LockMode);
		Assert.IsTrue(spotify.IsVolumeLocked);
		Assert.AreEqual(40, spotify.Volume);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void TheSecondPressMakesTheLockPersistentAndSavesIt()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Press(spotify, 2);

		Assert.AreEqual(VolumeLockMode.Persistent, spotify.LockMode);
		var entry = Assert.ContainsSingle(Settings.General.LockedAppVolumes);
		Assert.AreEqual("Spotify", entry.AppName);
		Assert.AreEqual(40, entry.Volume);
	}

	[TestMethod]
	public void TheThirdPressUnlocksTheAppAndRemovesTheEntry()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Press(spotify, 3);

		Assert.AreEqual(VolumeLockMode.None, spotify.LockMode);
		Assert.IsFalse(spotify.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ASessionLockIsNotAppliedWhenTheAppStartsAgain()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();
		var first = CreateApp("Spotify", 40);
		device.Processes.Add(first);
		Press(first);

		device.Processes.Remove(first);
		var second = CreateApp("Spotify", 100);
		device.Processes.Add(second);

		Assert.IsFalse(second.IsVolumeLocked);
		Assert.AreEqual(100, second.Volume);
	}

	[TestMethod]
	public void ASavedAppIsPersistent()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.AreEqual(VolumeLockMode.Persistent, spotify.LockMode);
	}

	[TestMethod]
	public void AppsOnOtherDevicesAreLockedTogether()
	{
		var onSpeakers = CreateApp("Spotify", 40);
		var onHeadphones = CreateApp("Spotify", 70, processId: 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", onSpeakers));
		ActiveDevices.Add(CreateDevice("2", "Headphones", onHeadphones));
		CreateLocks();

		Press(onSpeakers);

		Assert.IsTrue(onHeadphones.IsVolumeLocked);
		Assert.AreEqual(40, onHeadphones.Volume);

		Press(onHeadphones);

		Assert.AreEqual(VolumeLockMode.Persistent, onSpeakers.LockMode);
		Assert.ContainsSingle(Settings.General.LockedAppVolumes);

		Press(onSpeakers);

		Assert.IsFalse(onSpeakers.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ASavedAppIsLockedWhenItStarts()
	{
		SaveLocked(Settings, "Spotify", 25);
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();

		var spotify = CreateApp("Spotify", 100);
		device.Processes.Add(spotify);

		Assert.IsTrue(spotify.IsVolumeLocked);
		Assert.AreEqual(25, spotify.Volume);
	}

	[TestMethod]
	public void ASavedAppThatIsRunningAlreadyIsLockedAtStart()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.IsTrue(spotify.IsVolumeLocked);
		Assert.AreEqual(25, spotify.Volume);
	}

	[TestMethod]
	public void ASavedAppOnADeviceThatIsPluggedInLaterIsLocked()
	{
		SaveLocked(Settings, "Spotify", 25);
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		CreateLocks();

		var spotify = CreateApp("Spotify", 100);
		ActiveDevices.Add(CreateDevice("2", "Headphones", spotify));

		Assert.IsTrue(spotify.IsVolumeLocked);
	}

	[TestMethod]
	public void OtherAppsAreLeftAlone()
	{
		SaveLocked(Settings, "Spotify", 25);
		var chrome = CreateApp("Chrome", 100, processId: 200);
		SetDefaultDevice(CreateDevice("1", "Speakers", chrome));

		CreateLocks();

		Assert.IsFalse(chrome.IsVolumeLocked);
		Assert.AreEqual(100, chrome.Volume);
	}

	[TestMethod]
	public void TheAppNameIsMatchedWithoutCase()
	{
		SaveLocked(Settings, "spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.IsTrue(spotify.IsVolumeLocked);
	}

	[TestMethod]
	public void TheSystemSoundsCantBeLocked()
	{
		//the System Sounds have no process ID
		var systemSounds = CreateApp("System Sounds", 80, processId: 0);
		Assert.IsTrue(systemSounds.IsSystemSounds);
		SetDefaultDevice(CreateDevice("1", "Speakers", systemSounds));
		CreateLocks();

		systemSounds.ToggleVolumeLockCommand.Execute(null);

		Assert.IsFalse(systemSounds.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void LockingAgainTheSameAppKeepsOneEntryWithTheNewVolume()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var locks = CreateLocks();

		Press(spotify, 3);
		spotify.Volume = 55;
		Press(spotify, 2);

		var entry = Assert.ContainsSingle(Settings.General.LockedAppVolumes);
		Assert.AreEqual(55, entry.Volume);
		Assert.IsNotNull(locks);
	}

	[TestMethod]
	public void AnAppThatExitedIsNoLongerFollowed()
	{
		var spotify = CreateApp("Spotify", 40);
		var device = CreateDevice("1", "Speakers", spotify);
		SetDefaultDevice(device);
		CreateLocks();

		device.Processes.Remove(spotify);
		spotify.ToggleVolumeLockCommand.Execute(null);

		Assert.IsFalse(spotify.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void TheLockedAppsSurviveARestart()
	{
		var spotify = CreateApp("Spotify", 40);
		var device = CreateDevice("1", "Speakers", spotify);
		SetDefaultDevice(device);
		CreateLocks();
		Press(spotify, 2);

		//the next run reads the saved list
		var started = CreateApp("Spotify", 100);
		var newDevice = CreateDevice("2", "Speakers", started);
		ActiveDevices.Clear();
		SetDefaultDevice(newDevice);
		CreateLocks();

		Assert.IsTrue(started.IsVolumeLocked);
		Assert.AreEqual(40, started.Volume);
	}

	[TestMethod]
	public void ClosingGivesBackTheVolumesButKeepsTheSavedLocks()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var locks = CreateLocks();
		Assert.AreEqual(25, spotify.Volume);

		locks.ReleaseAll();

		Assert.IsFalse(spotify.IsVolumeLocked);
		Assert.AreEqual(100, spotify.Volume);
		Assert.ContainsSingle(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ClosingGivesBackTheVolumeOfASessionLockToo()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var locks = CreateLocks();
		Press(spotify);

		locks.ReleaseAll();

		Assert.IsFalse(spotify.IsVolumeLocked);
		Assert.AreEqual(40, spotify.Volume);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ClosingLeavesAnUnlockedAppAlone()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var locks = CreateLocks();
		spotify.Volume = 60;

		locks.ReleaseAll();

		Assert.AreEqual(60, spotify.Volume);
	}

	[TestMethod]
	public void TheFeatureIsInTheListOfTheFeaturesCreatedAtStartup()
		=> CollectionAssert.Contains(Volumey.Services.ServiceRegistration.Features, typeof(AppVolumeLockViewModel));
}

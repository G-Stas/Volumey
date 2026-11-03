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

	private static void Pick(AudioProcessModel app, VolumeLockMode mode) => app.LockMode = mode;

	private static void SaveLocked(VolumeySettings settings, string name, int volume)
		=> settings.General.LockedAppVolumes = new[] { new AppVolumeLockEntry { AppName = name, Volume = volume } };

	[TestMethod]
	public void UntilClosedLocksTheAppAndSavesNothing()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Pick(spotify, VolumeLockMode.Session);

		Assert.AreEqual(VolumeLockMode.Session, spotify.LockMode);
		Assert.IsTrue(spotify.IsVolumeLocked);
		Assert.AreEqual(40, spotify.Volume);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void AlwaysLocksTheAppAndSavesIt()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Pick(spotify, VolumeLockMode.Persistent);

		Assert.AreEqual(VolumeLockMode.Persistent, spotify.LockMode);
		Assert.IsTrue(spotify.IsVolumeLocked);
		var entry = Assert.ContainsSingle(Settings.General.LockedAppVolumes);
		Assert.AreEqual("Spotify", entry.AppName);
		Assert.AreEqual(40, entry.Volume);
	}

	[TestMethod]
	public void OffUnlocksTheAppAndRemovesTheEntry()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();
		Pick(spotify, VolumeLockMode.Persistent);

		Pick(spotify, VolumeLockMode.None);

		Assert.AreEqual(VolumeLockMode.None, spotify.LockMode);
		Assert.IsFalse(spotify.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void UntilClosedAfterAlwaysKeepsTheLockAndRemovesTheEntry()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();
		Pick(spotify, VolumeLockMode.Persistent);

		Pick(spotify, VolumeLockMode.Session);

		Assert.AreEqual(VolumeLockMode.Session, spotify.LockMode);
		Assert.IsTrue(spotify.IsVolumeLocked);
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
		Pick(first, VolumeLockMode.Session);

		device.Processes.Remove(first);
		var second = CreateApp("Spotify", 100, processId: 101);
		device.Processes.Add(second);

		Assert.IsFalse(second.IsVolumeLocked);
		Assert.AreEqual(100, second.Volume);
	}

	[TestMethod]
	public void ASessionLockIsKeptWhenTheAppsModelIsMadeAgain()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();
		var first = CreateApp("Spotify", 40);
		device.Processes.Add(first);
		Pick(first, VolumeLockMode.Session);

		//the device is read again (a format change) or the app moves: a new model of the same process
		device.Processes.Remove(first);
		var again = CreateApp("Spotify", 100);
		device.Processes.Add(again);

		Assert.AreEqual(VolumeLockMode.Session, again.LockMode);
		Assert.AreEqual(40, again.Volume);
	}

	[TestMethod]
	public void ASessionLockedAppIsLockedOnADeviceItStartsPlayingOnLater()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();
		Pick(spotify, VolumeLockMode.Session);

		var onHeadphones = CreateApp("Spotify", 70);
		ActiveDevices.Add(CreateDevice("2", "Headphones", onHeadphones));

		Assert.AreEqual(VolumeLockMode.Session, onHeadphones.LockMode);
		Assert.AreEqual(40, onHeadphones.Volume);
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
		var onHeadphones = CreateApp("Spotify", 70);
		SetDefaultDevice(CreateDevice("1", "Speakers", onSpeakers));
		ActiveDevices.Add(CreateDevice("2", "Headphones", onHeadphones));
		CreateLocks();

		Pick(onSpeakers, VolumeLockMode.Session);

		Assert.IsTrue(onHeadphones.IsVolumeLocked);
		Assert.AreEqual(40, onHeadphones.Volume);

		Pick(onHeadphones, VolumeLockMode.Persistent);

		Assert.AreEqual(VolumeLockMode.Persistent, onSpeakers.LockMode);
		Assert.ContainsSingle(Settings.General.LockedAppVolumes);

		Pick(onSpeakers, VolumeLockMode.None);

		Assert.IsFalse(onHeadphones.IsVolumeLocked);
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

		Pick(systemSounds, VolumeLockMode.Persistent);

		Assert.AreEqual(VolumeLockMode.None, systemSounds.LockMode);
		Assert.IsFalse(systemSounds.IsVolumeLocked);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ARefusedLockTellsTheMixerTheModeTheAppHas()
	{
		var systemSounds = CreateApp("System Sounds", 80, processId: 0);
		SetDefaultDevice(CreateDevice("1", "Speakers", systemSounds));
		CreateLocks();
		var changed = new List<string>();
		systemSounds.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		Pick(systemSounds, VolumeLockMode.Session);

		CollectionAssert.Contains(changed, nameof(AudioProcessModel.LockMode));
	}

	[TestMethod]
	public void LockingAgainTheSameAppKeepsOneEntryWithTheNewVolume()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		Pick(spotify, VolumeLockMode.Persistent);
		Pick(spotify, VolumeLockMode.None);
		spotify.Volume = 55;
		Pick(spotify, VolumeLockMode.Persistent);

		var entry = Assert.ContainsSingle(Settings.General.LockedAppVolumes);
		Assert.AreEqual(55, entry.Volume);
	}

	[TestMethod]
	public void AnAppThatExitedIsNoLongerFollowed()
	{
		var spotify = CreateApp("Spotify", 40);
		var device = CreateDevice("1", "Speakers", spotify);
		SetDefaultDevice(device);
		CreateLocks();

		device.Processes.Remove(spotify);
		Pick(spotify, VolumeLockMode.Persistent);

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
		Pick(spotify, VolumeLockMode.Persistent);

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
		Pick(spotify, VolumeLockMode.Session);

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

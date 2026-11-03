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

	//the processes that run; every process runs unless a test ends it
	private HashSet<uint> exited;

	//processes whose ID now runs another executable
	private Dictionary<uint, string> reused;

	[TestInitialize]
	public void Initialize()
	{
		routing = new Mock<IAppRouting>();
		//the same app on all devices
		routing.Setup(m => m.GetModels(It.IsAny<AudioProcessModel>()))
			.Returns<AudioProcessModel>(app => ActiveDevices.SelectMany(d => d.Processes).Where(p => p.Name == app.Name).ToList());
		exited = new HashSet<uint>();
		reused = new Dictionary<uint, string>();
	}

	private AppVolumeLockViewModel CreateLocks() => new AppVolumeLockViewModel(DeviceProvider.Object, routing.Object, IsRunning);

	private bool IsRunning(uint id, string path)
		=> !exited.Contains(id) && (path == null || !reused.TryGetValue(id, out var runs) || runs == path);

	//a process with an ID: the model of the System Sounds has none
	private AudioProcessModel CreateApp(string name, int volume, uint processId = 100, string filePath = "")
		=> new AudioProcessModel(volume, false, name, processId, filePath, null, null, Hotkeys);

	private static void SaveLocked(VolumeySettings settings, string name, int volume)
		=> settings.General.LockedAppVolumes = new[] { new AppVolumeLockEntry { AppName = name, Volume = volume } };

	[TestMethod]
	public void UntilClosedLocksTheAppAndSavesNothing()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		spotify.Lock.Mode = VolumeLockMode.UntilClosed;

		Assert.AreEqual(VolumeLockMode.UntilClosed, spotify.Lock.Mode);
		Assert.IsTrue(spotify.Lock.IsOn);
		Assert.AreEqual(40, spotify.Volume);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void AlwaysLocksTheAppAndSavesIt()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		spotify.Lock.Mode = VolumeLockMode.Always;

		Assert.AreEqual(VolumeLockMode.Always, spotify.Lock.Mode);
		Assert.IsTrue(spotify.Lock.IsOn);
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
		spotify.Lock.Mode = VolumeLockMode.Always;

		spotify.Lock.Mode = VolumeLockMode.Off;

		Assert.AreEqual(VolumeLockMode.Off, spotify.Lock.Mode);
		Assert.IsFalse(spotify.Lock.IsOn);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void UntilClosedAfterAlwaysKeepsTheLockAndRemovesTheEntry()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();
		spotify.Lock.Mode = VolumeLockMode.Always;

		spotify.Lock.Mode = VolumeLockMode.UntilClosed;

		Assert.AreEqual(VolumeLockMode.UntilClosed, spotify.Lock.Mode);
		Assert.IsTrue(spotify.Lock.IsOn);
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
		first.Lock.Mode = VolumeLockMode.UntilClosed;

		device.Processes.Remove(first);
		var second = CreateApp("Spotify", 100, processId: 101);
		device.Processes.Add(second);

		Assert.IsFalse(second.Lock.IsOn);
		Assert.AreEqual(100, second.Volume);
	}

	[TestMethod]
	public void ASessionLockEndsWhenTheAppExitsEvenIfItsProcessIdComesBack()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();
		var first = CreateApp("Spotify", 40);
		device.Processes.Add(first);
		first.Lock.Mode = VolumeLockMode.UntilClosed;

		exited.Add(100);
		device.Processes.Remove(first);
		//Windows gave a new process the same ID
		exited.Remove(100);
		var second = CreateApp("Spotify", 100);
		device.Processes.Add(second);

		Assert.IsFalse(second.Lock.IsOn);
	}

	[TestMethod]
	public void ASessionLockIsKeptWhenTheAppsModelIsMadeAgain()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();
		var first = CreateApp("Spotify", 40);
		device.Processes.Add(first);
		first.Lock.Mode = VolumeLockMode.UntilClosed;

		//the device is read again (a format change) or the app moves: a new model of the same process
		device.Processes.Remove(first);
		var again = CreateApp("Spotify", 100);
		device.Processes.Add(again);

		Assert.AreEqual(VolumeLockMode.UntilClosed, again.Lock.Mode);
		Assert.AreEqual(40, again.Volume);
	}

	[TestMethod]
	public void ASessionLockedAppIsLockedOnADeviceItStartsPlayingOnLater()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();
		spotify.Lock.Mode = VolumeLockMode.UntilClosed;

		var onHeadphones = CreateApp("Spotify", 70);
		ActiveDevices.Add(CreateDevice("2", "Headphones", onHeadphones));

		Assert.AreEqual(VolumeLockMode.UntilClosed, onHeadphones.Lock.Mode);
		Assert.AreEqual(40, onHeadphones.Volume);
	}

	[TestMethod]
	public void ASavedAppIsPersistent()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.AreEqual(VolumeLockMode.Always, spotify.Lock.Mode);
	}

	[TestMethod]
	public void AppsOnOtherDevicesAreLockedTogether()
	{
		var onSpeakers = CreateApp("Spotify", 40);
		var onHeadphones = CreateApp("Spotify", 70);
		SetDefaultDevice(CreateDevice("1", "Speakers", onSpeakers));
		ActiveDevices.Add(CreateDevice("2", "Headphones", onHeadphones));
		CreateLocks();

		onSpeakers.Lock.Mode = VolumeLockMode.UntilClosed;

		Assert.IsTrue(onHeadphones.Lock.IsOn);
		Assert.AreEqual(40, onHeadphones.Volume);

		onHeadphones.Lock.Mode = VolumeLockMode.Always;

		Assert.AreEqual(VolumeLockMode.Always, onSpeakers.Lock.Mode);
		Assert.ContainsSingle(Settings.General.LockedAppVolumes);

		onSpeakers.Lock.Mode = VolumeLockMode.Off;

		Assert.IsFalse(onHeadphones.Lock.IsOn);
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

		Assert.IsTrue(spotify.Lock.IsOn);
		Assert.AreEqual(25, spotify.Volume);
	}

	[TestMethod]
	public void ASavedAppThatIsRunningAlreadyIsLockedAtStart()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.IsTrue(spotify.Lock.IsOn);
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

		Assert.IsTrue(spotify.Lock.IsOn);
	}

	[TestMethod]
	public void OtherAppsAreLeftAlone()
	{
		SaveLocked(Settings, "Spotify", 25);
		var chrome = CreateApp("Chrome", 100, processId: 200);
		SetDefaultDevice(CreateDevice("1", "Speakers", chrome));

		CreateLocks();

		Assert.IsFalse(chrome.Lock.IsOn);
		Assert.AreEqual(100, chrome.Volume);
	}

	[TestMethod]
	public void TheAppNameIsMatchedWithoutCase()
	{
		SaveLocked(Settings, "spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));

		CreateLocks();

		Assert.IsTrue(spotify.Lock.IsOn);
	}

	[TestMethod]
	public void TheSystemSoundsCantBeLocked()
	{
		//the System Sounds have no process ID
		var systemSounds = CreateApp("System Sounds", 80, processId: 0);
		Assert.IsTrue(systemSounds.IsSystemSounds);
		SetDefaultDevice(CreateDevice("1", "Speakers", systemSounds));
		CreateLocks();

		systemSounds.Lock.Mode = VolumeLockMode.Always;

		Assert.AreEqual(VolumeLockMode.Off, systemSounds.Lock.Mode);
		Assert.IsFalse(systemSounds.Lock.IsOn);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ARefusedLockTellsTheMixerTheModeTheAppHas()
	{
		var systemSounds = CreateApp("System Sounds", 80, processId: 0);
		SetDefaultDevice(CreateDevice("1", "Speakers", systemSounds));
		CreateLocks();
		var changed = new List<string>();
		systemSounds.Lock.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		systemSounds.Lock.Mode = VolumeLockMode.UntilClosed;

		CollectionAssert.Contains(changed, nameof(AppVolumeLock.Mode));
	}

	[TestMethod]
	public void LockingAgainTheSameAppKeepsOneEntryWithTheNewVolume()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		CreateLocks();

		spotify.Lock.Mode = VolumeLockMode.Always;
		spotify.Lock.Mode = VolumeLockMode.Off;
		spotify.Volume = 55;
		spotify.Lock.Mode = VolumeLockMode.Always;

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
		spotify.Lock.Mode = VolumeLockMode.Always;

		Assert.IsFalse(spotify.Lock.IsOn);
		Assert.IsEmpty(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void TheLockedAppsSurviveARestart()
	{
		var spotify = CreateApp("Spotify", 40);
		var device = CreateDevice("1", "Speakers", spotify);
		SetDefaultDevice(device);
		CreateLocks();
		spotify.Lock.Mode = VolumeLockMode.Always;

		//the next run reads the saved list
		var started = CreateApp("Spotify", 100);
		var newDevice = CreateDevice("2", "Speakers", started);
		ActiveDevices.Clear();
		SetDefaultDevice(newDevice);
		CreateLocks();

		Assert.IsTrue(started.Lock.IsOn);
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

		Assert.IsFalse(spotify.Lock.IsOn);
		Assert.AreEqual(100, spotify.Volume);
		Assert.ContainsSingle(Settings.General.LockedAppVolumes);
	}

	[TestMethod]
	public void ClosingGivesBackTheVolumeOfASessionLockToo()
	{
		var spotify = CreateApp("Spotify", 40);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var locks = CreateLocks();
		spotify.Lock.Mode = VolumeLockMode.UntilClosed;

		locks.ReleaseAll();

		Assert.IsFalse(spotify.Lock.IsOn);
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
	public void ClosingAfterTheAppsModelIsMadeAgainGivesBackTheVolumeBeforeTheLock()
	{
		SaveLocked(Settings, "Spotify", 25);
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		var locks = CreateLocks();
		var spotify = CreateApp("Spotify", 100);
		device.Processes.Add(spotify);

		//a format change: the new model reads the held volume from the sessions
		device.Processes.Remove(spotify);
		var again = CreateApp("Spotify", 25);
		device.Processes.Add(again);
		locks.ReleaseAll();

		Assert.AreEqual(100, again.Volume);
	}

	[TestMethod]
	public void ClosingAfterAMoveGivesBackTheVolumeBeforeTheLock()
	{
		SaveLocked(Settings, "Spotify", 25);
		var spotify = CreateApp("Spotify", 100);
		var speakers = CreateDevice("1", "Speakers", spotify);
		SetDefaultDevice(speakers);
		var headphones = CreateDevice("2", "Headphones");
		ActiveDevices.Add(headphones);
		var locks = CreateLocks();

		var moved = CreateApp("Spotify", 25);
		headphones.Processes.Add(moved);
		speakers.Processes.Remove(spotify);
		locks.ReleaseAll();

		Assert.AreEqual(100, moved.Volume);
	}

	[TestMethod]
	public void ChangingTheModeKeepsTheVolumeBeforeTheLock()
	{
		SaveLocked(Settings, "Spotify", 25);
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		var locks = CreateLocks();
		var spotify = CreateApp("Spotify", 100);
		device.Processes.Add(spotify);

		spotify.Lock.Mode = VolumeLockMode.UntilClosed;
		device.Processes.Remove(spotify);
		var again = CreateApp("Spotify", 25);
		device.Processes.Add(again);
		locks.ReleaseAll();

		Assert.AreEqual(100, again.Volume);
	}

	[TestMethod]
	public void ALockEndsWhenItsProcessIdRunsAnotherExecutable()
	{
		const string spotifyPath = @"C:\Apps\Spotify.exe";
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		CreateLocks();
		var first = CreateApp("Spotify", 40, filePath: spotifyPath);
		device.Processes.Add(first);
		first.Lock.Mode = VolumeLockMode.UntilClosed;

		//the app exited and Windows gave its ID to another program, which is still in the process list
		reused[100] = @"C:\Other\Other.exe";
		device.Processes.Remove(first);
		reused.Remove(100);
		var second = CreateApp("Spotify", 100, filePath: spotifyPath);
		device.Processes.Add(second);

		Assert.IsFalse(second.Lock.IsOn);
	}

	[TestMethod]
	public void TheFeatureIsInTheListOfTheFeaturesCreatedAtStartup()
		=> CollectionAssert.Contains(Volumey.Services.ServiceRegistration.Features, typeof(AppVolumeLockViewModel));
}

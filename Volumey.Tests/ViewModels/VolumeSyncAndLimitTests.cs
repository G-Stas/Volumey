using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class VolumeSyncAndLimitTests : ViewModelTestBase
{
	[TestMethod]
	public void SyncAppliesMasterVolumeChangesToApps()
	{
		var app = CreateProcess("app", 50);
		var device = CreateDevice("1", "Speakers", app);
		SetDefaultDevice(device);
		var sync = new AppVolumeSyncViewModel(DeviceProvider.Object) { IsOn = true };

		device.Master.Volume = 30;

		Assert.AreEqual(30, app.Volume);
	}

	[TestMethod]
	public void SyncGivesNewAppsTheMasterVolume()
	{
		var device = CreateDevice("1", "Speakers", out _, volume: 40);
		SetDefaultDevice(device);
		var sync = new AppVolumeSyncViewModel(DeviceProvider.Object) { IsOn = true };
		var app = CreateProcess("app", 100);

		RaiseProcessCreated(device, app);

		Assert.AreEqual(40, app.Volume);
	}

	[TestMethod]
	public void SyncDoesNothingWhenOff()
	{
		var app = CreateProcess("app", 50);
		var device = CreateDevice("1", "Speakers", app);
		SetDefaultDevice(device);
		var sync = new AppVolumeSyncViewModel(DeviceProvider.Object) { IsOn = true };
		sync.IsOn = false;

		device.Master.Volume = 30;
		RaiseProcessCreated(device, CreateProcess("new", 100));

		Assert.AreEqual(50, app.Volume);
	}

	[TestMethod]
	public void SyncFollowsTheNewDefaultDevice()
	{
		var oldApp = CreateProcess("old", 50);
		var oldDevice = CreateDevice("1", "Speakers", oldApp);
		var newApp = CreateProcess("new", 50);
		var newDevice = CreateDevice("2", "Headphones", out _, 60, newApp);
		SetDefaultDevice(oldDevice);
		var sync = new AppVolumeSyncViewModel(DeviceProvider.Object) { IsOn = true };

		ChangeDefaultDevice(newDevice);
		Assert.AreEqual(60, newApp.Volume, "apps of the new device get its volume");

		newDevice.Master.Volume = 20;
		Assert.AreEqual(20, newApp.Volume, "the new device's master volume is synced");

		oldDevice.Master.Volume = 90;
		Assert.AreEqual(20, newApp.Volume, "the old device's master volume no longer matters");
		Assert.AreEqual(50, oldApp.Volume);
	}

	[TestMethod]
	public void SyncSettingIsSaved()
	{
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var sync = new AppVolumeSyncViewModel(DeviceProvider.Object) { IsOn = true };

		WaitFor(() => Settings.General.AppVolumeSyncEnabled, "the setting wasn't saved");
	}

	[TestMethod]
	public void VolumeLimitLowersNewAppsAtFullVolume()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		var limit = new VolumeLimitViewModel(DeviceProvider.Object) { VolumeLimit = 30, IsOn = true };
		var loud = CreateProcess("loud", 100);
		var quiet = CreateProcess("quiet", 20);

		RaiseProcessCreated(device, loud);
		RaiseProcessCreated(device, quiet);

		Assert.AreEqual(30, loud.Volume);
		Assert.AreEqual(20, quiet.Volume);
		Assert.AreEqual(30, Settings.General.VolumeLimit);
	}

	[TestMethod]
	public void VolumeLimitDoesNothingWhenOff()
	{
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		var limit = new VolumeLimitViewModel(DeviceProvider.Object) { VolumeLimit = 30, IsOn = true };
		limit.IsOn = false;
		var loud = CreateProcess("loud", 100);

		RaiseProcessCreated(device, loud);

		Assert.AreEqual(100, loud.Volume);
	}

	[TestMethod]
	public void VolumeLimitFollowsTheNewDefaultDevice()
	{
		var oldDevice = CreateDevice("1", "Speakers");
		var newDevice = CreateDevice("2", "Headphones");
		SetDefaultDevice(oldDevice);
		var limit = new VolumeLimitViewModel(DeviceProvider.Object) { VolumeLimit = 30, IsOn = true };

		ChangeDefaultDevice(newDevice);
		var onNew = CreateProcess("new", 100);
		var onOld = CreateProcess("old", 100);
		RaiseProcessCreated(newDevice, onNew);
		RaiseProcessCreated(oldDevice, onOld);

		Assert.AreEqual(30, onNew.Volume);
		Assert.AreEqual(100, onOld.Volume);
	}

	[TestMethod]
	public void LimitAndSyncStartFromTheSavedSettings()
	{
		Settings.General.VolumeLimit = 25;
		Settings.General.VolumeLimitEnabled = true;
		Settings.General.AppVolumeSyncEnabled = true;
		SetDefaultDevice(CreateDevice("1", "Speakers"));

		Assert.IsTrue(new VolumeLimitViewModel(DeviceProvider.Object).IsOn);
		Assert.AreEqual(25, new VolumeLimitViewModel(DeviceProvider.Object).VolumeLimit);
		Assert.IsTrue(new AppVolumeSyncViewModel(DeviceProvider.Object).IsOn);
	}
}

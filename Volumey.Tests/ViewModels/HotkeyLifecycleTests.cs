using System;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class HotkeyLifecycleTests : ViewModelTestBase
{
	private static readonly HotKey Up = new HotKey(Key.PageUp, ModifierKeys.Alt);
	private static readonly HotKey Down = new HotKey(Key.PageDown, ModifierKeys.Alt);

	[TestMethod]
	public void ADisposedDeviceIgnoresItsHotkeys()
	{
		ActivateHotkeys();
		var device = CreateDevice("1", "Speakers", out Mock<IAudioSessionVolume> masterVolume);
		Assert.IsTrue(device.SetVolumeHotkeys(Up, Down));

		device.Dispose();
		PressHotkey(Up);

		masterVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		//the view models release the hotkeys after the disposal, as they do on a default device switch
		device.ResetVolumeHotkeys();
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void AnOutsideVolumeChangeShowsTheNewValueAndIsNotWrittenBack()
	{
		Settings.Notifications.ReactToAllVolumeChanges = true;
		var sessionVolume = new Mock<IAudioSessionVolume>();
		var sessionEvents = new Mock<IAudioSessionStateNotifications>();
		var process = CreateProcess("Spotify", 50, withSession: false);
		process.AddSession(new Volumey.Model.AudioSessionModel(false, 50, "1", default, default, default, Guid.Empty, sessionVolume.Object, sessionEvents.Object));
		var shown = new System.Collections.Generic.List<int>();
		var mediator = new Mock<IAudioProcessStateMediator>();
		mediator.Setup(m => m.NotifyAudioStateChange(It.IsAny<Volumey.Model.IManagedMasterAudioSession>()))
				.Callback<Volumey.Model.IManagedMasterAudioSession>(s => shown.Add(s.Volume));
		process.SetStateMediator(mediator.Object);

		//the Windows mixer sets the app to 30
		sessionEvents.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(30, false));

		Assert.AreEqual(30, process.Volume);
		CollectionAssert.AreEqual(new[] { 30 }, shown, "the OSD should show the new volume, once");
		sessionVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		sessionVolume.Verify(m => m.SetMute(It.IsAny<bool>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void ASavedForegroundHotkeyThatFailsAtStartupShowsAnErrorAndTheOtherStillWorks()
	{
		Settings.Hotkeys.ForegroundVolumeUp = Up;
		Settings.Hotkeys.ForegroundVolumeDown = Down;
		//another app took the combination since the last start
		HotkeyManager.Setup(m => m.RegisterHotkey(Down)).Throws(new Win32Exception(1409));
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new ForegroundWindowVolumeViewModel(Hotkeys, Notifications.Object, AppRouting);

		ActivateHotkeys();

		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.Errors.VolumeDown);
		Assert.AreEqual(string.Empty, vm.Errors.VolumeUp);
		//the other hotkey stays registered
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Never);
		//and both stay saved for the next start
		Assert.AreEqual(Up, Settings.Hotkeys.ForegroundVolumeUp);
	}

	[TestMethod]
	public void SavedDeviceHotkeysThatFailAtStartupShowAnError()
	{
		var mute = new HotKey(Key.M, ModifierKeys.Alt);
		Settings.Hotkeys.DeviceVolumeUp = Up;
		Settings.Hotkeys.DeviceVolumeDown = Down;
		Settings.Hotkeys.DeviceMute = mute;
		//another app took the combinations since the last start
		HotkeyManager.Setup(m => m.RegisterHotkey(Down)).Throws(new Win32Exception(1409));
		HotkeyManager.Setup(m => m.RegisterHotkey(mute)).Throws(new Win32Exception(1409));
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		ActivateHotkeys();

		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.VolumeErrors.VolumeDown);
		Assert.AreEqual(string.Empty, vm.VolumeErrors.VolumeUp);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.OpenReg], vm.MuteError);
		//the free one keeps working
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Never);
		//they stay on and saved
		Assert.IsTrue(vm.VolumeHotkeysRegistered);
		Assert.IsTrue(vm.MuteHotkeyRegistered);
		Assert.AreEqual(mute, Settings.Hotkeys.DeviceMute);

		//still taken at the first retry
		Retries.RunPending();
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.VolumeErrors.VolumeDown);

		//the other app let them go: the next retry takes them and the errors go away
		HotkeyManager.Setup(m => m.RegisterHotkey(Down));
		HotkeyManager.Setup(m => m.RegisterHotkey(mute));
		Retries.RunPending();
		Assert.IsFalse(vm.VolumeErrors.Any);
		Assert.AreEqual(string.Empty, vm.MuteError);
		Assert.IsFalse(Retries.HasPending, "nothing left to retry");
		HotkeyManager.Verify(m => m.RegisterHotkey(Down), Times.Exactly(3));
	}

	[TestMethod]
	public void ASavedHotkeyIsRetriedWithGrowingDelaysUpToEveryFiveMinutes()
	{
		Settings.Hotkeys.OpenMixer = Up;
		HotkeyManager.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		var vm = new OpenHotkeyViewModel(Hotkeys);
		ActivateHotkeys();
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.OpenReg], vm.ErrorMessage);
		Assert.IsTrue(vm.IsOn);

		for(int i = 0; i < 12; i++)
			Retries.RunPending();

		var seconds = Retries.Delays.ConvertAll(d => (int)d.TotalSeconds);
		CollectionAssert.AreEqual(new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 300, 300, 300, 300 }, seconds);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.OpenReg], vm.ErrorMessage);

		HotkeyManager.Setup(m => m.RegisterHotkey(Up));
		Retries.RunPending();
		Assert.AreEqual(string.Empty, vm.ErrorMessage);
		int opened = 0;
		Hotkeys.OpenHotkeyPressed += () => opened++;
		PressHotkey(Up);
		Assert.AreEqual(1, opened);
	}

	[TestMethod]
	public void AHotkeyRefusedOnAModeSwitchShowsAnErrorUntilARetryTakesIt()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };
		Assert.IsFalse(vm.VolumeErrors.Any);

		//another app holds Up: the blocking manager refuses it
		var blocking = new Mock<IHotkeyManager>();
		blocking.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		Hotkeys.SetHotkeyManager(blocking.Object);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.VolumeErrors.VolumeUp);
		Assert.AreEqual(string.Empty, vm.VolumeErrors.VolumeDown);
		Assert.IsTrue(Hotkeys.IsRefused(Up));

		blocking.Setup(m => m.RegisterHotkey(Up));
		Retries.RunPending();
		Assert.IsFalse(vm.VolumeErrors.Any);
		Assert.IsFalse(Hotkeys.IsRefused(Up));
	}

	[TestMethod]
	public void TurningOffARefusedHotkeyStopsItsRetries()
	{
		Settings.Hotkeys.DeviceMute = Up;
		HotkeyManager.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		ActivateHotkeys();
		Assert.IsTrue(Retries.HasPending);

		vm.MuteHotkeyRegistered = false;

		Assert.IsFalse(Retries.HasPending);
		Assert.IsFalse(Hotkeys.IsRefused(Up));
		Assert.AreEqual(string.Empty, vm.MuteError);
		//the manager never held it
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Never);
	}

	[TestMethod]
	public void ANewHotkeyThatIsTakenIsRejectedNotRetried()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		HotkeyManager.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { MuteKey = Up };

		vm.MuteHotkeyRegistered = true;

		Assert.IsFalse(vm.MuteHotkeyRegistered);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.OpenReg], vm.MuteError);
		Assert.IsFalse(Retries.HasPending);
		Assert.IsNull(Settings.Hotkeys.DeviceMute);
	}

	[TestMethod]
	public void DeviceHotkeysThatFailOnTheNewDefaultDeviceShowAnErrorUntilTheyWork()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };
		Assert.IsFalse(vm.VolumeErrors.Any);

		//taken by another app while the old device released it
		HotkeyManager.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		ChangeDefaultDevice(CreateDevice("2", "Headphones"));
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.VolumeErrors.VolumeUp);

		//free again: the next default device gets them and the error goes away
		HotkeyManager.Setup(m => m.RegisterHotkey(Up));
		ChangeDefaultDevice(CreateDevice("3", "Monitor"));
		Assert.IsFalse(vm.VolumeErrors.Any);
	}

	[TestMethod]
	public void ASavedAppsHotkeysThatFailWhenItStartsShowAnError()
	{
		Settings.Hotkeys.Apps = new[] { new DataProvider.Settings.AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		var device = CreateDevice("1", "Speakers");
		SetDefaultDevice(device);
		var vm = new AppsHotkeysViewModel(Hotkeys, Notifications.Object, AppRouting);
		ActivateHotkeys();
		var app = vm.Apps[0];

		HotkeyManager.Setup(m => m.RegisterHotkey(Up)).Throws(new Win32Exception(1409));
		var first = CreateProcess("Spotify");
		RaiseProcessCreated(device, first);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], app.Errors.VolumeUp);
		Assert.AreEqual(Up, Settings.Hotkeys.Apps[0].VolumeUp);

		//it starts again once the combination is free: the error goes away
		HotkeyManager.Setup(m => m.RegisterHotkey(Up));
		Exit(device, first);
		var restarted = CreateProcess("Spotify");
		RaiseProcessCreated(device, restarted);
		Assert.IsTrue(restarted.AnyHotkeyRegistered);
		Assert.IsFalse(app.Errors.Any);
	}
}

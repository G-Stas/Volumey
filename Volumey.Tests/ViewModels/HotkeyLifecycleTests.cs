using System;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
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
		var process = CreateProcess("Spotify", 50);
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
		//the first hotkey of the pair isn't left registered
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		//they stay on and saved for the next start
		Assert.IsTrue(vm.VolumeHotkeysRegistered);
		Assert.IsTrue(vm.MuteHotkeyRegistered);
		Assert.AreEqual(mute, Settings.Hotkeys.DeviceMute);
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

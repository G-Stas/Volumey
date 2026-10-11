using System;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class DeviceHotkeysViewModelTests : ViewModelTestBase
{
	private static readonly HotKey Up = new HotKey(Key.Up, ModifierKeys.Control | ModifierKeys.Alt);
	private static readonly HotKey Down = new HotKey(Key.Down, ModifierKeys.Control | ModifierKeys.Alt);
	private static readonly HotKey Mute = new HotKey(Key.M, ModifierKeys.Control | ModifierKeys.Alt);

	private static void VerifyVolumeWritten(Mock<IAudioSessionVolume> masterVolume, int volume, Times times)
		=> masterVolume.Verify(m => m.SetVolume(volume, It.IsAny<Guid>()), times);

	[TestMethod]
	public void VolumeHotkeysAreRegisteredAndSaved()
	{
		ActivateHotkeys();
		var device = CreateDevice("1", "Speakers", out var masterVolume, volume: 50);
		SetDefaultDevice(device);
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down };

		vm.VolumeHotkeysRegistered = true;

		Assert.IsTrue(vm.VolumeHotkeysRegistered);
		Assert.AreEqual(string.Empty, vm.ErrorMessage);
		Assert.AreEqual(Up, Settings.Hotkeys.DeviceVolumeUp);
		Assert.AreEqual(Down, Settings.Hotkeys.DeviceVolumeDown);
		HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Down), Times.Once);

		PressHotkey(Up);
		VerifyVolumeWritten(masterVolume, 50 + Hotkeys.VolumeStep, Times.Once());
	}

	[TestMethod]
	public void SameHotkeyForUpAndDownIsRejected()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Up };

		vm.VolumeHotkeysRegistered = true;

		Assert.IsFalse(vm.VolumeHotkeysRegistered);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.Diff], vm.ErrorMessage);
		Assert.IsNull(Settings.Hotkeys.DeviceVolumeUp);
		HotkeyManager.Verify(m => m.RegisterHotkey(It.IsAny<HotKey>()), Times.Never);
	}

	[TestMethod]
	public void HotkeyUsedElsewhereIsRejected()
	{
		ActivateHotkeys();
		Settings.Hotkeys.OpenMixer = Up;
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down };

		vm.VolumeHotkeysRegistered = true;

		Assert.IsFalse(vm.VolumeHotkeysRegistered);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.HotkeyExists], vm.ErrorMessage);
	}

	[TestMethod]
	public void TurningVolumeHotkeysOffUnregistersAndForgetsThem()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };

		vm.VolumeHotkeysRegistered = false;

		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Down), Times.Once);
		Assert.IsNull(Settings.Hotkeys.DeviceVolumeUp);
		Assert.IsNull(Settings.Hotkeys.DeviceVolumeDown);
	}

	[TestMethod]
	public void MuteHotkeyIsRegisteredAndSaved()
	{
		ActivateHotkeys();
		var device = CreateDevice("1", "Speakers", out var masterVolume);
		SetDefaultDevice(device);
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { MuteKey = Mute };

		vm.MuteHotkeyRegistered = true;

		Assert.IsTrue(vm.MuteHotkeyRegistered);
		Assert.AreEqual(Mute, Settings.Hotkeys.DeviceMute);
		PressHotkey(Mute);
		masterVolume.Verify(m => m.SetMute(true, It.IsAny<Guid>()), Times.Once);

		vm.MuteHotkeyRegistered = false;
		Assert.IsNull(Settings.Hotkeys.DeviceMute);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Mute), Times.Once);
	}

	[TestMethod]
	public void TheMuteHotkeyMutesAndUnmutes()
	{
		//it used to mute only: the model waited for Windows' notification, which was skipped as Volumey's own change (owner found it)
		ActivateHotkeys();
		var device = CreateDevice("1", "Speakers", out var masterVolume);
		SetDefaultDevice(device);
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { MuteKey = Mute, MuteHotkeyRegistered = true };

		PressHotkey(Mute);
		Assert.IsTrue(device.Master.IsMuted);
		PressHotkey(Mute);
		Assert.IsFalse(device.Master.IsMuted);

		//hotkey changes carry the empty context, so Windows' notification comes back (the mixer and the OSD follow it)
		masterVolume.Verify(m => m.SetMute(true, GuidValue.Internal.Empty), Times.Once);
		masterVolume.Verify(m => m.SetMute(false, GuidValue.Internal.Empty), Times.Once);
	}

	[TestMethod]
	public void MutingInsteadOfZeroUnmutesOnVolumeUp()
	{
		ActivateHotkeys();
		Settings.Hotkeys.PreventResettingVolumeBalance = true;
		var device = CreateDevice("1", "Speakers", out var masterVolume, volume: Hotkeys.VolumeStep);
		SetDefaultDevice(device);
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };

		PressHotkey(Down);
		Assert.IsTrue(device.Master.IsMuted);
		masterVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);

		PressHotkey(Up);
		Assert.IsFalse(device.Master.IsMuted);
		masterVolume.Verify(m => m.SetMute(false, GuidValue.Internal.Empty), Times.Once);
	}

	[TestMethod]
	public void SavedHotkeysAreRegisteredWhenHotkeysActivate()
	{
		Settings.Hotkeys.DeviceVolumeUp = Up;
		Settings.Hotkeys.DeviceVolumeDown = Down;
		Settings.Hotkeys.DeviceMute = Mute;
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		Assert.IsTrue(vm.VolumeHotkeysRegistered);
		Assert.IsTrue(vm.MuteHotkeyRegistered);

		ActivateHotkeys();

		HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Down), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Mute), Times.Once);
	}

	[TestMethod]
	public void HotkeysMoveToTheNewDefaultDevice()
	{
		ActivateHotkeys();
		var oldDevice = CreateDevice("1", "Speakers", out var oldVolume, volume: 50);
		var newDevice = CreateDevice("2", "Headphones", out var newVolume, volume: 30);
		SetDefaultDevice(oldDevice);
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };

		ChangeDefaultDevice(newDevice);
		PressHotkey(Down);

		VerifyVolumeWritten(newVolume, 30 - Hotkeys.VolumeStep, Times.Once());
		oldVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void TogglingNotificationsWithoutADeviceDoesNotThrow()
	{
		ActivateHotkeys();
		var vm = new DeviceVolumeHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		Settings.Notifications.Enabled = !Settings.Notifications.Enabled;
		Settings.Notifications.Enabled = !Settings.Notifications.Enabled;
	}

	[TestMethod]
	public void OpenMixerHotkeyIsRegisteredSavedAndRaised()
	{
		ActivateHotkeys();
		var vm = new OpenHotkeyViewModel(Hotkeys) { Hotkey = Mute };
		int opened = 0;
		Hotkeys.OpenHotkeyPressed += () => opened++;

		vm.IsOn = true;
		PressHotkey(Mute);

		Assert.IsTrue(vm.IsOn);
		Assert.AreEqual(Mute, Settings.Hotkeys.OpenMixer);
		Assert.AreEqual(1, opened);

		vm.IsOn = false;
		Assert.IsFalse(vm.IsOn);
		Assert.IsNull(Settings.Hotkeys.OpenMixer);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Mute), Times.Once);
	}

	[TestMethod]
	public void BareF12IsRejectedForTheOpenMixerHotkey()
	{
		ActivateHotkeys();
		var vm = new OpenHotkeyViewModel(Hotkeys) { Hotkey = new HotKey(Key.F12) };

		vm.IsOn = true;

		Assert.IsFalse(vm.IsOn);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.F12], vm.ErrorMessage);
	}

	[TestMethod]
	public void SavedOpenMixerHotkeyIsRegisteredWhenHotkeysActivate()
	{
		Settings.Hotkeys.OpenMixer = Mute;
		var vm = new OpenHotkeyViewModel(Hotkeys);
		Assert.IsTrue(vm.IsOn);

		ActivateHotkeys();

		HotkeyManager.Verify(m => m.RegisterHotkey(Mute), Times.Once);
	}
}

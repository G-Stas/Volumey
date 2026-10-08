using System;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels
{
	[TestClass]
	public class DeviceHotkeysViewModelTests : ViewModelTestBase
	{
		private static readonly HotKey Up = new HotKey(Key.Up, ModifierKeys.Control | ModifierKeys.Alt);
		private static readonly HotKey Down = new HotKey(Key.Down, ModifierKeys.Control | ModifierKeys.Alt);
		private static readonly HotKey Mute = new HotKey(Key.M, ModifierKeys.Control | ModifierKeys.Alt);

		private static void VerifyVolumeWritten(Mock<IAudioSessionVolume> masterVolume, int volume, Times times)
			=> masterVolume.Verify(m => m.SetVolume(volume, ref It.Ref<Guid>.IsAny), times);

		[TestMethod]
		public void VolumeHotkeysAreRegisteredAndSaved()
		{
			ActivateHotkeys();
			var device = CreateDevice("1", "Speakers", out var masterVolume, volume: 50);
			SetDefaultDevice(device);
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { VolumeUp = Up, VolumeDown = Down };

			vm.VolumeHotkeysRegistered = true;

			Assert.IsTrue(vm.VolumeHotkeysRegistered);
			Assert.AreEqual(string.Empty, vm.ErrorMessage);
			Assert.AreEqual(Up, this.Settings.Hotkeys.DeviceVolumeUp);
			Assert.AreEqual(Down, this.Settings.Hotkeys.DeviceVolumeDown);
			this.HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
			this.HotkeyManager.Verify(m => m.RegisterHotkey(Down), Times.Once);

			PressHotkey(Up);
			VerifyVolumeWritten(masterVolume, 50 + this.Hotkeys.VolumeStep, Times.Once());
		}

		[TestMethod]
		public void SameHotkeyForUpAndDownIsRejected()
		{
			ActivateHotkeys();
			SetDefaultDevice(CreateDevice("1", "Speakers"));
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { VolumeUp = Up, VolumeDown = Up };

			vm.VolumeHotkeysRegistered = true;

			Assert.IsFalse(vm.VolumeHotkeysRegistered);
			Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.Diff], vm.ErrorMessage);
			Assert.IsNull(this.Settings.Hotkeys.DeviceVolumeUp);
			this.HotkeyManager.Verify(m => m.RegisterHotkey(It.IsAny<HotKey>()), Times.Never);
		}

		[TestMethod]
		public void HotkeyUsedElsewhereIsRejected()
		{
			ActivateHotkeys();
			this.Settings.Hotkeys.OpenMixer = Up;
			SetDefaultDevice(CreateDevice("1", "Speakers"));
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { VolumeUp = Up, VolumeDown = Down };

			vm.VolumeHotkeysRegistered = true;

			Assert.IsFalse(vm.VolumeHotkeysRegistered);
			Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.HotkeyExists], vm.ErrorMessage);
		}

		[TestMethod]
		public void TurningVolumeHotkeysOffUnregistersAndForgetsThem()
		{
			ActivateHotkeys();
			SetDefaultDevice(CreateDevice("1", "Speakers"));
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };

			vm.VolumeHotkeysRegistered = false;

			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Down), Times.Once);
			Assert.IsNull(this.Settings.Hotkeys.DeviceVolumeUp);
			Assert.IsNull(this.Settings.Hotkeys.DeviceVolumeDown);
		}

		[TestMethod]
		public void MuteHotkeyIsRegisteredAndSaved()
		{
			ActivateHotkeys();
			var device = CreateDevice("1", "Speakers", out var masterVolume);
			SetDefaultDevice(device);
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { MuteKey = Mute };

			vm.MuteHotkeyRegistered = true;

			Assert.IsTrue(vm.MuteHotkeyRegistered);
			Assert.AreEqual(Mute, this.Settings.Hotkeys.DeviceMute);
			PressHotkey(Mute);
			masterVolume.Verify(m => m.SetMute(true), Times.Once);

			vm.MuteHotkeyRegistered = false;
			Assert.IsNull(this.Settings.Hotkeys.DeviceMute);
			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Mute), Times.Once);
		}

		[TestMethod]
		public void SavedHotkeysAreRegisteredWhenHotkeysActivate()
		{
			this.Settings.Hotkeys.DeviceVolumeUp = Up;
			this.Settings.Hotkeys.DeviceVolumeDown = Down;
			this.Settings.Hotkeys.DeviceMute = Mute;
			SetDefaultDevice(CreateDevice("1", "Speakers"));
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object);
			Assert.IsTrue(vm.VolumeHotkeysRegistered);
			Assert.IsTrue(vm.MuteHotkeyRegistered);

			ActivateHotkeys();

			this.HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
			this.HotkeyManager.Verify(m => m.RegisterHotkey(Down), Times.Once);
			this.HotkeyManager.Verify(m => m.RegisterHotkey(Mute), Times.Once);
		}

		[TestMethod]
		public void HotkeysMoveToTheNewDefaultDevice()
		{
			ActivateHotkeys();
			var oldDevice = CreateDevice("1", "Speakers", out var oldVolume, volume: 50);
			var newDevice = CreateDevice("2", "Headphones", out var newVolume, volume: 30);
			SetDefaultDevice(oldDevice);
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object) { VolumeUp = Up, VolumeDown = Down, VolumeHotkeysRegistered = true };

			ChangeDefaultDevice(newDevice);
			PressHotkey(Down);

			VerifyVolumeWritten(newVolume, 30 - this.Hotkeys.VolumeStep, Times.Once());
			oldVolume.Verify(m => m.SetVolume(It.IsAny<int>(), ref It.Ref<Guid>.IsAny), Times.Never);
		}

		[TestMethod]
		public void TogglingNotificationsWithoutADeviceDoesNotThrow()
		{
			ActivateHotkeys();
			var vm = new DeviceVolumeHotkeysViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object);

			this.Settings.Notifications.Enabled = !this.Settings.Notifications.Enabled;
			this.Settings.Notifications.Enabled = !this.Settings.Notifications.Enabled;
		}

		[TestMethod]
		public void OpenMixerHotkeyIsRegisteredSavedAndRaised()
		{
			ActivateHotkeys();
			var vm = new OpenHotkeyViewModel(this.Hotkeys) { Hotkey = Mute };
			int opened = 0;
			this.Hotkeys.OpenHotkeyPressed += () => opened++;

			vm.IsOn = true;
			PressHotkey(Mute);

			Assert.IsTrue(vm.IsOn);
			Assert.AreEqual(Mute, this.Settings.Hotkeys.OpenMixer);
			Assert.AreEqual(1, opened);

			vm.IsOn = false;
			Assert.IsFalse(vm.IsOn);
			Assert.IsNull(this.Settings.Hotkeys.OpenMixer);
			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Mute), Times.Once);
		}

		[TestMethod]
		public void BareF12IsRejectedForTheOpenMixerHotkey()
		{
			ActivateHotkeys();
			var vm = new OpenHotkeyViewModel(this.Hotkeys) { Hotkey = new HotKey(Key.F12) };

			vm.IsOn = true;

			Assert.IsFalse(vm.IsOn);
			Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.F12], vm.ErrorMessage);
		}

		[TestMethod]
		public void SavedOpenMixerHotkeyIsRegisteredWhenHotkeysActivate()
		{
			this.Settings.Hotkeys.OpenMixer = Mute;
			var vm = new OpenHotkeyViewModel(this.Hotkeys);
			Assert.IsTrue(vm.IsOn);

			ActivateHotkeys();

			this.HotkeyManager.Verify(m => m.RegisterHotkey(Mute), Times.Once);
		}
	}
}

using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class AppAndDefaultDeviceHotkeysTests : ViewModelTestBase
{
	private static readonly HotKey Up = new HotKey(Key.PageUp, ModifierKeys.Control);
	private static readonly HotKey Down = new HotKey(Key.PageDown, ModifierKeys.Control);
	private static readonly HotKey Switch = new HotKey(Key.D1, ModifierKeys.Control | ModifierKeys.Shift);

	[TestMethod]
	public void AddingAnAppRegistersAndSavesItsHotkeys()
	{
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify", 50);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = spotify, VolumeUp = Up, VolumeDown = Down };

		vm.AddAppCommand.Execute(null);

		Assert.IsTrue(spotify.AnyHotkeyRegistered);
		Assert.IsTrue(vm.RegisteredProcesses.ContainsKey("Spotify"));
		var saved = Assert.ContainsSingle(Settings.Hotkeys.Apps);
		Assert.AreEqual("Spotify", saved.ProcessName);
		Assert.AreEqual(Up, saved.VolumeUp);
		Assert.IsNull(vm.SelectedProcess);
		Assert.IsNull(vm.VolumeUp);

		PressHotkey(Up);
		Assert.AreEqual(50 + Hotkeys.VolumeStep, spotify.Volume);
	}

	[TestMethod]
	public void InvalidAppHotkeysAreRejected()
	{
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = spotify, VolumeUp = Up, VolumeDown = null };

		vm.AddAppCommand.Execute(null);

		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.ErrorMessage);
		Assert.IsFalse(spotify.AnyHotkeyRegistered);
		Assert.IsEmpty(Settings.Hotkeys.Apps);
	}

	[TestMethod]
	public void RemovingAnAppUnregistersAndForgetsIt()
	{
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = spotify, VolumeUp = Up, VolumeDown = Down };
		vm.AddAppCommand.Execute(null);

		vm.SelectedRegApp = vm.RegisteredProcesses.Single();
		vm.RemoveAppCommand.Execute(null);

		Assert.IsFalse(spotify.AnyHotkeyRegistered);
		Assert.IsEmpty(vm.RegisteredProcesses);
		Assert.IsEmpty(Settings.Hotkeys.Apps);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void SavedAppHotkeysApplyToRunningAndLaterStartedApps()
	{
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		var running = CreateProcess("Spotify");
		var other = CreateProcess("Chrome");
		var device = CreateDevice("1", "Speakers", running, other);
		SetDefaultDevice(device);
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		ActivateHotkeys();
		Assert.IsTrue(running.AnyHotkeyRegistered);
		Assert.IsFalse(other.AnyHotkeyRegistered);

		var restarted = CreateProcess("Spotify");
		RaiseProcessCreated(device, restarted);
		Assert.IsTrue(restarted.AnyHotkeyRegistered);
	}

	[TestMethod]
	public void AppHotkeysMoveToTheNewDefaultDevice()
	{
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		var onOld = CreateProcess("Spotify");
		var onNew = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", onOld));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		Assert.IsTrue(onOld.AnyHotkeyRegistered);

		ChangeDefaultDevice(CreateDevice("2", "Headphones", onNew));

		Assert.IsFalse(onOld.AnyHotkeyRegistered);
		Assert.IsTrue(onNew.AnyHotkeyRegistered);
	}

	[TestMethod]
	public void VolumeStepIsSavedAndUsedByHotkeys()
	{
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		vm.VolumeStep = 5;

		Assert.AreEqual(5, Settings.General.VolumeStep);
		Assert.AreEqual(5, Hotkeys.VolumeStep);
	}

	[TestMethod]
	public void DefaultDeviceHotkeySwitchesTheDevice()
	{
		ActivateHotkeys();
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		ActiveDevices.Add(headphones);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys) { SelectedDevice = headphones, HotKey = Switch };

		vm.AddDeviceCommand.Execute(null);
		PressHotkey(Switch);

		DeviceProvider.Verify(m => m.SetDefaultDevice("2"), Times.Once);
		Assert.IsTrue(vm.RegisteredDevices.ContainsKey(Switch));
		WaitFor(() => Settings.Hotkeys.DefaultDevices.Count == 1, "the device hotkey wasn't saved");
		var saved = Settings.Hotkeys.DefaultDevices.Single();
		Assert.AreEqual("2", saved.DeviceId);
		Assert.AreEqual("Headphones", saved.DeviceName);
	}

	[TestMethod]
	public void TheSameDeviceOrHotkeyCannotBeAddedTwice()
	{
		ActivateHotkeys();
		var speakers = CreateDevice("1", "Speakers");
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(speakers);
		ActiveDevices.Add(headphones);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys) { SelectedDevice = headphones, HotKey = Switch };
		vm.AddDeviceCommand.Execute(null);

		vm.SelectedDevice = speakers;
		vm.HotKey = Switch;
		vm.AddDeviceCommand.Execute(null);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.HotkeyExists], vm.ErrorMessage);

		vm.SelectedDevice = headphones;
		vm.HotKey = Up;
		vm.AddDeviceCommand.Execute(null);
		Assert.HasCount(1, vm.RegisteredDevices);
	}

	[TestMethod]
	public void RemovingADeviceHotkeyUnregistersIt()
	{
		ActivateHotkeys();
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(headphones);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys) { SelectedDevice = headphones, HotKey = Switch };
		vm.AddDeviceCommand.Execute(null);

		vm.SelectedRegDev = vm.RegisteredDevices.Single();
		vm.RemoveDeviceCommand.Execute(null);
		PressHotkey(Switch);

		Assert.IsEmpty(vm.RegisteredDevices);
		Assert.IsEmpty(Settings.Hotkeys.DefaultDevices);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Switch), Times.Once);
		DeviceProvider.Verify(m => m.SetDefaultDevice(It.IsAny<string>()), Times.Never);
	}

	[TestMethod]
	public void SavedDeviceHotkeysFollowRenamedOrReplugedDevices()
	{
		Settings.Hotkeys.DefaultDevices = new[]
		{
			//same ID, the device was renamed
			new DeviceHotkeyEntry { Hotkey = Switch, DeviceId = "1", DeviceName = "Old name" },
			//same name, the device got a new ID
			new DeviceHotkeyEntry { Hotkey = Up, DeviceId = "old-id", DeviceName = "Headphones" }
		};
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		ActiveDevices.Add(CreateDevice("2", "Headphones"));

		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);

		Assert.AreEqual("Speakers", vm.RegisteredDevices[Switch].Name);
		Assert.AreEqual("2", vm.RegisteredDevices[Up].ID);
		var saved = Settings.Hotkeys.DefaultDevices.ToDictionary(d => d.Hotkey);
		Assert.AreEqual("Speakers", saved[Switch].DeviceName);
		Assert.AreEqual("2", saved[Up].DeviceId);

		ActivateHotkeys();
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void SavedDeviceHotkeysAreShownAfterARestart()
	{
		//#18: after a restart the page looked as if no hotkeys were registered
		Settings.Hotkeys.DefaultDevices = new[] { new DeviceHotkeyEntry { Hotkey = Switch, DeviceId = "2", DeviceName = "Headphones" } };
		var speakers = CreateDevice("1", "Speakers");
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(speakers);
		ActiveDevices.Add(headphones);

		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);
		Assert.AreEqual(Switch, vm.SelectedRegDev?.Key);

		//selecting a registered device shows its hotkey
		vm.SelectedDevice = headphones;
		Assert.AreEqual(Switch, vm.HotKey);
		Assert.AreEqual("2", vm.SelectedRegDev?.Value.ID);

		//another device doesn't keep that hotkey
		vm.SelectedDevice = speakers;
		Assert.IsNull(vm.HotKey);

		//but a hotkey the user typed stays
		vm.HotKey = Up;
		vm.SelectedDevice = null;
		vm.SelectedDevice = speakers;
		Assert.AreEqual(Up, vm.HotKey);
	}

	[TestMethod]
	public void AddedDeviceHotkeyIsSelectedInTheList()
	{
		ActivateHotkeys();
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(headphones);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys) { SelectedDevice = headphones, HotKey = Switch };

		vm.AddDeviceCommand.Execute(null);

		Assert.AreEqual(Switch, vm.SelectedRegDev?.Key);
	}

	[TestMethod]
	public void SystemHotkeyMovesBetweenTheListsAndIsSaved()
	{
		ActivateHotkeys();
		var vm = new SystemHotkeysViewModel(Hotkeys);
		Assert.HasCount(4, vm.Hotkeys);
		var playPause = vm.Hotkeys.Single(h => h.SystemKey == Key.MediaPlayPause);

		vm.SelectedHotkey = playPause;
		vm.ReplacementHotKey = Switch;
		vm.AddHotkeyCommand.Execute(null);

		Assert.HasCount(3, vm.Hotkeys);
		Assert.AreSame(playPause, Assert.ContainsSingle(vm.RegisteredHotkeys));
		var saved = Assert.ContainsSingle(Settings.Hotkeys.System);
		Assert.AreEqual(Key.MediaPlayPause, saved.SystemKey);
		Assert.AreEqual(Switch, saved.Replacement);
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);

		vm.SelectedRegisteredHotkey = playPause;
		vm.RemoveHotkeyCommand.Execute(null);

		Assert.HasCount(4, vm.Hotkeys);
		Assert.IsEmpty(vm.RegisteredHotkeys);
		Assert.IsEmpty(Settings.Hotkeys.System);
		Assert.IsNull(playPause.Replacement);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Switch), Times.Once);
	}

	[TestMethod]
	public void SavedSystemHotkeysAreRegisteredWhenHotkeysActivate()
	{
		Settings.Hotkeys.System = new[] { new SystemHotkeyEntry { SystemKey = Key.MediaNextTrack, Replacement = Switch } };

		var vm = new SystemHotkeysViewModel(Hotkeys);
		ActivateHotkeys();

		Assert.HasCount(3, vm.Hotkeys);
		Assert.AreEqual(Key.MediaNextTrack, Assert.ContainsSingle(vm.RegisteredHotkeys).SystemKey);
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);
	}

	[TestMethod]
	public void ForegroundHotkeysAreRegisteredSavedAndReset()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new ForegroundWindowVolumeViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUpHotkey = Up, VolumeDownHotkey = Down };

		vm.HotkeysRegistered = true;
		Assert.IsTrue(vm.HotkeysRegistered);
		Assert.AreEqual(Up, Settings.Hotkeys.ForegroundVolumeUp);
		Assert.AreEqual(Down, Settings.Hotkeys.ForegroundVolumeDown);

		vm.HotkeysRegistered = false;
		Assert.IsNull(Settings.Hotkeys.ForegroundVolumeUp);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Down), Times.Once);
	}

	[TestMethod]
	public void ForegroundHotkeysMustDiffer()
	{
		ActivateHotkeys();
		var vm = new ForegroundWindowVolumeViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { VolumeUpHotkey = Up, VolumeDownHotkey = Up };

		vm.HotkeysRegistered = true;

		Assert.IsFalse(vm.HotkeysRegistered);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.Diff], vm.ErrorMessage);
	}
}

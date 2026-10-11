using System;
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
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

		Assert.IsTrue(vm.TryAddApp());

		Assert.IsTrue(spotify.AnyHotkeyRegistered);
		var app = Assert.ContainsSingle(vm.Apps);
		Assert.AreEqual("Spotify", app.ProcessName);
		Assert.AreEqual(Up, app.VolumeUp);
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

		Assert.IsFalse(vm.TryAddApp());

		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.Missing], vm.AddErrors.VolumeDown);
		Assert.AreEqual(string.Empty, vm.AddErrors.VolumeUp);
		Assert.IsFalse(spotify.AnyHotkeyRegistered);
		Assert.IsEmpty(vm.Apps);
		Assert.IsEmpty(Settings.Hotkeys.Apps);
	}

	[TestMethod]
	public void TheAddDialogShowsAnErrorUnderTheHotkeyThatHasIt()
	{
		var taken = new HotKey(Key.M, ModifierKeys.Control);
		var refused = new HotKey(Key.R, ModifierKeys.Control);
		Settings.Hotkeys.DeviceMute = taken;
		ActivateHotkeys();
		HotkeyManager.Setup(m => m.RegisterHotkey(refused)).Throws(new System.InvalidOperationException("taken by another app"));
		var spotify = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = spotify, VolumeUp = Up, VolumeDown = taken };

		Assert.IsFalse(vm.TryAddApp());
		Assert.AreEqual(string.Empty, vm.AddErrors.VolumeUp);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.HotkeyExists], vm.AddErrors.VolumeDown);

		//a new hotkey in that box clears its error; one the system refuses shows there too
		vm.VolumeDown = refused;
		Assert.IsFalse(vm.AddErrors.Any);
		Assert.IsFalse(vm.TryAddApp());
		Assert.AreEqual(string.Empty, vm.AddErrors.VolumeUp);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.AddErrors.VolumeDown);
		Assert.IsFalse(spotify.AnyHotkeyRegistered);
		Assert.IsEmpty(vm.Apps);
	}

	[TestMethod]
	public void RemovingAnAppUnregistersAndForgetsIt()
	{
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = spotify, VolumeUp = Up, VolumeDown = Down };
		vm.TryAddApp();

		vm.Apps.Single().RemoveCommand.Execute(null);

		Assert.IsFalse(spotify.AnyHotkeyRegistered);
		Assert.IsEmpty(vm.Apps);
		Assert.IsEmpty(Settings.Hotkeys.Apps);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void AddDialogOffersOnlyAppsWithoutHotkeys()
	{
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		var chrome = CreateProcess("Chrome");
		SetDefaultDevice(CreateDevice("1", "Speakers", CreateProcess("Spotify"), chrome, CreateProcess("Chrome")));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object) { SelectedProcess = chrome, VolumeUp = Up };
		vm.TryAddApp(); //fails without the second hotkey, which sets an error

		vm.PrepareAddApp();

		Assert.AreEqual(chrome, Assert.ContainsSingle(vm.AddableProcesses));
		Assert.IsNull(vm.VolumeUp);
		Assert.IsFalse(vm.AddErrors.Any);
	}

	[TestMethod]
	public void ChangingARunningAppsHotkeyReregistersAndSavesIt()
	{
		var newUp = new HotKey(Key.Up, ModifierKeys.Control | ModifierKeys.Shift);
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify", 50);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		var app = vm.Apps.Single();

		app.VolumeUp = newUp;

		Assert.AreEqual(newUp, app.VolumeUp);
		Assert.IsFalse(app.Errors.Any);
		Assert.AreEqual(newUp, Settings.Hotkeys.Apps.Single().VolumeUp);
		Assert.AreEqual(Down, Settings.Hotkeys.Apps.Single().VolumeDown);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		PressHotkey(Up);
		Assert.AreEqual(50, spotify.Volume);
		PressHotkey(newUp);
		Assert.AreEqual(50 + Hotkeys.VolumeStep, spotify.Volume);
	}

	[TestMethod]
	public void ChangingAStoppedAppsHotkeyOnlySavesIt()
	{
		var newDown = new HotKey(Key.Down, ModifierKeys.Control | ModifierKeys.Shift);
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		vm.Apps.Single().VolumeDown = newDown;

		Assert.AreEqual(newDown, Settings.Hotkeys.Apps.Single().VolumeDown);
		HotkeyManager.Verify(m => m.RegisterHotkey(It.IsAny<HotKey>()), Times.Never);
	}

	[TestMethod]
	public void InvalidHotkeyChangesKeepTheOldHotkeys()
	{
		var taken = new HotKey(Key.M, ModifierKeys.Control);
		Settings.Hotkeys.DeviceMute = taken;
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		var spotify = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		var app = vm.Apps.Single();
		var changes = 0;
		app.PropertyChanged += (_, e) => { if(e.PropertyName == nameof(AppHotkeysItem.VolumeUp)) changes++; };

		foreach(var (hotkey, error) in new[]
				{
					(null, ErrorMessageType.NeedsBoth),
					(Down, ErrorMessageType.Diff),
					(taken, ErrorMessageType.HotkeyExists),
					(new HotKey(Key.F12), ErrorMessageType.F12)
				})
		{
			app.VolumeUp = hotkey;
			//the box shows Up again, so the message names the rejected hotkey
			string expected = hotkey == null ? ErrorDictionary.Instance[error] : $"{hotkey}: {ErrorDictionary.Instance[error]}";
			Assert.AreEqual(expected, app.Errors.VolumeUp);
			Assert.AreEqual(string.Empty, app.Errors.VolumeDown);
			Assert.AreEqual(Up, app.VolumeUp);
		}

		//the hotkey box is told the old hotkey again after each rejected change
		Assert.AreEqual(4, changes);
		Assert.AreEqual(Up, Settings.Hotkeys.Apps.Single().VolumeUp);
		Assert.IsTrue(spotify.AnyHotkeyRegistered);
		HotkeyManager.Verify(m => m.UnregisterHotkey(It.IsAny<HotKey>()), Times.Never);
	}

	[TestMethod]
	public void AHotkeyChangeTheSystemRefusesKeepsTheOldHotkeys()
	{
		var refused = new HotKey(Key.Up, ModifierKeys.Control | ModifierKeys.Shift);
		Settings.Hotkeys.Apps = new[] { new AppHotkeysEntry { ProcessName = "Spotify", VolumeUp = Up, VolumeDown = Down } };
		ActivateHotkeys();
		HotkeyManager.Setup(m => m.RegisterHotkey(refused)).Throws(new System.InvalidOperationException("taken by another app"));
		var spotify = CreateProcess("Spotify", 50);
		SetDefaultDevice(CreateDevice("1", "Speakers", spotify));
		var vm = new AppsHotkeysViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);
		var app = vm.Apps.Single();

		app.VolumeUp = refused;

		Assert.AreEqual($"{refused}: {ErrorDictionary.Instance[ErrorMessageType.VolumeReg]}", app.Errors.VolumeUp);
		Assert.AreEqual(Up, app.VolumeUp);
		Assert.AreEqual(Up, Settings.Hotkeys.Apps.Single().VolumeUp);
		PressHotkey(Up);
		Assert.AreEqual(50 + Hotkeys.VolumeStep, spotify.Volume);
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
	public void TurningOnADevicesHotkeyRegistersAndSavesIt()
	{
		ActivateHotkeys();
		var headphones = CreateDevice("2", "Headphones");
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		ActiveDevices.Add(headphones);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);
		var entry = vm.Devices.Single(d => d.Model == headphones);

		entry.Hotkey = Switch;
		entry.HotkeyRegistered = true;
		PressHotkey(Switch);

		Assert.IsTrue(entry.HotkeyRegistered);
		DeviceProvider.Verify(m => m.SetDefaultDevice("2"), Times.Once);
		var saved = Settings.Hotkeys.DefaultDevices.Single();
		Assert.AreEqual("2", saved.DeviceId);
		Assert.AreEqual("Headphones", saved.DeviceName);
		Assert.AreEqual(Switch, saved.Hotkey);
	}

	[TestMethod]
	public void AHotkeyCannotBeUsedForTwoDevices()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		ActiveDevices.Add(CreateDevice("2", "Headphones"));
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);
		var (speakers, headphones) = (vm.Devices[0], vm.Devices[1]);
		headphones.Hotkey = Switch;
		headphones.HotkeyRegistered = true;

		speakers.Hotkey = Switch;
		speakers.HotkeyRegistered = true;

		Assert.IsFalse(speakers.HotkeyRegistered);
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.HotkeyExists], speakers.ErrorMessage);
		//a new hotkey clears the error
		speakers.Hotkey = Up;
		Assert.AreEqual(string.Empty, speakers.ErrorMessage);
		Assert.HasCount(1, Settings.Hotkeys.DefaultDevices);
	}

	[TestMethod]
	public void TurningOffADevicesHotkeyUnregistersIt()
	{
		ActivateHotkeys();
		SetDefaultDevice(CreateDevice("2", "Headphones"));
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);
		var entry = vm.Devices.Single();
		entry.Hotkey = Switch;
		entry.HotkeyRegistered = true;

		entry.HotkeyRegistered = false;
		PressHotkey(Switch);

		Assert.IsEmpty(Settings.Hotkeys.DefaultDevices);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Switch), Times.Once);
		DeviceProvider.Verify(m => m.SetDefaultDevice(It.IsAny<string>()), Times.Never);
		//the hotkey stays in the box to turn it on again
		Assert.AreEqual(Switch, entry.Hotkey);
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

		Assert.HasCount(2, vm.Devices);
		var speakers = vm.Devices.Single(d => d.Hotkey == Switch);
		Assert.AreEqual("Speakers", speakers.Name);
		Assert.IsTrue(speakers.IsConnected && speakers.HotkeyRegistered);
		Assert.AreEqual("2", vm.Devices.Single(d => d.Hotkey == Up).Id);
		var saved = Settings.Hotkeys.DefaultDevices.ToDictionary(d => d.Hotkey);
		Assert.AreEqual("Speakers", saved[Switch].DeviceName);
		Assert.AreEqual("2", saved[Up].DeviceId);

		ActivateHotkeys();
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void SavedHotkeysOfDisconnectedDevicesAreListed()
	{
		//#18: after a restart the page looked as if no hotkeys were registered; an unplugged device's hotkey is shown too
		Settings.Hotkeys.DefaultDevices = new[] { new DeviceHotkeyEntry { Hotkey = Switch, DeviceId = "3", DeviceName = "Monitor" } };
		SetDefaultDevice(CreateDevice("1", "Speakers"));

		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);

		var monitor = vm.Devices.Last();
		Assert.AreEqual("Monitor", monitor.Name);
		Assert.IsFalse(monitor.IsConnected);
		Assert.IsTrue(monitor.HotkeyRegistered);
		//the default device is selected first
		Assert.AreEqual("Speakers", vm.SelectedDevice.Name);

		//once its hotkey is off, nothing is left to show for it
		vm.SelectedDevice = monitor;
		monitor.HotkeyRegistered = false;
		Assert.HasCount(1, vm.Devices);
		Assert.AreEqual("Speakers", vm.SelectedDevice.Name);
		Assert.IsEmpty(Settings.Hotkeys.DefaultDevices);
	}

	[TestMethod]
	public void TheListFollowsDevicesComingAndGoing()
	{
		ActivateHotkeys();
		var speakers = CreateDevice("1", "Speakers");
		SetDefaultDevice(speakers);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);

		var headphones = CreateDevice("2", "Headphones");
		ActiveDevices.Add(headphones);
		var entry = vm.Devices.Single(d => d.Name == "Headphones");
		entry.Hotkey = Switch;
		entry.HotkeyRegistered = true;

		//unplugged: kept (it has a hotkey), after the connected ones
		var monitor = CreateDevice("3", "Monitor");
		ActiveDevices.Add(monitor);
		ActiveDevices.Remove(headphones);
		Assert.IsFalse(entry.IsConnected);
		Assert.AreSame(entry, vm.Devices.Last());

		//plugged in again: the same entry, connected again
		var again = CreateDevice("2", "Headphones");
		ActiveDevices.Add(again);
		Assert.AreSame(again, entry.Model);
		Assert.HasCount(3, vm.Devices);

		//a device without a hotkey is removed
		ActiveDevices.Remove(monitor);
		Assert.HasCount(2, vm.Devices);

		ChangeDefaultDevice(again);
		Assert.IsTrue(entry.IsDefault);
		Assert.IsFalse(vm.Devices.Single(d => d.Name == "Speakers").IsDefault);
	}

	[TestMethod]
	public void SetAsDefaultOnlyForAConnectedDeviceThatIsntTheDefault()
	{
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		ActiveDevices.Add(CreateDevice("2", "Headphones"));
		Settings.Hotkeys.DefaultDevices = new[] { new DeviceHotkeyEntry { Hotkey = Switch, DeviceId = "3", DeviceName = "Monitor" } };
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);

		vm.SelectedDevice = vm.Devices.Single(d => d.Name == "Speakers");
		Assert.IsFalse(vm.SetAsDefaultCommand.CanExecute(null));
		vm.SelectedDevice = vm.Devices.Single(d => d.Name == "Monitor");
		Assert.IsFalse(vm.SetAsDefaultCommand.CanExecute(null));

		vm.SelectedDevice = vm.Devices.Single(d => d.Name == "Headphones");
		vm.SetAsDefaultCommand.Execute(null);
		DeviceProvider.Verify(m => m.SetDefaultDevice("2"), Times.Once);
	}

	private static readonly DeviceFormat At48 = new DeviceFormat(2, 48000, 384000, 8, 32, 24, 3);
	private static readonly DeviceFormatOption Option16At44 = new DeviceFormatOption(16, 16, 44100);
	private static readonly DeviceFormatOption Option24At48 = new DeviceFormatOption(24, 32, 48000);
	private static readonly DeviceFormatOption Option24At96 = new DeviceFormatOption(24, 32, 96000);

	/// <summary>
	/// The selected (default) device's settings, once they're read
	/// </summary>
	private DeviceSettings LoadedSettings(DefaultDeviceHotkeysViewModel vm)
	{
		var settings = vm.SelectedDevice.Settings;
		WaitFor(() => settings.CanChooseFormat, "the device's settings weren't read");
		return settings;
	}

	private void SetUpFeatures(string id, DeviceFeatureState state)
		=> DeviceProvider.Setup(m => m.GetDeviceFeaturesAsync(id)).ReturnsAsync(state);

	[TestMethod]
	public void TheSelectedDevicesFormatsAreListedWithTheCurrentOneSelected()
	{
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option16At44, Option24At48, Option24At96));
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);

		var settings = LoadedSettings(vm);

		Assert.HasCount(3, settings.SupportedFormats);
		Assert.AreEqual(Option24At48, settings.SelectedFormat);
		//the real bits, not the 32-bit container
		Assert.AreEqual("24 bit, 48000 Hz", settings.Format);
		DeviceProvider.Verify(m => m.SetDeviceFormatAsync(It.IsAny<string>(), It.IsAny<DeviceFormatOption>()), Times.Never);
	}

	[TestMethod]
	public void PickingAFormatSetsIt()
	{
		DeviceProvider.Setup(m => m.SetDeviceFormatAsync("1", Option24At96)).ReturnsAsync(true);
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option24At48, Option24At96));
		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));

		settings.SelectedFormat = Option24At96;

		WaitFor(() => !settings.IsBusy, "the format change didn't finish");
		DeviceProvider.Verify(m => m.SetDeviceFormatAsync("1", Option24At96), Times.Once);
		Assert.AreEqual(string.Empty, settings.Error ?? string.Empty);
		Assert.AreEqual(Option24At96, settings.SelectedFormat);
	}

	[TestMethod]
	public void ARefusedFormatShowsAnErrorAndTheCurrentOneAgain()
	{
		DeviceProvider.Setup(m => m.SetDeviceFormatAsync("1", Option24At96)).ReturnsAsync(false);
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option24At48, Option24At96));
		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));

		settings.SelectedFormat = Option24At96;

		WaitFor(() => settings.Error == ErrorDictionary.Instance[ErrorMessageType.FormatRefused], "no error shown");
		Assert.AreEqual(Option24At48, settings.SelectedFormat);
		Assert.IsFalse(settings.IsBusy);
	}

	[TestMethod]
	public void ARebuiltDeviceShowsItsNewState()
	{
		var old = CreateDevice("1", "Speakers", At48, Option24At48, Option24At96);
		SetDefaultDevice(old);
		var vm = new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys);
		var speakers = vm.SelectedDevice;
		LoadedSettings(vm);

		//Windows reconfigured the device: the provider inserts the new model, then removes the old one
		var rebuilt = CreateDevice("1", "Speakers", new DeviceFormat(2, 96000, 768000, 8, 32, 24, 3), Option24At48, Option24At96);
		ActiveDevices.Insert(0, rebuilt);
		ActiveDevices.Remove(old);

		Assert.AreSame(rebuilt, speakers.Model);
		var settings = LoadedSettings(vm);
		Assert.AreEqual(Option24At96, settings.SelectedFormat);
		Assert.AreEqual("24 bit, 96000 Hz", settings.Format);
	}

	[TestMethod]
	public void TheDevicesSwitchesAreShown()
	{
		SetUpFeatures("1", new DeviceFeatureState(AllowExclusive: true, ExclusivePriority: false));
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option24At48));

		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));

		WaitFor(() => settings.HasExclusiveMode, "the switches weren't read");
		Assert.IsTrue(settings.AllowExclusive);
		Assert.IsFalse(settings.ExclusivePriority);
		//nothing is written while the state is shown
		DeviceProvider.Verify(m => m.SetDeviceSwitchAsync(It.IsAny<string>(), It.IsAny<DeviceSwitch>(), It.IsAny<bool>()), Times.Never);
	}

	[TestMethod]
	public void FeaturesThatCantBeReadAreHidden()
	{
		SetUpFeatures("1", DeviceFeatureState.Unknown);
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option24At48));

		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));

		Assert.IsFalse(settings.HasExclusiveMode);
		Assert.IsFalse(settings.HasBalance);
	}

	[TestMethod]
	public void SwitchesAreSetAndAFailureTurnsThemBack()
	{
		SetUpFeatures("1", new DeviceFeatureState(true, true));
		DeviceProvider.Setup(m => m.SetDeviceSwitchAsync("1", DeviceSwitch.AllowExclusive, false)).ReturnsAsync(true);
		DeviceProvider.Setup(m => m.SetDeviceSwitchAsync("1", DeviceSwitch.ExclusivePriority, false)).ReturnsAsync(false);
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, Option24At48));
		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));
		WaitFor(() => settings.HasExclusiveMode, "the switches weren't read");

		settings.ExclusivePriority = false;
		WaitFor(() => settings.Error == ErrorDictionary.Instance[ErrorMessageType.SettingFailed], "no error shown");
		Assert.IsTrue(settings.ExclusivePriority);

		settings.AllowExclusive = false;
		WaitFor(() => !settings.IsBusy, "the change didn't finish");
		Assert.IsFalse(settings.AllowExclusive);
		//priority only matters while apps may take exclusive control
		Assert.IsFalse(settings.CanSetExclusivePriority);
		DeviceProvider.Verify(m => m.SetDeviceSwitchAsync("1", DeviceSwitch.AllowExclusive, false), Times.Once);
	}

	[TestMethod]
	public void TheBalanceShowsAndSetsTheFirstTwoChannels()
	{
		SetDefaultDevice(CreateDevice("1", "Speakers", At48, out var device, Option24At48));
		device.Setup(m => m.GetChannelVolumes()).Returns(new[] { 0.7f, 0.5f, 0.7f, 0.7f });
		var settings = LoadedSettings(new DefaultDeviceHotkeysViewModel(DeviceProvider.Object, Hotkeys));
		WaitFor(() => settings.HasBalance, "the balance wasn't read");

		Assert.AreEqual(70d, settings.Left);
		Assert.AreEqual(50d, settings.Right);

		settings.Left = 35;
		device.Verify(m => m.SetChannelVolume(0, 0.35f), Times.Once);
		device.Verify(m => m.SetChannelVolume(1, It.IsAny<float>()), Times.Never);
	}

	[TestMethod]
	public void MediaKeyHotkeyIsSetChangedAndCleared()
	{
		ActivateHotkeys();
		var vm = new SystemHotkeysViewModel(Hotkeys);
		CollectionAssert.AreEqual(new[] { Key.MediaPlayPause, Key.MediaPreviousTrack, Key.MediaNextTrack, Key.MediaStop }, vm.Keys.Select(k => k.MediaKey).ToArray());
		var playPause = vm.Keys[0];

		playPause.Hotkey = Switch;
		Assert.AreEqual(Switch, playPause.Hotkey);
		var saved = Assert.ContainsSingle(Settings.Hotkeys.System);
		Assert.AreEqual(Key.MediaPlayPause, saved.SystemKey);
		Assert.AreEqual(Switch, saved.Replacement);
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);

		playPause.Hotkey = Up;
		Assert.AreEqual(Up, Assert.ContainsSingle(Settings.Hotkeys.System).Replacement);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Switch), Times.Once);
		HotkeyManager.Verify(m => m.RegisterHotkey(Up), Times.Once);

		playPause.ClearCommand.Execute(null);
		Assert.IsNull(playPause.Hotkey);
		Assert.IsEmpty(Settings.Hotkeys.System);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void MediaKeyRejectsAHotkeyInUseAndKeepsItsOwn()
	{
		ActivateHotkeys();
		var vm = new SystemHotkeysViewModel(Hotkeys);
		vm.Keys[0].Hotkey = Switch;
		vm.Keys[1].Hotkey = Up;

		vm.Keys[1].Hotkey = Switch;

		Assert.AreEqual(Up, vm.Keys[1].Hotkey);
		StringAssert.StartsWith(vm.Keys[1].Error, Switch.ToString());
		Assert.HasCount(2, Settings.Hotkeys.System);
		HotkeyManager.Verify(m => m.UnregisterHotkey(It.IsAny<HotKey>()), Times.Never);

		vm.Keys[1].Hotkey = Down;
		Assert.AreEqual(string.Empty, vm.Keys[1].Error);
	}

	[TestMethod]
	public void MediaKeyKeepsItsHotkeyWhenTheSystemRefusesTheNewOne()
	{
		ActivateHotkeys();
		var vm = new SystemHotkeysViewModel(Hotkeys);
		vm.Keys[2].Hotkey = Up;
		HotkeyManager.Setup(m => m.RegisterHotkey(Down)).Throws(new System.InvalidOperationException("taken by another app"));

		vm.Keys[2].Hotkey = Down;

		Assert.AreEqual(Up, vm.Keys[2].Hotkey);
		Assert.IsFalse(string.IsNullOrEmpty(vm.Keys[2].Error));
		Assert.AreEqual(Up, Assert.ContainsSingle(Settings.Hotkeys.System).Replacement);
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Never);
	}

	[TestMethod]
	public void SavedMediaKeyHotkeysAreRegisteredWhenHotkeysActivate()
	{
		var refused = new HotKey(Key.F9, ModifierKeys.Control);
		Settings.Hotkeys.System = new[]
		{
			new SystemHotkeyEntry { SystemKey = Key.MediaNextTrack, Replacement = Switch },
			new SystemHotkeyEntry { SystemKey = Key.MediaStop, Replacement = refused }
		};
		HotkeyManager.Setup(m => m.RegisterHotkey(refused)).Throws(new System.InvalidOperationException("taken by another app"));

		var vm = new SystemHotkeysViewModel(Hotkeys);
		ActivateHotkeys();

		Assert.AreEqual(Switch, vm.Keys[2].Hotkey);
		Assert.AreEqual(string.Empty, vm.Keys[2].Error ?? string.Empty);
		HotkeyManager.Verify(m => m.RegisterHotkey(Switch), Times.Once);
		Assert.AreEqual(refused, vm.Keys[3].Hotkey);
		Assert.IsFalse(string.IsNullOrEmpty(vm.Keys[3].Error));

		//a hotkey that never registered isn't unregistered when it's cleared
		vm.Keys[3].ClearCommand.Execute(null);
		HotkeyManager.Verify(m => m.UnregisterHotkey(refused), Times.Never);
		Assert.AreEqual(Key.MediaNextTrack, Assert.ContainsSingle(Settings.Hotkeys.System).SystemKey);
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
		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.Diff], vm.Errors.VolumeDown);
		Assert.AreEqual(string.Empty, vm.Errors.VolumeUp);
	}
}

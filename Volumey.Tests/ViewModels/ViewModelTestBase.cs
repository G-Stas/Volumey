using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

/// <summary>
/// Fresh settings, a fresh hotkey service with a fake hotkey manager, and fake device provider and notifications for every test.
/// </summary>
public abstract class ViewModelTestBase
{
	protected VolumeySettings Settings { get; private set; }
	protected HotkeyService Hotkeys { get; private set; }
	protected Mock<IHotkeyManager> HotkeyManager { get; private set; }
	protected Mock<INotificationService> Notifications { get; private set; }
	protected Mock<IDeviceProvider> DeviceProvider { get; private set; }
	protected ObservableCollection<OutputDeviceModel> ActiveDevices { get; private set; }

	[TestInitialize]
	public void InitializeBase()
	{
		Settings = new VolumeySettings();
		SettingsProvider.UseSettings(Settings);
		ErrorDictionary.UpdateErrorDictionary();

		Hotkeys = new HotkeyService();
		HotkeyManager = new Mock<IHotkeyManager>();
		Notifications = new Mock<INotificationService>();
		ActiveDevices = new ObservableCollection<OutputDeviceModel>();
		DeviceProvider = new Mock<IDeviceProvider>();
		DeviceProvider.SetupProperty(m => m.DefaultDevice);
		DeviceProvider.Setup(m => m.ActiveDevices).Returns(ActiveDevices);
	}

	/// <summary>
	/// Sets the hotkey manager, like the main window does on startup.
	/// </summary>
	protected void ActivateHotkeys() => Hotkeys.SetHotkeyManager(HotkeyManager.Object);

	protected void PressHotkey(Controls.HotKey hotkey)
		=> HotkeyManager.Raise(m => m.HotkeyPressed += null, hotkey);

	/// <summary>
	/// A device whose master volume writes go to <paramref name="masterVolume"/>.
	/// </summary>
	protected OutputDeviceModel CreateDevice(string id, string name, out Mock<IAudioSessionVolume> masterVolume, int volume = 70,
		params AudioProcessModel[] processes)
	{
		masterVolume = new Mock<IAudioSessionVolume>();
		var master = new MasterSessionModel(name, name, volume, false, id, null, masterVolume.Object, new Mock<IMasterVolumeNotificationHandler>().Object, Hotkeys);
		var device = new Mock<IDevice>();
		device.Setup(m => m.GetId()).Returns(id);
		return new OutputDeviceModel(device.Object, new Mock<IDeviceStateNotificationHandler>().Object, new Mock<ISessionProvider>().Object,
									 master, processes.ToList());
	}

	protected OutputDeviceModel CreateDevice(string id, string name, params AudioProcessModel[] processes)
		=> CreateDevice(id, name, out _, 70, processes);

	/// <summary>
	/// A device running at <paramref name="format"/> that supports <paramref name="supported"/>
	/// </summary>
	protected OutputDeviceModel CreateDevice(string id, string name, DeviceFormat format, params DeviceFormatOption[] supported)
		=> CreateDevice(id, name, format, out _, supported);

	/// <summary>
	/// A device running at <paramref name="format"/> that supports <paramref name="supported"/>; <paramref name="device"/> fakes the rest
	/// (e.g. its channel volumes)
	/// </summary>
	protected OutputDeviceModel CreateDevice(string id, string name, DeviceFormat format, out Mock<IDevice> device, params DeviceFormatOption[] supported)
	{
		var master = new MasterSessionModel(name, name, 70, false, id, null, new Mock<IAudioSessionVolume>().Object,
			new Mock<IMasterVolumeNotificationHandler>().Object, Hotkeys);
		device = new Mock<IDevice>();
		device.Setup(m => m.GetId()).Returns(id);
		device.Setup(m => m.GetDeviceFormat()).Returns(format);
		device.Setup(m => m.GetSupportedFormats()).Returns(supported);
		device.Setup(m => m.GetChannelVolumes()).Returns(Array.Empty<float>());
		return new OutputDeviceModel(device.Object, new Mock<IDeviceStateNotificationHandler>().Object, new Mock<ISessionProvider>().Object,
									 master, new List<AudioProcessModel>());
	}

	protected AudioProcessModel CreateProcess(string name, int volume = 50)
		=> AudioProcessModelTests.GetProcessMock(name, volume, false, Hotkeys);

	protected void SetDefaultDevice(OutputDeviceModel device)
	{
		if(device != null && !ActiveDevices.Contains(device))
			ActiveDevices.Add(device);
		DeviceProvider.Object.DefaultDevice = device;
	}

	protected void ChangeDefaultDevice(OutputDeviceModel device)
	{
		SetDefaultDevice(device);
		DeviceProvider.Raise(m => m.DefaultDeviceChanged += null, device);
	}

	/// <summary>
	/// Raises <see cref="OutputDeviceModel.ProcessCreated"/>, as a new audio session of a new process would.
	/// </summary>
	protected static void RaiseProcessCreated(OutputDeviceModel device, AudioProcessModel process)
	{
		var field = typeof(OutputDeviceModel).GetField(nameof(OutputDeviceModel.ProcessCreated), BindingFlags.Instance | BindingFlags.NonPublic);
		(field!.GetValue(device) as Action<AudioProcessModel>)?.Invoke(process);
	}

	/// <summary>
	/// Waits until <paramref name="condition"/> holds: some view models finish their work on the thread pool. Fails after 2 seconds.
	/// </summary>
	protected static void WaitFor(Func<bool> condition, string message)
	{
		var watch = Stopwatch.StartNew();
		while(!condition())
		{
			if(watch.ElapsedMilliseconds > 2000)
				Assert.Fail(message);
			Thread.Sleep(10);
		}
	}
}

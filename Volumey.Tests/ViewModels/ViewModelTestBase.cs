using System;
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

namespace Volumey.Tests.ViewModels
{
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
			this.Settings = new VolumeySettings();
			SettingsProvider.UseSettings(this.Settings);
			ErrorDictionary.UpdateErrorDictionary();

			this.Hotkeys = new HotkeyService();
			this.HotkeyManager = new Mock<IHotkeyManager>();
			this.Notifications = new Mock<INotificationService>();
			this.ActiveDevices = new ObservableCollection<OutputDeviceModel>();
			this.DeviceProvider = new Mock<IDeviceProvider>();
			this.DeviceProvider.SetupProperty(m => m.DefaultDevice);
			this.DeviceProvider.Setup(m => m.ActiveDevices).Returns(this.ActiveDevices);
		}

		/// <summary>
		/// Sets the hotkey manager, like the main window does on startup.
		/// </summary>
		protected void ActivateHotkeys() => this.Hotkeys.SetHotkeyManager(this.HotkeyManager.Object);

		protected void PressHotkey(Controls.HotKey hotkey)
			=> this.HotkeyManager.Raise(m => m.HotkeyPressed += null, hotkey);

		/// <summary>
		/// A device whose master volume writes go to <paramref name="masterVolume"/>.
		/// </summary>
		protected OutputDeviceModel CreateDevice(string id, string name, out Mock<IAudioSessionVolume> masterVolume, int volume = 70,
			params AudioProcessModel[] processes)
		{
			masterVolume = new Mock<IAudioSessionVolume>();
			var master = new MasterSessionModel(name, name, volume, false, id, null, masterVolume.Object, new Mock<IMasterVolumeNotificationHandler>().Object, this.Hotkeys);
			var device = new Mock<IDevice>();
			device.Setup(m => m.GetId()).Returns(id);
			return new OutputDeviceModel(device.Object, new Mock<IDeviceStateNotificationHandler>().Object, new Mock<ISessionProvider>().Object,
			                             master, processes.ToList());
		}

		protected OutputDeviceModel CreateDevice(string id, string name, params AudioProcessModel[] processes)
			=> CreateDevice(id, name, out _, 70, processes);

		protected AudioProcessModel CreateProcess(string name, int volume = 50)
			=> AudioProcessModelTests.GetProcessMock(name, volume, false, this.Hotkeys);

		protected void SetDefaultDevice(OutputDeviceModel device)
		{
			if(device != null && !this.ActiveDevices.Contains(device))
				this.ActiveDevices.Add(device);
			this.DeviceProvider.Object.DefaultDevice = device;
		}

		protected void ChangeDefaultDevice(OutputDeviceModel device)
		{
			this.SetDefaultDevice(device);
			this.DeviceProvider.Raise(m => m.DefaultDeviceChanged += null, device);
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
		/// Some view models save settings on the thread pool.
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
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using log4net;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.DataProvider;

/// <summary>
/// Provides data about active and default output devices.
/// </summary>
public sealed class DeviceProvider : IDeviceProvider, INotifyPropertyChanged
{
	public ObservableCollection<OutputDeviceModel> ActiveDevices { get; } = new ObservableCollection<OutputDeviceModel>();
	public event Action<OutputDeviceModel> DeviceDisabled;
	public event Action<OutputDeviceModel> DefaultDeviceChanged;
	public event Action<OutputDeviceModel, OutputDeviceModel> DeviceReplaced;

	private OutputDeviceModel defaultDevice;
	public OutputDeviceModel DefaultDevice
	{
		get => defaultDevice;
		set
		{
			defaultDevice = value;
			OnPropertyChanged();
		}
	}

	private readonly PolicyClient PolicyClient = new PolicyClient();

	private bool noOutputDevices;
	public bool NoOutputDevices
	{
		get => noOutputDevices;
		set
		{
			noOutputDevices = value;
			OnPropertyChanged();
		}
	}

	private readonly IDeviceStateNotificationHandler deviceStateNotificationHandler;
	private readonly IDeviceEnumerator deviceEnumerator;

	/// <summary>Delays of the attempts to rebuild a reconfigured device: Windows may still be busy right after the notification</summary>
	private static readonly TimeSpan[] RebuildDelays =
		{ TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
	private volatile bool disposed;
	private static ILog logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceProvider));

	private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	internal DeviceProvider(IDeviceEnumerator deviceEnumerator, IDeviceStateNotificationHandler deviceStateHandler)
	{
		deviceStateNotificationHandler = deviceStateHandler;
		this.deviceEnumerator = deviceEnumerator;
		deviceStateNotificationHandler.DefaultDeviceChanged += OnDefaultDeviceChanged;
		deviceStateNotificationHandler.ActiveDeviceAdded += OnActiveDeviceAdded;

		string defaultDeviceId = deviceEnumerator.GetDefaultDeviceId();
		var devices = deviceEnumerator.GetCurrentActiveDevices(deviceStateNotificationHandler);
		if(devices.Count > 0)
		{
			foreach(var device in devices)
			{
				device.Disabled += OnDeviceDisabled;
				device.Invalidated += OnDeviceInvalidated;
				ActiveDevices.Add(device);

				if(device.CompareId(defaultDeviceId))
				{
					DefaultDevice = device;
				}
			}

			if(DefaultDevice == null)
			{
				DefaultDevice = ActiveDevices[0];
			}
		}
		else
			NoOutputDevices = true;
	}

	private DeviceProvider() => NoOutputDevices = true;

	/// <summary>
	/// Creates the provider of the app's audio devices. If Windows audio can't be reached, the provider has no devices.
	/// </summary>
	internal static IDeviceProvider Create(IHotkeyService hotkeys)
	{
		try
		{
			var devEnum = new DeviceEnumerator(hotkeys);
			return new DeviceProvider(devEnum, new DeviceStateNotificationsHandler(devEnum));
		}
		catch(Exception e)
		{
			//everything depends on this object, so create an empty one which shows that there are no output devices
			Logger.Fatal("Failed to get device provider object", e);
			return new DeviceProvider();
		}
	}
	public void SetDefaultDevice(string id)
	{
		try
		{
			//Check if the device to set is enabled
			if(ActiveDevices.Any(device => device.CompareId(id)))
			{
				//Prevent if it's already the default device
				if(DefaultDevice != null && !DefaultDevice.CompareId(id))
				{
					PolicyClient.SetDefaultEndpointDevice(id);
				}
			}
		}
		catch(Exception e)
		{
			Logger.Error("Failed to change default device", e);
		}
	}

	public Task<bool> SetDeviceFormatAsync(string deviceId, DeviceFormatOption format) => Task.Run(() =>
	{
		try
		{
			PolicyClient.SetDeviceFormat(deviceId, format);
			Logger.Info($"Device format set: {format.Bits} bit, {format.SamplesPerSec} Hz (container {format.ContainerBits})");
			return true;
		}
		catch(Exception e)
		{
			//undocumented interface: Windows may refuse the format or the call may stop working with an update
			Logger.Error($"Failed to set the device format {format.Bits} bit, {format.SamplesPerSec} Hz", e);
			return false;
		}
	});

	private DeviceFeatures features;
	private DeviceFeatures Features => features ??= new DeviceFeatures(PolicyClient);

	public Task<DeviceFeatureState> GetDeviceFeaturesAsync(string deviceId) => Task.Run(() =>
	{
		try
		{
			return Features.Read(deviceId);
		}
		catch(Exception e)
		{
			Logger.Error("Failed to read the device's features", e);
			return DeviceFeatureState.Unknown;
		}
	});

	public Task<bool> SetDeviceSwitchAsync(string deviceId, DeviceSwitch setting, bool on) => Task.Run(() =>
	{
		try
		{
			Features.Set(deviceId, setting, on);
			Logger.Info($"Device setting changed: {setting} {(on ? "on" : "off")}");
			return true;
		}
		catch(Exception e)
		{
			//undocumented interface: it may stop working with a Windows update
			Logger.Error($"Failed to turn {setting} {(on ? "on" : "off")}", e);
			return false;
		}
	});

	private void OnDeviceDisabled(OutputDeviceModel device)
	{
		Logger.Info($"Output device disabled: [{device.Master.DeviceFriendlyName}]");
		dispatcher.Invoke(() =>
		{
			//display NoOutputDevice placeholder in mixer view before deleting the last element
			if(ActiveDevices.Count == 1)
				NoOutputDevices = true;

			DeviceDisabled?.Invoke(device);
			device.Disabled -= OnDeviceDisabled;
			device.Invalidated -= OnDeviceInvalidated;
			ActiveDevices.Remove(device);

			device.Dispose();
		});
	}

	private void OnActiveDeviceAdded(OutputDeviceModel newDevice)
	{
		Logger.Info($"Output device added: [{newDevice.Master.DeviceFriendlyName}]");
		dispatcher.Invoke(() =>
		{
			newDevice.Disabled += OnDeviceDisabled;
			newDevice.Invalidated += OnDeviceInvalidated;
			ActiveDevices.Add(newDevice);

			if(NoOutputDevices)
			{
				DefaultDevice = newDevice;
				//invoke the event to display a new default device in mixer view before NoOutputDevices placeholder will be removed
				DefaultDeviceChanged?.Invoke(DefaultDevice);
				NoOutputDevices = false;
			}
		});
	}

	private void OnDefaultDeviceChanged(string deviceId)
	{
		dispatcher.Invoke(() =>
		{
			if(deviceId == null)
			{
				DefaultDevice = null;
				DefaultDeviceChanged?.Invoke(DefaultDevice);
				return;
			}

			//on the UI thread, the only one that changes ActiveDevices
			var newDefaultDevice = ActiveDevices.FirstOrDefault(device => device.CompareId(deviceId));
			if(newDefaultDevice != null && DefaultDevice != newDefaultDevice)
			{
				Logger.Info($"Default output device changed: [{newDefaultDevice.Master.DeviceFriendlyName}]");
				DefaultDevice = newDefaultDevice;
				DefaultDeviceChanged?.Invoke(DefaultDevice);
			}
		});
	}

	/// <summary>
	/// Windows reconfigured the device (format or spatial sound changed, #82, #85): its sessions are gone and its objects stopped working,
	/// so the model is built again from fresh objects and replaces the old one. Before 2.0 the whole app restarted instead.
	/// </summary>
	private void OnDeviceInvalidated(OutputDeviceModel invalid)
		=> RebuildAsync(invalid).Forget(Logger, "Failed to rebuild a reconfigured device");

	private async System.Threading.Tasks.Task RebuildAsync(OutputDeviceModel invalid)
	{
		foreach(var delay in RebuildDelays)
		{
			await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
			if(disposed)
				return;
			bool done = false;
			dispatcher.Invoke(() => done = TryReplaceDevice(invalid));
			if(done)
				return;
		}
		Logger.Error($"Failed to rebuild the reconfigured device: [{invalid.Master.DeviceFriendlyName}]");
	}

	/// <returns>False if the new model couldn't be built yet</returns>
	private bool TryReplaceDevice(OutputDeviceModel invalid)
	{
		int index = ActiveDevices.IndexOf(invalid);
		//it was disabled in the meantime
		if(index < 0)
			return true;

		var fresh = deviceEnumerator.CreateDeviceModel(invalid.Id, deviceStateNotificationHandler);
		if(fresh == null)
			return false;

		invalid.Disabled -= OnDeviceDisabled;
		invalid.Invalidated -= OnDeviceInvalidated;
		fresh.Disabled += OnDeviceDisabled;
		fresh.Invalidated += OnDeviceInvalidated;
		//add the new model before the listeners switch to it, and remove the old one after that:
		//replacing it in place makes a combo box that shows it lose its selection and write null back
		ActiveDevices.Insert(index, fresh);

		if(DefaultDevice == invalid)
		{
			DefaultDevice = fresh;
			//listeners move hotkeys, notifications etc. from the old model to the new one
			DefaultDeviceChanged?.Invoke(fresh);
		}
		DeviceReplaced?.Invoke(invalid, fresh);
		ActiveDevices.Remove(invalid);
		Logger.Info($"Rebuilt the reconfigured device: [{fresh.Master.DeviceFriendlyName}], {fresh.Processes.Count} apps");

		invalid.Dispose();
		return true;
	}

	public void Dispose()
	{
		//no rebuilds after the app has started to exit
		disposed = true;
		deviceStateNotificationHandler?.Dispose();
		foreach(var device in ActiveDevices)
			device.Dispose();
	}

	public event PropertyChangedEventHandler PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

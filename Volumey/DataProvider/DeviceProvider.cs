#nullable enable
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
	public event Action<OutputDeviceModel>? DeviceDisabled;
	public event Action<OutputDeviceModel?>? DefaultDeviceChanged;
	public event Action<OutputDeviceModel, OutputDeviceModel>? DeviceReplaced;

	private OutputDeviceModel? defaultDevice;
	public OutputDeviceModel? DefaultDevice
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

	//null until Windows audio could be reached (see Create)
	private IDeviceStateNotificationHandler? deviceStateNotificationHandler;
	private IDeviceEnumerator? deviceEnumerator;

	/// <summary>Delays of the attempts to rebuild a reconfigured device: Windows may still be busy right after the notification</summary>
	private static readonly TimeSpan[] RebuildDelays =
		{ TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
	private volatile bool disposed;
	private static ILog? logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceProvider));

	private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	internal DeviceProvider(IDeviceEnumerator deviceEnumerator, IDeviceStateNotificationHandler deviceStateHandler)
		=> Connect(deviceEnumerator, deviceStateHandler);

	/// <summary>Not connected to Windows audio yet: no devices until <see cref="Connect"/></summary>
	internal DeviceProvider() => NoOutputDevices = true;

	/// <summary>
	/// Reads the devices and starts listening to Windows. On the UI thread. Called by the constructor, or later when
	/// Windows audio couldn't be reached at start (<see cref="Create"/>); then the listeners learn the default device
	/// through <see cref="DefaultDeviceChanged"/>, as when the first device is plugged in.
	/// </summary>
	internal void Connect(IDeviceEnumerator enumerator, IDeviceStateNotificationHandler deviceStateHandler)
	{
		deviceStateNotificationHandler = deviceStateHandler;
		deviceEnumerator = enumerator;
		deviceStateNotificationHandler.DefaultDeviceChanged += OnDefaultDeviceChanged;
		deviceStateNotificationHandler.ActiveDeviceAdded += OnActiveDeviceAdded;

		string? defaultDeviceId = deviceEnumerator.GetDefaultDeviceId();
		var devices = deviceEnumerator.GetCurrentActiveDevices(deviceStateNotificationHandler);
		if(devices.Count == 0)
		{
			NoOutputDevices = true;
			return;
		}
		OutputDeviceModel? newDefault = null;
		foreach(var device in devices)
		{
			device.Disabled += OnDeviceDisabled;
			device.Invalidated += OnDeviceInvalidated;
			ActiveDevices.Add(device);
			if(device.CompareId(defaultDeviceId))
				newDefault = device;
		}
		DefaultDevice = newDefault ?? ActiveDevices[0];
		//show the default device in the mixer before the NoOutputDevices placeholder goes away
		DefaultDeviceChanged?.Invoke(DefaultDevice);
		NoOutputDevices = false;
	}

	/// <summary>Attempts at start, one right after the other (on the UI thread), before the app starts without devices</summary>
	internal const int QuickAttempts = 3;
	private static readonly TimeSpan QuickAttemptPause = TimeSpan.FromMilliseconds(200);
	private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
	internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

	/// <summary>The delay before background retry number <paramref name="retry"/> (0-based): 1 s, doubling, at most <see cref="MaxRetryDelay"/></summary>
	internal static TimeSpan RetryDelay(int retry)
		=> retry >= 20 ? MaxRetryDelay : TimeSpan.FromTicks(Math.Min(FirstRetryDelay.Ticks << retry, MaxRetryDelay.Ticks));

	/// <summary>
	/// Creates the provider of the app's audio devices. Windows audio can refuse at start for a moment (the device
	/// enumerator's class factory can fail with 0x80070020, "the file is in use"), so it's tried <see cref="QuickAttempts"/>
	/// times; after that the app starts without devices and keeps trying in the background with growing delays (<see cref="RetryDelay"/>).
	/// </summary>
	internal static IDeviceProvider Create(IHotkeyService hotkeys)
	{
		for(int attempt = 1; ; attempt++)
		{
			try
			{
				var devEnum = new DeviceEnumerator(hotkeys);
				return new DeviceProvider(devEnum, new DeviceStateNotificationsHandler(devEnum));
			}
			catch(Exception e) when(attempt < QuickAttempts)
			{
				Logger.Warn($"Couldn't reach Windows audio (attempt {attempt}), trying again", e);
				System.Threading.Thread.Sleep(QuickAttemptPause);
			}
			catch(Exception e)
			{
				//everything depends on this object: start without devices (the mixer shows that) and keep trying
				Logger.Error(IsAudioServiceMissing(e)
					? "The Windows Audio service isn't installed, starting without devices"
					: $"Couldn't reach Windows audio after {QuickAttempts} attempts, trying again in the background", e);
				var provider = new DeviceProvider();
				provider.RetryConnectAsync(hotkeys).Forget(Logger, "Failed to retry reaching Windows audio");
				return provider;
			}
		}
	}

	private async Task RetryConnectAsync(IHotkeyService hotkeys)
	{
		Exception? lastError = null;
		for(int retry = 0; ; retry++)
		{
			await Task.Delay(RetryDelay(retry)).ConfigureAwait(false);
			if(disposed)
				return;
			bool connected = await dispatcher.InvokeAsync(() =>
			{
				if(disposed)
					return true;
				try
				{
					var devEnum = new DeviceEnumerator(hotkeys);
					Connect(devEnum, new DeviceStateNotificationsHandler(devEnum));
					Logger.Info($"Reached Windows audio after {QuickAttempts + retry + 1} attempts");
					return true;
				}
				catch(Exception e)
				{
					lastError = e;
					return false;
				}
			});
			if(connected)
				return;
			//every 12th retry (once an hour after the delay reached 5 minutes), so a lasting failure can't flood Loggly.
			//Without the audio service the start's error said it all: it won't change while the app runs
			if((retry + 1) % 12 == 0 && !IsAudioServiceMissing(lastError))
				Logger.Error($"Still can't reach Windows audio after {QuickAttempts + retry + 1} attempts", lastError);
		}
	}

	private const int ErrorServiceDoesNotExist = unchecked((int)0x80070424);

	/// <summary>The Windows Audio service isn't installed (Windows editions or builds without it)</summary>
	internal static bool IsAudioServiceMissing(Exception? e) => e?.HResult == ErrorServiceDoesNotExist;

	public void SetDefaultDevice(string? id)
	{
		try
		{
			if(id == null)
				return;
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
		catch(DeviceFormatException e)
		{
			//Windows refused it; the Devices page says so and shows the current format again
			Logger.Warn($"Windows refused the device format {format.Bits} bit, {format.SamplesPerSec} Hz (container {format.ContainerBits})", e);
			return false;
		}
		catch(Exception e)
		{
			//undocumented interface: the call may stop working with an update
			Logger.Error($"Failed to set the device format {format.Bits} bit, {format.SamplesPerSec} Hz", e);
			return false;
		}
	});

	private DeviceFeatures? features;
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
			//Windows can report a new device twice (added, then its state changed to active): one model per device
			foreach(var device in ActiveDevices)
			{
				if(device.CompareId(newDevice.Id))
				{
					Logger.Info($"The device is already listed: [{newDevice.Master.DeviceFriendlyName}]");
					newDevice.Dispose();
					return;
				}
			}

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
			//Windows' default device that couldn't be read at first: its default device notification found no model
			else if(DefaultDevice != newDevice && newDevice.CompareId(deviceEnumerator?.GetDefaultDeviceId()))
			{
				Logger.Info($"Default output device changed: [{newDevice.Master.DeviceFriendlyName}]");
				DefaultDevice = newDevice;
				DefaultDeviceChanged?.Invoke(DefaultDevice);
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
	/// Windows reconfigured the device (format or spatial sound changed): its sessions are gone and its objects stopped working,
	/// so the model is built again from fresh objects and replaces the old one.
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

		//a device only becomes invalid after Connect, which sets both
		if(deviceEnumerator == null || deviceStateNotificationHandler == null)
			return false;
		OutputDeviceModel? fresh;
		try { fresh = deviceEnumerator.CreateDeviceModel(invalid.Id, deviceStateNotificationHandler); }
		catch(DeviceNotReadyException) { return false; }
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
		//called again when the container is disposed on exit
		if(disposed)
			return;
		//no rebuilds after the app has started to exit
		disposed = true;
		deviceStateNotificationHandler?.Dispose();
		foreach(var device in ActiveDevices)
			device.Dispose();
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

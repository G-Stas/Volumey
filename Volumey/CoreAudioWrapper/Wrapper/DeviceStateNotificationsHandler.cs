#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using NAudio.CoreAudioApi;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Provides notifications when an audio endpoint device is added or removed,
/// when the state or properties of an endpoint device change,
/// or when there is a change in the default role assigned to an endpoint device.
/// NAudio raises the notifications on COM threads. They are passed to the UI thread without waiting:
/// a waiting callback thread deadlocks with the UI thread when that releases a device's COM objects,
/// because the release waits for running callbacks to return.
/// </summary>
public sealed class DeviceStateNotificationsHandler : IDeviceStateNotificationHandler
{
	public event Action<string>? DefaultDeviceChanged;
	public event Action<string>? NameChanged;
	public event Action<string>? IconPathChanged;
	public event Action<string>? FormatChanged;
	public event Action<string>? DeviceDisabled;
	public event Action<OutputDeviceModel>? ActiveDeviceAdded;

	private readonly DeviceEnumerator enumerator;
	private readonly MMDeviceNotificationClient notificationClient;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceStateNotificationsHandler));

	public DeviceStateNotificationsHandler(DeviceEnumerator enumerator)
	{
		this.enumerator = enumerator;
		notificationClient = enumerator.Enumerator.CreateNotificationClient(useSynchronizationContext: false);
		notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
		notificationClient.DeviceAdded += OnDeviceAdded;
		notificationClient.DeviceStateChanged += OnDeviceStateChanged;
		notificationClient.PropertyValueChanged += OnPropertyValueChanged;
	}

	/// <summary>
	/// Called when the default Endpoint device for a given role changes
	/// </summary>
	private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
	{
		if(e.Flow == DataFlow.Render && e.Role == Role.Console)
		{
			Post(() => DefaultDeviceChanged?.Invoke(e.DeviceId));
		}
	}

	/// <summary>
	/// Called when a new Endpoint device is added to the system
	/// </summary>
	private void OnDeviceAdded(object? sender, DeviceNotificationEventArgs e)
		=> AddIfActiveOutput(e.DeviceId);

	/// <summary>
	/// Called when the state of an Endpoint device changes
	/// </summary>
	private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
	{
		//every output device model ignores other devices' IDs, so input devices need no check
		if(e.NewState != DeviceState.Active)
			Post(() => DeviceDisabled?.Invoke(e.DeviceId));
		else
			AddIfActiveOutput(e.DeviceId);
	}

	//one device read at a time, in the order of Windows' notifications
	private readonly SemaphoreSlim buildLock = new SemaphoreSlim(1, 1);

	/// <summary>
	/// Reads the device on the thread pool (a device and its apps take many Windows calls, seconds for a busy Bluetooth device)
	/// and lists it on the UI thread
	/// </summary>
	private void AddIfActiveOutput(string deviceId) => AddIfActiveOutputAsync(deviceId).Forget(Logger, "Failed to check/add new device");

	private async Task AddIfActiveOutputAsync(string deviceId)
	{
		//off the COM thread that brought the notification: it must not wait
		await Task.Yield();
		await buildLock.WaitAsync().ConfigureAwait(false);
		try
		{
			if(disposed)
				return;
			OutputDeviceModel? model;
			try
			{
				//null for input or inactive devices; their objects are released off the UI thread
				model = enumerator.CreateDeviceModel(deviceId, this);
			}
			catch(DeviceNotReadyException)
			{
				Post(() => AddWhenReady(deviceId));
				return;
			}
			if(model != null)
				Post(() =>
				{
					//reconfigured while it was read: read again (nothing is listed if it's gone)
					if(!List(model))
						AddWhenReady(deviceId);
				});
		}
		finally
		{
			buildLock.Release();
		}
	}

	/// <summary>On the UI thread: passes on a model built off it, unless the device went away or changed in the meantime</summary>
	/// <returns>False if the model was dropped and the device should be read again</returns>
	private bool List(OutputDeviceModel model, bool atStart = false)
	{
		if(disposed || !model.IsUsable())
		{
			model.Dispose();
			return disposed;
		}
		IsAddingStartDevice = atStart;
		try
		{
			ActiveDeviceAdded?.Invoke(model);
		}
		finally
		{
			IsAddingStartDevice = false;
		}
		return true;
	}

	/// <summary>Delays of the attempts to read a device Windows refused, about a minute in all</summary>
	private static readonly TimeSpan[] NotReadyDelays =
	{
		TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
		TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)
	};

	//the devices being read again; on the UI thread
	private readonly HashSet<string> notReadyDevices = new HashSet<string>();
	private volatile bool disposed;

	/// <summary>On the UI thread</summary>
	public void AddWhenReady(string deviceId, bool atStart = false)
	{
		if(notReadyDevices.Add(deviceId))
			RetryAddAsync(deviceId, atStart).Forget(Logger, "Failed to read a device again");
	}

	public bool IsAddingStartDevice { get; private set; }

	private async Task RetryAddAsync(string deviceId, bool atStart)
	{
		Exception? lastError = null;
		try
		{
			foreach(var delay in NotReadyDelays)
			{
				await Task.Delay(delay).ConfigureAwait(false);
				if(disposed || App.Current?.Dispatcher is not { } dispatcher)
					return;
				OutputDeviceModel? model;
				await buildLock.WaitAsync().ConfigureAwait(false);
				try
				{
					//null once the device is gone or no longer active
					model = enumerator.CreateDeviceModel(deviceId, this);
				}
				catch(DeviceNotReadyException e)
				{
					lastError = e.InnerException;
					continue;
				}
				finally
				{
					buildLock.Release();
				}
				if(model == null)
					return;
				bool listed = await dispatcher.InvokeAsync(() =>
				{
					if(!List(model, atStart))
						return false;
					if(!disposed)
						Logger.Info("Read the output device that Windows refused before");
					return true;
				});
				if(listed)
					return;
			}
			Logger.Error($"Windows refused the output device {NotReadyDelays.Length + 1} times", lastError);
		}
		finally
		{
			if(App.Current?.Dispatcher is { } dispatcher)
				_ = dispatcher.InvokeAsync(() => notReadyDevices.Remove(deviceId));
		}
	}

	private void OnPropertyValueChanged(object? sender, DevicePropertyChangedEventArgs e)
	{
		var key = e.PropertyKey;
		if(IsKey(key, PropertyKeys.PKEY_Device_FriendlyName) || IsKey(key, PropertyKeys.PKEY_Device_DeviceDesc))
			Post(() => NameChanged?.Invoke(e.DeviceId));
		else if(IsKey(key, PropertyKeys.PKEY_Device_IconPath))
			Post(() => IconPathChanged?.Invoke(e.DeviceId));
		else if(IsKey(key, PropertyKeys.PKEY_AudioEngine_DeviceFormat))
			Post(() => FormatChanged?.Invoke(e.DeviceId));
	}

	/// <summary>
	/// Runs the action on the UI thread without blocking the calling COM thread. The dispatcher keeps the order of the notifications.
	/// </summary>
	private void Post(Action action)
	{
		try
		{
			App.Current?.Dispatcher.InvokeAsync(() =>
			{
				try { action(); }
				catch(Exception e) { Logger.Error("Failed to handle a device notification", e); }
			});
		}
		catch(Exception e)
		{
			Logger.Error("Failed to dispatch a device notification", e);
		}
	}

	private static bool IsKey(PropertyKey key, PropertyKey expected)
		=> key.formatId == expected.formatId && key.propertyId == expected.propertyId;

	public void Dispose()
	{
		disposed = true;
		notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
		notificationClient.DeviceAdded -= OnDeviceAdded;
		notificationClient.DeviceStateChanged -= OnDeviceStateChanged;
		notificationClient.PropertyValueChanged -= OnPropertyValueChanged;
		//Unregister on the thread pool because this call occasionally hangs and might hang the entire application when executed in the UI thread :\
		Task.Run(() => notificationClient.Dispose());
	}
}

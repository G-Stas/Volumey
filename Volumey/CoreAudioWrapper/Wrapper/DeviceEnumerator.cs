#nullable enable
using System;
using System.Collections.Generic;
using log4net;
using NAudio.CoreAudioApi;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.CoreAudioWrapper.Wrapper;

public sealed class DeviceEnumerator : IDeviceEnumerator
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceEnumerator));

	internal MMDeviceEnumerator Enumerator { get; }

	/// <summary>
	/// Given to the models it creates
	/// </summary>
	internal IHotkeyService Hotkeys { get; }

	public DeviceEnumerator(IHotkeyService hotkeys)
	{
		Hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
		Enumerator = new MMDeviceEnumerator();
	}

	/// <summary>
	/// The friendly name of a device in any state (also unplugged or disabled); null when Windows no longer knows it.
	/// Makes its own enumerator: for any thread, also while Windows audio couldn't be reached at start
	/// </summary>
	internal static string? GetDeviceName(string deviceId)
	{
		try
		{
			using var enumerator = new MMDeviceEnumerator();
			using var device = enumerator.GetDevice(deviceId);
			return device.FriendlyName;
		}
		catch(Exception e)
		{
			Logger.Debug($"Couldn't read the name of the device {deviceId}", e);
			return null;
		}
	}

	public uint GetDeviceCount()
	{
		try
		{
			using var collection = Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
			return (uint)collection.Count;
		}
		catch(Exception e)
		{
			Logger.Error("Failed to get device count", e);
		}
		return 0;
	}

	public string? GetDefaultDeviceId()
	{
		if(GetDeviceCount() == 0)
			return null;

		string id = string.Empty;
		try
		{
			if(!Enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var dev))
				return string.Empty;
			using(dev)
				id = dev.ID;
		}
		catch(Exception e)
		{
			Logger.Error("Failed to get default output device id", e);
		}
		return id;
	}

	public List<OutputDeviceModel> GetCurrentActiveDevices(IDeviceStateNotificationHandler dsn)
	{
		var devices = new List<OutputDeviceModel>();
		MMDeviceCollection activeDevices;
		int count;

		try
		{
			activeDevices = Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
			count = activeDevices.Count;
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to get output devices collection", e);
			return devices;
		}

		using(activeDevices)
		{
			for(int i = 0; i < count; i++)
			{
				AudioDevice? device = null;
				try
				{
					device = new AudioDevice(activeDevices[i]);
					var deviceModel = AudioModelFactory.CreateOutputDeviceModel(device, dsn, Hotkeys);
					if(deviceModel != null)
						devices.Add(deviceModel);
					else
						device.Dispose();
				}
				catch(DeviceNotReadyException)
				{
					string id = device!.GetId();
					device.Dispose();
					if(id.Length > 0)
						dsn.AddWhenReady(id, atStart: true);
				}
				catch(Exception e)
				{
					Logger.Error($"Failed to enumerate output devices", e);
				}
			}
		}
		return devices;
	}

	public OutputDeviceModel? CreateDeviceModel(string deviceId, IDeviceStateNotificationHandler dsn)
	{
		AudioDevice? device = null;
		try
		{
			device = new AudioDevice(Enumerator.GetDevice(deviceId));
			OutputDeviceModel? model = null;
			if(device.GetDataFlow() == DataFlow.Render && device.GetState() == DeviceState.Active)
				model = AudioModelFactory.CreateOutputDeviceModel(device, dsn, Hotkeys);
			if(model == null)
				ReleaseLater(device);
			return model;
		}
		catch(DeviceNotReadyException)
		{
			ReleaseLater(device);
			throw;
		}
		catch(Exception e) when(DeviceNotReadyException.IsTransient(e))
		{
			//the device was removed between Windows' notification and this read
			Logger.Debug("The output device is gone", e);
			ReleaseLater(device);
			return null;
		}
		catch(Exception e)
		{
			Logger.Error("Failed to create the model of an output device", e);
			ReleaseLater(device);
			return null;
		}
	}

	//a COM release waits for running audio callbacks, which may wait for the UI thread
	private static void ReleaseLater(AudioDevice? device)
	{
		if(device != null)
			_ = System.Threading.Tasks.Task.Run(device.Dispose);
	}
}

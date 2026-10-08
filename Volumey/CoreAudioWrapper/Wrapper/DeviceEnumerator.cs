using System;
using System.Collections.Generic;
using log4net;
using NAudio.CoreAudioApi;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public sealed class DeviceEnumerator : IDeviceEnumerator
	{
		private ILog logger;
		private ILog Logger => logger ??=LogManager.GetLogger(typeof(DeviceEnumerator));

		internal MMDeviceEnumerator Enumerator { get; }

		/// <summary>
		/// Given to the models it creates
		/// </summary>
		internal IHotkeyService Hotkeys { get; }

		public DeviceEnumerator(IHotkeyService hotkeys)
		{
			this.Hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
			this.Enumerator = new MMDeviceEnumerator();
		}

		public uint GetDeviceCount()
		{
			try
			{
				using var collection = this.Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
				return (uint)collection.Count;
			}
			catch(Exception e)
			{
				Logger.Error("Failed to get device count", e);
			}
			return 0;
		}

		public string GetDefaultDeviceId()
		{
			if(this.GetDeviceCount() == 0)
				return null;

			string id = string.Empty;
			try
			{
				if(!this.Enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var dev))
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
				activeDevices = this.Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
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
					try
					{
						var device = new AudioDevice(activeDevices[i]);
						var deviceModel = AudioModelFactory.CreateOutputDeviceModel(device, dsn, this.Hotkeys);
						if(deviceModel != null)
							devices.Add(deviceModel);
						else
							device.Dispose();
					}
					catch(Exception e)
					{
						Logger.Error($"Failed to enumerate output devices", e);
					}
				}
			}
			return devices;
		}
	}
}

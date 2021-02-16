using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public sealed class DeviceEnumerator : IDeviceEnumerator
	{
		private ILog logger;
		private ILog Logger => logger ??=LogManager.GetLogger(typeof(DeviceEnumerator));
		
		public IMMDeviceEnumerator MMDeviceEnumerator { get; }
		
		public DeviceEnumerator() => this.MMDeviceEnumerator = GetCOMEnumerator();

		public uint GetDeviceCount()
		{
			try
			{
				Marshal.ThrowExceptionForHR(this.MMDeviceEnumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.Active,
				                                                          out IMMDeviceCollection collection));
				collection.GetCount(out uint devicesCount);
				return devicesCount;
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
				var dev = this.MMDeviceEnumerator.GetDefaultOutputDevice();
				if(dev == null)
					return string.Empty;
				dev.GetId(out id);
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
			IMMDeviceCollection activeDevices = null;
			uint count = 0;

			try
			{
				Marshal.ThrowExceptionForHR(this.MMDeviceEnumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.Active, out activeDevices));
				activeDevices?.GetCount(out count);
				if(count == 0)
					return devices;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get output devices collection", e);
				return devices;
			}

			for(uint i = 0; i < count; i++)
			{
				try
				{
					IMMDevice dev = null;
					activeDevices?.Item(i, out dev);
					if(dev == null)
						continue;
					var device = new MMDevice(dev);
					var deviceModel = device.GetOutputDeviceModel(dsn);
					if(deviceModel != null)
						devices.Add(deviceModel);
				}
				catch(Exception e)
				{
					Logger.Error($"Failed to enumerate output devices", e);
				}
			}
			return devices;
		}

		private static IMMDeviceEnumerator GetCOMEnumerator()
		{
			MMDeviceEnumerator comObj = new MMDeviceEnumerator();
			IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator) comObj;
			return deviceEnumerator;
		}
	}
}
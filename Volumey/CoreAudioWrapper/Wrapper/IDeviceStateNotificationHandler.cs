using System;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IDeviceStateNotificationHandler : IMMNotificationClient, IDisposable
	{
		public event Action<string> DefaultDeviceChanged;
		public event Action<string> NameChanged;
		public event Action<string> IconPathChanged;
		public event Action<string> FormatChanged;
		public event Action<string> DeviceDisabled;
		public event Action<OutputDeviceModel> ActiveDeviceAdded;
	}
}
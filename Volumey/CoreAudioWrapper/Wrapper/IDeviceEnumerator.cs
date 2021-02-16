using System.Collections.Generic;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IDeviceEnumerator
	{
		IMMDeviceEnumerator MMDeviceEnumerator { get; }

		uint GetDeviceCount();

		string GetDefaultDeviceId();

		List<OutputDeviceModel> GetCurrentActiveDevices(IDeviceStateNotificationHandler dsn);
	}
}
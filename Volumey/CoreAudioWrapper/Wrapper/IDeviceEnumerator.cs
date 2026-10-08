using System.Collections.Generic;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IDeviceEnumerator
	{
		uint GetDeviceCount();

		string GetDefaultDeviceId();

		List<OutputDeviceModel> GetCurrentActiveDevices(IDeviceStateNotificationHandler dsn);
	}
}

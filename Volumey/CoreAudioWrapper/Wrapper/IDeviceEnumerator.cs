#nullable enable
using System.Collections.Generic;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IDeviceEnumerator
{
	uint GetDeviceCount();

	/// <summary>
	/// Null if there's no active output device, empty if there's no default one or it can't be read
	/// </summary>
	string? GetDefaultDeviceId();

	List<OutputDeviceModel> GetCurrentActiveDevices(IDeviceStateNotificationHandler dsn);

	/// <summary>
	/// Builds a new model of an active output device from fresh Windows objects. Null if it isn't an active output device or reading it failed.
	/// </summary>
	OutputDeviceModel? CreateDeviceModel(string deviceId, IDeviceStateNotificationHandler dsn);
}

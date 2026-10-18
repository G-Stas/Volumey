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

	/// <summary>
	/// The models of the active output devices. A device Windows refuses for a moment is left out and handed to
	/// <see cref="IDeviceStateNotificationHandler.AddWhenReady"/>.
	/// </summary>
	List<OutputDeviceModel> GetCurrentActiveDevices(IDeviceStateNotificationHandler dsn);

	/// <summary>
	/// Builds a new model of an active output device from fresh Windows objects. Null if it isn't an active output device or reading it failed.
	/// </summary>
	/// <exception cref="DeviceNotReadyException">Windows refused the device for a moment; it can be read again later</exception>
	OutputDeviceModel? CreateDeviceModel(string deviceId, IDeviceStateNotificationHandler dsn);
}

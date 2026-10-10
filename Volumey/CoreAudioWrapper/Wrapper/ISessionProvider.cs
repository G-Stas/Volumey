#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface ISessionProvider : IDisposable
{
	event Action<object, SessionCreatedEventArgs>? SessionCreated;

	/// <summary>
	/// A new session couldn't be read because the device's audio engine was reconfigured (AUDCLNT_E_DEVICE_INVALIDATED):
	/// the device's objects don't work anymore and have to be created again
	/// </summary>
	event Action? DeviceInvalidated;
}

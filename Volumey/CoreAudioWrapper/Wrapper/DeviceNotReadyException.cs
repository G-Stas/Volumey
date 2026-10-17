#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Windows refused to read an active output device for a moment: it is being reconfigured (format change, a driver restart)
/// or was removed while it was read. Reading it again a little later usually works.
/// </summary>
public sealed class DeviceNotReadyException : Exception
{
	private const int ErrorDeviceRemoved = unchecked((int)0x80070651);
	private const int DeviceInvalidated = unchecked((int)0x88890004);
	private const int UnsupportedFormat = unchecked((int)0x88890008);
	private const int DeviceInUse = unchecked((int)0x8889000A);
	private const int ServiceNotRunning = unchecked((int)0x88890010);
	private const int ResourcesInvalidated = unchecked((int)0x8889001E);

	public DeviceNotReadyException(Exception inner) : base(inner.Message, inner) { }

	/// <summary>True for the Windows errors of a device that can be read again a little later</summary>
	internal static bool IsTransient(Exception e)
		=> e.HResult is ErrorDeviceRemoved or DeviceInvalidated or UnsupportedFormat or DeviceInUse or ServiceNotRunning or ResourcesInvalidated;
}

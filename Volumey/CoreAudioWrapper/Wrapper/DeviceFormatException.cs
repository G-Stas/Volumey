#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Windows refused a step of setting a device's format; the message names the step and the formats involved
/// </summary>
public sealed class DeviceFormatException : Exception
{
	public DeviceFormatException(string message, Exception inner) : base(message, inner)
		=> HResult = inner.HResult;
}

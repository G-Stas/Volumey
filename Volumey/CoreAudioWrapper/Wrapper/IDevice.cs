#nullable enable
using System;
using System.Drawing;
using System.Windows.Media;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IDevice : IDisposable
{
	string GetId();

	string GetFriendlyName();

	string GetDeviceDesc();

	ImageSource? GetIconSource();

	/// <summary>
	/// A new icon owned by the caller, or null if the device has none (or it can't be read)
	/// </summary>
	Icon? GetIcon();

	DeviceFormat? GetDeviceFormat();
}

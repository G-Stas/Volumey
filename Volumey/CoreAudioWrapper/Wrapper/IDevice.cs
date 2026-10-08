using System;
using System.Drawing;
using System.Windows.Media;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IDevice : IDisposable
	{
		string GetId();

		string GetFriendlyName();

		string GetDeviceDesc();

		ImageSource GetIconSource();

		Icon GetIcon();

		DeviceFormat? GetDeviceFormat();
	}
}

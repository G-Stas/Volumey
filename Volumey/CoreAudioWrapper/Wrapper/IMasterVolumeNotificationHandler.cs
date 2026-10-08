using System;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IMasterVolumeNotificationHandler : IDisposable
	{
		event Action<VolumeChangedEventArgs> VolumeChanged;
	}
}

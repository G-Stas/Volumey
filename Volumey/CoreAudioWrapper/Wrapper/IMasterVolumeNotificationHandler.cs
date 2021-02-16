using System;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IMasterVolumeNotificationHandler : IAudioEndpointVolumeCallback, IDisposable
	{
		event Action<VolumeChangedEventArgs> VolumeChanged;
	}
}
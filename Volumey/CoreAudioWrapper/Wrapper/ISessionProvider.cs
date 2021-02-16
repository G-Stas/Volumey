using System;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface ISessionProvider : IAudioSessionNotification, IDisposable
	{
		event Action<AudioSessionModel> SessionCreated;
	}
}
using System;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface ISessionProvider : IAudioSessionNotification, IDisposable
	{
		event Action<object, SessionCreatedEventArgs> SessionCreated;
	}
}
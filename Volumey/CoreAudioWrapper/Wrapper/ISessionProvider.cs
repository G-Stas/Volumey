using System;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface ISessionProvider : IDisposable
	{
		event Action<object, SessionCreatedEventArgs> SessionCreated;
	}
}

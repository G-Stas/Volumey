using System;
using System.Windows.Media;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IAudioSessionStateNotifications : IDisposable
	{
		public event Action SessionEnded;
		public event Action<VolumeChangedEventArgs> VolumeChanged;
		public event Action<string> NameChanged;
		public event Action<AudioSessionDisconnectReason> Disconnected;
		public event Action<AudioSessionState> StateChanged;
	}
}

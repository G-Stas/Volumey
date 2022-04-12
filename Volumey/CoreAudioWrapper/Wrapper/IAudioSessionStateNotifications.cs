using System;
using System.Windows.Media;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IAudioSessionStateNotifications : IAudioSessionEvents, IDisposable
	{
		public event Action SessionEnded;
		public event Action<VolumeChangedEventArgs> VolumeChanged;
		public event Action<ImageSource> IconPathChanged;
		public event Action<string> NameChanged;
		public event Action<AudioSessionDisconnectReason> Disconnected;
		public event Action<AudioSessionState> StateChanged;
	}
}
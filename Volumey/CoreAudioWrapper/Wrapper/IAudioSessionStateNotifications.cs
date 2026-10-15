#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IAudioSessionStateNotifications : IDisposable
{
	public event Action? SessionEnded;
	/// <summary>The session stopped playing (also when its app exits: Windows keeps the session, inactive)</summary>
	public event Action? Deactivated;
	public event Action<VolumeChangedEventArgs>? VolumeChanged;
	public event Action<string>? NameChanged;
	public event Action<AudioSessionDisconnectReason>? Disconnected;
}

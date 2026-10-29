#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IAudioSessionStateNotifications : IDisposable
{
	public event Action? SessionEnded;
	/// <summary>The session stopped playing (also when its app exits: Windows keeps the session, inactive)</summary>
	public event Action? Deactivated;
	/// <summary>The session started playing</summary>
	public event Action? Activated;
	public event Action<VolumeChangedEventArgs>? VolumeChanged;
	public event Action<string>? NameChanged;
	public event Action<AudioSessionDisconnectReason>? Disconnected;
}

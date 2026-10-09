namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// The state of an audio session. Same values as the Core Audio AudioSessionState enum.
/// </summary>
public enum AudioSessionState
{
	Inactive,
	Active,
	Expired
}

/// <summary>
/// The reason an audio session was disconnected. Same values as the Core Audio AudioSessionDisconnectReason enum.
/// </summary>
public enum AudioSessionDisconnectReason
{
	DeviceRemoval,
	ServerShutdown,
	FormatChanged,
	SessionLogoff,
	SessionDisconnected,
	ExclusiveModeOverride
}

#nullable enable
namespace Volumey.CoreAudioWrapper.Wrapper;

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

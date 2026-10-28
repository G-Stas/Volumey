#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IAudioSessionVolume
{
	float GetVolume();

	/// <summary>
	/// Sets the volume. Implementations may write in the background, in which case failures are reported through <see cref="WriteFailed"/>
	/// </summary>
	void SetVolume(int newVolume, Guid context);

	bool GetMute();

	/// <param name="context">The event context of the write (the device volume: Volumey's own changes carry <see cref="GuidValue.Internal.VolumeGUID"/>
	/// and its notification is skipped, hotkey changes carry <see cref="GuidValue.Internal.Empty"/>); sessions ignore it (see SessionEchoFilter)</param>
	void SetMute(bool newState, Guid context);

	/// <summary>
	/// The highest sample level (0..1) of the last device period, before this volume is applied; 0 without a meter
	/// </summary>
	float GetPeak() => 0f;

	/// <summary>
	/// True while a write is queued or running (always false for volumes that write synchronously).
	/// It's read for every notification on the UI thread, so it must never block
	/// </summary>
	bool HasPendingWrites => false;

	/// <summary>
	/// Raised on a worker thread once the queued writes are done and at least one failed: the error and what the device really has now
	/// (null if even that couldn't be read)
	/// </summary>
	event Action<Exception, VolumeChangedEventArgs?>? WriteFailed { add { } remove { } }
}

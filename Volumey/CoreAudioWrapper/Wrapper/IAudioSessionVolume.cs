#nullable enable
using System;
using System.Threading.Tasks;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IAudioSessionVolume
{
	float GetVolume();

	/// <summary>
	/// Sets the volume. Implementations may write in the background, in which case failures are reported through <see cref="WritesCompleted"/>
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
	/// Raised on a worker thread each time the queued writes are done: the error of the last write that failed (null if none did)
	/// and what the device has now (null if it couldn't be read)
	/// </summary>
	event Action<Exception?, VolumeChangedEventArgs?>? WritesCompleted { add { } remove { } }

	/// <summary>
	/// Completes once the write in progress (if any) has finished. It doesn't block, so it can be awaited before the device is released
	/// </summary>
	Task WaitForWritesAsync() => Task.CompletedTask;
}

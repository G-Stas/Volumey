#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IAudioSessionVolume
{
	float GetVolume();

	void SetVolume(int newVolume, Guid context);

	bool GetMute();

	/// <param name="context">The event context of the write (the device volume: Volumey's own changes carry <see cref="GuidValue.Internal.VolumeGUID"/>
	/// and its notification is skipped, hotkey changes carry <see cref="GuidValue.Internal.Empty"/>); sessions ignore it (see SessionEchoFilter)</param>
	void SetMute(bool newState, Guid context);

	/// <summary>
	/// The highest sample level (0..1) of the last device period, before this volume is applied; 0 without a meter
	/// </summary>
	float GetPeak() => 0f;
}

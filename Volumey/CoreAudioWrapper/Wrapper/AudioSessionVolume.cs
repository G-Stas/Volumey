#nullable enable
using System;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Controls the volume of an audio session
/// </summary>
public sealed class AudioSessionVolume : IAudioSessionVolume
{
	private readonly SimpleAudioVolume sAudioVol;

	/// <summary>
	/// Recognizes session callbacks caused by the writes of this object
	/// </summary>
	internal SessionEchoFilter EchoFilter { get; }

	internal AudioSessionVolume(SimpleAudioVolume simpleAudioVolume)
	{
		sAudioVol = simpleAudioVolume ?? throw new ArgumentNullException(nameof(simpleAudioVolume));
		EchoFilter = new SessionEchoFilter(VolumePercent.FromScalar(GetVolume()), GetMute());
	}

	/// <summary>
	/// Get the volume of the audio session
	/// </summary>
	public float GetVolume() => sAudioVol.Volume;

	/// <summary>
	/// Set the volume of the audio session.
	/// The context is ignored: session volume is always written with an empty event context (see <see cref="SessionEchoFilter"/>).
	/// </summary>
	public void SetVolume(int newVolume, Guid context)
	{
		newVolume = Math.Clamp(newVolume, 0, 100);

		int systemSessionVolume = -1;
		try { systemSessionVolume = VolumePercent.FromScalar(GetVolume()); }
		catch { }

		//prevent setting volume in system if the audio session already has the same value
		if(systemSessionVolume != newVolume)
		{
			EchoFilter.OnVolumeWritten(newVolume);
			sAudioVol.Volume = VolumePercent.ToScalar(newVolume);
		}
	}

	/// <summary>
	/// Get the mute state of the current audio client.
	/// </summary>
	public bool GetMute() => sAudioVol.Mute;

	/// <summary>
	/// Set the mute state of the current audio client.
	/// </summary>
	//the context is ignored: NAudio drops it from session events, the echo filter recognizes Volumey's own writes instead
	public void SetMute(bool newState, Guid context)
	{
		EchoFilter.OnMuteWritten(newState);
		sAudioVol.Mute = newState;
	}
}

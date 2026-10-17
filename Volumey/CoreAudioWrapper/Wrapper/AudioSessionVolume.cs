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
	private readonly AudioMeterInformation? meter;

	/// <summary>
	/// Recognizes session callbacks caused by the writes of this object
	/// </summary>
	internal SessionEchoFilter EchoFilter { get; }

	internal AudioSessionVolume(SimpleAudioVolume simpleAudioVolume, AudioMeterInformation? meter = null)
	{
		sAudioVol = simpleAudioVolume ?? throw new ArgumentNullException(nameof(simpleAudioVolume));
		this.meter = meter;
		EchoFilter = new SessionEchoFilter(VolumePercent.FromScalar(GetVolume()), GetMute());
	}

	/// <summary>
	/// The session's peak meter (measured before the session volume). Read many times a second while the mixer shows level meters:
	/// a session that just ended fails, which reads as silence.
	/// </summary>
	public float GetPeak()
	{
		try
		{
			return meter?.MasterPeakValue ?? 0f;
		}
		catch
		{
			return 0f;
		}
	}

	/// <summary>
	/// Get the volume of the audio session
	/// </summary>
	public float GetVolume() => sAudioVol.Volume;

	/// <summary>
	/// Set the volume of the audio session. The caller writes only a value that differs from the one it knows,
	/// so the current volume isn't read from Windows first.
	/// The context is ignored: session volume is always written with an empty event context (see <see cref="SessionEchoFilter"/>).
	/// </summary>
	public void SetVolume(int newVolume, Guid context)
	{
		newVolume = Math.Clamp(newVolume, 0, 100);
		EchoFilter.OnVolumeWritten(newVolume);
		sAudioVol.Volume = VolumePercent.ToScalar(newVolume);
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

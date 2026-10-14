#nullable enable
using System;
using System.Threading.Tasks;
using log4net;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudioDisconnectReason = NAudio.CoreAudioApi.Interfaces.AudioSessionDisconnectReason;
using NAudioSessionState = NAudio.CoreAudioApi.Interfaces.AudioSessionState;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Provides notifications of session-related events such as changes in the volume level, display name, and session state.
/// Owns the session control and releases it on dispose.
/// </summary>
public sealed class AudioSessionStateNotifications : IAudioSessionStateNotifications, IAudioSessionEventsHandler
{
	public event Action? SessionEnded;
	public event Action? Deactivated;
	public event Action<VolumeChangedEventArgs>? VolumeChanged;
	public event Action<string>? NameChanged;
	public event Action<AudioSessionDisconnectReason>? Disconnected;
	private readonly AudioSessionControl sessionControl;
	private readonly SessionEchoFilter echoFilter;
	private bool disposed;

	internal AudioSessionStateNotifications(AudioSessionControl sControl, SessionEchoFilter echoFilter)
	{
		sessionControl = sControl;
		this.echoFilter = echoFilter;
	}

	public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }

	public void OnDisplayNameChanged(string newDisplayName)
		=> NameChanged?.Invoke(newDisplayName);

	public void OnGroupingParamChanged(ref Guid groupingId) { }

	//a session's icon is read once when it's created; changes aren't shown
	public void OnIconPathChanged(string newIconPath) { }

	public void OnSessionDisconnected(NAudioDisconnectReason disconnectReason)
		=> Disconnected?.Invoke((AudioSessionDisconnectReason)disconnectReason);

	public void OnVolumeChanged(float newVolume, bool newMuteState)
	{
		int newVolumeValue = VolumePercent.FromScalar(newVolume);

		//don't fire event if callback was called because of changes of volume inside the app.
		//Checked first: it needs no calls to Windows, and most callbacks are echoes while the app changes the volume
		if(echoFilter != null && echoFilter.IsOwnWrite(newVolumeValue, newMuteState))
			return;

		//prevent invoking event if the new state is not the same as the actual session state (an outdated callback)
		try
		{
			var simpleVolume = sessionControl.SimpleAudioVolume;
			if(newVolumeValue != VolumePercent.FromScalar(simpleVolume.Volume) || newMuteState != simpleVolume.Mute)
				return;
		}
		catch
		{
			return;
		}

		echoFilter?.Remember(newVolumeValue, newMuteState);
		VolumeChanged?.Invoke(new VolumeChangedEventArgs(newVolumeValue, newMuteState));
	}

	public void OnStateChanged(NAudioSessionState newState)
	{
		if(newState == NAudioSessionState.AudioSessionStateExpired)
			SessionEnded?.Invoke();
		else if(newState == NAudioSessionState.AudioSessionStateInactive)
			Deactivated?.Invoke();
	}

	public void RegisterNotifications()
		=> sessionControl.RegisterEventClient(this);

	public void Dispose()
	{
		if(disposed)
			return;
		disposed = true;
		//on the thread pool: unregistering and releasing wait for running session callbacks, and those may wait for the UI thread,
		//where most disposals happen (the same deadlock as releasing a device on the UI thread)
		var control = sessionControl;
		_ = Task.Run(() =>
		{
			try { control.UnRegisterEventClient(this); }
			catch { }
			try { control.Dispose(); }
			catch(Exception e) { LogManager.GetLogger(typeof(AudioSessionStateNotifications)).Warn("Failed to release an audio session", e); }
		});
	}
}

public readonly struct VolumeChangedEventArgs
{
	public int NewVolume { get; }
	public bool IsMuted { get; }

	public VolumeChangedEventArgs(int newVol, bool newState)
	{
		NewVolume = newVol;
		IsMuted = newState;
	}
}

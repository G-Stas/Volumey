#nullable enable
using System;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Provides notifications of changes in the volume level and muting state of an audio endpoint device.
/// </summary>
class MasterVolumeNotificationHandler : IMasterVolumeNotificationHandler
{
	public event Action<VolumeChangedEventArgs>? VolumeChanged;
	private readonly AudioEndpointVolume endpointVolume;

	public MasterVolumeNotificationHandler(AudioEndpointVolume eVolume)
	{
		endpointVolume = eVolume;
	}

	//NAudio raises it on the synchronization context the endpoint volume was created on (the UI thread), or on a COM thread without one
	private void OnNotify(AudioVolumeNotificationData data)
	{
		//don't fire event if callback was called because of changes of volume inside the app
		if(IsOwnVolumeChange(data.EventContext))
			return;

		int newVolumeValue = VolumePercent.FromScalar(data.MasterVolume);

		//prevent invoking event if new volume value is not the same as the actual device volume value
		int actualVolumeValue;
		try { actualVolumeValue = VolumePercent.FromScalar(endpointVolume.MasterVolumeLevelScalar); }
		catch { return; }
		if(newVolumeValue != actualVolumeValue)
			return;
		VolumeChanged?.Invoke(new VolumeChangedEventArgs(newVolumeValue, data.Muted));
	}

	/// <summary>
	/// A change the mixer made itself, which it already shows. Balance changes aren't: they can move the device volume (see <see cref="GuidValue.Internal.BalanceGUID"/>)
	/// </summary>
	internal static bool IsOwnVolumeChange(Guid context) => context.Equals(GuidValue.Internal.VolumeGUID);

	public void RegisterMVolumeNotifications() => endpointVolume.OnVolumeNotification += OnNotify;

	public void Dispose()
	{
		endpointVolume.OnVolumeNotification -= OnNotify;
		VolumeChanged = null;
	}
}

using System;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notifications of changes in the volume level and muting state of an audio endpoint device.
    /// </summary>
    class MasterVolumeNotificationHandler : IMasterVolumeNotificationHandler
    {
        public event Action<VolumeChangedEventArgs> VolumeChanged;
        private readonly AudioEndpointVolume endpointVolume;

        public MasterVolumeNotificationHandler(AudioEndpointVolume eVolume)
        {
            this.endpointVolume = eVolume;
        }

        //NAudio raises it on the synchronization context the endpoint volume was created on (the UI thread), or on a COM thread without one
        private void OnNotify(AudioVolumeNotificationData data)
        {
            //don't fire event if callback was called because of changes of volume inside the app
            if(data.EventContext.Equals(GuidValue.Internal.VolumeGUID))
                return;

            int newVolumeValue = Convert.ToInt32(data.MasterVolume * 100);

            //prevent invoking event if new volume value is not the same as the actual device volume value
            int actualVolumeValue;
            try { actualVolumeValue = Convert.ToInt32(this.endpointVolume.MasterVolumeLevelScalar * 100); }
            catch { return; }
            if(newVolumeValue != actualVolumeValue)
                return;
            this.VolumeChanged?.Invoke(new VolumeChangedEventArgs(newVolumeValue, data.Muted));
        }

        public void RegisterMVolumeNotifications() => this.endpointVolume.OnVolumeNotification += OnNotify;

        public void Dispose()
        {
            this.endpointVolume.OnVolumeNotification -= OnNotify;
            this.VolumeChanged = null;
        }
    }
}

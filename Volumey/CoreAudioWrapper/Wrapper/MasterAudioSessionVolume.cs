using System;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Controls the volume of a master session
    /// </summary>
    class MasterAudioSessionVolume : IAudioSessionVolume
    {
        private readonly AudioEndpointVolume audioVolume;

        public MasterAudioSessionVolume(AudioEndpointVolume aVol)
           => audioVolume = aVol;

        /// <summary>
        /// Get's the master volume level as a uniform scaling factor.
        /// </summary>
        public float GetVolume() => this.audioVolume.MasterVolumeLevelScalar;

        /// <summary>
        /// Sets the master volume level, using a uniform scaling factor.
        /// </summary>
        public void SetVolume(int volumeLevel, ref Guid context)
        {
            if(volumeLevel < 0)
                volumeLevel = 0;
            if(volumeLevel > 100)
                volumeLevel = 100;

            int sysVol = -1;
            try { sysVol = Convert.ToInt32(this.GetVolume() * 100); }
            catch { }

            //prevent setting volume in system if the master session already has the same value
            if(sysVol != volumeLevel)
            {
                //NAudio passes NotificationGuid as the event context of its writes
                lock(this.audioVolume)
                {
                    this.audioVolume.NotificationGuid = context;
                    this.audioVolume.MasterVolumeLevelScalar = volumeLevel / 100.0f;
                }
            }
        }

        /// <summary>
        /// Get the mute state for the master volume.
        /// </summary>
        public bool GetMute() => this.audioVolume.Mute;

        /// <summary>
        /// Set the mute state for the master volume.
        /// </summary>
        public void SetMute(bool muteState)
        {
            lock(this.audioVolume)
            {
                this.audioVolume.NotificationGuid = GuidValue.Internal.VolumeGUID;
                this.audioVolume.Mute = muteState;
            }
        }
    }
}

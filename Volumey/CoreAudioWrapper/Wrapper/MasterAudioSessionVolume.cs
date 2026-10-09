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
        //own lock object: locking the NAudio object could interfere with locks NAudio takes on it
        private readonly System.Threading.Lock writeLock = new();

        public MasterAudioSessionVolume(AudioEndpointVolume aVol)
           => audioVolume = aVol;

        /// <summary>
        /// Get's the master volume level as a uniform scaling factor.
        /// </summary>
        public float GetVolume() => this.audioVolume.MasterVolumeLevelScalar;

        /// <summary>
        /// Sets the master volume level, using a uniform scaling factor.
        /// </summary>
        public void SetVolume(int volumeLevel, Guid context)
        {
            volumeLevel = Math.Clamp(volumeLevel, 0, 100);

            int sysVol = -1;
            try { sysVol = AudioSessionVolume.ToPercent(this.GetVolume()); }
            catch { }

            //prevent setting volume in system if the master session already has the same value
            if(sysVol != volumeLevel)
            {
                //NAudio passes NotificationGuid as the event context of its writes
                lock(this.writeLock)
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
            lock(this.writeLock)
            {
                this.audioVolume.NotificationGuid = GuidValue.Internal.VolumeGUID;
                this.audioVolume.Mute = muteState;
            }
        }
    }
}

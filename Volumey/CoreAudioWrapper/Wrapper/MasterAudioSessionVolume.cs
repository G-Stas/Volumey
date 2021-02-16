using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Controls the volume of a master session
    /// </summary>
    class MasterAudioSessionVolume : IAudioSessionVolume
    {
        private readonly IAudioEndpointVolume audioVolume;

        public MasterAudioSessionVolume(IAudioEndpointVolume aVol)
           => audioVolume = aVol;

        /// <summary>
        /// Get's the master volume level as a uniform scaling factor.
        /// </summary>
        public float GetVolume()
        {
            Marshal.ThrowExceptionForHR(this.audioVolume.GetMasterVolumeLevelScalar(out float volumeLevel));
            return volumeLevel;
        }

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
                float level = volumeLevel / 100.0f;
                Marshal.ThrowExceptionForHR(this.audioVolume.SetMasterVolumeLevelScalar(level, ref context));
            }
        }

        /// <summary>
        /// Get the mute state for the master volume.
        /// </summary>
        public bool GetMute()
        {
            Marshal.ThrowExceptionForHR(this.audioVolume.GetMute(out bool muteState));
            return muteState;
        }

        /// <summary>
        /// Set the mute state for the master volume.
        /// </summary>
        public void SetMute(bool muteState) 
            => Marshal.ThrowExceptionForHR(this.audioVolume.SetMute(muteState, ref GuidValue.Internal.VolumeGUID));
    }
}

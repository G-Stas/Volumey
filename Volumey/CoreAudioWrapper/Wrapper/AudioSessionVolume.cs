using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Controls the volume of an audio session
    /// </summary>
    public sealed class AudioSessionVolume : IAudioSessionVolume
    {
        private ISimpleAudioVolume sAudioVol { get; set; }

        public AudioSessionVolume(IAudioSessionControl2 sessionControl) 
            => sAudioVol = (ISimpleAudioVolume)sessionControl;
        
        /// <summary>
        /// Get the volume of the audio session
        /// </summary>
        public float GetVolume()
        {
            Marshal.ThrowExceptionForHR(sAudioVol.GetMasterVolume(out float volumeLevel));
            return volumeLevel;
        }
        
        /// <summary>
        /// Set the volume of the audio session
        /// </summary>
        public void SetVolume(int newVolume, ref Guid context)
        {
            newVolume = ConstraintVolume(newVolume);

            int systemSessionVolume = -1;
            try { systemSessionVolume = Convert.ToInt32(this.GetVolume() * 100); }
            catch { }
            
            //prevent setting volume in system if the audio session already has the same value
            if(systemSessionVolume != newVolume)
            {
                float volumeLevel = newVolume / 100.0f;
                Marshal.ThrowExceptionForHR(sAudioVol.SetMasterVolume(volumeLevel, ref context));
            }
        }
        
        /// <summary>
        /// Get the mute state of the current audio client.
        /// </summary>
        public bool GetMute()
        {
            Marshal.ThrowExceptionForHR(sAudioVol.GetMute(out bool muteState));
            return muteState;
        }
        
        /// <summary>
        /// Set the mute state of the current audio client.
        /// </summary>
        public void SetMute(bool newState) 
            => Marshal.ThrowExceptionForHR(sAudioVol.SetMute(newState, ref GuidValue.Internal.VolumeGUID));
        
        private static int ConstraintVolume(int vol) => vol < 0 ? 0 : (vol > 100 ? 100 : vol);
    }
}
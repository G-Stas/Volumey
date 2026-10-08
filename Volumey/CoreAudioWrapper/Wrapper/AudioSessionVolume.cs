using System;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper
{
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
            this.sAudioVol = simpleAudioVolume ?? throw new ArgumentNullException(nameof(simpleAudioVolume));
            this.EchoFilter = new SessionEchoFilter(ToPercent(this.GetVolume()), this.GetMute());
        }

        /// <summary>
        /// Get the volume of the audio session
        /// </summary>
        public float GetVolume() => this.sAudioVol.Volume;

        /// <summary>
        /// Set the volume of the audio session.
        /// The context is ignored: session volume is always written with an empty event context (see <see cref="SessionEchoFilter"/>).
        /// </summary>
        public void SetVolume(int newVolume, ref Guid context)
        {
            newVolume = ConstraintVolume(newVolume);

            int systemSessionVolume = -1;
            try { systemSessionVolume = ToPercent(this.GetVolume()); }
            catch { }

            //prevent setting volume in system if the audio session already has the same value
            if(systemSessionVolume != newVolume)
            {
                this.EchoFilter.OnVolumeWritten(newVolume);
                this.sAudioVol.Volume = newVolume / 100.0f;
            }
        }

        /// <summary>
        /// Get the mute state of the current audio client.
        /// </summary>
        public bool GetMute() => this.sAudioVol.Mute;

        /// <summary>
        /// Set the mute state of the current audio client.
        /// </summary>
        public void SetMute(bool newState)
        {
            this.EchoFilter.OnMuteWritten(newState);
            this.sAudioVol.Mute = newState;
        }

        internal static int ToPercent(float volume) => Convert.ToInt32(volume * 100);

        private static int ConstraintVolume(int vol) => vol < 0 ? 0 : (vol > 100 ? 100 : vol);
    }
}

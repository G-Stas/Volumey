using System;

namespace Volumey.CoreAudioWrapper.CoreAudio
{
    struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public bool isMuted;
        public float masterVolume;
        public uint channelsCount;
        public float channelVolume;
    }
}

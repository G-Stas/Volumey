﻿using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notifications of changes in the volume level and muting state of an audio endpoint device. 
    /// </summary>
    class MasterVolumeNotificationHandler : IMasterVolumeNotificationHandler
    {
        public event Action<VolumeChangedEventArgs> VolumeChanged;

        private int prevVolume = -1;
        private bool prevMuteState;
        
        private IAudioEndpointVolume endpointVolume;

        public MasterVolumeNotificationHandler(IAudioEndpointVolume eVolume)
        {
            this.endpointVolume = eVolume;
        }

        public int OnNotify(IntPtr notifyData)
        {
            //https://github.com/naudio/NAudio/blob/master/NAudio/CoreAudioApi/AudioEndpointVolumeCallback.cs
            //"Since AUDIO_VOLUME_NOTIFICATION_DATA is dynamic in length based on the
            //number of audio channels available we cannot just call PtrToStructure 
            //to get all data, thats why it is split up into two steps, first the static
            //data is marshalled into the data structure, then with some IntPtr math the
            //remaining floats are read from memory."

            //get only static data:
            AUDIO_VOLUME_NOTIFICATION_DATA data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(notifyData);

            //don't fire event if callback was called because of changes of volume inside the app
            if(data.guidEventContext.Equals(GuidValue.Internal.VolumeGUID))
                return 0;
            
            int newVolume = Convert.ToInt32(data.masterVolume * 100);
            
            if(prevVolume != newVolume || prevMuteState != data.isMuted)
            {
                prevVolume = newVolume;
                prevMuteState = data.isMuted;
                this.VolumeChanged?.Invoke(new VolumeChangedEventArgs(newVolume, data.isMuted));
            }
            
            Marshal.DestroyStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(notifyData);
            return 0;
        }

        public void RegisterMVolumeNotifications() => 
            Marshal.ThrowExceptionForHR(this.endpointVolume.RegisterControlChangeNotify(this));

        private void UnregisterMVolumeNotifications() =>
            this.endpointVolume.UnregisterControlChangeNotify(this);

        public void Dispose()
        {
            this.UnregisterMVolumeNotifications();
            this.VolumeChanged = null;
        }
    }
}

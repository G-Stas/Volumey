using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IAudioSessionEvents), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionEvents
    {
        [PreserveSig]
        int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string newDisplayName, ref Guid eventContext);

        [PreserveSig]
        int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string newIconPath, ref Guid eventContext);
        
        [PreserveSig]
        int OnSimpleVolumeChanged(float newVolume, bool newMuteState, ref Guid eventContext);

        /// <summary>
        /// Called when the channel volume of an AudioSession changes.
        /// </summary>
        /// <param name="channelCount">The number of channels in the channel array.</param>
        /// <param name="channelVolumes">An array containing the new channel volumes.</param>
        /// <param name="changedChannel">-1 if all channnels were changed, otherwise the channel volume which changed, 0..ChannelCount-1</param>
        /// <param name="eventContext">Context passed to SetVolume routine.</param>
        /// <returns></returns>
        [PreserveSig]
        int OnChannelVolumeChanged(uint channelCount, float[] channelVolumes, int changedChannel, ref Guid eventContext);
        
        [PreserveSig]
        int OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext);
        
        [PreserveSig]
        int OnStateChanged(AudioSessionState newState);

        [PreserveSig]
        int OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason);
    }
}

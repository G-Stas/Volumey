using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{    
    [Guid(GuidValue.External.ISimpleAudioVolume), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        /// <summary>
        /// Set the master volume of the current audio client.
        /// </summary>
        /// <param name="level">New amplitude for the audio stream.</param>
        /// <param name="eventContext">Context passed to notification routine, GUID_NULL if NULL.</param>
        /// <returns></returns>
        int SetMasterVolume([In][MarshalAs(UnmanagedType.R4)] float level, [In][MarshalAs(UnmanagedType.LPStruct)] ref Guid eventContext);

        /// <summary>
        /// Get the master volume of the current audio client.
        /// </summary>
        /// <param name="volumeLevel">New amplitude for the audio stream.</param>
        /// <returns></returns>
        int GetMasterVolume([Out][MarshalAs(UnmanagedType.R4)] out float volumeLevel);

        /// <summary>
        /// Set the mute state of the current audio client.
        /// </summary>
        int SetMute([In][MarshalAs(UnmanagedType.Bool)] bool newState, [In][MarshalAs(UnmanagedType.LPStruct)] ref Guid eventContext);

        /// <summary>
        /// Get the mute state of the current audio client.
        /// </summary>
        int GetMute([Out][MarshalAs(UnmanagedType.Bool)] out bool state);
    }
}

using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IAudioEndpointVolume), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        /// <summary>
        /// Registers the client for change notifications on this volume control.
        /// </summary>
        /// <param name="endpointVolCallback">Notification interface that gets called when volume changes.</param>
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback endpointVolCallback);

        /// <summary>
        /// Unregisters the client for change notifications on this volume control.
        /// </summary>
        /// <param name="endpointVolCallback">Notification interface instance to unregister.</param>
        [PreserveSig]
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback endpointVolCallback);

        int GetChannelCount(out uint channelCount);

        /// <summary>
        /// Not implemented
        /// </summary>
        int SetMasterVolumeLevel();

        /// <summary>
        /// Sets the master volume level, using a uniform scaling factor.
        /// </summary>
        /// <param name="volumeLevel">The new volume level, a uniform scaling factor valued (0 to 1.0)</param>
        /// <param name="eventContext"></param>
        /// <returns></returns>
        int SetMasterVolumeLevelScalar([In][MarshalAs(UnmanagedType.R4)] float volumeLevel, [In][MarshalAs(UnmanagedType.LPStruct)] ref Guid eventContext);

        /// <summary>
        /// Not implemented
        /// </summary>
        int GetMasterVolumeLevel();

        /// <summary>
        /// Get's the master volume level as a uniform scaling factor.
        /// </summary>
        /// <param name="volumeLevel">Current master volume level as a uniform scaling factor (0 to 1.0 valid range).</param>
        /// <returns></returns>
        int GetMasterVolumeLevelScalar([Out][MarshalAs(UnmanagedType.R4)] out float volumeLevel);

        /// <summary>
        /// Not implemented
        /// </summary>
        int SetChannelVolumeLevel();

        /// <summary>
        /// Not implemented
        /// </summary>
        int SetChannelVolumeLevelScalar();

        /// <summary>
        /// Not implemented
        /// </summary>
        int GetChannelVolumeLevel();

        /// <summary>
        ///Get the volume level for the specified channel as a uniform scaling factor (0 to 1 linear range).
        /// </summary>
        int GetChannelVolumeLevelScalar(uint channelNum, out float volLevel);

        /// <summary>
        /// Set the mute state
        /// </summary>
        /// <param name="muteState">TRUE to mute, FALSE to un-mute.</param>
        /// <param name="eventContext"></param>
        int SetMute([In][MarshalAs(UnmanagedType.Bool)] bool muteState, [In][MarshalAs(UnmanagedType.LPStruct)] ref Guid eventContext);

        /// <summary>
        /// Get the mute state
        /// </summary>
        /// <param name="muteState">The current mute state (TRUE = muted, FALSE = not muted).</param>
        int GetMute([Out][MarshalAs(UnmanagedType.Bool)] out bool muteState);
    }
}

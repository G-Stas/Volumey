using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IAudioSessionControl), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionControl
    {
        int GetState(out AudioSessionState state);

        /// <summary>
        /// If the client has not called IAudioSessionControl::SetDisplayName to set the display name, the string will be empty.
        /// </summary>
        /// <param name="sDisplayName"></param>
        /// <returns></returns>
        int GetDisplayName([Out][MarshalAs(UnmanagedType.LPWStr)] out string sDisplayName);
        int SetDisplayName([In][MarshalAs(UnmanagedType.LPWStr)] string displayName, Guid context);
        int GetIconPath([Out][MarshalAs(UnmanagedType.LPWStr)] out string shellResource);
        /// <summary>
        /// Not implemented
        /// </summary>
        int SetIconPath();
        /// <summary>
        /// Not implemented
        /// </summary>
        int GetGroupingParam();
        /// <summary>
        /// Not implemented
        /// </summary>
        int SetGroupingParam();
        int RegisterAudioSessionNotification(IAudioSessionEvents notificationReceiver);
        int UnregisterAudioSessionNotification(IAudioSessionEvents notificationHolder);
    }
}

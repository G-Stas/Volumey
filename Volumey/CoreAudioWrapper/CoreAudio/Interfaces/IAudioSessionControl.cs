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
        /// All of the audio sessions that have the same grouping parameter value are under the control of the same volume-level slider in the system volume-control program, Sndvol. For more information, see Grouping Parameters.
        /// If a client has never called SetGroupingParam to assign a grouping parameter to an audio session, the session's grouping parameter value is GUID_NULL by default
        /// and a call to GetGroupingParam retrieves this value.
        /// A grouping parameter value of GUID_NULL indicates that the session does not belong to any grouping.
        /// In that case, the session has its own volume-level slider in the Sndvol program.
        /// </summary>
        int GetGroupingParam([Out] out Guid groupingParam);
        
        /// <summary>
        /// Not implemented
        /// </summary>
        int SetGroupingParam();
        int RegisterAudioSessionNotification(IAudioSessionEvents notificationReceiver);
        int UnregisterAudioSessionNotification(IAudioSessionEvents notificationHolder);
    }
}

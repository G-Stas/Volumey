using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IAudioSessionControl2), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionControl2 : IAudioSessionControl
    {
        #region IAudioSessionControl functions
        new int GetState(out AudioSessionState state);
        new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string sDisplayName);
        new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string displayName, Guid context);
        new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string shellResource);
        /// <summary>
        /// Not implemented
        /// </summary>
        new int SetIconPath();
        /// <summary>
        /// Not implemented
        /// </summary>
        new int GetGroupingParam();
        /// <summary>
        /// Not implemented
        /// </summary>
        new int SetGroupingParam();
        
        new int RegisterAudioSessionNotification(IAudioSessionEvents notificationReceiver);

        [PreserveSig]
        new int UnregisterAudioSessionNotification(IAudioSessionEvents notificationHolder);
        #endregion

	    #region IAudioSessionControl2 functions

        /// <summary>
        /// Retrieves the AudioSession ID.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string sessionId);

        /// <summary>
        /// Retrieves the AudioSession instance ID.
        /// </summary>
        /// <param name="sessionInstanceId"></param>
        /// <returns></returns>
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string sessionInstanceId);

        /// <summary>
        /// Retrieves the AudioSession Process ID.
        /// </summary>
        /// <returns></returns>
        int GetProcessId([Out][MarshalAs(UnmanagedType.U4)] out uint processId);

        /// <summary>
        /// Indicates whether the session is a system sounds session.
        /// </summary>
        /// <returns>S_OK - The session is a system sounds session. S_FALSE - The session is not a system sounds session.</returns>
        [PreserveSig]
        int IsSystemSoundsSession();
        #endregion
    }
}

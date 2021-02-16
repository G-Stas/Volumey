using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IMMNotificationClient), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        /// <summary>
        /// Called when the state of an Endpoint device changes
        /// </summary>
        /// <param name="deviceId">The ID of the Endpoint device whose state has changed</param>
        /// <param name="newState">The new state of the device</param>
        [PreserveSig]
        int OnDeviceStateChanged(string deviceId, DeviceState newState);

        /// <summary>
        /// Called when a new Endpoint device is added to the system
        /// </summary>
        /// <param name="deviceId">The ID of the new Endpoint device</param>
        [PreserveSig]
        int OnDeviceAdded(string deviceId);

        /// <summary>
        /// Called when an Endpoint device is removed from the system
        /// </summary>
        /// <param name="deviceId">The ID of the Endpoint device that was removed</param>
        [PreserveSig]
        int OnDeviceRemoved(string deviceId);

        /// <summary>
        /// Called when the default Endpoint device for a given role changes
        /// </summary>
        /// <param name="flow">The dataflow direction</param>
        /// <param name="role">The role</param>
        /// <param name="deviceId">The ID of the Endpoint device that is now the default for the specified role</param>
        [PreserveSig]
        int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string deviceId);

        [PreserveSig]
        int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PROPERTYKEY key);
    }
}

using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IMMDeviceEnumerator), InterfaceType(ComInterfaceType.InterfaceIsIUnknown),]
    public interface IMMDeviceEnumerator
    {
        /// <summary>
        /// Enumerates Endpoint devices
        /// </summary>
        /// <param name="flow">The dataflow direction of Endpoint devices to enumerate</param>
        /// <param name="state">The allowed states to filter by.  Typically, this is DEVICE_STATE_ACTIVE</param>
        /// <param name="collection">Devices collection</param>
        int EnumAudioEndpoints([In] EDataFlow flow, [In] DeviceState state, [Out] out IMMDeviceCollection collection);

        /// <summary>
        /// Retrieves the default audio endpoint for the specified data-flow direction and role.
        /// The caller is responsible for releasing the interface, when it is no longer needed, by calling the interface's Release method.
        /// </summary>
        /// <param name="dataflow">The data-flow direction for the endpoint device.</param>
        /// <param name="role">The role of the endpoint device.</param>
        /// <param name="MMDevice">Pointer to a pointer variable into which the method writes the address of the IMMDevice interface of the endpoint object for the default audio endpoint device</param>
        int GetDefaultAudioEndpoint([In] EDataFlow dataflow, [In] ERole role, [Out] out IMMDevice MMDevice);

        /// <summary>
        /// Gets the device with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the device to retrieve</param>
        /// <param name="device">Address of a pointer that will receive the device</param>
        int GetDevice([In][MarshalAs(UnmanagedType.LPWStr)] string id, [Out] out IMMDevice device);

        /// <summary>
        /// Registers the specified client to receive Endpoint device notifications
        /// </summary>
        /// <param name="client">Pointer to an IMMNotificationClient interface on an object implemented by the client</param>
        int RegisterEndpointNotificationCallback([In] IMMNotificationClient client);

        /// <summary>
        /// Unregisters a client that was registered in a previous call to RegisterEndpointNotificationCallback
        /// </summary>
        /// <param name="client">The client to unregister</param>
        [PreserveSig]
        int UnregisterEndpointNotificationCallback([In] IMMNotificationClient client);

        IMMDevice GetDefaultOutputDevice()
        {
            this.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out IMMDevice device);
            return device;
        }
    }
}

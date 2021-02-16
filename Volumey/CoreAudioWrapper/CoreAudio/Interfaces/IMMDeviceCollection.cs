using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    [Guid(GuidValue.External.IMMDeviceCollection), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        /// <summary>
        /// Returns the number of devices in the collection
        /// </summary>
        /// <param name="devicesCount"></param>
        int GetCount([Out][MarshalAs(UnmanagedType.U4)] out uint devicesCount);

        /// <summary>
        /// Gets the device at the specified index in the collection
        /// </summary>
        /// <param name="deviceIndex">The index</param>
        /// <param name="device"></param>
        int Item([In][MarshalAs(UnmanagedType.U4)] uint deviceIndex, out IMMDevice device);
    }
}
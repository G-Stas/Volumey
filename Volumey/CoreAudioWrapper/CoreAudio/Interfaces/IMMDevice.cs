using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
	[Guid(GuidValue.External.IMMDevice), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMMDevice
	{
		/// <summary>
		/// The Activate method creates a COM object with the specified interface.
		/// </summary>
		/// <param name="iid">The interface identifier.</param>
		/// <param name="context">The execution context in which the code that manages the newly created object will run. </param>
		/// <param name="activationParams">Set to NULL to activate an IAudioClient, IAudioEndpointVolume, IAudioMeterInformation, IAudioSessionManager, or IDeviceTopology interface on an audio endpoint device.</param>
		/// <param name="o"></param>
		int Activate(ref Guid iid, uint context, IntPtr activationParams,
			[MarshalAs(UnmanagedType.IUnknown)] out object o);

		/// <summary>
		/// Retrieves an interface to the device's property store.
		/// </summary>
		/// <param name="stgmAccess">The storage-access mode.</param>
		/// <param name="propertyStore">This interface exposes methods used to enumerate and manipulate property values.</param>
		/// <returns></returns>
		int OpenPropertyStore([MarshalAs(UnmanagedType.U4)] STGM stgmAccess, out IPropertyStore propertyStore);

		[PreserveSig]
		int GetId([Out][MarshalAs(UnmanagedType.LPWStr)] out string s);

		int GetState([MarshalAs(UnmanagedType.U4)] out DeviceState state);
	}
}
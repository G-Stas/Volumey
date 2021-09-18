using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
	[Guid(GuidValue.External.IPolicyConfig), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPolicyConfig
	{
		[PreserveSig]
		int GetMixFormat(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In] IntPtr ppFormat);

		[PreserveSig]
		int GetDeviceFormat(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
			[In] IntPtr ppFormat);

		[PreserveSig]
		int ResetDeviceFormat([In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

		[PreserveSig]
		int SetDeviceFormat(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In] IntPtr pEndpointFormat,
			[In] IntPtr mixFormat);

		[PreserveSig]
		int GetProcessingPeriod(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
			[In] IntPtr pmftDefaultPeriod,
			[In] IntPtr pmftMinimumPeriod);

		[PreserveSig]
		int SetProcessingPeriod(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In] IntPtr pmftPeriod);

		[PreserveSig]
		int GetShareMode(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In] IntPtr pMode);

		[PreserveSig]
		int SetShareMode(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In] IntPtr mode);

		[PreserveSig]
		int GetPropertyValue(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
			[In] IntPtr key,
			[In] IntPtr pv);

		[PreserveSig]
		int SetPropertyValue(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
			[In] IntPtr key,
			[In] IntPtr pv);

		[PreserveSig]
		int SetDefaultEndpoint(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.U4)] ERole role);

		[PreserveSig]
		int SetEndpointVisibility(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
			[In][MarshalAs(UnmanagedType.Bool)] bool bVisible);
	}
}
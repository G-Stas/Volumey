#nullable enable
using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

/// <summary>
/// Raw calls into Windows' undocumented audio policy factory (WinRT class Windows.Media.Internal.AudioPolicyConfig in AudioSes.dll).
/// The methods are on the activation factory itself. .NET has no WinRT string marshalling, so the strings are raw HSTRING handles
/// and the methods are called through their vtable slots, which are only valid for an interface ID listed in <see cref="KnownIids"/>.
/// </summary>
internal static unsafe class AudioPolicyConfigFactory
{
	internal const string ClassName = "Windows.Media.Internal.AudioPolicyConfig";

	/// <summary>Windows 11 (build 21390 and later)</summary>
	internal static readonly Guid Iid21390 = new("ab3d4648-e242-459f-b02f-541c70306324");

	/// <summary>Windows 10 1803 up to build 21390</summary>
	internal static readonly Guid IidDownlevel = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");

	/// <summary>The interface IDs whose vtable layout is known (the same for both), in order of preference</summary>
	internal static readonly Guid[] KnownIids = [Iid21390, IidDownlevel];

	private static readonly Guid IidInspectable = new("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");

	//EDataFlow.eRender
	private const int Render = 0;

	//IUnknown (0-2), IInspectable (3-5), 19 methods Volumey doesn't use (6-24)
	private const int SlotGetIids = 3;
	private const int SlotSetPersistedDefaultAudioEndpoint = 25;
	private const int SlotGetPersistedDefaultAudioEndpoint = 26;

	internal const int E_INVALIDARG = unchecked((int)0x80070057);

	/// <summary>The factory as an IInspectable: through AudioSes.dll's export first, then through the WinRT registry</summary>
	internal static int Activate(out IntPtr factory)
	{
		IntPtr className = CreateString(ClassName);
		try
		{
			int hr = DllGetActivationFactory(className, out factory);
			if(hr >= 0)
				return hr;
			return RoGetActivationFactory(className, IidInspectable, out factory);
		}
		finally
		{
			WindowsDeleteString(className);
		}
	}

	internal static int GetIids(IntPtr inspectable, out Guid[] iids)
	{
		uint count = 0;
		Guid* list = null;
		int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint*, Guid**, int>)Slot(inspectable, SlotGetIids))(inspectable, &count, &list);
		iids = hr >= 0 ? new ReadOnlySpan<Guid>(list, (int)count).ToArray() : [];
		Marshal.FreeCoTaskMem((IntPtr)list);
		return hr;
	}

	internal static int QueryInterface(IntPtr unknown, Guid iid, out IntPtr result) => Marshal.QueryInterface(unknown, in iid, out result);

	internal static void Release(IntPtr unknown) => Marshal.Release(unknown);

	/// <param name="policyDeviceId">The device in the form <c>\\?\SWD#MMDEVAPI#...</c>; null resets the app to the default device</param>
	internal static int SetPersistedDefaultAudioEndpoint(IntPtr policy, uint processId, ERole role, string? policyDeviceId)
	{
		IntPtr deviceId = policyDeviceId == null ? IntPtr.Zero : CreateString(policyDeviceId);
		try
		{
			return ((delegate* unmanaged[Stdcall]<IntPtr, uint, int, int, IntPtr, int>)Slot(policy, SlotSetPersistedDefaultAudioEndpoint))(
				policy, processId, Render, (int)role, deviceId);
		}
		finally
		{
			WindowsDeleteString(deviceId);
		}
	}

	/// <param name="policyDeviceId">The device in the form <c>\\?\SWD#MMDEVAPI#...</c>; empty when the app follows the default device</param>
	internal static int GetPersistedDefaultAudioEndpoint(IntPtr policy, uint processId, ERole role, out string policyDeviceId)
	{
		IntPtr deviceId = IntPtr.Zero;
		int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, int, int, IntPtr*, int>)Slot(policy, SlotGetPersistedDefaultAudioEndpoint))(
			policy, processId, Render, (int)role, &deviceId);
		try
		{
			char* buffer = WindowsGetStringRawBuffer(deviceId, out uint length);
			policyDeviceId = hr >= 0 && buffer != null ? new string(buffer, 0, (int)length) : "";
			return hr;
		}
		finally
		{
			WindowsDeleteString(deviceId);
		}
	}

	private static void* Slot(IntPtr instance, int slot) => (*(void***)instance)[slot];

	private static IntPtr CreateString(string value)
	{
		Marshal.ThrowExceptionForHR(WindowsCreateString(value, value.Length, out IntPtr result));
		return result;
	}

	[DllImport("AudioSes.dll")]
	private static extern int DllGetActivationFactory(IntPtr activatableClassId, out IntPtr factory);

	[DllImport("combase.dll")]
	private static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

	[DllImport("combase.dll", CharSet = CharSet.Unicode)]
	private static extern int WindowsCreateString(string sourceString, int length, out IntPtr result);

	//a null handle is the empty string: deleting it does nothing
	[DllImport("combase.dll")]
	private static extern int WindowsDeleteString(IntPtr value);

	//null for a null handle
	[DllImport("combase.dll")]
	private static extern char* WindowsGetStringRawBuffer(IntPtr value, out uint length);
}

#nullable enable
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Device settings through the undocumented IPolicyConfig interface (what Windows' sound settings use): the default device, the format
/// </summary>
public class PolicyClient
{
	[ComImport, Guid(GuidValue.External.PolicyConfigCOM)]
	private class PolicyClientCOM { }

	private readonly IPolicyConfig instance = (IPolicyConfig)new PolicyClientCOM();

	//KSDATAFORMAT_SUBTYPE_PCM
	private static readonly Guid PcmSubFormat = new Guid("00000001-0000-0010-8000-00aa00389b71");

	/// <exception cref="COMException">Windows refused the switch (it used to fail silently)</exception>
	public void SetDefaultEndpointDevice(string deviceId)
	{
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Console));
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Multimedia));
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Communications));
	}

	/// <summary>
	/// Sets the format the audio engine runs the device at, as Windows' format list does: the device format becomes integer PCM with the
	/// option's bits and rate (channels and speaker layout stay), and the shared-mode mix format follows the rate. Windows then reconfigures
	/// the device, which disconnects its sessions (Volumey rebuilds the device's model then).
	/// </summary>
	/// <exception cref="COMException">Windows refused the format (e.g. AUDCLNT_E_UNSUPPORTED_FORMAT)</exception>
	public void SetDeviceFormat(string deviceId, DeviceFormatOption option)
		//runs on the thread pool: an object of the calling thread (see WithPolicy)
		=> WithPolicy(policy =>
		{
			SetDeviceFormat(policy, deviceId, option);
			return 0;
		});

	/// <summary>
	/// Reads a numeric (VT_UI4) property of the device's endpoint store, e.g. the enhancements or exclusive mode switches; null if it isn't set
	/// or has another type. Works from any thread (an own object per call).
	/// </summary>
	/// <exception cref="COMException">Windows refused the read</exception>
	public uint? GetEndpointValue(string deviceId, EndpointProperty property)
		=> WithPolicy(policy => WithKey(property, key =>
		{
			IntPtr value = Marshal.AllocCoTaskMem(PropVariantSize);
			try
			{
				Clear(value, PropVariantSize);
				Marshal.ThrowExceptionForHR(policy.GetPropertyValue(deviceId, false, key, value));
				ushort type = (ushort)Marshal.ReadInt16(value);
				uint? result = type == VtUi4 ? (uint)Marshal.ReadInt32(value, 8) : null;
				PropVariantClear(value);
				return result;
			}
			finally
			{
				Marshal.FreeCoTaskMem(value);
			}
		}));

	/// <summary>
	/// Writes a numeric (VT_UI4) property of the device's endpoint store, as Windows' sound settings do (no admin rights needed).
	/// Works from any thread (an own object per call).
	/// </summary>
	/// <exception cref="COMException">Windows refused the change</exception>
	public void SetEndpointValue(string deviceId, EndpointProperty property, uint newValue)
		=> WithPolicy(policy => WithKey(property, key =>
		{
			IntPtr value = Marshal.AllocCoTaskMem(PropVariantSize);
			try
			{
				Clear(value, PropVariantSize);
				Marshal.WriteInt16(value, (short)VtUi4);
				Marshal.WriteInt32(value, 8, (int)newValue);
				Marshal.ThrowExceptionForHR(policy.SetPropertyValue(deviceId, false, key, value));
				return 0;
			}
			finally
			{
				Marshal.FreeCoTaskMem(value);
			}
		}));

	//PROPVARIANT: vt (2), reserved (6), value (16 on x64)
	private const int PropVariantSize = 24;
	private const ushort VtUi4 = 19;

	[DllImport("ole32.dll")]
	private static extern int PropVariantClear(IntPtr value);

	private static void Clear(IntPtr memory, int size)
	{
		for(int i = 0; i < size; i++)
			Marshal.WriteByte(memory, i, 0);
	}

	/// <summary>
	/// Runs the call on an IPolicyConfig object of the calling thread: the interface has no proxy, so the UI thread's object can't be
	/// called from another thread (E_NOINTERFACE)
	/// </summary>
	private static T WithPolicy<T>(Func<IPolicyConfig, T> call)
	{
		var policy = (IPolicyConfig)new PolicyClientCOM();
		try
		{
			return call(policy);
		}
		finally
		{
			Marshal.ReleaseComObject(policy);
		}
	}

	//PROPERTYKEY: fmtid (16), pid (4)
	private static T WithKey<T>(EndpointProperty property, Func<IntPtr, T> call)
	{
		IntPtr key = Marshal.AllocCoTaskMem(20);
		try
		{
			Marshal.Copy(property.FormatId.ToByteArray(), 0, key, 16);
			Marshal.WriteInt32(key, 16, (int)property.PropertyId);
			return call(key);
		}
		finally
		{
			Marshal.FreeCoTaskMem(key);
		}
	}

	private static void SetDeviceFormat(IPolicyConfig policy, string deviceId, DeviceFormatOption option)
	{
		byte[] device = ReadFormat(policy.GetDeviceFormat(deviceId, false, out var devicePtr), devicePtr);
		byte[] mix = ReadFormat(policy.GetMixFormat(deviceId, out var mixPtr), mixPtr);
		var current = DeviceFormat.FromWaveFormatEx(device) ?? throw new InvalidOperationException("The device's format couldn't be read");

		byte[] newDevice = Extensible(current.Channels, option.SamplesPerSec, option.ContainerBits, option.Bits,
			current.ChannelMask != 0 ? current.ChannelMask : DefaultChannelMask(current.Channels), PcmSubFormat);
		//the mix format keeps its sample type (float) and layout, only the rate changes
		WithRate(mix, option.SamplesPerSec);

		IntPtr newDevicePtr = Marshal.AllocCoTaskMem(newDevice.Length);
		IntPtr newMixPtr = Marshal.AllocCoTaskMem(mix.Length);
		try
		{
			Marshal.Copy(newDevice, 0, newDevicePtr, newDevice.Length);
			Marshal.Copy(mix, 0, newMixPtr, mix.Length);
			Marshal.ThrowExceptionForHR(policy.SetDeviceFormat(deviceId, newDevicePtr, newMixPtr));
		}
		finally
		{
			Marshal.FreeCoTaskMem(newDevicePtr);
			Marshal.FreeCoTaskMem(newMixPtr);
		}
	}

	/// <summary>
	/// Copies a WAVEFORMATEX(TENSIBLE) Windows returned and frees it
	/// </summary>
	private static byte[] ReadFormat(int hr, IntPtr format)
	{
		Marshal.ThrowExceptionForHR(hr);
		if(format == IntPtr.Zero)
			throw new InvalidOperationException("Windows returned no format");
		try
		{
			//WAVEFORMATEX is 18 bytes, followed by cbSize extra bytes
			int size = 18 + (ushort)Marshal.ReadInt16(format, 16);
			var bytes = new byte[size];
			Marshal.Copy(format, bytes, 0, size);
			return bytes;
		}
		finally
		{
			Marshal.FreeCoTaskMem(format);
		}
	}

	private static void WithRate(byte[] format, uint rate)
	{
		ushort blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(12));
		BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), rate);
		BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(8), rate * blockAlign);
	}

	private static byte[] Extensible(ushort channels, uint rate, ushort containerBits, ushort validBits, uint channelMask, Guid subFormat)
	{
		var format = new byte[40];
		var span = format.AsSpan();
		ushort blockAlign = (ushort)(channels * containerBits / 8);
		BinaryPrimitives.WriteUInt16LittleEndian(span, 0xFFFE);
		BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2), channels);
		BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4), rate);
		BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8), rate * blockAlign);
		BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(12), blockAlign);
		BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(14), containerBits);
		BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(16), 22);
		BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(18), validBits);
		BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20), channelMask);
		subFormat.TryWriteBytes(span.Slice(24));
		return format;
	}

	//SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT for stereo, SPEAKER_FRONT_CENTER for mono; others keep "no layout"
	private static uint DefaultChannelMask(ushort channels) => channels switch { 1 => 0x4, 2 => 0x3, _ => 0 };
}

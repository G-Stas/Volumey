#nullable enable
using System;
using System.Buffers.Binary;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// The shared-mode stream format of an audio endpoint device (its WAVEFORMATEX, plus the WAVEFORMATEXTENSIBLE fields that matter here).
/// </summary>
/// <param name="BitsPerSample">The container size, e.g. 32 for 24-bit samples in 32-bit containers</param>
/// <param name="ValidBitsPerSample">The bits that carry the sample (WAVEFORMATEXTENSIBLE), 0 for a plain WAVEFORMATEX</param>
/// <param name="ChannelMask">The speaker positions (WAVEFORMATEXTENSIBLE), 0 for a plain WAVEFORMATEX</param>
public readonly record struct DeviceFormat(ushort Channels, uint SamplesPerSec, uint AvgBytesPerSec, ushort BlockAlign, ushort BitsPerSample,
	ushort ValidBitsPerSample = 0, uint ChannelMask = 0)
{
	//WAVEFORMATEX: wFormatTag(2) nChannels(2) nSamplesPerSec(4) nAvgBytesPerSec(4) nBlockAlign(2) wBitsPerSample(2) cbSize(2)
	private const int WaveFormatExBaseSize = 16;
	//WAVEFORMATEXTENSIBLE adds wValidBitsPerSample(2) dwChannelMask(4) SubFormat(16) after cbSize
	private const int WaveFormatExtensibleSize = 40;
	private const ushort WaveFormatExtensibleTag = 0xFFFE;

	/// <summary>
	/// The sample's bits as Windows shows them ("24 bit" for 24-bit samples in 32-bit containers)
	/// </summary>
	public ushort Bits => ValidBitsPerSample != 0 ? ValidBitsPerSample : BitsPerSample;

	/// <summary>
	/// Reads the format from a WAVEFORMATEX (or WAVEFORMATEXTENSIBLE) blob, e.g. the PKEY_AudioEngine_DeviceFormat property.
	/// </summary>
	public static DeviceFormat? FromWaveFormatEx(ReadOnlySpan<byte> blob)
	{
		if(blob.Length < WaveFormatExBaseSize)
			return null;
		bool extensible = blob.Length >= WaveFormatExtensibleSize && BinaryPrimitives.ReadUInt16LittleEndian(blob) == WaveFormatExtensibleTag;
		return new DeviceFormat(
			Channels: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(2)),
			SamplesPerSec: BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(4)),
			AvgBytesPerSec: BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(8)),
			BlockAlign: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(12)),
			BitsPerSample: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(14)),
			ValidBitsPerSample: extensible ? BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(18)) : (ushort)0,
			ChannelMask: extensible ? BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(20)) : 0);
	}
}

/// <summary>
/// A format a device can run at (from <see cref="IDevice.GetSupportedFormats"/>): the sample's bits, their container and the sample rate.
/// The channels stay the device's.
/// </summary>
public readonly record struct DeviceFormatOption(ushort Bits, ushort ContainerBits, uint SamplesPerSec)
{
	/// <summary>
	/// Whether the device runs at this option (same bits and rate; the container doesn't show in Windows' list either)
	/// </summary>
	public bool Matches(DeviceFormat format) => format.Bits == Bits && format.SamplesPerSec == SamplesPerSec;
}

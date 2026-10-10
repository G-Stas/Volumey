#nullable enable
using System;
using System.Buffers.Binary;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// The shared-mode stream format of an audio endpoint device (the base fields of its WAVEFORMATEX).
/// </summary>
public readonly record struct DeviceFormat(ushort Channels, uint SamplesPerSec, uint AvgBytesPerSec, ushort BlockAlign, ushort BitsPerSample)
{
	//WAVEFORMATEX: wFormatTag(2) nChannels(2) nSamplesPerSec(4) nAvgBytesPerSec(4) nBlockAlign(2) wBitsPerSample(2) cbSize(2)
	private const int WaveFormatExBaseSize = 16;

	/// <summary>
	/// Reads the format from a WAVEFORMATEX (or WAVEFORMATEXTENSIBLE) blob, e.g. the PKEY_AudioEngine_DeviceFormat property.
	/// </summary>
	public static DeviceFormat? FromWaveFormatEx(ReadOnlySpan<byte> blob)
	{
		if(blob.Length < WaveFormatExBaseSize)
			return null;
		return new DeviceFormat(
			Channels: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(2)),
			SamplesPerSec: BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(4)),
			AvgBytesPerSec: BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(8)),
			BlockAlign: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(12)),
			BitsPerSample: BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(14)));
	}
}

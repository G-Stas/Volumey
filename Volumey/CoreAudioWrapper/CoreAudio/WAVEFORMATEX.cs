using System.Runtime.InteropServices;

namespace Volumey.CoreAudioWrapper.CoreAudio
{
	/// <summary>
	/// Defines the format of waveform-audio data.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct WAVEFORMATEX
	{
		/// <summary>
		/// Waveform-audio format type.
		/// </summary>
		public readonly ushort formatTag;

		/// <summary>
		/// Number of channels in the waveform-audio data. Monaural data uses one channel and stereo data uses two channels.
		/// </summary>
		public readonly ushort nChannels;

		/// <summary>
		/// Sample rate, in samples per second (hertz).
		/// </summary>
		public readonly uint samplesPerSec;

		/// <summary>
		/// Required average data-transfer rate, in bytes per second, for the format tag.
		/// </summary>
		public readonly uint avgBytesPerSec;

		/// <summary>
		/// Block alignment, in bytes. The block alignment is the minimum atomic unit of data for the <see cref="formatTag"/> format type.
		/// </summary>
		public readonly ushort blockAlign;

		/// <summary>
		/// Bits per sample for the <see cref="formatTag"/> format type.
		/// </summary>
		public readonly ushort bitsPerSample;

		/// <summary>
		/// Size, in bytes, of extra format information appended to the end of the WAVEFORMATEX structure. 
		/// </summary>
		public readonly ushort cbSize;

		public bool Equals(WAVEFORMATEX? other)
		{
			if(!other.HasValue)
				return false;
			var value = other.Value;
			return nChannels == value.nChannels &&
			       samplesPerSec == value.samplesPerSec &&
			       avgBytesPerSec == value.avgBytesPerSec &&
			       blockAlign == value.blockAlign &&
			       bitsPerSample == value.bitsPerSample;
		}
	}
}
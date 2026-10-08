using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Tests
{
	[TestClass]
	public class CoreAudioWrapperTests
	{
		private long now;

		private SessionEchoFilter CreateFilter(int volume = 50, bool mute = false)
			=> new SessionEchoFilter(volume, mute, () => this.now);

		[TestMethod]
		public void OwnVolumeWriteIsAnEcho()
		{
			var filter = CreateFilter();
			filter.OnVolumeWritten(60);

			Assert.IsTrue(filter.IsEcho(60, false));
			//Windows can deliver the same event again
			Assert.IsTrue(filter.IsEcho(60, false));
		}

		[TestMethod]
		public void RepeatedVolumeEchoAfterAMuteWriteIsStillAnEcho()
		{
			//the case that un-muted the app: a repeat of the volume echo arrived after the mute was written
			var filter = CreateFilter(volume: 100);
			filter.OnVolumeWritten(37);
			Assert.IsTrue(filter.IsEcho(37, false));
			filter.OnMuteWritten(true);

			Assert.IsTrue(filter.IsEcho(37, false));
			Assert.IsTrue(filter.IsEcho(37, true));
		}

		[TestMethod]
		public void OwnMuteWriteIsAnEcho()
		{
			var filter = CreateFilter();
			filter.OnMuteWritten(true);

			Assert.IsTrue(filter.IsEcho(50, true));
		}

		[TestMethod]
		public void ExternalChangeIsNotAnEcho()
		{
			var filter = CreateFilter();
			filter.OnVolumeWritten(60);

			Assert.IsFalse(filter.IsEcho(30, false));
			Assert.IsFalse(filter.IsEcho(60, true));
		}

		[TestMethod]
		public void EchoOfALaterWriteConsumesEarlierWrites()
		{
			var filter = CreateFilter();
			filter.OnVolumeWritten(51);
			filter.OnVolumeWritten(52);
			filter.OnVolumeWritten(53);

			Assert.IsTrue(filter.IsEcho(52, false));
			Assert.IsFalse(filter.IsEcho(51, false));
			Assert.IsTrue(filter.IsEcho(53, false));
		}

		[TestMethod]
		public void WriteSnapshotsKeepTheOtherValue()
		{
			var filter = CreateFilter(volume: 40, mute: false);
			filter.OnMuteWritten(true);
			filter.OnVolumeWritten(45);

			Assert.IsTrue(filter.IsEcho(40, true));
			Assert.IsTrue(filter.IsEcho(45, true));
		}

		[TestMethod]
		public void ExternalChangeUpdatesTheKnownState()
		{
			var filter = CreateFilter(volume: 40, mute: false);
			Assert.IsFalse(filter.IsEcho(70, false));

			filter.OnMuteWritten(true);
			Assert.IsTrue(filter.IsEcho(70, true));
		}

		[TestMethod]
		public void OldWritesExpire()
		{
			var filter = CreateFilter();
			filter.OnVolumeWritten(60);
			this.now += SessionEchoFilter.EchoWindowMs + 1;

			Assert.IsFalse(filter.IsEcho(60, false));
		}

		[TestMethod]
		public void DeviceFormatIsReadFromWaveFormatEx()
		{
			//WAVEFORMATEXTENSIBLE: 48 kHz, 2 channels, 32-bit float, cbSize 22
			byte[] blob =
			{
				0xFE, 0xFF, 0x02, 0x00, 0x80, 0xBB, 0x00, 0x00, 0x00, 0xDC, 0x05, 0x00, 0x08, 0x00, 0x20, 0x00, 0x16, 0x00,
				0x20, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71
			};

			var format = DeviceFormat.FromWaveFormatEx(blob);

			Assert.AreEqual(new DeviceFormat(Channels: 2, SamplesPerSec: 48000, AvgBytesPerSec: 384000, BlockAlign: 8, BitsPerSample: 32), format);
		}

		[TestMethod]
		public void DeviceFormatOfAShortBlobIsNull()
		{
			Assert.IsNull(DeviceFormat.FromWaveFormatEx(new byte[10]));
		}
	}
}

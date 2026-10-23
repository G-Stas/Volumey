using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Tests;

[TestClass]
public class CoreAudioWrapperTests
{
	private long now;

	private SessionEchoFilter CreateFilter(int volume = 50, bool mute = false)
		=> new SessionEchoFilter(volume, mute, () => now);

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
		//a repeat of the volume echo arrives after the mute was written; taken as an outside change, it would un-mute the app
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
	public void AnOutdatedCallbackIsntRememberedAsTheCurrentState()
	{
		//an outside change of the volume to 30, then a mute write: the mute echo reports the volume Windows has (30)
		var filter = CreateFilter();
		Assert.IsFalse(filter.IsOwnWrite(30, false));
		filter.Remember(30, false);
		//an outdated callback is checked but not remembered
		Assert.IsFalse(filter.IsOwnWrite(80, false));
		filter.OnMuteWritten(true);

		Assert.IsTrue(filter.IsOwnWrite(30, true));
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
		now += SessionEchoFilter.EchoWindowMs + 1;

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

		Assert.AreEqual(new DeviceFormat(Channels: 2, SamplesPerSec: 48000, AvgBytesPerSec: 384000, BlockAlign: 8, BitsPerSample: 32, ValidBitsPerSample: 32, ChannelMask: 3), format);
	}

	[TestMethod]
	public void DeviceFormatOfAShortBlobIsNull()
	{
		Assert.IsNull(DeviceFormat.FromWaveFormatEx(new byte[10]));
	}

	[TestMethod]
	public void FormatsAreDescribedForTheLog()
	{
		//the float mix format of DeviceFormatIsReadFromWaveFormatEx
		byte[] mix =
		{
			0xFE, 0xFF, 0x02, 0x00, 0x80, 0xBB, 0x00, 0x00, 0x00, 0xDC, 0x05, 0x00, 0x08, 0x00, 0x20, 0x00, 0x16, 0x00,
			0x20, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71
		};
		//WAVEFORMATEX without the extension: PCM, 16 bit, 2 channels, 44.1 kHz
		byte[] plain = { 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00, 0x04, 0x00, 0x10, 0x00, 0x00, 0x00 };

		Assert.AreEqual("float 32/32 bit 2 ch 48000 Hz mask 0x3", PolicyClient.Describe(mix));
		Assert.AreEqual("tag 0x1 16 bit 2 ch 44100 Hz", PolicyClient.Describe(plain));
		Assert.AreEqual("10 bytes", PolicyClient.Describe(new byte[10]));
	}
}

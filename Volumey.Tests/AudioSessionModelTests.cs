using System.Windows.Media.Imaging;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Xunit;

namespace Volumey.Tests
{
	public class AudioSessionModelTests
	{
		private AudioSessionModel model;
		private Mock<IAudioSessionStateNotifications> sessionStateNotif;

		public AudioSessionModelTests()
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			this.sessionStateNotif = new Mock<IAudioSessionStateNotifications>();

			this.model = new AudioSessionModel(false, 50, "app", new BitmapImage(), sessionVolumeMock.Object,
				sessionStateNotif.Object);
		}

		[Fact]
		public void VolumeShouldBeChangedToCorrectValue()
		{
			var newVolume = this.model.Volume + 1;

			this.sessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(newVolume, this.model.IsMuted) });

			Assert.Equal(this.model.Volume, newVolume);
		}

		[Fact]
		public void MuteStateShouldBeChanged()
		{
			var newMuteState = !this.model.IsMuted;

			this.sessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(this.model.Volume, newMuteState) });

			Assert.Equal(this.model.IsMuted, newMuteState);
		}

		[Fact]
		public void IconSourceShouldBeChanged()
		{
			var newImageSource = new BitmapImage();
			
			this.sessionStateNotif.Raise(m => m.IconPathChanged += null, newImageSource);
			
			Assert.Equal(this.model.AppIcon, newImageSource);
		}
		
		internal static AudioSessionModel GetSessionMock(string name, int volume, bool muteState)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var sessionStateNotifications = new Mock<IAudioSessionStateNotifications>();
            
			return new AudioSessionModel
			(muteState, volume, name, null, 
				sessionVolumeMock.Object, sessionStateNotifications.Object);
		}
	}
}
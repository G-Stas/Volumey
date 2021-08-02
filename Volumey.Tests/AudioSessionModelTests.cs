using System.Windows.Input;
using System.Windows.Media.Imaging;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using Xunit;

namespace Volumey.Tests
{
	public class AudioSessionModelTests
	{
		private AudioSessionModel model;
		private Mock<IAudioSessionStateNotifications> sessionStateNotif;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public AudioSessionModelTests()
		{
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
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

			Assert.Equal(newVolume, this.model.Volume);
		}

		[Fact]
		public void MuteStateShouldBeChanged()
		{
			var newMuteState = !this.model.IsMuted;

			this.sessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(this.model.Volume, newMuteState) });

			Assert.Equal(newMuteState, this.model.IsMuted);
		}

		[Fact]
		public void IconSourceShouldBeChanged()
		{
			var newImageSource = new BitmapImage();
			
			this.sessionStateNotif.Raise(m => m.IconPathChanged += null, newImageSource);
			
			Assert.Equal(newImageSource, this.model.AppIcon);
		}

		[Fact]
		public void HotkeyShouldIncrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetHotkeys(upHotkey, downHotkey);
			var newVolume = this.model.Volume + HotkeysControl.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, ref GuidValue.Internal.Empty), Times.AtLeastOnce);
			//simualate callback from external API
			this.sessionStateNotif.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.model.IsMuted));
			Assert.Equal(newVolume, this.model.Volume);
		}
		
		[Fact]
		public void RandomHotkeyShouldNotChangeVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetHotkeys(upHotkey, downHotkey);
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));
			
			//assert
			//verify increment/decrement volume calls to external API are not made
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume + HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume - HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
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
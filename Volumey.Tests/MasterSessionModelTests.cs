using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
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
	public class MasterSessionModelTests
	{
		private MasterSessionModel model;
		private Mock<IMasterVolumeNotificationHandler> mVolumeNotifMock;
		
		private OutputDeviceModel deviceOwner;
		private Mock<IDevice> iDeviceMock;
		private Mock<IDeviceStateNotificationHandler> deviceStateMock;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public MasterSessionModelTests()
		{
			this.mVolumeNotifMock = new Mock<IMasterVolumeNotificationHandler>();
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
			
			this.model = new MasterSessionModel("speakers", "speakers", 70, 
				false, "id", new BitmapImage(), this.sessionVolumeMock.Object, mVolumeNotifMock.Object);

			this.deviceStateMock = new Mock<IDeviceStateNotificationHandler>();
			this.iDeviceMock = new Mock<IDevice>();
			
			this.iDeviceMock.Setup(m => m.GetId()).Returns("111");
			
			this.deviceOwner = new OutputDeviceModel(this.iDeviceMock.Object, this.deviceStateMock.Object,
				new Mock<ISessionProvider>().Object, this.model, new ObservableCollection<AudioSessionModel>());
		}

		[Fact]
		public void VolumeShouldBeChangedToCorrectValue()
		{
			var newVolume = this.model.Volume + 1;

			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, false));
			
			Assert.Equal(this.model.Volume, newVolume);
		}

		[Fact]
		public void MuteStateShouldBeChanged()
		{
			var newMuteState = !this.model.IsMuted;

			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null,
				new VolumeChangedEventArgs(this.model.Volume, newMuteState));

			Assert.Equal(this.model.IsMuted, newMuteState);
		}

		[Fact]
		public void IconPathChangedEvent_IconSourceShouldBeChanged()
		{
			var newIconSource = new BitmapImage();

			this.iDeviceMock.Setup(m => m.GetIconSource()).Returns(newIconSource);
			this.deviceStateMock.Raise(m => m.IconPathChanged += null, this.deviceOwner.Id);
			
			Assert.Equal(this.model.Icon, newIconSource);
		}

		[Fact]
		public void NameChangedEvent_NamePropertiesShouldBeChanged()
		{
			var newFriendlyName = "speakers";
			var newDeviceDesc = "speakers-speakers";

			this.iDeviceMock.Setup(m => m.GetFriendlyName()).Returns(newFriendlyName);
			this.iDeviceMock.Setup(m => m.GetDeviceDesc()).Returns(newDeviceDesc);

			this.deviceStateMock.Raise(m => m.NameChanged += null, this.deviceOwner.Id);

			Assert.Equal(this.model.DeviceFriendlyName, newFriendlyName);
			Assert.Equal(this.model.DeviceDesc, newDeviceDesc);
		}
		
		[Fact]
		public void HotkeyShouldIncrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			var newVolume = this.model.Volume + HotkeysControl.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, ref GuidValue.Internal.Empty), Times.AtLeastOnce);
			//simualate callback from external API
			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.model.IsMuted));
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
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));
			
			//assert
			//verify increment/decrement volume calls to external API are not made
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume + HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume - HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
		}

		internal static MasterSessionModel GetMasterMock(string name, int volume, bool muteState, string id, ImageSource imageSource)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var masterVolumeHandler = new Mock<IMasterVolumeNotificationHandler>();

			return new MasterSessionModel
			(name, name, volume, muteState, id,
				imageSource, sessionVolumeMock.Object, masterVolumeHandler.Object);
		}
	}
}
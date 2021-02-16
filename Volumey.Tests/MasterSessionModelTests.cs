using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
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

		public MasterSessionModelTests()
		{
			this.mVolumeNotifMock = new Mock<IMasterVolumeNotificationHandler>();
			var sVolumeMock = new Mock<IAudioSessionVolume>();
			
			this.model = new MasterSessionModel("speakers", "speakers", 70, 
				false, new BitmapImage(), sVolumeMock.Object, mVolumeNotifMock.Object);

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
			
			Assert.Equal(this.model.DeviceIcon, newIconSource);
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

		internal static MasterSessionModel GetMasterMock(string name, int volume, bool muteState, ImageSource imageSource)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var masterVolumeHandler = new Mock<IMasterVolumeNotificationHandler>();

			return new MasterSessionModel
			(name, name, volume, muteState,
				imageSource, sessionVolumeMock.Object, masterVolumeHandler.Object);
		}
	}
}
using System.Collections.ObjectModel;
using System.Reflection;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.ViewModel;
using Xunit;

namespace Volumey.Tests
{
	public class DeviceProviderViewModelTests
	{
		private DeviceProviderViewModel model;
		private Mock<IDeviceProvider> deviceProviderMock;

		public DeviceProviderViewModelTests()
		{
			this.deviceProviderMock = new Mock<IDeviceProvider>();
			
			var deviceStateNotif = new Mock<IDeviceStateNotificationHandler>();
			var device1 = OutputDeviceModelTests.GetDeviceMock("1", "speakers", deviceStateNotif.Object);
			var device2 = OutputDeviceModelTests.GetDeviceMock("2", "headphones", deviceStateNotif.Object);
			var activeDevices = new ObservableCollection<OutputDeviceModel> {device1, device2};

			this.deviceProviderMock.Setup(m => m.ActiveDevices).Returns(activeDevices);
			this.deviceProviderMock.Setup(m => m.DefaultDevice).Returns(device1);
			this.deviceProviderMock.Setup(m => m.NoOutputDevices).Returns(false);
			
			FieldInfo instance = typeof(DeviceProvider).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
			instance.SetValue(null, deviceProviderMock.Object);
			
			this.model = new DeviceProviderViewModel();
		}

		[Fact]
		public void DeviceDisabledEvent_DisabledSelectedDeviceShouldChangeToDefaultDevice()
		{
			//select not default output device
			var disabledDevice = this.model.SelectedDevice = this.deviceProviderMock.Object.ActiveDevices[1];

			this.deviceProviderMock.Raise(m => m.DeviceDisabled += null, disabledDevice);
			
			Assert.Equal(this.deviceProviderMock.Object.DefaultDevice, this.model.SelectedDevice);
		}

		[Fact]
		public void DefaultDeviceChangedEvent_SelectedDeviceShouldChangeToNewDefaultDevice()
		{
			var newDefaultDevice = this.deviceProviderMock.Object.ActiveDevices[1];

			this.deviceProviderMock.Setup(m => m.DefaultDevice).Returns(newDefaultDevice);
			this.deviceProviderMock.Raise(m => m.DefaultDeviceChanged += null, newDefaultDevice);
			
			Assert.Equal(newDefaultDevice, this.model.SelectedDevice);
		}
	}
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;
using Xunit;

namespace Volumey.Tests
{
	public class DeviceProviderTests
	{
		private DeviceProvider model;
		private OutputDeviceModel defaultDevice;

		private Mock<IDeviceStateNotificationHandler> deviceStateNotifMock;

		public DeviceProviderTests()
		{
			var iDeviceEnumMock = new Mock<IDeviceEnumerator>();
			
			deviceStateNotifMock = new Mock<IDeviceStateNotificationHandler>();

			defaultDevice = OutputDeviceModelTests.GetDeviceMock("111", "speakers", this.deviceStateNotifMock.Object);
			var device = OutputDeviceModelTests.GetDeviceMock("123", "device", this.deviceStateNotifMock.Object);
			var devices = new List<OutputDeviceModel> {defaultDevice, device};
			iDeviceEnumMock.Setup(m => m.GetCurrentActiveDevices(deviceStateNotifMock.Object)).Returns(devices);
			iDeviceEnumMock.Setup(m => m.GetDefaultDeviceId()).Returns(defaultDevice.Id);

			model = GetDeviceProviderMock(iDeviceEnumMock.Object, deviceStateNotifMock.Object);
		}

		[Fact]
		public void DefaultDeviceShouldReferenceCorrectObject()
		{
			Assert.Equal(this.model.DefaultDevice, this.defaultDevice);
		}

		[Fact]
		public void ActiveDeviceAddedEvent_NewDeviceShouldBeAdded()
		{
			var devicesCount = this.model.ActiveDevices.Count;
			var newDevice = OutputDeviceModelTests.GetDeviceMock("222", "headphones", this.deviceStateNotifMock.Object);
		
			this.deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, newDevice);
		
			Assert.Equal(devicesCount + 1, this.model.ActiveDevices.Count);
			Assert.Contains(newDevice, this.model.ActiveDevices);
		}

		[Fact]
		public void DeviceDisabledEvent_DisabledDeviceShouldBeRemoved()
		{
			var deviceCount = model.ActiveDevices.Count;
			var disabledDevice = defaultDevice;
			
			deviceStateNotifMock.Raise(m => m.DeviceDisabled += null, disabledDevice.Id);
			
			Assert.Equal(deviceCount - 1, model.ActiveDevices.Count);
			Assert.DoesNotContain(disabledDevice, this.model.ActiveDevices);
		}

		[Fact]
		public void FirstDeviceAdded_NoOutputDeviceShouldBeFalse()
		{
			//remove all active devices
			var activeDevicesCount = this.model.ActiveDevices.Count;
			for(int i = activeDevicesCount - 1; i >= 0; i--)
				deviceStateNotifMock.Raise(m => m.DeviceDisabled += null, this.model.ActiveDevices[i].Id);
			
			Assert.True(this.model.NoOutputDevices);	

			var newDevice = OutputDeviceModelTests.GetDeviceMock("222", "newDevice", this.deviceStateNotifMock.Object);
			deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, newDevice);
			
			Assert.False(this.model.NoOutputDevices);
			Assert.Contains(newDevice, this.model.ActiveDevices);
			Assert.Equal(newDevice, this.model.DefaultDevice);
		}

		[Fact]
		public void DefaultDeviceChangedEvent_DefaultDevicePropertyShouldBeChanged()
		{
			var device = OutputDeviceModelTests.GetDeviceMock("222", "headphones", this.deviceStateNotifMock.Object);
			deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, device);

			deviceStateNotifMock.Raise(m => m.DefaultDeviceChanged += null, device.Id);

			Assert.Equal(device, this.model.DefaultDevice);
		}

		[Fact]
		public static void DefaultDeviceShouldNotBeNull_WhenDeviceEnumeratorReturnsInvalidDefaultDeviceId()
		{
			var IDeviceEnumMock = new Mock<IDeviceEnumerator>();
			var deviceStateNotificationMock = new Mock<IDeviceStateNotificationHandler>();

			var device = OutputDeviceModelTests.GetDeviceMock("111", "speakers", deviceStateNotificationMock.Object); 
			var devices = new List<OutputDeviceModel> {device};
			IDeviceEnumMock.Setup(m => m.GetCurrentActiveDevices(deviceStateNotificationMock.Object)).Returns(devices);
			IDeviceEnumMock.Setup(m => m.GetDefaultDeviceId()).Returns(String.Empty);
			
			var model = (DeviceProvider) Activator.CreateInstance(typeof(DeviceProvider),
				BindingFlags.NonPublic | BindingFlags.Instance, null,
				new object[] {IDeviceEnumMock.Object, deviceStateNotificationMock.Object}, CultureInfo.InvariantCulture,
				null);
			
			Assert.NotNull(model.DefaultDevice);
		}

		public static DeviceProvider GetDeviceProviderMock(IDeviceEnumerator deviceEnum, IDeviceStateNotificationHandler deviceState)
		{
			return (DeviceProvider) Activator.CreateInstance(typeof(DeviceProvider),
				BindingFlags.NonPublic | BindingFlags.Instance, null,
				new object[] {deviceEnum, deviceState}, CultureInfo.InvariantCulture,
				null);
		}
		
	}
}
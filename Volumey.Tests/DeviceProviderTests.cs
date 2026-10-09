using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
	[TestClass]
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

		[TestMethod]
		public void DefaultDeviceShouldReferenceCorrectObject()
		{
			Assert.AreEqual(this.model.DefaultDevice, this.defaultDevice);
		}

		[TestMethod]
		public void ActiveDeviceAddedEvent_NewDeviceShouldBeAdded()
		{
			var devicesCount = this.model.ActiveDevices.Count;
			var newDevice = OutputDeviceModelTests.GetDeviceMock("222", "headphones", this.deviceStateNotifMock.Object);
		
			this.deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, newDevice);
		
			Assert.AreEqual(devicesCount + 1, this.model.ActiveDevices.Count);
			Assert.Contains(newDevice, this.model.ActiveDevices);
		}

		[TestMethod]
		public void DeviceDisabledEvent_DisabledDeviceShouldBeRemoved()
		{
			var deviceCount = model.ActiveDevices.Count;
			var disabledDevice = defaultDevice;
			
			deviceStateNotifMock.Raise(m => m.DeviceDisabled += null, disabledDevice.Id);
			
			Assert.AreEqual(deviceCount - 1, model.ActiveDevices.Count);
			Assert.DoesNotContain(disabledDevice, this.model.ActiveDevices);
		}

		[TestMethod]
		public void FirstDeviceAdded_NoOutputDeviceShouldBeFalse()
		{
			//remove all active devices
			var activeDevicesCount = this.model.ActiveDevices.Count;
			for(int i = activeDevicesCount - 1; i >= 0; i--)
				deviceStateNotifMock.Raise(m => m.DeviceDisabled += null, this.model.ActiveDevices[i].Id);
			
			Assert.IsTrue(this.model.NoOutputDevices);	

			var newDevice = OutputDeviceModelTests.GetDeviceMock("222", "newDevice", this.deviceStateNotifMock.Object);
			deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, newDevice);
			
			Assert.IsFalse(this.model.NoOutputDevices);
			Assert.Contains(newDevice, this.model.ActiveDevices);
			Assert.AreEqual(newDevice, this.model.DefaultDevice);
		}

		[TestMethod]
		public void DefaultDeviceChangedEvent_DefaultDevicePropertyShouldBeChanged()
		{
			var device = OutputDeviceModelTests.GetDeviceMock("222", "headphones", this.deviceStateNotifMock.Object);
			deviceStateNotifMock.Raise(m => m.ActiveDeviceAdded += null, device);

			deviceStateNotifMock.Raise(m => m.DefaultDeviceChanged += null, device.Id);

			Assert.AreEqual(device, this.model.DefaultDevice);
		}

		[TestMethod]
		public void DefaultDeviceShouldNotBeNull_WhenDeviceEnumeratorReturnsInvalidDefaultDeviceId()
		{
			var IDeviceEnumMock = new Mock<IDeviceEnumerator>();
			var deviceStateNotificationMock = new Mock<IDeviceStateNotificationHandler>();

			var device = OutputDeviceModelTests.GetDeviceMock("111", "speakers", deviceStateNotificationMock.Object); 
			var devices = new List<OutputDeviceModel> {device};
			IDeviceEnumMock.Setup(m => m.GetCurrentActiveDevices(deviceStateNotificationMock.Object)).Returns(devices);
			IDeviceEnumMock.Setup(m => m.GetDefaultDeviceId()).Returns(String.Empty);
			
			var model = new DeviceProvider(IDeviceEnumMock.Object, deviceStateNotificationMock.Object);
			
			Assert.IsNotNull(model.DefaultDevice);
		}

		[TestMethod]
		public void ReconfiguredDefaultDeviceIsRebuilt()
		{
			//#82, #85: spatial sound on/off
			var (provider, enumerator, sessions, handler) = CreateWithInvalidatableDefault();
			var old = provider.DefaultDevice;
			var fresh = OutputDeviceModelTests.GetDeviceMock("111", "speakers", handler.Object);
			enumerator.Setup(m => m.CreateDeviceModel("111", handler.Object)).Returns(fresh);
			OutputDeviceModel changedTo = null;
			bool listedWhenChanged = false;
			(OutputDeviceModel, OutputDeviceModel) replaced = default;
			//both models are listed while the listeners switch: a combo box showing the old one loses its selection
			//(and writes null back) if it's replaced in place
			provider.DefaultDeviceChanged += d =>
			{
				changedTo = d;
				listedWhenChanged = provider.ActiveDevices.Contains(d) && provider.ActiveDevices.Contains(old);
			};
			provider.DeviceReplaced += (o, n) => replaced = (o, n);

			sessions.Raise(m => m.DeviceInvalidated += null);

			WaitFor(() => provider.DefaultDevice == fresh, "the device wasn't rebuilt");
			Assert.AreEqual(fresh, provider.ActiveDevices[0]);
			Assert.HasCount(2, provider.ActiveDevices);
			Assert.AreEqual(fresh, changedTo);
			Assert.IsTrue(listedWhenChanged);
			Assert.AreEqual((old, fresh), replaced);
			Assert.DoesNotContain(old, provider.ActiveDevices);
		}

		[TestMethod]
		public void ReconfiguredOtherDeviceIsRebuiltWithoutChangingTheDefault()
		{
			var handler = new Mock<IDeviceStateNotificationHandler>();
			var enumerator = new Mock<IDeviceEnumerator>();
			var sessions = new Mock<ISessionProvider>();
			var speakers = OutputDeviceModelTests.GetDeviceMock("111", "speakers", handler.Object);
			var headphones = OutputDeviceModelTests.GetDeviceMock("123", "headphones", handler.Object, sessions);
			enumerator.Setup(m => m.GetCurrentActiveDevices(handler.Object)).Returns(new List<OutputDeviceModel> { speakers, headphones });
			enumerator.Setup(m => m.GetDefaultDeviceId()).Returns("111");
			var fresh = OutputDeviceModelTests.GetDeviceMock("123", "headphones", handler.Object);
			enumerator.Setup(m => m.CreateDeviceModel("123", handler.Object)).Returns(fresh);
			var provider = new DeviceProvider(enumerator.Object, handler.Object);
			bool defaultChanged = false;
			provider.DefaultDeviceChanged += d => defaultChanged = true;

			sessions.Raise(m => m.DeviceInvalidated += null);

			WaitFor(() => provider.ActiveDevices[1] == fresh, "the device wasn't rebuilt");
			Assert.AreEqual(speakers, provider.DefaultDevice);
			Assert.IsFalse(defaultChanged);
		}

		[TestMethod]
		public void RebuildingIsRetriedWhileWindowsIsBusy()
		{
			var (provider, enumerator, sessions, handler) = CreateWithInvalidatableDefault();
			var fresh = OutputDeviceModelTests.GetDeviceMock("111", "speakers", handler.Object);
			enumerator.SetupSequence(m => m.CreateDeviceModel("111", handler.Object)).Returns((OutputDeviceModel)null).Returns(fresh);

			sessions.Raise(m => m.DeviceInvalidated += null);

			WaitFor(() => provider.DefaultDevice == fresh, "the device wasn't rebuilt on the second attempt", 5000);
			enumerator.Verify(m => m.CreateDeviceModel("111", handler.Object), Times.Exactly(2));
		}

		private static (DeviceProvider, Mock<IDeviceEnumerator>, Mock<ISessionProvider>, Mock<IDeviceStateNotificationHandler>) CreateWithInvalidatableDefault()
		{
			var handler = new Mock<IDeviceStateNotificationHandler>();
			var enumerator = new Mock<IDeviceEnumerator>();
			var sessions = new Mock<ISessionProvider>();
			var speakers = OutputDeviceModelTests.GetDeviceMock("111", "speakers", handler.Object, sessions);
			var headphones = OutputDeviceModelTests.GetDeviceMock("123", "headphones", handler.Object);
			enumerator.Setup(m => m.GetCurrentActiveDevices(handler.Object)).Returns(new List<OutputDeviceModel> { speakers, headphones });
			enumerator.Setup(m => m.GetDefaultDeviceId()).Returns("111");
			return (new DeviceProvider(enumerator.Object, handler.Object), enumerator, sessions, handler);
		}

		private static void WaitFor(Func<bool> condition, string message, int timeoutMs = 3000)
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();
			while(!condition())
			{
				if(watch.ElapsedMilliseconds > timeoutMs)
					Assert.Fail(message);
				System.Threading.Thread.Sleep(20);
			}
		}

		public static DeviceProvider GetDeviceProviderMock(IDeviceEnumerator deviceEnum, IDeviceStateNotificationHandler deviceState)
		{
			return new DeviceProvider(deviceEnum, deviceState);
		}
		
	}
}
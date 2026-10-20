using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel;

namespace Volumey.Tests;

[TestClass]
public class DeviceProviderViewModelTests
{
	private DeviceProviderViewModel model;
	private Mock<IDeviceProvider> deviceProviderMock;

	public DeviceProviderViewModelTests()
	{
		deviceProviderMock = new Mock<IDeviceProvider>();

		var deviceStateNotif = new Mock<IDeviceStateNotificationHandler>();
		var device1 = OutputDeviceModelTests.GetDeviceMock("1", "speakers", deviceStateNotif.Object);
		var device2 = OutputDeviceModelTests.GetDeviceMock("2", "headphones", deviceStateNotif.Object);
		var activeDevices = new ObservableCollection<OutputDeviceModel> { device1, device2 };

		deviceProviderMock.Setup(m => m.ActiveDevices).Returns(activeDevices);
		deviceProviderMock.Setup(m => m.DefaultDevice).Returns(device1);
		deviceProviderMock.Setup(m => m.NoOutputDevices).Returns(false);
		model = new DeviceProviderViewModel(deviceProviderMock.Object, new Mock<IAppShell>().Object);
	}

	[TestMethod]
	public void DeviceDisabledEvent_DisabledSelectedDeviceShouldChangeToDefaultDevice()
	{
		//select not default output device
		var disabledDevice = model.SelectedDevice = deviceProviderMock.Object.ActiveDevices[1];

		deviceProviderMock.Raise(m => m.DeviceDisabled += null, disabledDevice);

		Assert.AreEqual(deviceProviderMock.Object.DefaultDevice, model.SelectedDevice);
	}

	[TestMethod]
	public void DefaultDeviceChangedEvent_SelectedDeviceShouldChangeToNewDefaultDevice()
	{
		var newDefaultDevice = deviceProviderMock.Object.ActiveDevices[1];

		deviceProviderMock.Setup(m => m.DefaultDevice).Returns(newDefaultDevice);
		deviceProviderMock.Raise(m => m.DefaultDeviceChanged += null, newDefaultDevice);

		Assert.AreEqual(newDefaultDevice, model.SelectedDevice);
	}

	[TestMethod]
	public void ShowDefaultDeviceSelectsTheDefaultDevice()
	{
		model.SelectedDevice = deviceProviderMock.Object.ActiveDevices[1];

		model.ShowDefaultDevice();

		Assert.AreEqual(deviceProviderMock.Object.DefaultDevice, model.SelectedDevice);
	}

	[TestMethod]
	public void ReplacedSelectedDeviceIsSelectedAgain()
	{
		var oldDevice = model.SelectedDevice = deviceProviderMock.Object.ActiveDevices[1];
		var newDevice = OutputDeviceModelTests.GetDeviceMock("2", "headphones", new Mock<IDeviceStateNotificationHandler>().Object);

		deviceProviderMock.Raise(m => m.DeviceReplaced += null, oldDevice, newDevice);

		Assert.AreEqual(newDevice, model.SelectedDevice);
	}
}

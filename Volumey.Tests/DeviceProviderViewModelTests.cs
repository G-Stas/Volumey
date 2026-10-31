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

	[TestMethod]
	public void SwitchToNextDeviceCyclesThroughActiveDevicesAndWrapsAround()
	{
		var notifMock = new Mock<IDeviceStateNotificationHandler>().Object;
		var dev1 = OutputDeviceModelTests.GetDeviceMock("id-1", "device1", notifMock);
		var dev2 = OutputDeviceModelTests.GetDeviceMock("id-2", "device2", notifMock);
		var dev3 = OutputDeviceModelTests.GetDeviceMock("id-3", "device3", notifMock);
		var activeDevices = new ObservableCollection<OutputDeviceModel> { dev1, dev2, dev3 };

		var providerMock = new Mock<IDeviceProvider>();
		providerMock.SetupGet(p => p.ActiveDevices).Returns(activeDevices);
		providerMock.SetupGet(p => p.DefaultDevice).Returns(() => dev2);

		var shellMock = new Mock<IAppShell>();
		var vm = new DeviceProviderViewModel(providerMock.Object, shellMock.Object);

		// Second device -> third device
		vm.SwitchToNextDevice();
		providerMock.Verify(p => p.SetDefaultDevice("id-3"), Times.Once);

		// Third device -> wraps around to first device
		providerMock.SetupGet(p => p.DefaultDevice).Returns(() => dev3);
		vm.SwitchToNextDevice();
		providerMock.Verify(p => p.SetDefaultDevice("id-1"), Times.Once);
	}

	[TestMethod]
	public void SecondSwitchBeforeWindowsReportsTheFirstGoesToTheDeviceAfterIt()
	{
		var notifMock = new Mock<IDeviceStateNotificationHandler>().Object;
		var dev1 = OutputDeviceModelTests.GetDeviceMock("id-1", "device1", notifMock);
		var dev2 = OutputDeviceModelTests.GetDeviceMock("id-2", "device2", notifMock);
		var dev3 = OutputDeviceModelTests.GetDeviceMock("id-3", "device3", notifMock);
		var activeDevices = new ObservableCollection<OutputDeviceModel> { dev1, dev2, dev3 };

		//like DeviceProvider: a switch is remembered as requested, DefaultDevice changes only when Windows reports it
		string requested = null;
		var providerMock = new Mock<IDeviceProvider>();
		providerMock.SetupGet(p => p.ActiveDevices).Returns(activeDevices);
		providerMock.SetupGet(p => p.DefaultDevice).Returns(dev1);
		providerMock.SetupGet(p => p.RequestedDefaultDeviceId).Returns(() => requested);
		providerMock.Setup(p => p.SetDefaultDevice(It.IsAny<string>())).Callback<string>(id => requested = id);

		var vm = new DeviceProviderViewModel(providerMock.Object, new Mock<IAppShell>().Object);

		vm.SwitchToNextDevice();
		vm.SwitchToNextDevice();

		providerMock.Verify(p => p.SetDefaultDevice("id-2"), Times.Once);
		providerMock.Verify(p => p.SetDefaultDevice("id-3"), Times.Once);
	}

	[TestMethod]
	public void SwitchToNextDeviceDoesNothingWhenSingleOrNoDevice()
	{
		var notifMock = new Mock<IDeviceStateNotificationHandler>().Object;
		var dev1 = OutputDeviceModelTests.GetDeviceMock("id-1", "device1", notifMock);
		var activeDevices = new ObservableCollection<OutputDeviceModel> { dev1 };

		var providerMock = new Mock<IDeviceProvider>();
		providerMock.SetupGet(p => p.ActiveDevices).Returns(activeDevices);
		providerMock.SetupGet(p => p.DefaultDevice).Returns(dev1);

		var shellMock = new Mock<IAppShell>();
		var vm = new DeviceProviderViewModel(providerMock.Object, shellMock.Object);

		vm.SwitchToNextDevice();
		providerMock.Verify(p => p.SetDefaultDevice(It.IsAny<string>()), Times.Never);

		activeDevices.Clear();
		vm.SwitchToNextDevice();
		providerMock.Verify(p => p.SetDefaultDevice(It.IsAny<string>()), Times.Never);
	}
}

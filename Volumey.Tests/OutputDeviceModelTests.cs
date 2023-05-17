using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Xunit;

namespace Volumey.Tests
{
    public class OutputDeviceModelTests
    {
        private OutputDeviceModel model;
        private Mock<IDevice> deviceMock;
        private Mock<IDeviceStateNotificationHandler> deviceStateMock;
        private Mock<ISessionProvider> sProviderMock;
        
        private static string deviceId = "111";
        private string deviceName = "headphones";

        public OutputDeviceModelTests()
        {
            var master = MasterSessionModelTests.GetMasterMock(deviceName, 70, false, deviceId, null);

            var processes = new List<AudioProcessModel>();
            processes.Add(AudioProcessModelTests.GetProcessMock("app", 50, false));
            
            this.deviceMock = new Mock<IDevice>();

            this.deviceMock.Setup(m => m.GetId()).Returns(deviceId);
            
            this.deviceStateMock = new Mock<IDeviceStateNotificationHandler>();
            this.sProviderMock = new Mock<ISessionProvider>();
            
            this.model = new OutputDeviceModel(deviceMock.Object, deviceStateMock.Object, sProviderMock.Object, master, processes);
        }

        [Fact]
        public void ProcessExitedEvent_ProcessShouldBeRemoved()
        {
            var process = this.model.Processes[0];
            MethodInfo processExitedHandler = typeof(OutputDeviceModel).GetMethod("OnProcessExitedAsync",
                                                                                   BindingFlags.NonPublic |
                                                                                   BindingFlags.Instance);

            //Simulate that the handler was called from the background thread because normally it invokes by events
            Task.Run(() =>
            {
                var task = (Task)processExitedHandler?.Invoke(this.model, new object[] { process });
                task.Wait();
            }).ContinueWith((t) =>
            {
                Assert.DoesNotContain(process, this.model.Processes);
            });
        }
        

        [Fact]
        public void DeviceNameChangedEvent_NamePropertiesShouldChange()
        {
            string newDeviceName = "speakers";
            this.deviceMock.Setup(m => m.GetFriendlyName()).Returns(newDeviceName);
            this.deviceMock.Setup(m => m.GetDeviceDesc()).Returns(newDeviceName);
            
            this.deviceStateMock.Raise(m => m.NameChanged += null, new object[] { deviceId });
            
            Assert.Equal(newDeviceName, this.model.Master.Name);
            Assert.Equal(newDeviceName, this.model.Master.DeviceDesc);
            Assert.Equal(newDeviceName, this.model.Master.DeviceFriendlyName);
        }

        [Fact]
        public void IconPathChangedEvent_DeviceIconShouldChanged()
        {
            var newIcon = SystemIcons.WinLogo;
            this.deviceMock.Setup(m => m.GetIcon()).Returns(newIcon);

            this.deviceStateMock.Raise(m => m.IconPathChanged += null, new object[] { deviceId });
            
            Assert.Equal(newIcon, this.model.Master.Icon);
        }

        internal static OutputDeviceModel GetDeviceMock(string id, string name, IDeviceStateNotificationHandler deviceStateHandler)
        {
            var master = MasterSessionModelTests.GetMasterMock(name, 70, false, id, null);
            
            var processes = new List<AudioProcessModel>();
            processes.Add(AudioProcessModelTests.GetProcessMock("app", 50, false));
		
            var deviceMock = new Mock<IDevice>();
            deviceMock.Setup(m => m.GetId()).Returns(id);
		
            var sProviderMock = new Mock<ISessionProvider>();
		
            return new OutputDeviceModel(deviceMock.Object, deviceStateHandler, sProviderMock.Object, master,
                                         processes);
        }
    }
}

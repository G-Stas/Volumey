using System.Collections.ObjectModel;
using System.Drawing;
using System.Reflection;
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
            var sessions = new ObservableCollection<AudioSessionModel>();
            sessions.Add(AudioSessionModelTests.GetSessionMock("app", 50, false, deviceId));
            
            this.deviceMock = new Mock<IDevice>();

            this.deviceMock.Setup(m => m.GetId()).Returns(deviceId);
            
            this.deviceStateMock = new Mock<IDeviceStateNotificationHandler>();
            this.sProviderMock = new Mock<ISessionProvider>();
            
            this.model = new OutputDeviceModel(deviceMock.Object, deviceStateMock.Object, sProviderMock.Object, master, sessions);
        }

        [Fact]
        public void SessionEndedEvent_SessionShouldBeRemoved()
        {
            var session = this.model.Sessions[0];
            MethodInfo OnSessionEndedHandler = typeof(AudioSessionModel).GetMethod("OnSessionEnded",
                BindingFlags.NonPublic | BindingFlags.Instance);

            OnSessionEndedHandler?.Invoke(session, null);

            Assert.DoesNotContain(session, this.model.Sessions);
        }

        [Fact]
        public void SessionCreatedEvent_SessionShouldBeAdded()
        {
            var sessionCount = model.Sessions.Count;
            var newSession = AudioSessionModelTests.GetSessionMock("session", 50, true, deviceId);
            
            this.sProviderMock.Raise(m => m.SessionCreated += null, new object[] {newSession});
            
            Assert.Equal(sessionCount + 1, this.model.Sessions.Count);
            Assert.Contains(newSession, this.model.Sessions);
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
            var sessions = new ObservableCollection<AudioSessionModel>();
            sessions.Add(AudioSessionModelTests.GetSessionMock("app", 50, false, "id"));
		
            var deviceMock = new Mock<IDevice>();
            deviceMock.Setup(m => m.GetId()).Returns(id);
		
            var sProviderMock = new Mock<ISessionProvider>();
		
            return new OutputDeviceModel(deviceMock.Object, deviceStateHandler, sProviderMock.Object, master,
                sessions);
        }
    }
}

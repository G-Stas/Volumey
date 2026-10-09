using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
    [TestClass]
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

        [TestMethod]
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
        

        [TestMethod]
        public void DeviceNameChangedEvent_NamePropertiesShouldChange()
        {
            string newDeviceName = "speakers";
            this.deviceMock.Setup(m => m.GetFriendlyName()).Returns(newDeviceName);
            this.deviceMock.Setup(m => m.GetDeviceDesc()).Returns(newDeviceName);
            
            this.deviceStateMock.Raise(m => m.NameChanged += null, new object[] { deviceId });
            
            Assert.AreEqual(newDeviceName, this.model.Master.Name);
            Assert.AreEqual(newDeviceName, this.model.Master.DeviceDesc);
            Assert.AreEqual(newDeviceName, this.model.Master.DeviceFriendlyName);
        }

        [TestMethod]
        public void IconPathChangedEvent_DeviceIconShouldChanged()
        {
            var newIcon = SystemIcons.WinLogo;
            this.deviceMock.Setup(m => m.GetIcon()).Returns(newIcon);

            this.deviceStateMock.Raise(m => m.IconPathChanged += null, new object[] { deviceId });
            
            Assert.AreEqual(newIcon, this.model.Master.Icon);
        }

        [TestMethod]
        public async Task ADisposedModelIsSafeToUse()
        {
            //listeners get the old device after it was disposed (#57)
            this.model.Dispose();
            this.model.Dispose();

            Assert.IsEmpty(await this.model.GetImmutableProcessesAsync());
            Assert.IsEmpty(this.model.GetImmutableProcesses());
            this.model.ResetVolumeHotkeys();
            this.model.ResetMuteHotkeys();
            this.model.ResetStateNotificationMediator(force: true);
        }

        [TestMethod]
        public void FormatChangeDisconnectsInvalidateTheDeviceOnce()
        {
            //#82, #85: turning spatial sound off disconnects all sessions without a format notification
            var notifications1 = new Mock<IAudioSessionStateNotifications>();
            var notifications2 = new Mock<IAudioSessionStateNotifications>();
            var model = CreateModelWithSessions(notifications1.Object, notifications2.Object);
            int invalidated = 0;
            model.Invalidated += m => invalidated++;

            notifications1.Raise(m => m.Disconnected += null, AudioSessionDisconnectReason.FormatChanged);
            notifications2.Raise(m => m.Disconnected += null, AudioSessionDisconnectReason.FormatChanged);

            Assert.AreEqual(1, invalidated);
        }

        [TestMethod]
        public void OtherDisconnectsDontInvalidateTheDevice()
        {
            var notifications = new Mock<IAudioSessionStateNotifications>();
            var model = CreateModelWithSessions(notifications.Object);
            int invalidated = 0;
            model.Invalidated += m => invalidated++;

            notifications.Raise(m => m.Disconnected += null, AudioSessionDisconnectReason.SessionLogoff);
            notifications.Raise(m => m.SessionEnded += null);

            Assert.AreEqual(0, invalidated);
        }

        [TestMethod]
        public void AnUnreadableNewSessionInvalidatesTheDevice()
        {
            int invalidated = 0;
            this.model.Invalidated += m => invalidated++;

            this.sProviderMock.Raise(m => m.DeviceInvalidated += null);

            Assert.AreEqual(1, invalidated);
        }

        [TestMethod]
        public void OnlyARealFormatChangeInvalidatesTheDevice()
        {
            var stereo48 = new DeviceFormat(2, 48000, 288000, 6, 24);
            var stereo96 = new DeviceFormat(2, 96000, 576000, 6, 24);
            this.deviceMock.Setup(m => m.GetDeviceFormat()).Returns(stereo48);
            var model = new OutputDeviceModel(this.deviceMock.Object, this.deviceStateMock.Object, this.sProviderMock.Object,
                MasterSessionModelTests.GetMasterMock(deviceName, 70, false, deviceId, null), new List<AudioProcessModel>());
            int invalidated = 0;
            model.Invalidated += m => invalidated++;

            this.deviceStateMock.Raise(m => m.FormatChanged += null, deviceId);
            Assert.AreEqual(0, invalidated);

            this.deviceMock.Setup(m => m.GetDeviceFormat()).Returns(stereo96);
            this.deviceStateMock.Raise(m => m.FormatChanged += null, "another device");
            Assert.AreEqual(0, invalidated);
            this.deviceStateMock.Raise(m => m.FormatChanged += null, deviceId);
            Assert.AreEqual(1, invalidated);
        }

        private OutputDeviceModel CreateModelWithSessions(params IAudioSessionStateNotifications[] sessionNotifications)
        {
            var processes = new List<AudioProcessModel>();
            int n = 0;
            foreach(var notifications in sessionNotifications)
            {
                var process = AudioProcessModelTests.GetProcessMock("app" + n, 50, false);
                process.AddSession(new AudioSessionModel(false, 50, (n++).ToString(), default, default, default, System.Guid.Empty,
                    new Mock<IAudioSessionVolume>().Object, notifications));
                processes.Add(process);
            }
            return new OutputDeviceModel(this.deviceMock.Object, this.deviceStateMock.Object, this.sProviderMock.Object,
                MasterSessionModelTests.GetMasterMock(deviceName, 70, false, deviceId, null), processes);
        }

        internal static OutputDeviceModel GetDeviceMock(string id, string name, IDeviceStateNotificationHandler deviceStateHandler)
            => GetDeviceMock(id, name, deviceStateHandler, new Mock<ISessionProvider>());

        internal static OutputDeviceModel GetDeviceMock(string id, string name, IDeviceStateNotificationHandler deviceStateHandler, Mock<ISessionProvider> sProviderMock)
        {
            var master = MasterSessionModelTests.GetMasterMock(name, 70, false, id, null);
            
            var processes = new List<AudioProcessModel>();
            processes.Add(AudioProcessModelTests.GetProcessMock("app", 50, false));
		
            var deviceMock = new Mock<IDevice>();
            deviceMock.Setup(m => m.GetId()).Returns(id);

            return new OutputDeviceModel(deviceMock.Object, deviceStateHandler, sProviderMock.Object, master,
                                         processes);
        }
    }
}

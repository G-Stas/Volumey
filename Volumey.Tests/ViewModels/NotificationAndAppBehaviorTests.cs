using System;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Notification.Wpf.Controls;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels
{
	[TestClass]
	public class NotificationAndAppBehaviorTests : ViewModelTestBase
	{
		private static readonly ScreenInfo Primary = new ScreenInfo(".//DISPLAY1", true, 1920, 1040, 1920, 1080, 0, 1920, 0, 1040, 1);
		private static readonly ScreenInfo Second = new ScreenInfo(".//DISPLAY2", false, 2560, 1400, 2560, 1440, 1920, 4480, 0, 1400, 1);

		private Mock<IScreenInfoProvider> screens;
		private Mock<IAppShell> shell;
		private Mock<ISystemIntegration> systemIntegration;

		[TestInitialize]
		public void Initialize()
		{
			this.screens = new Mock<IScreenInfoProvider>();
			this.screens.Setup(m => m.GetAllScreensInfo()).Returns(new[] { Primary, Second });
			this.screens.Setup(m => m.GetPrimaryScreenInfo()).Returns(Primary);
			this.shell = new Mock<IAppShell>();
			this.systemIntegration = new Mock<ISystemIntegration>();
		}

		private NotificationViewModel CreateNotifications()
			=> new NotificationViewModel(this.DeviceProvider.Object, this.Notifications.Object, this.screens.Object);

		private AppBehaviorViewModel CreateAppBehavior(IHotkeyService hotkeys = null)
			=> new AppBehaviorViewModel(hotkeys ?? this.Hotkeys, this.DeviceProvider.Object, this.screens.Object, this.shell.Object, this.systemIntegration.Object);

		[TestMethod]
		public void SavedNotificationSettingsAreAppliedOnStart()
		{
			this.Settings.Notifications.Position = NotificationPosition.BottomCenter;
			this.Settings.Notifications.VerticalIndent = 40;
			this.Settings.Notifications.HorizontalIndent = 30;
			this.Settings.Notifications.DisplayTimeSeconds = 5;
			this.Settings.Notifications.ScreenIndex = 1;

			var vm = CreateNotifications();

			Assert.AreEqual(NotificationPosition.BottomCenter, vm.SelectedPosition);
			Assert.IsTrue(vm.NotificationsPositionIsCentered);
			Assert.AreEqual(Second, vm.SelectedScreen);
			this.Notifications.Verify(m => m.ChangePosition(NotificationPosition.BottomCenter));
			this.Notifications.Verify(m => m.SetVerticalIndent(40));
			this.Notifications.Verify(m => m.SetHorizontalIndent(30));
			this.Notifications.Verify(m => m.SetNotificationDisplayTime(5));
			this.Notifications.Verify(m => m.SetWindowWorkArea(Second.WorkingAreaLeft, Second.WorkingAreaTop, Second.Width, Second.Height));
		}

		[TestMethod]
		public void UnknownScreenFallsBackToThePrimaryOne()
		{
			this.Settings.Notifications.ScreenIndex = 7;

			var vm = CreateNotifications();

			Assert.AreEqual(Primary, vm.SelectedScreen);
			Assert.AreEqual(0, this.Settings.Notifications.ScreenIndex);
		}

		[TestMethod]
		public void NotificationChangesAreAppliedAndSaved()
		{
			var vm = CreateNotifications();

			vm.SelectedPosition = NotificationPosition.TopRight;
			vm.VerticalIndent = 25;
			vm.DisplayTime = 7;
			vm.NotificationsEnabled = !this.Settings.Notifications.Enabled;
			bool enabled = vm.NotificationsEnabled;

			this.Notifications.Verify(m => m.ChangePosition(NotificationPosition.TopRight));
			this.Notifications.Verify(m => m.SetVerticalIndent(25));
			this.Notifications.Verify(m => m.SetNotificationDisplayTime(7));
			Assert.AreEqual(25, this.Settings.Notifications.VerticalIndent);
			Assert.AreEqual(7, this.Settings.Notifications.DisplayTimeSeconds);
			WaitFor(() => this.Settings.Notifications.Position == NotificationPosition.TopRight, "the position wasn't saved");
			WaitFor(() => this.Settings.Notifications.Enabled == enabled, "the on/off state wasn't saved");
		}

		[TestMethod]
		public void OutOfRangeValuesAreReset()
		{
			var vm = CreateNotifications();
			vm.HorizontalIndent = 50;

			vm.HorizontalIndent = NotificationViewModel.MaxIndent + 1;
			vm.DisplayTime = vm.MaxDisplayTime + 1;

			Assert.AreEqual(NotificationViewModel.MinIndent, vm.HorizontalIndent);
			Assert.AreEqual(50, this.Settings.Notifications.HorizontalIndent, "an invalid indent isn't saved");
			Assert.AreEqual(3, vm.DisplayTime);
		}

		[TestMethod]
		public void PreviewShowsAndClosesTheExample()
		{
			var vm = CreateNotifications();

			vm.PreviewIsOn = true;
			vm.UnloadedCommand.Execute(null);

			this.Notifications.Verify(m => m.ShowNotificationExample(), Times.Once);
			this.Notifications.Verify(m => m.CloseNotificationExample(), Times.Once);
			Assert.IsFalse(vm.PreviewIsOn);
		}

		[TestMethod]
		public void ReactingToAllChangesWatchesTheDefaultDeviceApps()
		{
			var app = CreateProcess("app");
			var device = CreateDevice("1", "Speakers", app);
			SetDefaultDevice(device);
			var vm = CreateNotifications();

			vm.ReactToAllVolumeChanges = true;

			WaitFor(() => app.StateNotificationMediator != null, "the default device's apps didn't get the notification mediator");
			Assert.IsNotNull(device.Master.StateNotificationMediator);
			Assert.IsTrue(this.Settings.Notifications.ReactToAllVolumeChanges);
			var newApp = CreateProcess("new");
			RaiseProcessCreated(device, newApp);
			Assert.IsNotNull(newApp.StateNotificationMediator);

			vm.ReactToAllVolumeChanges = false;
			WaitFor(() => app.StateNotificationMediator == null, "the mediator wasn't removed");
		}

		[TestMethod]
		public void WindowStartsHiddenWhenStartedMinimized()
		{
			this.shell.Setup(m => m.StartMinimized).Returns(true);
			Assert.IsFalse(CreateAppBehavior().WindowIsVisible);

			this.shell.Setup(m => m.StartMinimized).Returns(false);
			Assert.IsTrue(CreateAppBehavior().WindowIsVisible);
		}

		[TestMethod]
		public void WindowOptionsAreSaved()
		{
			var vm = CreateAppBehavior();

			vm.PopupEnabled = true;
			vm.AlwaysOnTop = true;
			vm.DeviceViewAtTheBottom = true;
			vm.DisplayDeviceIconAtTray = true;
			vm.SelectedScreen = Second;

			Assert.IsTrue(this.Settings.Window.PopupMode);
			Assert.IsTrue(this.Settings.Window.AlwaysOnTop);
			Assert.IsTrue(this.Settings.Window.DeviceViewAtTheBottom);
			Assert.IsTrue(this.Settings.Window.DisplayDeviceIconAtTray);
			Assert.AreEqual(1, this.Settings.Window.ScreenIndex);
			Assert.IsTrue(vm.DisplayMinimalistic);
		}

		[TestMethod]
		public void TrayClickTogglesTheWindow()
		{
			this.shell.Setup(m => m.StartMinimized).Returns(true);
			var vm = CreateAppBehavior();
			int requests = 0;
			vm.DisplayAppRequested += (settingsPage, visible, popup) => requests++;

			vm.TrayClickCommand.Execute(null);
			Assert.IsTrue(vm.WindowIsVisible);
			Assert.AreEqual(1, requests);

			vm.TrayClickCommand.Execute(null);
			Assert.IsFalse(vm.WindowIsVisible);
		}

		[TestMethod]
		public void PopupHidesWhenItLosesFocus()
		{
			this.shell.Setup(m => m.StartMinimized).Returns(true);
			var vm = CreateAppBehavior();
			vm.PopupEnabled = true;
			vm.TrayClickCommand.Execute(null);
			Assert.IsTrue(vm.WindowIsVisible);

			vm.LostFocusCommand.Execute(null);

			Assert.IsFalse(vm.WindowIsVisible);
		}

		[TestMethod]
		public void ClosingTheWindowOnlyHidesIt()
		{
			var vm = CreateAppBehavior();
			var args = new CancelEventArgs();

			vm.ClosingCommand.Execute(args);

			Assert.IsTrue(args.Cancel);
			Assert.IsFalse(vm.WindowIsVisible);
		}

		[TestMethod]
		public void OpenMixerHotkeyTogglesTheWindow()
		{
			this.shell.Setup(m => m.StartMinimized).Returns(true);
			var hotkeys = new Mock<IHotkeyService>();
			var vm = CreateAppBehavior(hotkeys.Object);

			hotkeys.Raise(m => m.OpenHotkeyPressed += null);

			Assert.IsTrue(vm.WindowIsVisible);
		}

		[TestMethod]
		public void SessionEndingSavesThePositionAndShutsDown()
		{
			var hotkeys = new Mock<IHotkeyService>();
			var vm = CreateAppBehavior(hotkeys.Object);
			vm.WindowLeft = 123;
			vm.WindowTop = 45;

			this.shell.Raise(m => m.SessionEnding += null);

			Assert.AreEqual(123, this.Settings.Window.Left);
			Assert.AreEqual(45, this.Settings.Window.Top);
			hotkeys.Verify(m => m.Dispose(), Times.Once);
			this.shell.Verify(m => m.Shutdown(), Times.Once);
		}

		[TestMethod]
		public void DeviceFormatChangeRestartsTheAppOnce()
		{
			this.shell.Setup(m => m.StartMinimized).Returns(true);
			this.shell.Setup(m => m.Dispatcher).Returns(System.Windows.Threading.Dispatcher.CurrentDispatcher);
			var vm = CreateAppBehavior(new Mock<IHotkeyService>().Object);
			var device = CreateDevice("1", "Speakers");

			this.DeviceProvider.Raise(m => m.DeviceFormatChanged += null, device);
			this.DeviceProvider.Raise(m => m.DeviceFormatChanged += null, device);

			this.shell.Verify(m => m.StartNewInstance(true), Times.Once);
			this.shell.Verify(m => m.Shutdown(), Times.Once);
		}

		[TestMethod]
		public void AutostartAndStartMenuUseTheSystemIntegration()
		{
			this.systemIntegration.Setup(m => m.LaunchesAtStartup()).Returns(false);
			this.systemIntegration.Setup(m => m.EnableLaunchAtStartup()).Returns(true);
			this.systemIntegration.Setup(m => m.IsInStartMenu()).Returns(true);
			var vm = CreateAppBehavior();
			Assert.IsFalse(vm.LaunchAtStartup);
			Assert.IsTrue(vm.AddToStartMenu);

			vm.LaunchAtStartup = true;
			vm.AddToStartMenu = false;

			Assert.IsTrue(vm.LaunchAtStartup);
			this.systemIntegration.Verify(m => m.EnableLaunchAtStartup(), Times.Once);
			this.systemIntegration.Verify(m => m.RemoveFromStartMenu(), Times.Once);
		}

		[TestMethod]
		public void DeviceProviderIsDisposedWhenTheAppExits()
		{
			var vm = new DeviceProviderViewModel(this.DeviceProvider.Object, this.shell.Object);

			this.shell.Raise(m => m.Exit += null);

			this.DeviceProvider.Verify(m => m.Dispose(), Times.Once);
		}
	}
}

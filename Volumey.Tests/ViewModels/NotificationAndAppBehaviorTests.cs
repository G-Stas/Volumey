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

namespace Volumey.Tests.ViewModels;

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
		screens = new Mock<IScreenInfoProvider>();
		screens.Setup(m => m.GetAllScreensInfo()).Returns(new[] { Primary, Second });
		shell = new Mock<IAppShell>();
		systemIntegration = new Mock<ISystemIntegration>();
	}

	private NotificationViewModel CreateNotifications()
		=> new NotificationViewModel(DeviceProvider.Object, Notifications.Object, screens.Object);

	private AppBehaviorViewModel CreateAppBehavior(IHotkeyService hotkeys = null)
		=> new AppBehaviorViewModel(hotkeys ?? Hotkeys, screens.Object, shell.Object, systemIntegration.Object);

	[TestMethod]
	public void SavedNotificationSettingsAreAppliedOnStart()
	{
		Settings.Notifications.Position = NotificationPosition.BottomCenter;
		Settings.Notifications.VerticalIndent = 40;
		Settings.Notifications.HorizontalIndent = 30;
		Settings.Notifications.DisplayTimeSeconds = 5;
		Settings.Notifications.ScreenName = Second.Name;

		var vm = CreateNotifications();

		Assert.AreEqual(NotificationPosition.BottomCenter, vm.SelectedPosition);
		Assert.IsTrue(vm.NotificationsPositionIsCentered);
		Assert.AreEqual(Second, vm.SelectedScreen);
		Notifications.Verify(m => m.ChangePosition(NotificationPosition.BottomCenter));
		Notifications.Verify(m => m.SetVerticalIndent(40));
		Notifications.Verify(m => m.SetHorizontalIndent(30));
		Notifications.Verify(m => m.SetNotificationDisplayTime(5));
		Notifications.Verify(m => m.SetWindowWorkArea(Second.WorkingAreaLeft, Second.WorkingAreaTop, Second.Width, Second.Height));
	}

	[TestMethod]
	public void TheOsdAreaOfAScaledScreenIsInDeviceIndependentUnits()
	{
		//#76: a 125% screen right of a 100% one, values in physical pixels as WpfScreenHelper gives them
		var scaled = new ScreenInfo(@"\\.\DISPLAY2", false, 1920, 1020, 1920, 1080, 2560, 4480, 0, 1020, 1.25);
		screens.Setup(m => m.GetAllScreensInfo()).Returns(new[] { Primary, scaled });
		Settings.Notifications.ScreenName = scaled.Name;

		CreateNotifications();

		Notifications.Verify(m => m.SetWindowWorkArea(2048, 0, 1536, 816));
	}

	[TestMethod]
	public void MissingScreenFallsBackToThePrimaryOneAndStaysRemembered()
	{
		//#92: e.g. a monitor that turns on after the app has started
		Settings.Notifications.ScreenName = @".//DISPLAY3";
		Settings.Window.ScreenName = @".//DISPLAY3";

		var notifications = CreateNotifications();
		var window = CreateAppBehavior();

		Assert.AreEqual(Primary, notifications.SelectedScreen);
		Assert.AreEqual(Primary, window.SelectedScreen);
		Assert.AreEqual(@".//DISPLAY3", Settings.Notifications.ScreenName);
		Assert.AreEqual(@".//DISPLAY3", Settings.Window.ScreenName);
		Notifications.Verify(m => m.SetWindowWorkArea(Primary.WorkingAreaLeft, Primary.WorkingAreaTop, Primary.Width, Primary.Height));
	}

	[TestMethod]
	public void TheWindowMovesToTheRememberedScreenWhenItConnects()
	{
		Settings.Window.ScreenName = Second.Name;
		screens.Setup(m => m.GetAllScreensInfo()).Returns(new[] { Primary });
		shell.Setup(m => m.Dispatcher).Returns(StartDispatcherThread());
		var vm = CreateAppBehavior();
		Assert.AreEqual(Primary, vm.SelectedScreen);

		screens.Setup(m => m.GetAllScreensInfo()).Returns(new[] { Primary, Second });
		vm.LoadedCommand.Execute(null);

		WaitFor(() => vm.SelectedScreen.Equals(Second), "the window didn't move to the remembered screen");
		Assert.HasCount(2, vm.AllScreens);
	}

	[TestMethod]
	public void AnEmptyScreenFromTheComboBoxIsIgnored()
	{
		Settings.Window.ScreenName = Second.Name;
		var vm = CreateAppBehavior();

		//a combo box writes back an empty item while its list is replaced
		vm.SelectedScreen = default;

		Assert.AreEqual(Second, vm.SelectedScreen);
		Assert.AreEqual(Second.Name, Settings.Window.ScreenName);
	}

	private static System.Windows.Threading.Dispatcher StartDispatcherThread()
	{
		System.Windows.Threading.Dispatcher dispatcher = null;
		using var started = new System.Threading.ManualResetEventSlim();
		var thread = new System.Threading.Thread(() =>
		{
			dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
			started.Set();
			System.Windows.Threading.Dispatcher.Run();
		})
		{ IsBackground = true };
		thread.SetApartmentState(System.Threading.ApartmentState.STA);
		thread.Start();
		started.Wait();
		return dispatcher;
	}

	[TestMethod]
	public void NotificationChangesAreAppliedAndSaved()
	{
		var vm = CreateNotifications();

		vm.SelectedPosition = NotificationPosition.TopRight;
		vm.VerticalIndent = 25;
		vm.DisplayTime = 7;
		vm.NotificationsEnabled = !Settings.Notifications.Enabled;
		bool enabled = vm.NotificationsEnabled;

		Notifications.Verify(m => m.ChangePosition(NotificationPosition.TopRight));
		Notifications.Verify(m => m.SetVerticalIndent(25));
		Notifications.Verify(m => m.SetNotificationDisplayTime(7));
		Assert.AreEqual(25, Settings.Notifications.VerticalIndent);
		Assert.AreEqual(7, Settings.Notifications.DisplayTimeSeconds);
		WaitFor(() => Settings.Notifications.Position == NotificationPosition.TopRight, "the position wasn't saved");
		WaitFor(() => Settings.Notifications.Enabled == enabled, "the on/off state wasn't saved");
	}

	[TestMethod]
	public void OutOfRangeValuesAreReset()
	{
		var vm = CreateNotifications();
		vm.HorizontalIndent = 50;

		vm.HorizontalIndent = NotificationViewModel.MaxIndent + 1;
		vm.DisplayTime = vm.MaxDisplayTime + 1;

		Assert.AreEqual(NotificationViewModel.MinIndent, vm.HorizontalIndent);
		Assert.AreEqual(50, Settings.Notifications.HorizontalIndent, "an invalid indent isn't saved");
		Assert.AreEqual(3, vm.DisplayTime);
	}

	[TestMethod]
	public void PreviewShowsAndClosesTheExample()
	{
		var vm = CreateNotifications();

		vm.PreviewIsOn = true;
		vm.UnloadedCommand.Execute(null);

		Notifications.Verify(m => m.ShowNotificationExample(), Times.Once);
		Notifications.Verify(m => m.CloseNotificationExample(), Times.Once);
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
		Assert.IsTrue(Settings.Notifications.ReactToAllVolumeChanges);
		var newApp = CreateProcess("new");
		RaiseProcessCreated(device, newApp);
		Assert.IsNotNull(newApp.StateNotificationMediator);

		vm.ReactToAllVolumeChanges = false;
		WaitFor(() => app.StateNotificationMediator == null, "the mediator wasn't removed");
	}

	[TestMethod]
	public void ADisposedOldDefaultDeviceDoesntBreakTheSwitch()
	{
		//#57: after sleep a headset is removed (its model disposed) before Windows switches the default device
		var headset = CreateDevice("1", "Headset", CreateProcess("app"));
		SetDefaultDevice(headset);
		var vm = CreateNotifications();
		vm.ReactToAllVolumeChanges = true;
		WaitFor(() => headset.Master.StateNotificationMediator != null, "the headset didn't get the notification mediator");

		headset.Dispose();
		var speakers = CreateDevice("2", "Speakers", CreateProcess("other"));
		ChangeDefaultDevice(speakers);

		WaitFor(() => speakers.Master.StateNotificationMediator != null, "the new default device wasn't set up");
	}

	[TestMethod]
	public void WindowStartsHiddenWhenStartedMinimized()
	{
		shell.Setup(m => m.StartMinimized).Returns(true);
		Assert.IsFalse(CreateAppBehavior().WindowIsVisible);

		shell.Setup(m => m.StartMinimized).Returns(false);
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

		Assert.IsTrue(Settings.Window.PopupMode);
		Assert.IsTrue(Settings.Window.AlwaysOnTop);
		Assert.IsTrue(Settings.Window.DeviceViewAtTheBottom);
		Assert.IsTrue(Settings.Window.DisplayDeviceIconAtTray);
		Assert.AreEqual(Second.Name, Settings.Window.ScreenName);
		Assert.IsTrue(vm.DisplayMinimalistic);
	}

	[TestMethod]
	public void TrayClickTogglesTheWindow()
	{
		shell.Setup(m => m.StartMinimized).Returns(true);
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
		shell.Setup(m => m.StartMinimized).Returns(true);
		var vm = CreateAppBehavior();
		vm.PopupEnabled = true;
		vm.TrayClickCommand.Execute(null);
		Assert.IsTrue(vm.WindowIsVisible);

		vm.LostFocusCommand.Execute(null);

		Assert.IsFalse(vm.WindowIsVisible);
	}

	[TestMethod]
	public void StartingTheAppAgainOpensThePopupInPopupMode()
	{
		shell.Setup(m => m.StartMinimized).Returns(true);
		var vm = CreateAppBehavior();
		vm.PopupEnabled = true;
		bool? openedAsPopup = null;
		vm.DisplayAppRequested += (settingsPage, visible, popup) => openedAsPopup = popup;

		vm.OnShowRequestedBySecondInstance();

		Assert.IsTrue(vm.WindowIsVisible);
		Assert.IsTrue(vm.DisplayMinimalistic);
		Assert.AreEqual(true, openedAsPopup);

		//a second request doesn't hide the popup like a tray click would
		vm.OnShowRequestedBySecondInstance();
		Assert.IsTrue(vm.WindowIsVisible);
		Assert.AreEqual(true, openedAsPopup);
	}

	[TestMethod]
	public void StartingTheAppAgainOpensTheWindowWithoutPopupMode()
	{
		shell.Setup(m => m.StartMinimized).Returns(true);
		var vm = CreateAppBehavior();
		bool? openedAsPopup = null;
		vm.DisplayAppRequested += (settingsPage, visible, popup) => openedAsPopup = popup;

		vm.OnShowRequestedBySecondInstance();
		vm.OnShowRequestedBySecondInstance();

		Assert.IsTrue(vm.WindowIsVisible);
		Assert.IsFalse(vm.DisplayMinimalistic);
		Assert.AreEqual(false, openedAsPopup);
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
		shell.Setup(m => m.StartMinimized).Returns(true);
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

		shell.Raise(m => m.SessionEnding += null);

		Assert.AreEqual(123, Settings.Window.Left);
		Assert.AreEqual(45, Settings.Window.Top);
		hotkeys.Verify(m => m.Dispose(), Times.Once);
		shell.Verify(m => m.Shutdown(), Times.Once);
	}

	[TestMethod]
	public void AutostartAndStartMenuUseTheSystemIntegration()
	{
		systemIntegration.Setup(m => m.LaunchesAtStartup()).Returns(false);
		systemIntegration.Setup(m => m.EnableLaunchAtStartup()).Returns(true);
		systemIntegration.Setup(m => m.IsInStartMenu()).Returns(true);
		var vm = CreateAppBehavior();
		Assert.IsFalse(vm.LaunchAtStartup);
		Assert.IsTrue(vm.AddToStartMenu);

		vm.LaunchAtStartup = true;
		vm.AddToStartMenu = false;

		Assert.IsTrue(vm.LaunchAtStartup);
		systemIntegration.Verify(m => m.EnableLaunchAtStartup(), Times.Once);
		systemIntegration.Verify(m => m.RemoveFromStartMenu(), Times.Once);
	}

	[TestMethod]
	public void DeviceProviderIsDisposedWhenTheAppExits()
	{
		var vm = new DeviceProviderViewModel(DeviceProvider.Object, shell.Object);

		shell.Raise(m => m.Exit += null);

		DeviceProvider.Verify(m => m.Dispose(), Times.Once);
	}
}

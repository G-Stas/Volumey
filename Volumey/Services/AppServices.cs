using System;
using System.Windows;
using Volumey.DataProvider;
using Volumey.ViewModel.Settings;

namespace Volumey.Services;

/// <summary>
/// The app's shared services, created once (composition root).
/// Only the parameterless constructors that XAML needs, and controls, read <see cref="Current"/>;
/// every other class gets its services through its constructor, so tests can pass their own.
/// Each service is created on first use.
/// </summary>
internal sealed class AppServices
{
	private static AppServices current;

	/// <summary>
	/// The services of the running app. Use on the UI thread.
	/// </summary>
	internal static AppServices Current => current ??= CreateDefault();

	private readonly Lazy<IHotkeyService> hotkeys;
	private readonly Lazy<IDeviceProvider> devices;
	private readonly Lazy<INotificationService> notifications;
	private readonly Lazy<IScreenInfoProvider> screens;
	private readonly Lazy<IAppShell> shell;
	private readonly Lazy<ISystemIntegration> systemIntegration;

	internal AppServices(Func<IHotkeyService> hotkeys, Func<IDeviceProvider> devices, Func<INotificationService> notifications,
		Func<IScreenInfoProvider> screens, Func<IAppShell> shell, Func<ISystemIntegration> systemIntegration)
	{
		this.hotkeys = new Lazy<IHotkeyService>(hotkeys);
		this.devices = new Lazy<IDeviceProvider>(devices);
		this.notifications = new Lazy<INotificationService>(notifications);
		this.screens = new Lazy<IScreenInfoProvider>(screens);
		this.shell = new Lazy<IAppShell>(shell);
		this.systemIntegration = new Lazy<ISystemIntegration>(systemIntegration);
	}

	public IHotkeyService Hotkeys => hotkeys.Value;

	/// <summary>
	/// Creating it enumerates the audio devices and their sessions
	/// </summary>
	public IDeviceProvider Devices => devices.Value;

	public INotificationService Notifications => notifications.Value;

	public IScreenInfoProvider Screens => screens.Value;

	public IAppShell Shell => shell.Value;

	public ISystemIntegration SystemIntegration => systemIntegration.Value;

	private static AppServices CreateDefault()
	{
		AppServices services = null;
		services = new AppServices(
			() => new HotkeyService(),
			() => DeviceProvider.Create(services.Hotkeys),
			() => new NotificationService(),
			() => new ScreenInfoProvider(),
			() => new AppShell(Application.Current),
			() => new SystemIntegration());
		return services;
	}
}

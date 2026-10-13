using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Volumey.DataProvider;
using Volumey.View;
using Volumey.ViewModel;
using Volumey.ViewModel.Settings;

namespace Volumey.Services;

/// <summary>
/// The app's services, view models and windows (composition root). Everything is a singleton: a second instance
/// would register hotkeys twice or show a second tray icon.
/// The view models' constructors are internal (they take internal types), so they're registered with factories
/// </summary>
internal static class ServiceRegistration
{
	internal static IServiceCollection AddVolumey(this IServiceCollection services, Application application)
	{
		services.AddSingleton<IHotkeyService, HotkeyService>();
		//creating it enumerates the audio devices and their sessions
		services.AddSingleton(sp => DeviceProvider.Create(sp.Get<IHotkeyService>()));
		services.AddSingleton<INotificationService>(sp => new NotificationService(sp.Get<IHotkeyService>()));
		services.AddSingleton<IScreenInfoProvider, ScreenInfoProvider>();
		services.AddSingleton<IAppShell>(_ => new AppShell(application));
		services.AddSingleton<ISystemIntegration, SystemIntegration>();
		services.AddSingleton(_ => new SupportLinks());

		services.AddSingleton<ThemeViewModel>();
		services.AddSingleton(sp => new AppBehaviorViewModel(sp.Get<IHotkeyService>(), sp.Get<IScreenInfoProvider>(),
			sp.Get<IAppShell>(), sp.Get<ISystemIntegration>()));
		services.AddSingleton(sp => new DeviceProviderViewModel(sp.Get<IDeviceProvider>(), sp.Get<IAppShell>()));

		services.AddSingleton(_ => new LangSettings());
		services.AddSingleton(sp => new AppsHotkeysViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>(), sp.Get<INotificationService>()));
		services.AddSingleton(sp => new DeviceVolumeHotkeysViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>(), sp.Get<INotificationService>()));
		services.AddSingleton(sp => new OpenHotkeyViewModel(sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new VolumeLimitViewModel(sp.Get<IDeviceProvider>()));
		services.AddSingleton(sp => new AppVolumeSyncViewModel(sp.Get<IDeviceProvider>()));
		services.AddSingleton(sp => new DefaultDeviceHotkeysViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new ForegroundWindowVolumeViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>(), sp.Get<INotificationService>()));
		services.AddSingleton(sp => new NotificationViewModel(sp.Get<IDeviceProvider>(), sp.Get<INotificationService>(), sp.Get<IScreenInfoProvider>()));
		services.AddSingleton(sp => new SystemHotkeysViewModel(sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new AboutViewModel(sp.Get<SupportLinks>()));
		//the arguments in this order: the hotkey features register their saved hotkeys in the order they were created
		services.AddSingleton(sp => new SettingsViewModel(sp.Get<IHotkeyService>(), sp.Get<LangSettings>(), sp.Get<AppsHotkeysViewModel>(),
			sp.Get<DeviceVolumeHotkeysViewModel>(), sp.Get<OpenHotkeyViewModel>(), sp.Get<VolumeLimitViewModel>(), sp.Get<AppVolumeSyncViewModel>(),
			sp.Get<DefaultDeviceHotkeysViewModel>(), sp.Get<ForegroundWindowVolumeViewModel>(), sp.Get<NotificationViewModel>(),
			sp.Get<SystemHotkeysViewModel>(), sp.Get<AboutViewModel>()));

		services.AddSingleton(sp => new MainView(sp.Get<AppBehaviorViewModel>(), sp.Get<SettingsViewModel>()));
		return services;
	}

	private static T Get<T>(this IServiceProvider provider) => provider.GetRequiredService<T>();
}

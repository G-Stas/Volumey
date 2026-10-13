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
		services.AddSingleton(sp => DeviceProvider.Create(sp.GetRequiredService<IHotkeyService>()));
		services.AddSingleton<INotificationService>(sp => new NotificationService(sp.GetRequiredService<IHotkeyService>()));
		services.AddSingleton<IScreenInfoProvider, ScreenInfoProvider>();
		services.AddSingleton<IAppShell>(_ => new AppShell(application));
		services.AddSingleton<ISystemIntegration, SystemIntegration>();

		services.AddSingleton<ThemeViewModel>();
		services.AddSingleton(sp => new AppBehaviorViewModel(sp.GetRequiredService<IHotkeyService>(), sp.GetRequiredService<IScreenInfoProvider>(),
			sp.GetRequiredService<IAppShell>(), sp.GetRequiredService<ISystemIntegration>()));
		services.AddSingleton(sp => new DeviceProviderViewModel(sp.GetRequiredService<IDeviceProvider>(), sp.GetRequiredService<IAppShell>()));
		services.AddSingleton(sp => new SettingsViewModel(sp.GetRequiredService<IHotkeyService>(), sp.GetRequiredService<IDeviceProvider>(),
			sp.GetRequiredService<INotificationService>(), sp.GetRequiredService<IScreenInfoProvider>()));

		services.AddSingleton(sp => new MainView(sp.GetRequiredService<AppBehaviorViewModel>(), sp.GetRequiredService<SettingsViewModel>()));
		return services;
	}
}

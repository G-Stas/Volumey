#nullable enable
using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.View;
using Volumey.View.SettingsPage;
using Volumey.ViewModel;
using Volumey.ViewModel.Pages;
using Volumey.ViewModel.Settings;
using Wpf.Ui.Abstractions;

namespace Volumey.Services;

/// <summary>
/// The app's services, view models and windows (composition root). Everything is a singleton: a second instance
/// would register hotkeys twice or show a second tray icon.
/// The view models' constructors are internal (they take internal types), so they're registered with factories
/// </summary>
internal static class ServiceRegistration
{
	/// <summary>
	/// The settings features the app creates at startup, in this order, not when their page first opens: the language is applied,
	/// the hotkey features register their saved hotkeys (in this order) once the hotkey service gets a manager, the volume limit,
	/// app volume sync and the OSD start following the devices, and the support links are read
	/// </summary>
	internal static readonly Type[] Features =
	[
		typeof(LangSettings),
		typeof(AppsHotkeysViewModel),
		typeof(DeviceVolumeHotkeysViewModel),
		typeof(OpenHotkeyViewModel),
		typeof(VolumeLimitViewModel),
		typeof(AppVolumeSyncViewModel),
		typeof(DefaultDeviceHotkeysViewModel),
		typeof(ForegroundWindowVolumeViewModel),
		typeof(NotificationViewModel),
		typeof(SystemHotkeysViewModel),
		typeof(AboutViewModel)
	];

	internal static IServiceCollection AddVolumey(this IServiceCollection services, Application application)
	{
		services.AddSingleton<IHotkeyService, HotkeyService>();
		//creating it enumerates the audio devices and their sessions
		services.AddSingleton(sp => DeviceProvider.Create(sp.Get<IHotkeyService>()));
		services.AddSingleton<INotificationService>(sp => new NotificationService(sp.Get<IHotkeyService>()));
		//creating it doesn't call Windows: the policy factory is read on first use
		services.AddSingleton<IAppAudioRouting>(_ => new AudioPolicyRouting());
		services.AddSingleton(sp => new AppRouting(sp.Get<IDeviceProvider>(), sp.Get<IAppAudioRouting>(), DeviceEnumerator.GetDeviceName,
			sp.Get<IAppShell>().Dispatcher));
		services.AddSingleton<IAppRouting>(sp => sp.Get<AppRouting>());
		services.AddSingleton<IScreenInfoProvider, ScreenInfoProvider>();
		services.AddSingleton<IAppShell>(_ => new AppShell(application));
		services.AddSingleton<ISystemIntegration, SystemIntegration>();
		services.AddSingleton(_ => new SupportLinks());

		services.AddSingleton<ThemeViewModel>();
		services.AddSingleton(sp => new AppBehaviorViewModel(sp.Get<IHotkeyService>(), sp.Get<IScreenInfoProvider>(),
			sp.Get<IAppShell>(), sp.Get<ISystemIntegration>()));
		services.AddSingleton(sp => new DeviceProviderViewModel(sp.Get<IDeviceProvider>(), sp.Get<IAppShell>(), sp.Get<IAppRouting>()));
		services.AddSingleton(sp => new HotkeyModeViewModel(sp.Get<IHotkeyService>()));

		services.AddSingleton(_ => new LangSettings());
		services.AddSingleton(sp => new AppsHotkeysViewModel(sp.Get<IHotkeyService>(), sp.Get<INotificationService>(), sp.Get<IAppRouting>()));
		services.AddSingleton(sp => new DeviceVolumeHotkeysViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>(), sp.Get<INotificationService>()));
		services.AddSingleton(sp => new OpenHotkeyViewModel(sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new VolumeLimitViewModel(sp.Get<IDeviceProvider>()));
		services.AddSingleton(sp => new AppVolumeSyncViewModel(sp.Get<IDeviceProvider>()));
		services.AddSingleton(sp => new DefaultDeviceHotkeysViewModel(sp.Get<IDeviceProvider>(), sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new ForegroundWindowVolumeViewModel(sp.Get<IHotkeyService>(), sp.Get<INotificationService>(), sp.Get<IAppRouting>()));
		services.AddSingleton(sp => new NotificationViewModel(sp.Get<IDeviceProvider>(), sp.Get<INotificationService>(), sp.Get<IScreenInfoProvider>()));
		services.AddSingleton(sp => new SystemHotkeysViewModel(sp.Get<IHotkeyService>()));
		services.AddSingleton(sp => new AboutViewModel(sp.Get<SupportLinks>()));

		services.AddSingleton(sp => new GeneralPageViewModel(sp.Get<HotkeyModeViewModel>(), sp.Get<ThemeViewModel>(), sp.Get<AppBehaviorViewModel>(),
			sp.Get<LangSettings>(), sp.Get<AppsHotkeysViewModel>()));
		services.AddSingleton(sp => new MixerPageViewModel(sp.Get<IHotkeyService>(), sp.Get<HotkeyModeViewModel>(), sp.Get<AppBehaviorViewModel>(),
			sp.Get<OpenHotkeyViewModel>(), sp.Get<VolumeLimitViewModel>(), sp.Get<AppVolumeSyncViewModel>()));
		services.AddSingleton(sp => new AppHotkeysPageViewModel(sp.Get<IHotkeyService>(), sp.Get<HotkeyModeViewModel>(), sp.Get<ForegroundWindowVolumeViewModel>(),
			sp.Get<AppsHotkeysViewModel>()));
		services.AddSingleton(sp => new DevicesPageViewModel(sp.Get<IHotkeyService>(), sp.Get<HotkeyModeViewModel>(), sp.Get<DeviceVolumeHotkeysViewModel>(),
			sp.Get<DefaultDeviceHotkeysViewModel>()));
		services.AddSingleton(sp => new MediaKeysPageViewModel(sp.Get<IHotkeyService>(), sp.Get<HotkeyModeViewModel>(), sp.Get<SystemHotkeysViewModel>()));

		services.AddSingleton(sp => new GeneralPage(sp.Get<GeneralPageViewModel>()));
		services.AddSingleton(sp => new MixerPage(sp.Get<MixerPageViewModel>()));
		services.AddSingleton(sp => new AppHotkeysPage(sp.Get<AppHotkeysPageViewModel>()));
		services.AddSingleton(sp => new DevicesPage(sp.Get<DevicesPageViewModel>()));
		services.AddSingleton(sp => new MediaKeysPage(sp.Get<MediaKeysPageViewModel>()));
		services.AddSingleton(sp => new NotificationsPage(sp.Get<NotificationViewModel>()));
		services.AddSingleton(sp => new AboutPage(sp.Get<AboutViewModel>()));
		services.AddSingleton<INavigationViewPageProvider>(sp => new PageProvider(sp));

		services.AddSingleton(sp => new SettingsWindow(sp.Get<INavigationViewPageProvider>()));
		//the settings window is created when it first opens
		services.AddSingleton<Func<SettingsWindow>>(sp => () => sp.Get<SettingsWindow>());
		services.AddSingleton(sp => new LevelMeters(sp.Get<AppBehaviorViewModel>(), sp.Get<DeviceProviderViewModel>()));
		services.AddSingleton(sp => new MainViewModel(sp.Get<AppBehaviorViewModel>(), sp.Get<DeviceProviderViewModel>(), sp.Get<ThemeViewModel>(),
			sp.Get<LevelMeters>()));
		services.AddSingleton(sp => new MainView(sp.Get<MainViewModel>(), sp.Get<HotkeyModeViewModel>(), sp.Get<Func<SettingsWindow>>(), sp.Get<INotificationService>()));
		return services;
	}

	private static T Get<T>(this IServiceProvider provider) where T : notnull => provider.GetRequiredService<T>();
}

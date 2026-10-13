using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.DataProvider;
using Volumey.Services;
using Volumey.ViewModel;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

/// <summary>
/// The app's registrations with fake services in place of the ones that touch Windows or the network
/// </summary>
[TestClass]
public class ServiceRegistrationTests : ViewModelTestBase
{
	private ServiceProvider BuildProvider()
	{
		var shell = new Mock<IAppShell>();
		shell.Setup(m => m.Dispatcher).Returns(Dispatcher.CurrentDispatcher);
		var screens = new Mock<IScreenInfoProvider>();
		screens.Setup(m => m.GetAllScreensInfo()).Returns([]);
		var services = new ServiceCollection().AddVolumey(null);
		//registered later, so they win
		services.AddSingleton<IHotkeyService>(Hotkeys);
		services.AddSingleton(DeviceProvider.Object);
		services.AddSingleton(Notifications.Object);
		services.AddSingleton(screens.Object);
		services.AddSingleton(shell.Object);
		services.AddSingleton(new Mock<ISystemIntegration>().Object);
		services.AddSingleton(new SupportLinks(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"),
			() => Task.FromException<string>(new IOException("offline"))));
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
	}

	[TestMethod]
	public void EveryRegistrationCanBeResolved()
	{
		//ValidateOnBuild checks that every registered service's dependencies are registered
		using var provider = BuildProvider();
	}

	[TestMethod]
	public void TheMixerViewModelsExistOnce()
	{
		using var provider = BuildProvider();

		Assert.AreSame(provider.GetRequiredService<AppBehaviorViewModel>(), provider.GetRequiredService<AppBehaviorViewModel>());
		Assert.AreSame(provider.GetRequiredService<DeviceProviderViewModel>(), provider.GetRequiredService<DeviceProviderViewModel>());
		Assert.AreSame(DeviceProvider.Object, provider.GetRequiredService<DeviceProviderViewModel>().DeviceProvider);
	}

	[TestMethod]
	public void TheSettingsViewModelHasTheContainersFeatureViewModels()
	{
		using var provider = BuildProvider();

		var settings = provider.GetRequiredService<SettingsViewModel>();

		Assert.AreSame(provider.GetRequiredService<AppsHotkeysViewModel>(), settings.HotkeysViewModel);
		Assert.AreSame(provider.GetRequiredService<DeviceVolumeHotkeysViewModel>(), settings.DeviceVolumeHotkeysViewModel);
		Assert.AreSame(provider.GetRequiredService<OpenHotkeyViewModel>(), settings.OpenHotkeyViewModel);
		Assert.AreSame(provider.GetRequiredService<VolumeLimitViewModel>(), settings.VolumeLimitViewModel);
		Assert.AreSame(provider.GetRequiredService<AppVolumeSyncViewModel>(), settings.AppVolumeSyncViewModel);
		Assert.AreSame(provider.GetRequiredService<DefaultDeviceHotkeysViewModel>(), settings.DefaultDeviceHotkeysViewModel);
		Assert.AreSame(provider.GetRequiredService<ForegroundWindowVolumeViewModel>(), settings.ForegroundWindowVolumeViewModel);
		Assert.AreSame(provider.GetRequiredService<NotificationViewModel>(), settings.NotificationsViewModel);
		Assert.AreSame(provider.GetRequiredService<SystemHotkeysViewModel>(), settings.SystemHotkeysViewModel);
		Assert.AreSame(provider.GetRequiredService<AboutViewModel>(), settings.About);
		Assert.AreSame(provider.GetRequiredService<LangSettings>(), settings.LangSettings);
	}
}

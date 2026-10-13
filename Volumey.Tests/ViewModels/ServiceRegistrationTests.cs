using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.DataProvider;
using Volumey.Services;
using Volumey.View;
using Volumey.ViewModel;
using Volumey.ViewModel.Pages;
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

		var main = provider.GetRequiredService<MainViewModel>();
		Assert.AreSame(provider.GetRequiredService<AppBehaviorViewModel>(), main.Window);
		Assert.AreSame(provider.GetRequiredService<DeviceProviderViewModel>(), main.Devices);
	}

	[TestMethod]
	public void EveryFeatureCreatedAtStartupIsRegistered()
	{
		using var provider = BuildProvider();

		foreach(var feature in ServiceRegistration.Features)
			Assert.IsNotNull(provider.GetService(feature), feature.Name);
	}

	[TestMethod]
	public void ThePageViewModelsHaveTheContainersFeatureViewModels()
	{
		using var provider = BuildProvider();

		var general = provider.GetRequiredService<GeneralPageViewModel>();
		Assert.AreSame(provider.GetRequiredService<HotkeyModeViewModel>(), general.HotkeyMode);
		Assert.AreSame(provider.GetRequiredService<AppBehaviorViewModel>(), general.Window);
		Assert.AreSame(provider.GetRequiredService<LangSettings>(), general.Language);
		Assert.AreSame(provider.GetRequiredService<AppsHotkeysViewModel>(), general.AppHotkeys);

		var mixer = provider.GetRequiredService<MixerPageViewModel>();
		Assert.AreSame(provider.GetRequiredService<AppBehaviorViewModel>(), mixer.Window);
		Assert.AreSame(provider.GetRequiredService<OpenHotkeyViewModel>(), mixer.OpenHotkey);
		Assert.AreSame(provider.GetRequiredService<VolumeLimitViewModel>(), mixer.VolumeLimit);
		Assert.AreSame(provider.GetRequiredService<AppVolumeSyncViewModel>(), mixer.AppVolumeSync);

		var apps = provider.GetRequiredService<AppHotkeysPageViewModel>();
		Assert.AreSame(provider.GetRequiredService<ForegroundWindowVolumeViewModel>(), apps.Foreground);
		Assert.AreSame(provider.GetRequiredService<AppsHotkeysViewModel>(), apps.AppHotkeys);

		var devices = provider.GetRequiredService<DevicesPageViewModel>();
		Assert.AreSame(provider.GetRequiredService<DeviceVolumeHotkeysViewModel>(), devices.DeviceVolume);
		Assert.AreSame(provider.GetRequiredService<DefaultDeviceHotkeysViewModel>(), devices.DefaultDevice);
	}

	[TestMethod]
	public void ThePageProviderGivesTheContainersPage()
	{
		var page = new object();
		using var provider = new ServiceCollection().AddSingleton(page).BuildServiceProvider();
		var pages = new PageProvider(provider);

		Assert.AreSame(page, pages.GetPage(typeof(object)));
		Assert.IsNull(pages.GetPage(typeof(string)));
	}
}

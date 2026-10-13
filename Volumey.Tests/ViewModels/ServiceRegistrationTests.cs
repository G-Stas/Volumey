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
/// The app's registrations with fake services in place of the ones that touch Windows
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
}

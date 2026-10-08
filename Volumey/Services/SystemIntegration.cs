using Volumey.Helper;

namespace Volumey.Services
{
	/// <summary>
	/// <see cref="ISystemIntegration"/> over <see cref="SystemIntegrationHelper"/> (registry Run key, Start Menu shortcut).
	/// The Store build has neither: its autostart is the manifest's startup task.
	/// </summary>
	internal sealed class SystemIntegration : ISystemIntegration
	{
#if(STORE)
		public bool LaunchesAtStartup() => false;

		public bool EnableLaunchAtStartup() => false;

		public void DisableLaunchAtStartup() { }

		public bool IsInStartMenu() => false;

		public bool AddToStartMenu() => false;

		public void RemoveFromStartMenu() { }
#else
		public bool LaunchesAtStartup() => SystemIntegrationHelper.CheckIfStartupRegistryKeyExists();

		public bool EnableLaunchAtStartup() => SystemIntegrationHelper.EnableLaunchAtStartup();

		public void DisableLaunchAtStartup() => SystemIntegrationHelper.DisableLaunchAtStartup();

		public bool IsInStartMenu() => SystemIntegrationHelper.CheckIfStartMenuLinkExists();

		public bool AddToStartMenu() => SystemIntegrationHelper.AddToStartMenu();

		public void RemoveFromStartMenu() => SystemIntegrationHelper.RemoveFromStartMenu();
#endif
	}
}

namespace Volumey.Services
{
	/// <summary>
	/// Autostart and the Start Menu shortcut of the non-Store build (the Store build uses the manifest's startup task).
	/// </summary>
	public interface ISystemIntegration
	{
		bool LaunchesAtStartup();

		bool EnableLaunchAtStartup();

		void DisableLaunchAtStartup();

		bool IsInStartMenu();

		bool AddToStartMenu();

		void RemoveFromStartMenu();
	}
}

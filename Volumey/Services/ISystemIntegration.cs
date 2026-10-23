#nullable enable
namespace Volumey.Services;

/// <summary>
/// Autostart and the Start Menu shortcut of the non-Store build (the Store build uses the manifest's startup task),
/// and whether Windows can open the sound settings.
/// </summary>
public interface ISystemIntegration
{
	System.Threading.Tasks.Task<bool> CanOpenSoundSettingsAsync();

	bool LaunchesAtStartup();

	bool EnableLaunchAtStartup();

	void DisableLaunchAtStartup();

	bool IsInStartMenu();

	bool AddToStartMenu();

	void RemoveFromStartMenu();
}

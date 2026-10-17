#nullable enable
namespace Volumey.ViewModel;

/// <summary>
/// The mixer window's view models: the window's behavior, the devices and their apps (mixer and tray), the theme (tray icon)
/// and the level meters
/// </summary>
public sealed class MainViewModel
{
	public AppBehaviorViewModel Window { get; }
	public DeviceProviderViewModel Devices { get; }
	public ThemeViewModel Theme { get; }
	public LevelMeters Meters { get; }

	internal MainViewModel(AppBehaviorViewModel window, DeviceProviderViewModel devices, ThemeViewModel theme, LevelMeters meters)
	{
		Window = window;
		Devices = devices;
		Theme = theme;
		Meters = meters;
	}
}

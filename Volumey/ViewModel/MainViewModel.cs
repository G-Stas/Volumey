#nullable enable
namespace Volumey.ViewModel;

/// <summary>
/// The mixer window's view models: the window's behavior, the devices and their apps (mixer and tray), and the theme (tray icon)
/// </summary>
public sealed class MainViewModel
{
	public AppBehaviorViewModel Window { get; }
	public DeviceProviderViewModel Devices { get; }
	public ThemeViewModel Theme { get; }

	internal MainViewModel(AppBehaviorViewModel window, DeviceProviderViewModel devices, ThemeViewModel theme)
	{
		Window = window;
		Devices = devices;
		Theme = theme;
	}
}

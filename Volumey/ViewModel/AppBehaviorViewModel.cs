#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Timers;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using log4net;
using Microsoft.Win32;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel;

public sealed partial class AppBehaviorViewModel : ObservableObject
{
	private bool windowIsVisible;
	public bool WindowIsVisible
	{
		get => windowIsVisible;
		set
		{
			windowIsVisible = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Requests showing the mixer window with these parameters: is the window visible already, is it shown as the popup
	/// </summary>
	public event Action<bool, bool>? DisplayAppRequested;

	/// <summary>
	/// Requests showing the settings window (a window of its own; the mixer window stays as it is)
	/// </summary>
	public event Action? SettingsRequested;

	private bool displayedMinimalistic = false;

	/// <summary>
	/// Used to hide title bar and taskbar icon in popup mode
	/// </summary>
	public bool DisplayMinimalistic
	{
		get => displayedMinimalistic;
		set
		{
			displayedMinimalistic = value;
			OnPropertyChanged();
		}
	}

	private bool popupEnabled;

	/// <summary>
	/// Controls whether the window will be displayed without titlebar, tabs and will hide after losing focus when displayed by clicking on tray icon.
	/// It doesn't affect the window behavior when it's displayed by trays context menu items. 
	/// </summary>
	public bool PopupEnabled
	{
		get => popupEnabled;
		set
		{
			popupEnabled = value;
			DisplayMinimalistic = value;
			displayedAsPopup = value;

			SettingsProvider.Settings.Window.PopupMode = value;

			OnPropertyChanged();
		}
	}

	[ObservableProperty]
	public partial bool AlwaysOnTop { get; set; }

	partial void OnAlwaysOnTopChanged(bool value) => SettingsProvider.Settings.Window.AlwaysOnTop = value;

	/// <summary>
	/// The mixer opens where it was last, in both modes; off: next to the tray on the chosen screen (dragging works either way)
	/// </summary>
	[ObservableProperty]
	public partial bool RememberPosition { get; set; }

	partial void OnRememberPositionChanged(bool value) => SettingsProvider.Settings.Window.RememberPosition = value;

	[ObservableProperty]
	public partial bool DeviceViewAtTheBottom { get; set; }

	partial void OnDeviceViewAtTheBottomChanged(bool value) => SettingsProvider.Settings.Window.DeviceViewAtTheBottom = value;

	/// <summary>The mixer's output device picker; without it the mixer shows the default device</summary>
	[ObservableProperty]
	public partial bool ShowDevicePicker { get; set; }

	partial void OnShowDevicePickerChanged(bool value) => SettingsProvider.Settings.Window.ShowDevicePicker = value;

	[ObservableProperty]
	public partial bool DisplayDeviceIconAtTray { get; set; }

	partial void OnDisplayDeviceIconAtTrayChanged(bool value) => SettingsProvider.Settings.Window.DisplayDeviceIconAtTray = value;

	[ObservableProperty]
	public partial bool UseVolumeyIconAtTray { get; set; }

	partial void OnUseVolumeyIconAtTrayChanged(bool value) => SettingsProvider.Settings.Window.UseVolumeyIconAtTray = value;

	private bool launchAtStartup;
	public bool LaunchAtStartup
	{
		get => launchAtStartup;
		set
		{
#if(!STORE)
			if(value == launchAtStartup)
				return;

			if(value)
				launchAtStartup = systemIntegration.EnableLaunchAtStartup();
			else
			{
				launchAtStartup = false;
				systemIntegration.DisableLaunchAtStartup();
			}
			StartupBlocked = value && !launchAtStartup;
			OnPropertyChanged();
#endif
		}
	}

	/// <summary>Windows refused the autostart entry (security software or a policy); the Startup section says so until it's closed</summary>
	[ObservableProperty]
	public partial bool StartupBlocked { get; set; }

	//reading the shortcut is a call into the shell (about 20 ms): it's made on the thread pool, so the first window doesn't wait for it.
	//Created on the UI thread, the view model gets the result there
	private async System.Threading.Tasks.Task ReadStartMenuStateAsync()
	{
		addToStartMenu = await System.Threading.Tasks.Task.Run(systemIntegration.IsInStartMenu);
		OnPropertyChanged(nameof(AddToStartMenu));
	}

	private bool addToStartMenu;
	public bool AddToStartMenu
	{
		get => addToStartMenu;
		set
		{
#if(!STORE)
			if(value == addToStartMenu)
				return;

			if(value)
				addToStartMenu = systemIntegration.AddToStartMenu();
			else
			{
				addToStartMenu = false;
				systemIntegration.RemoveFromStartMenu();
			}
			OnPropertyChanged();
#endif
		}
	}

	//saved on exit only: they change on every window move
	[ObservableProperty]
	public partial double WindowLeft { get; set; }

	[ObservableProperty]
	public partial double WindowTop { get; set; }

	//the width of the window mode (the popup's width is fixed, the height of both is computed when they open); 0 = not saved yet
	[ObservableProperty]
	public partial double WindowWidth { get; set; }

	//where the popup was last dragged to, in physical pixels (null: never); saved on exit only, like the window mode's position
	public double? PopupLeft { get; set; }

	public double? PopupTop { get; set; }

	private readonly ScreenSelection screens;

	/// <summary>
	/// The screen the window is shown on
	/// </summary>
	public ScreenInfo SelectedScreen
	{
		get => screens.Selected;
		set => screens.Select(value);
	}

	public ObservableCollection<ScreenInfo> AllScreens => screens.All;

	private readonly IHotkeyService hotkeys;
	private readonly IAppShell shell;
	private readonly ISystemIntegration systemIntegration;

	/// <summary>
	/// Indicates whether the window is currently displayed as popup or not
	/// </summary>
	private bool displayedAsPopup = false;

	/// <summary>
	/// Blocks displaying app by left clicking on tray.
	/// Used when app is displayed as popup to prevent instant displaying again after it has lost focus because of the clicking on tray 
	/// </summary>
	private bool trayBlocked = false;

	/// <summary>
	/// Unlocks tray with delay (<see cref="trayBlockDelay"/>)
	/// </summary>
	private Timer trayBlockTimer;

	/// <summary>
	/// Amount of ms to block tray for 
	/// </summary>
	private const double trayBlockDelay = 300;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(AppBehaviorViewModel));

	internal AppBehaviorViewModel(IHotkeyService hotkeys, IScreenInfoProvider screenInfoProvider, IAppShell shell,
		ISystemIntegration systemIntegration)
	{
		this.hotkeys = hotkeys;
		screens = new ScreenSelection(screenInfoProvider, () => SettingsProvider.Settings.Window.ScreenName,
			name => SettingsProvider.Settings.Window.ScreenName = name, () => shell.Dispatcher,
			_ => OnPropertyChanged(nameof(SelectedScreen)), Logger);
		this.shell = shell;
		this.systemIntegration = systemIntegration;
		WindowIsVisible = !shell.StartMinimized;
		shell.SessionEnding += OnExit;

		hotkeys.OpenHotkeyPressed += OnOpenHotkeyPressed;
		ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

		trayBlockTimer = new Timer(trayBlockDelay);
		trayBlockTimer.AutoReset = false;
		trayBlockTimer.Elapsed += OnBlockTrayTimerElapsed;

		popupEnabled = SettingsProvider.Settings.Window.PopupMode;
		AlwaysOnTop = SettingsProvider.Settings.Window.AlwaysOnTop;
		RememberPosition = SettingsProvider.Settings.Window.RememberPosition;
		DeviceViewAtTheBottom = SettingsProvider.Settings.Window.DeviceViewAtTheBottom;
		ShowDevicePicker = SettingsProvider.Settings.Window.ShowDevicePicker;
		DisplayDeviceIconAtTray = SettingsProvider.Settings.Window.DisplayDeviceIconAtTray;
		UseVolumeyIconAtTray = SettingsProvider.Settings.Window.UseVolumeyIconAtTray;

		WindowLeft = SettingsProvider.Settings.Window.Left;
		WindowTop = SettingsProvider.Settings.Window.Top;
		WindowWidth = SettingsProvider.Settings.Window.Width;
		PopupLeft = SettingsProvider.Settings.Window.PopupLeft;
		PopupTop = SettingsProvider.Settings.Window.PopupTop;

		//always false in the Store build, whose autostart is the manifest's startup task
		launchAtStartup = systemIntegration.LaunchesAtStartup();
		ReadStartMenuStateAsync().Forget(Logger, "Failed to read the Start menu shortcut");
		ReadSoundSettingsSupportAsync().Forget(Logger, "Failed to check the sound settings");

		SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;

		screens.Load();
	}

	private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
	{
		screens.Refresh();
	}

	/// <summary>
	/// Blocks tray and setups timer to unlock it with delay
	/// </summary>
	private void BlockTrayWithTimer()
	{
		trayBlocked = true;
		trayBlockTimer.Stop();
		trayBlockTimer.Start();
	}

	private void OnExit()
	{
		SettingsProvider.Settings.Window.Left = WindowLeft;
		SettingsProvider.Settings.Window.Top = WindowTop;
		SettingsProvider.Settings.Window.Width = WindowWidth;
		SettingsProvider.Settings.Window.PopupLeft = PopupLeft;
		SettingsProvider.Settings.Window.PopupTop = PopupTop;
		//logging shuts down last, in App.OnExit, so errors while releasing hotkeys and devices are still logged
		hotkeys.Dispose();
		shell.Shutdown();
	}

	/// <summary>
	/// Changes window visibility if requested and invokes the event the view uses to show the mixer window
	/// </summary>
	/// <param name="displayAsPopup"/>
	/// <param name="reverseDisplaying">Flag to control whether to close the window if it's displayed or not</param>
	private void OpenCommand(bool displayAsPopup, bool reverseDisplaying = false)
	{
		displayedAsPopup = displayAsPopup;
		if(reverseDisplaying)
		{
			if(!windowIsVisible)
			{
				if(popupEnabled && !displayedMinimalistic)
					DisplayMinimalistic = true;
				//bring the window to the foreground
				DisplayAppRequested?.Invoke(WindowIsVisible, displayedAsPopup);
				WindowIsVisible = true;
				screens.Refresh();
			}
			else
				WindowIsVisible = false;
		}
		else
		{
			if(popupEnabled && displayedMinimalistic)
				DisplayMinimalistic = false;
			DisplayAppRequested?.Invoke(WindowIsVisible, displayedAsPopup);
			if(!WindowIsVisible)
			{
				WindowIsVisible = true;
				screens.Refresh();
			}
		}
	}

	private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
	{
		//"The operating system communicates with your application window by passing messages to it.
		// A message is simply a numeric code that designates a particular event."
		//When a user attempts to launch the second instance of the app
		//it sends a window message of type WM_SHOWME to all top-level windows in the system.
		//This event handler checks every window message for this type of message
		//to bring the window of the first instance to top or make it visible
		//if it was minimized to tray

		if(msg.message == Startup.WM_SHOWME)
			OnShowRequestedBySecondInstance();
	}

	/// <summary>
	/// Starting the app again opens the running instance like a tray click: as a popup in popup mode, otherwise as a normal window
	/// </summary>
	internal void OnShowRequestedBySecondInstance()
	{
		if(popupEnabled && !WindowIsVisible)
			OpenCommand(displayAsPopup: true, reverseDisplaying: true);
		else if(!popupEnabled)
			OpenCommand(displayAsPopup: false, reverseDisplaying: false);
		else
			//already visible in popup mode: bring it to the front as it is
			DisplayAppRequested?.Invoke(WindowIsVisible, displayedAsPopup);
	}

	private void OnBlockTrayTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		trayBlocked = false;
	}

	[RelayCommand]
	private void LostFocus()
	{
		if(popupEnabled && displayedAsPopup)
		{
			//If the window was displayed as popup, and it lost its focus we block tray icon left clicking for some time
			//to prevent the situation when focus was lost because of the clicking on the tray icon in which case the window would disappear and instantly show up again.
			BlockTrayWithTimer();
			WindowIsVisible = displayedAsPopup = false;
		}
	}

	private void OnOpenHotkeyPressed()
	{
		OpenCommand(displayAsPopup: popupEnabled, reverseDisplaying: true);
	}

	[RelayCommand]
	private void Closing(object e)
	{
		if(e is CancelEventArgs args)
		{
			args.Cancel = true;
			WindowIsVisible = false;
		}
	}

	[RelayCommand]
	private void TrayMixer() => OpenCommand(displayAsPopup: false, reverseDisplaying: false);

	[RelayCommand]
	private void TraySettings() => SettingsRequested?.Invoke();

	[RelayCommand]
	private void TrayClick()
	{
		if(!trayBlocked)
			OpenCommand(displayAsPopup: popupEnabled, reverseDisplaying: true);
	}

	[RelayCommand]
	private void TrayExit() => OnExit();

	[RelayCommand]
	private static void SoundControlPanel() => SystemSoundUtilities.StartSoundControlPanel();

	/// <summary>The tray menu's item, shown only while <see cref="CanOpenSoundSettings"/></summary>
	[RelayCommand]
	private static void SoundSettings() => SystemSoundUtilities.StartSoundSettings(fallBackToControlPanel: false);

	/// <summary>The mixer's button, which is always there: the Sound control panel opens when Windows doesn't open the settings</summary>
	[RelayCommand]
	private static void MixerSoundSettings() => SystemSoundUtilities.StartSoundSettings(fallBackToControlPanel: true);

	/// <summary>Whether Windows can open the sound settings (the Settings app may be removed)</summary>
	[ObservableProperty]
	public partial bool CanOpenSoundSettings { get; private set; } = true;

	private async System.Threading.Tasks.Task ReadSoundSettingsSupportAsync()
		=> CanOpenSoundSettings = await systemIntegration.CanOpenSoundSettingsAsync();

	[RelayCommand]
	private void Loaded() => screens.Refresh();
}

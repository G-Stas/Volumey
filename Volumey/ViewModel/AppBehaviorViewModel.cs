using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows.Input;
using System.Windows.Interop;
using log4net;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel;

public sealed class AppBehaviorViewModel : INotifyPropertyChanged
{
	public ICommand ClosingCommand { get; }
	public ICommand LostFocusCommand { get; }
	public ICommand TrayMixerCommand { get; }
	public ICommand TraySettingsCommand { get; }
	public ICommand TrayExitCommand { get; }
	public ICommand TrayClickCommand { get; }
	public ICommand SoundControlPanelCommand { get; }
	public ICommand SoundSettingsCommand { get; }

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
	/// Requests app display with these parameters: is settings page, is window visible, is in popup mode
	/// </summary>
	public event Action<bool, bool, bool> DisplayAppRequested;

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

	private bool alwaysOnTop;
	public bool AlwaysOnTop
	{
		get => alwaysOnTop;
		set
		{
			alwaysOnTop = value;

			SettingsProvider.Settings.Window.AlwaysOnTop = value;

			OnPropertyChanged();
		}
	}

	private bool deviceViewAtTheBottom;
	public bool DeviceViewAtTheBottom
	{
		get => deviceViewAtTheBottom;
		set
		{
			deviceViewAtTheBottom = value;

			SettingsProvider.Settings.Window.DeviceViewAtTheBottom = value;

			OnPropertyChanged();
		}
	}

	private bool displayDeviceIconAtTray;
	public bool DisplayDeviceIconAtTray
	{
		get => displayDeviceIconAtTray;
		set
		{
			displayDeviceIconAtTray = value;

			SettingsProvider.Settings.Window.DisplayDeviceIconAtTray = value;

			OnPropertyChanged();
		}
	}

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
			OnPropertyChanged();
#endif
		}
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

	private double windowLeft;
	public double WindowLeft
	{
		get => windowLeft;
		set
		{
			windowLeft = value;
			OnPropertyChanged();
		}
	}

	private double windowTop;
	public double WindowTop
	{
		get => windowTop;
		set
		{
			windowTop = value;
			OnPropertyChanged();
		}
	}

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

	public ICommand LoadedCommand { get; set; }

	private static ILog _logger;
	private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppBehaviorViewModel));

	public AppBehaviorViewModel() : this(AppServices.Current.Hotkeys, AppServices.Current.Screens,
		AppServices.Current.Shell, AppServices.Current.SystemIntegration)
	{ }

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
		ClosingCommand = new ActionCommand(OnWindowClosing);
		TrayMixerCommand = new ActionCommand(() => OpenCommand(isSettingsPage: false, displayAsPopup: false, reverseDisplaying: false));
		TraySettingsCommand = new ActionCommand(() => OpenCommand(isSettingsPage: true, displayAsPopup: false, reverseDisplaying: false));
		TrayClickCommand = new ActionCommand(() =>
		{
			{
				if(!trayBlocked)
					OpenCommand(isSettingsPage: false, displayAsPopup: popupEnabled, reverseDisplaying: true);
			}
		});
		TrayExitCommand = new ActionCommand(OnExit);
		SoundControlPanelCommand = new ActionCommand(SystemSoundUtilities.StartSoundControlPanel);
		SoundSettingsCommand = new ActionCommand(SystemSoundUtilities.StartSoundSettings);
		LostFocusCommand = new ActionCommand(OnLostFocus);
		shell.SessionEnding += OnExit;

		hotkeys.OpenHotkeyPressed += OnOpenHotkeyPressed;
		ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

		trayBlockTimer = new Timer(trayBlockDelay);
		trayBlockTimer.AutoReset = false;
		trayBlockTimer.Elapsed += OnBlockTrayTimerElapsed;

		popupEnabled = SettingsProvider.Settings.Window.PopupMode;
		alwaysOnTop = SettingsProvider.Settings.Window.AlwaysOnTop;
		deviceViewAtTheBottom = SettingsProvider.Settings.Window.DeviceViewAtTheBottom;
		displayDeviceIconAtTray = SettingsProvider.Settings.Window.DisplayDeviceIconAtTray;

		windowLeft = SettingsProvider.Settings.Window.Left;
		windowTop = SettingsProvider.Settings.Window.Top;

		LoadedCommand = new ActionCommand(screens.Refresh);

		//always false in the Store build, whose autostart is the manifest's startup task
		launchAtStartup = systemIntegration.LaunchesAtStartup();
		addToStartMenu = systemIntegration.IsInStartMenu();

		SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;

		screens.Load();
	}

	private void OnSystemDisplaySettingsChanged(object sender, EventArgs e)
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
		SettingsProvider.Settings.Window.Left = windowLeft;
		SettingsProvider.Settings.Window.Top = windowTop;
		//logging shuts down last, in App.OnExit, so errors while releasing hotkeys and devices are still logged
		hotkeys.Dispose();
		shell.Shutdown();
	}

	/// <summary>
	/// Changes window visibility if requested and invokes event which is used by the view to open the requested page 
	/// </summary>
	/// <param name="displayAsPopup"/>
	/// <param name="isSettingsPage"/>
	/// <param name="reverseDisplaying">Flag to control whether to close the window if it's displayed or not</param>
	private void OpenCommand(bool isSettingsPage, bool displayAsPopup, bool reverseDisplaying = false)
	{
		displayedAsPopup = displayAsPopup;
		if(reverseDisplaying)
		{
			if(!windowIsVisible)
			{
				if(popupEnabled && !displayedMinimalistic)
					DisplayMinimalistic = true;
				//bring the window to the foreground and change page
				DisplayAppRequested?.Invoke(isSettingsPage, WindowIsVisible, displayedAsPopup);
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
			DisplayAppRequested?.Invoke(isSettingsPage, WindowIsVisible, displayedAsPopup);
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
	/// Starting the app again opens the running instance like a tray click: as a popup in popup mode (#74), otherwise as a normal window
	/// </summary>
	internal void OnShowRequestedBySecondInstance()
	{
		if(popupEnabled && !WindowIsVisible)
			OpenCommand(isSettingsPage: false, displayAsPopup: true, reverseDisplaying: true);
		else if(!popupEnabled)
			OpenCommand(isSettingsPage: false, displayAsPopup: false, reverseDisplaying: false);
		else
			//already visible in popup mode: bring it to the front as it is
			DisplayAppRequested?.Invoke(false, WindowIsVisible, displayedAsPopup);
	}

	private void OnBlockTrayTimerElapsed(object sender, ElapsedEventArgs e)
	{
		trayBlocked = false;
	}

	private void OnLostFocus()
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
		OpenCommand(isSettingsPage: false, displayAsPopup: popupEnabled, reverseDisplaying: true);
	}

	private void OnWindowClosing(object e)
	{
		if(e is CancelEventArgs args)
		{
			args.Cancel = true;
			WindowIsVisible = false;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	~AppBehaviorViewModel()
	{
		SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
	}
}

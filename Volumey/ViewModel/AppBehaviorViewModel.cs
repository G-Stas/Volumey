using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Interop;
using log4net;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using Volumey.Services;

namespace Volumey.ViewModel
{
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
	        get => this.displayedMinimalistic;
	        set
	        {
		        this.displayedMinimalistic = value;
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
	        get => this.popupEnabled;
	        set
	        {
		        this.popupEnabled = value;
		        this.DisplayMinimalistic = value;
		        this.displayedAsPopup = value;

		        SettingsProvider.Settings.Window.PopupMode = value;
		        
		        OnPropertyChanged();
	        }
        }

        private bool alwaysOnTop;
        public bool AlwaysOnTop
        {
	        get => this.alwaysOnTop;
	        set
	        {
		        this.alwaysOnTop = value;
		        
		        SettingsProvider.Settings.Window.AlwaysOnTop = value;
		        
		        OnPropertyChanged();
	        }
        }

        private bool deviceViewAtTheBottom;
        public bool DeviceViewAtTheBottom
        {
	        get => this.deviceViewAtTheBottom;
	        set
	        {
		        this.deviceViewAtTheBottom = value;

		        SettingsProvider.Settings.Window.DeviceViewAtTheBottom = value;
		        
		        OnPropertyChanged();
	        }
        }

        private bool displayDeviceIconAtTray;
        public bool DisplayDeviceIconAtTray
        {
	        get => this.displayDeviceIconAtTray;
	        set
	        {
		        this.displayDeviceIconAtTray = value;

		        SettingsProvider.Settings.Window.DisplayDeviceIconAtTray = value;
		        
		        OnPropertyChanged();
	        }
        }

        private bool launchAtStartup;
        public bool LaunchAtStartup
        {
	        get => this.launchAtStartup;
	        set
	        {
		        #if(!STORE)
		        if(value == launchAtStartup)
			        return;

		        if(value)
			        this.launchAtStartup = this.systemIntegration.EnableLaunchAtStartup();
		        else
		        {
			        this.launchAtStartup = false;
			        this.systemIntegration.DisableLaunchAtStartup();
		        }
		        OnPropertyChanged();
				#endif
	        }
        }

        private bool addToStartMenu;
        public bool AddToStartMenu
        {
	        get => this.addToStartMenu;
	        set
	        {
				#if(!STORE)
		        if(value == addToStartMenu)
			        return;

		        if(value)
			        addToStartMenu = this.systemIntegration.AddToStartMenu();
		        else
		        {
			        this.addToStartMenu = false;
			        this.systemIntegration.RemoveFromStartMenu();
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

        private ScreenInfo selectedScreen;
        public ScreenInfo SelectedScreen
        {
	        get => selectedScreen;
	        set
	        {
		        //only a listed screen is the user's choice: the combo box writes back an empty value while the list is replaced
		        if(!AllScreens.Contains(value))
			        return;
		        ShowScreen(value);
		        SettingsProvider.Settings.Window.ScreenName = value.Name;
	        }
        }

        /// <summary>
        /// Uses the screen without remembering it, so a fallback screen doesn't replace the user's choice (#92)
        /// </summary>
        private void ShowScreen(ScreenInfo screen)
        {
	        this.selectedScreen = screen;
	        OnPropertyChanged(nameof(SelectedScreen));
        }

        public ObservableCollection<ScreenInfo> AllScreens { get; set; } = new ObservableCollection<ScreenInfo>();

        private readonly IScreenInfoProvider screenInfoProvider;
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
			AppServices.Current.Shell, AppServices.Current.SystemIntegration) { }

		internal AppBehaviorViewModel(IHotkeyService hotkeys, IScreenInfoProvider screenInfoProvider, IAppShell shell,
			ISystemIntegration systemIntegration)
		{
			this.hotkeys = hotkeys;
			this.screenInfoProvider = screenInfoProvider;
			this.shell = shell;
			this.systemIntegration = systemIntegration;
			this.WindowIsVisible = !shell.StartMinimized;
			this.ClosingCommand = new ActionCommand(OnWindowClosing);
			this.TrayMixerCommand = new ActionCommand(() => OpenCommand(isSettingsPage: false, displayAsPopup: false, reverseDisplaying: false));
			this.TraySettingsCommand = new ActionCommand(() => OpenCommand(isSettingsPage: true, displayAsPopup: false, reverseDisplaying: false));
			this.TrayClickCommand = new ActionCommand(() => {
			{
				if(!trayBlocked)
					OpenCommand(isSettingsPage: false, displayAsPopup: popupEnabled, reverseDisplaying: true);
			} });
			this.TrayExitCommand = new ActionCommand(OnExit);
			this.SoundControlPanelCommand = new ActionCommand(SystemSoundUtilities.StartSoundControlPanel);
			this.SoundSettingsCommand = new ActionCommand(SystemSoundUtilities.StartSoundSettings);
			this.LostFocusCommand = new ActionCommand(OnLostFocus);
			shell.SessionEnding += OnExit;

			hotkeys.OpenHotkeyPressed += OnOpenHotkeyPressed;
			ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

			this.trayBlockTimer = new Timer(trayBlockDelay);
			this.trayBlockTimer.AutoReset = false;
			this.trayBlockTimer.Elapsed += OnBlockTrayTimerElapsed;

			this.popupEnabled = SettingsProvider.Settings.Window.PopupMode;
			this.alwaysOnTop = SettingsProvider.Settings.Window.AlwaysOnTop;
			this.deviceViewAtTheBottom = SettingsProvider.Settings.Window.DeviceViewAtTheBottom;
			this.displayDeviceIconAtTray = SettingsProvider.Settings.Window.DisplayDeviceIconAtTray;

			this.windowLeft = SettingsProvider.Settings.Window.Left;
			this.windowTop = SettingsProvider.Settings.Window.Top;
			
			this.LoadedCommand = new ActionCommand(this.CheckIfSelectedScreenIsAvailable);

			//always false in the Store build, whose autostart is the manifest's startup task
			this.launchAtStartup = systemIntegration.LaunchesAtStartup();
			this.addToStartMenu = systemIntegration.IsInStartMenu();
			
			SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;

			SetSelectedScreen();
		}

		private void OnSystemDisplaySettingsChanged(object sender, EventArgs e)
		{
			CheckIfSelectedScreenIsAvailable();
		}

		/// <summary>
		/// Blocks tray and setups timer to unlock it with delay
		/// </summary>
		private void BlockTrayWithTimer()
		{
			this.trayBlocked = true;
			this.trayBlockTimer.Stop();
			this.trayBlockTimer.Start();
		}

		private void SetSelectedScreen()
		{
			foreach(var screen in screenInfoProvider.GetAllScreensInfo())
				AllScreens.Add(screen);

			ShowScreen(ScreenInfo.Find(AllScreens, SettingsProvider.Settings.Window.ScreenName));
		}

		/// <summary>
		/// Executes after the window was displayed and when the screens change: updates <see cref="AllScreens"/>
		/// and picks the remembered screen again, or the primary one while it's not connected.
		/// </summary>
		private void CheckIfSelectedScreenIsAvailable()
		{
			Task.Run(() =>
			{
				var screens = screenInfoProvider.GetAllScreensInfo().ToList();

				//on the UI thread, which owns the window that follows SelectedScreen
				void Apply()
				{
					if(screens.Count != AllScreens.Count || screens.Except(AllScreens).Any())
					{
						AllScreens.Clear();
						foreach(var screen in screens)
							AllScreens.Add(screen);
					}
					//also updates the selection in the UI, which is lost when AllScreens is replaced
					ShowScreen(ScreenInfo.Find(screens, SettingsProvider.Settings.Window.ScreenName));
				}

				var dispatcher = this.shell.Dispatcher;
				if(dispatcher == null || dispatcher.CheckAccess())
					Apply();
				else
					dispatcher.Invoke(Apply);
			}).ContinueWith(task =>
			{
				if(task.Exception != null)
				{
					Logger.Error("Failed to check the screen for availability", task.Exception.Flatten());
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}

		private void OnExit()
		{
			SettingsProvider.Settings.Window.Left = this.windowLeft;
			SettingsProvider.Settings.Window.Top = this.windowTop;
			LogManager.Shutdown();
			this.hotkeys.Dispose();
			this.shell.Shutdown();
		}

		/// <summary>
		/// Changes window visibility if requested and invokes event which is used by the view to open the requested page 
		/// </summary>
		/// <param name="displayAsPopup"/>
		/// <param name="isSettingsPage"/>
		/// <param name="reverseDisplaying">Flag to control whether to close the window if it's displayed or not</param>
		private void OpenCommand(bool isSettingsPage, bool displayAsPopup, bool reverseDisplaying = false)
		{
			this.displayedAsPopup = displayAsPopup;
			if(reverseDisplaying)
			{
				if(!windowIsVisible)
				{
					if(this.popupEnabled && !this.displayedMinimalistic)
						this.DisplayMinimalistic = true;
					//bring the window to the foreground and change page
					this.DisplayAppRequested?.Invoke(isSettingsPage, WindowIsVisible, this.displayedAsPopup);
					this.WindowIsVisible = true;
					CheckIfSelectedScreenIsAvailable();
				}
				else
					this.WindowIsVisible = false;
			}
			else
			{
				if(this.popupEnabled && this.displayedMinimalistic)
					this.DisplayMinimalistic = false;
				this.DisplayAppRequested?.Invoke(isSettingsPage, WindowIsVisible, this.displayedAsPopup);
				if(!WindowIsVisible)
				{
					WindowIsVisible = true;
					CheckIfSelectedScreenIsAvailable();
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
				this.OnShowRequestedBySecondInstance();
		}

		/// <summary>
		/// Starting the app again opens the running instance like a tray click: as a popup in popup mode (#74), otherwise as a normal window
		/// </summary>
		internal void OnShowRequestedBySecondInstance()
		{
			if(this.popupEnabled && !this.WindowIsVisible)
				this.OpenCommand(isSettingsPage: false, displayAsPopup: true, reverseDisplaying: true);
			else if(!this.popupEnabled)
				this.OpenCommand(isSettingsPage: false, displayAsPopup: false, reverseDisplaying: false);
			else
				//already visible in popup mode: bring it to the front as it is
				this.DisplayAppRequested?.Invoke(false, this.WindowIsVisible, this.displayedAsPopup);
		}

		private void OnBlockTrayTimerElapsed(object sender, ElapsedEventArgs e)
		{
			this.trayBlocked = false;
		}

		private void OnLostFocus()
		{
			if(this.popupEnabled && this.displayedAsPopup)
			{
				//If the window was displayed as popup, and it lost its focus we block tray icon left clicking for some time
				//to prevent the situation when focus was lost because of the clicking on the tray icon in which case the window would disappear and instantly show up again.
				this.BlockTrayWithTimer();
				this.WindowIsVisible = this.displayedAsPopup = false;
			}
		}

		private void OnOpenHotkeyPressed()
		{
			this.OpenCommand(isSettingsPage: false, displayAsPopup: this.popupEnabled, reverseDisplaying: true);
		}

		private void OnWindowClosing(object e)
		{
			if(e is CancelEventArgs args)
			{
				args.Cancel = true;
				this.WindowIsVisible = false;
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
}
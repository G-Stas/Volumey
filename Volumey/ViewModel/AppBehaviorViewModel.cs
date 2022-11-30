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

		        SettingsProvider.Settings.PopupEnabled = value;
		        _ = SettingsProvider.SaveSettings();
		        
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
		        
		        SettingsProvider.Settings.AlwaysOnTop = value;
		        _ = SettingsProvider.SaveSettings();
		        
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

		        SettingsProvider.Settings.DeviceViewAtTheBottom = value;
		        _ = SettingsProvider.SaveSettings();
		        
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

		        SettingsProvider.Settings.DisplayDeviceIconAtTray = value;
		        _ = SettingsProvider.SaveSettings();
		        
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
			        this.launchAtStartup = SystemIntegrationHelper.EnableLaunchAtStartup();
		        else
		        {
			        this.launchAtStartup = false;
			        SystemIntegrationHelper.DisableLaunchAtStartup();
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
			        addToStartMenu = SystemIntegrationHelper.AddToStartMenu();
		        else
		        {
			        this.addToStartMenu = false;
			        SystemIntegrationHelper.RemoveFromStartMenu();
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
		        SettingsProvider.Settings.WindowLeft = value;
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
		        SettingsProvider.Settings.WindowTop = value;
		        OnPropertyChanged();
	        }
        }

        private ScreenInfo selectedScreen;
        public ScreenInfo SelectedScreen
        {
	        get => selectedScreen;
	        set
	        {
		        selectedScreen = value;
		        OnPropertyChanged();
		        
		        int index = AllScreens.IndexOf(value);
		        if(SettingsProvider.Settings.SelectedScreenIndex != index)
		        {
			        SettingsProvider.Settings.SelectedScreenIndex = index;
			        _ = SettingsProvider.SaveSettings();
		        }
	        }
        }

        public ObservableCollection<ScreenInfo> AllScreens { get; set; } = new ObservableCollection<ScreenInfo>();

        private IScreenInfoProvider screenInfoProvider = new ScreenInfoProvider();
        
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

		private bool restartInvoked = false;

		public AppBehaviorViewModel()
		{
			this.WindowIsVisible = !Startup.StartMinimized;
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
			App.Current.SessionEnding += (sender, args) => OnExit();

			HotkeysControl.OpenHotkeyPressed += OnOpenHotkeyPressed;
			ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
			DeviceProvider.GetInstance().DeviceFormatChanged += OnDeviceFormatChanged;

			this.trayBlockTimer = new Timer(trayBlockDelay);
			this.trayBlockTimer.AutoReset = false;
			this.trayBlockTimer.Elapsed += OnBlockTrayTimerElapsed;

			this.popupEnabled = SettingsProvider.Settings.PopupEnabled;
			this.alwaysOnTop = SettingsProvider.Settings.AlwaysOnTop;
			this.deviceViewAtTheBottom = SettingsProvider.Settings.DeviceViewAtTheBottom;
			this.displayDeviceIconAtTray = SettingsProvider.Settings.DisplayDeviceIconAtTray;

			this.windowLeft = SettingsProvider.Settings.WindowLeft;
			this.windowTop = SettingsProvider.Settings.WindowTop;
			
			this.LoadedCommand = new ActionCommand(this.CheckIfSelectedScreenIsAvailable);

			#if(!STORE)
			this.launchAtStartup = SystemIntegrationHelper.CheckIfStartupRegistryKeyExists();
			this.addToStartMenu = SystemIntegrationHelper.CheckIfStartMenuLinkExists();
			#endif
			
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

			int selectedScreenIndex = SettingsProvider.Settings.SelectedScreenIndex;
			if(AllScreens.Count - 1 < selectedScreenIndex || selectedScreenIndex < 0)
				SelectedScreen = screenInfoProvider.GetPrimaryScreenInfo();
			else
				SelectedScreen = AllScreens[selectedScreenIndex];
		}

		/// <summary>
		/// Executes after the window was displayed to check if the selected screen is available.
		/// If not, will reset the <see cref="SelectedScreen"/> to the primary screen.
		/// Also will update <see cref="AllScreens"/> to actual data.
		/// </summary>
		private void CheckIfSelectedScreenIsAvailable()
		{
			Task.Run(() =>
			{
				var screens = screenInfoProvider.GetAllScreensInfo().ToList();

				if(screens.Count != AllScreens.Count || screens.Except(AllScreens).Any())
				{
					App.Current.Dispatcher.Invoke(() =>
					{
						AllScreens.Clear();
						foreach(var screen in screens)
							AllScreens.Add(screen);
					});
				}

				if(!screens.Contains(SelectedScreen))
				{
					SelectedScreen = AllScreens.FirstOrDefault(s => s.IsPrimary);
				}
				//Update the SelectedScreen property in the UI as it will be set to none if the AllScreens collection would change
				else
					OnPropertyChanged(nameof(SelectedScreen));
			}).ContinueWith(task =>
			{
				if(task.Exception != null)
				{
					Logger.Error("Failed to check the screen for availability", task.Exception.Flatten());
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}

		private void OnDeviceFormatChanged(OutputDeviceModel sender)
		{
			//Prevent a situation when device format structure changes several times when its format is changed in system thus invoking several restarts
			if(restartInvoked)
				return;
			//After the device format is changed every stream of its sessions become invalid and returns 0x88890004/AUDCLNT_E_DEVICE_INVALIDATED when used
			//so you have to somehow update this data about sessions.
			//The only working way of handling this event I have found is restarting the app.
			//Releasing COM interfaces related to the device and retrieving them again to handle format change as recommended in documentation does nothing.
			//TODO find a better way to handle device format change
			restartInvoked = true;
			RestartApp();	
		}

		private void RestartApp()
		{
			var pathToExe = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
			if(this.WindowIsVisible)
				Process.Start(pathToExe, Startup.RestartArg);
			else
				Process.Start(pathToExe, $"{Startup.RestartArg} {Startup.MinimizedArg}");
			App.Current.Dispatcher.Invoke(OnExit);
		}
		
		private async void OnExit()
		{
			await SettingsProvider.SaveSettings().ConfigureAwait(true);
			LogManager.Shutdown();
			HotkeysControl.Dispose();
			App.Current.Shutdown();
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
				this.OpenCommand(false, displayAsPopup: false, reverseDisplaying: false);
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
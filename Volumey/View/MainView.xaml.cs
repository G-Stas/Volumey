using System;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Volumey.Helper;
using Volumey.ViewModel;
using log4net;
using MahApps.Metro.Controls.Dialogs;
using Volumey.DataProvider;
using Volumey.View.DialogContent;
using Volumey.ViewModel.Settings;
using Timer = System.Timers.Timer;

namespace Volumey.View
{
	public partial class MainView
	{
		private IntPtr Hwnd;

        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int SC_MOVE = 0xF010;
        private const int SC_SIZE = 0xF000;
        private const int WM_INITMENU = 0x0116;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_MINIMIZE = 0xF020;
        private const long MF_GRAYED = 0x00000001L;
        private const long MF_BYCOMMAND = 0x00000000L;
        private const int WM_CLOSE = 0x0010;
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int WM_SYSCOMMAND = 0x112;
        private const int RESTART_NO_REBOOT = 0x8;
        private const int WM_HOTKEY = 0x0312;
        private int prevMsg;

        private Action<int> HotkeyMessageHandler;
        private ILog logger;
        private ILog Logger => logger ??= LogManager.GetLogger(typeof(MainView));
        
        private Timer idleTimer;
        private bool requestReview;

		public MainView()
		{
			InitializeComponent();
            
			//handle all double clicks to prevent maximizing window by double clicking on titlebar
			PreviewMouseDoubleClick += (sender, e) => e.Handled = true;

            //force create HWND of the window
            //since the window is hidden at the startup
            //and HWND of the window is not going to be created until the window is shown at least once
            this.Hwnd = ForceCreateHwnd();
            
            //use created hwnd to register window messages handler & initialize hotkey manager
            var source = HwndSource.FromHwnd(this.Hwnd);
            source?.AddHook(WndProc);
            InitializeHotkeysManager();

            //register the app to restart it automatically in case it's going to be updated while running
            NativeMethods.RegisterApplicationRestart(Startup.MinimizedArg, RESTART_NO_REBOOT);

            this.ContentRendered += ((s, a) => ActivateIfLoaded());
            if(this.DataContext is MainViewModel vm)
	            vm.OpenCommandEvent += ActivateIfLoaded;

            if(!SettingsProvider.Settings.UserHasRated)
	            CheckDateForReviewRequest();
		}
		
		private IntPtr ForceCreateHwnd() => this.Hwnd = new WindowInteropHelper(this).EnsureHandle();

		private void InitializeHotkeysManager()
		{
			var hotkeyManager = new HotkeyManager(this.Hwnd);
			this.HotkeyMessageHandler = hotkeyManager.GetMessageHandler();
			HotkeysControl.SetHotkeyManager(hotkeyManager);
		}

		private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
		{
			SetWindowPosition();
			#if(!DEBUG)
			try
			{
				var updateIsAvailable = await UpdateHelper.CheckIfUpdateIsAvailable().ConfigureAwait(false);
				if(updateIsAvailable)
					await App.Current.Dispatcher.InvokeAsync(async () => await DisplayCustomDialog(new UpdateDialog()).ConfigureAwait(false));
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to check for update", e);
			}
			#endif
		}

		private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			bool isVisible = (bool) e.NewValue;
			if(isVisible)
            {
	            if(this.IsLoaded)
	            {
		            //resize window to fit its content before displaying it
		            this.SizeToContent = SizeToContent.Height;
		            
		            //limit maxheight to 65% of the main desktop's height
		            this.MaxHeight = SystemParameters.WorkArea.Height * 0.65;
				
		            //update layout before setting windows position to use actual height of the window when calculating its position
		            this.UpdateLayout(); 
		            this.SetWindowPosition();
					this.Activate();
				}
	            if(requestReview)
		            StartIdleTimer();
            }
			else if(requestReview)
			{
				if(this.idleTimer != null)
				    StopIdleTimer();
			}
		}

		/// <summary>
		/// Move window to the right bottom corner of display
		/// </summary>
		private void SetWindowPosition()
		{
			var deskWorkArea = SystemParameters.WorkArea;
			this.Left = deskWorkArea.Right - this.ActualWidth;
			var topPos = deskWorkArea.Bottom - this.ActualHeight;
			this.Top = topPos < 0 ? 0 : topPos;
		}

		private void ActivateIfLoaded()
		{
			if(this.IsLoaded)
				this.Activate();
		}
        
        /// <summary>
        /// Limit window height to its content height when tab/selected device has changed
        /// </summary>
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
	        //prevent reacting to SelectionChanged event of music session & theme combo boxes on settings tab
	        if(!(e.OriginalSource is ComboBox && this.TabControl.SelectedIndex == 1))
		        this.SizeToContent = SizeToContent.Height;
        }
        
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
	        switch(msg)
	        {
		        case WM_SYSCOMMAND:
		        {
			        //Catch a moment when window is starting to move
			        //to set ResizeMode.NoRisize to prevent snapping window to edges
			        var wPar = wParam.ToInt32();
			        if((wPar & SC_MOVE) == SC_MOVE && ResizeMode != ResizeMode.NoResize)
				        ResizeMode = ResizeMode.NoResize;
			        
			        //the window's MaxHeight is limited for auto-resize.
			        //but it will reset its MaxHeight when manual resizing starts (which is indicated by the SC_SIZE parameter)
			        //because the user should be able to resize the window to any height.
			        else if((wPar & SC_SIZE) == SC_SIZE && !double.IsPositiveInfinity(this.MaxHeight))
				        this.MaxHeight = double.PositiveInfinity;
			        break;
		        }
				
		        //Set ResizeMode back to the default value when moving was finished
		        case WM_EXITSIZEMOVE:
		        {
			        if(ResizeMode != ResizeMode.CanResize)
				        ResizeMode = ResizeMode.CanResize;
			        break;
		        }

		        //Disable Maximize/Minimize menu items in title bar's context menu every time it opens
		        case WM_INITMENU:
		        {
			        var menu = NativeMethods.GetSystemMenu(this.Hwnd, false);
			        NativeMethods.EnableMenuItem(menu, SC_MAXIMIZE, MF_GRAYED | MF_BYCOMMAND);
			        NativeMethods.EnableMenuItem(menu, SC_MINIMIZE, MF_GRAYED | MF_BYCOMMAND);
			        break;
		        }
			
		        //Unregister application restart if it was closed "normal way"
		        case WM_CLOSE:
		        {
			        //Hidden window doesn't receive WM_QUERYENDSESSION message which indicates about closing the app because of an update
			        //and which has to be used to register app restart
			        //so instead window registers app restart at startup and unregisters when it's closed "normal way"	
					
			        //When the window is closed by "exit" menu item in the context menu i.e. normal way, the previous message is always WM_ACTIVATEAPP.
			        //It recieves other messages before WM_CLOSE when it's closed by update/crash
			        if(prevMsg == WM_ACTIVATEAPP)
                        NativeMethods.UnregisterApplicationRestart();
			        break;
		        }

		        case WM_HOTKEY:
		        {
			        HotkeyMessageHandler(lParam.ToInt32());
			        break;
		        }
	        }
	        prevMsg = msg;
	        return IntPtr.Zero;
        }
        
        private void CheckDateForReviewRequest()
        {
	        var settings = SettingsProvider.Settings;
	        if((DateTime.Today - settings.FirstLaunchDate).Days >= 7 && settings.LaunchCount >= 7)
		        this.requestReview = true;
        }
        
        private void StartIdleTimer()
        {
	        this.PreviewMouseDown += OnPreviewInputDown;
	        this.PreviewKeyDown += OnPreviewInputDown;
	        this.idleTimer = new Timer(10000) { AutoReset = false };
	        this.idleTimer.Elapsed += OnIdleTimeElapsed;
	        this.idleTimer.Enabled = true;
        }

        private void StopIdleTimer()
        {
	        this.PreviewMouseDown -= OnPreviewInputDown;
	        this.PreviewKeyDown -= OnPreviewInputDown;
	        idleTimer.Enabled = false;
	        idleTimer.Elapsed -= OnIdleTimeElapsed;
	        idleTimer.Dispose();
	        idleTimer = null;
        }

        private async void OnIdleTimeElapsed(object sender, ElapsedEventArgs e)
        {
	        StopIdleTimer();
	        await App.Current.Dispatcher.InvokeAsync(async () =>
	        {
		        var reviewDialog = new ReviewDialog();
		        reviewDialog.DialogResult += OnReviewDialogResult;
		        await DisplayCustomDialog(reviewDialog).ConfigureAwait(false); 
	        });
        }
        
        private void OnPreviewInputDown(object sender, InputEventArgs  e)
        {
	        if(this.idleTimer != null && this.idleTimer.Enabled)
	        {
		        this.idleTimer.Stop();
		        this.idleTimer.Start();
	        }
        }
        
        private async Task DisplayCustomDialog(DialogContent.DialogContent content)
        {
	        var settings = new MetroDialogSettings
	        {
		        AnimateShow = false,
		        AnimateHide = false,
		        OwnerCanCloseWithDialog = false,
	        };

	        try
	        {
		        var dialog = new CustomDialog(settings) { Background = Brushes.Transparent, Content = content };
		        content.DialogResult += OnDialogResult;
		        await this.ShowMetroDialogAsync(dialog).ConfigureAwait(false);
		        NativeMethods.FlashWindow(this.Hwnd);
	        }
	        catch(Exception e)
	        {
		        Logger.Error("Failed to display custom dialog", e);
	        }
        }
        
        private async void OnDialogResult(DialogContent.DialogContent sender)
        {
	        sender.DialogResult -= OnDialogResult;
	        try
	        {
		        if(sender.ParentDialog != null)
		        {
			        await App.Current.Dispatcher.InvokeAsync(async () =>
			        {
				        await this.HideMetroDialogAsync(sender.ParentDialog).ConfigureAwait(false);
			        });
		        }
	        }
	        catch(Exception e)
	        {
		        Logger.Error("Failed to hide custom dialog", e);
	        }
        }
        
        private void OnReviewDialogResult(DialogContent.DialogContent sender)
        {
	        sender.DialogResult -= OnReviewDialogResult;
	        requestReview = false;	
        }
	}
}
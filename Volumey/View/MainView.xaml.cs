using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using Hardcodet.Wpf.TaskbarNotification;
using Volumey.Helper;
using Volumey.ViewModel;
using log4net;
using ModernWpf.Controls;
using ModernWpf.Media.Animation;
using ModernWpf.Navigation;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.View
{
	public partial class MainView : INotifyPropertyChanged
	{
		private IntPtr Hwnd;
		
        private const int WM_INITMENU = 0x0116;
        private const int SC_MINIMIZE = 0xF020;
        private const long MF_GRAYED = 0x00000001L;
        private const long MF_BYCOMMAND = 0x00000000L;
        private const int WM_CLOSE = 0x0010;
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int RESTART_NO_REBOOT = 0x8;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private int prevMsg;
        private bool positionChanged;

        private const int ScrollStep = 30;
        private static object NavPaneHeight = (double)32;
        private static object NavPaneHiddenHeight = (double)0;
        private const int BorderIndent = 11;

        public static readonly DependencyProperty SelectedScreenProperty = DependencyProperty.Register("SelectedScreen", typeof(ScreenInfo) , typeof(MainView), new PropertyMetadata(SelectedScreenChangedCallback));

        private ScreenInfo SelectedScreen => (ScreenInfo)GetValue(SelectedScreenProperty);

        private Action<int> HotkeyMessageHandler;
        private ILog logger;
        private ILog Logger => logger ??= LogManager.GetLogger(typeof(MainView));
        
        private readonly NavigationTransitionInfo transitionFromRight = new SlideNavigationTransitionInfo
	        { Effect = SlideNavigationTransitionEffect.FromRight };

        private readonly NavigationTransitionInfo transitionFromLeft = new SlideNavigationTransitionInfo
	        { Effect = SlideNavigationTransitionEffect.FromLeft };

		private readonly NavigationTransitionInfo suppressTransition = new SuppressNavigationTransitionInfo();

		public MainView()
		{
			InitializeComponent();
			
            //force create HWND of the window
            //since the window is hidden at the startup
            //and HWND of the window is not going to be created until the window is shown at least once
            this.Hwnd = ForceCreateHwnd();
            
            //use created hwnd to register window messages handler
            var source = HwndSource.FromHwnd(this.Hwnd);
            source?.AddHook(WndProc);

            //register the app to restart it automatically in case it's going to be updated while running
            NativeMethods.RegisterApplicationRestart(Startup.MinimizedArg, RESTART_NO_REBOOT);

            this.ContentRendered += ((s, a) => ActivateIfLoaded(isSettingsPage: this.ContentFrame.Content is SettingsView, windowIsVisible: false, isPopupMode: Startup.StartMinimized && SettingsProvider.Settings.PopupEnabled));
            if(this.DataContext is AppBehaviorViewModel vm)
	            vm.DisplayAppRequested += ActivateIfLoaded;
            
            SetTrayIconTooltip();
            SetControlsNameScope();
            SetBindings();

            this.ContentFrame.SizeChanged += (sender, args) => OnCurrentPageSizeChanged();
            this.ContentFrame.Navigated += (sender, args) =>
            {
	            if(args.SourcePageType() == typeof(SettingsView))
		            NavView.SelectedItem = NavView.SettingsItem;
	            else
		            NavView.SelectedItem = NavView.MenuItems[0];
            };

			//Load SettingsViewModel from resources so hotkeys could be loaded on app startup instead of waiting when settings view will be opened to load it
			var settingsVm = (SettingsViewModel)TryFindResource("SettingsViewModel");
			this.HotkeyMessageHandler = settingsVm.GetHotkeyMessageHandler();
			settingsVm.SetWindowHandle(this.Hwnd);
		}

		/// <summary>
		/// Set name scopes for controls so they could bind to other controls by ElementName
		/// </summary>
		private void SetControlsNameScope()
		{
			var nameScope = NameScope.GetNameScope(this);
			if(this.Tray.ContextMenu != null)
				NameScope.SetNameScope(this.Tray.ContextMenu, nameScope);
			if(TryFindResource("TextControlContextMenu") is ContextMenu cm)
				NameScope.SetNameScope(cm, nameScope);
			if(this.Tray.TrayToolTip is ToolTip tooltip)
				NameScope.SetNameScope(tooltip, nameScope);
		}

		private void SetBindings()
		{
			var appBehaviorVm = (AppBehaviorViewModel)this.DataContext;
			var displayBinding = new Binding
			{
				Source = appBehaviorVm,
				Path = new PropertyPath("SelectedScreen"),
				Mode = BindingMode.OneWay,
				UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
			};
			BindingOperations.SetBinding(this, SelectedScreenProperty, displayBinding);
		}

		private void SetTrayIconTooltip()
		{
			if(UpdateHelper.IsWindows11())
			{
				//Since custom tooltip style for TrayIcon isn't working on Windows 11
				//We are using default tooltip style and bind its text property to current device data
				var tooltipTextBinding = (MultiBinding)TryFindResource("TrayTextTooltip");
				BindingOperations.SetBinding(this.Tray, TaskbarIcon.ToolTipTextProperty, tooltipTextBinding);
			}
			else
			{ 
				var tooltip = (ToolTip)TryFindResource("TrayCustomTooltip");
				this.Tray.TrayToolTip = tooltip;
			}
		}

		private IntPtr ForceCreateHwnd() => this.Hwnd = new WindowInteropHelper(this).EnsureHandle();

		private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			bool isVisible = (bool) e.NewValue;
			if(isVisible)
            {
				this.positionChanged = false;
	            if(this.IsLoaded)
	            {
		            this.LimitWindowHeightIfNecessary();
				
		            //update layout before setting windows position to use actual height of the window when calculating its position
		            this.UpdateLayout();
					this.SetWindowPosition();
					this.Activate();
					this.Focus();
	            }
            }
		}

		private enum TaskBarLocation { TOP, BOTTOM, LEFT, RIGHT }
		private TaskBarLocation GetTaskBarLocation()
		{
			if (SystemParameters.WorkArea.Left > 0)
				return TaskBarLocation.LEFT;
			if (SystemParameters.WorkArea.Top > 0)
				return TaskBarLocation.TOP;
			if (SystemParameters.WorkArea.Left == 0
			  && SystemParameters.WorkArea.Width < SystemParameters.PrimaryScreenWidth)
				return TaskBarLocation.RIGHT;
			return TaskBarLocation.BOTTOM;
		}

		/// <summary>
		/// Move window to the right corner depending on the taskbar location
		/// </summary>
		private void SetWindowPosition()
		{
			TaskBarLocation taskBarLocation = GetTaskBarLocation();
			double topPos;

			switch (taskBarLocation)
			{
				case TaskBarLocation.TOP:

					topPos = SelectedScreen.WorkingAreaTop + BorderIndent;
					MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - this.ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
					break;

                case TaskBarLocation.BOTTOM:

                    topPos = SelectedScreen.WorkingAreaBottom - this.ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
                    MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - this.ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
                    break;

                case TaskBarLocation.LEFT:

                    topPos = SelectedScreen.WorkingAreaBottom - this.ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
                    MoveTo(SelectedScreen.WorkingAreaLeft + BorderIndent, topPos < 0 ? 0 : topPos);
                    break;

                case TaskBarLocation.RIGHT:

                    topPos = SelectedScreen.WorkingAreaBottom - this.ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
                    MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - this.ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
                    break;
            }
        }
		
		private void MoveTo(double left, double top)
		{
			NativeMethods.SetWindowPos(this.Hwnd, (IntPtr)NativeMethods.SpecialWindowHandles.Top,
			             (int)left, (int)top, (int)this.Width, (int)this.Height, NativeMethods.SetWindowPosFlags.ShowWindow);
		}

		private void LimitWindowHeightIfNecessary()
		{
			var desktopHeight = SelectedScreen.Height;
			var maxHeight = desktopHeight * 0.6;
			
			if(this.ContentFrame.Content is MixerView)
			{
				if(this.ActualHeight > maxHeight)
				{
					this.SizeToContent = SizeToContent.Manual;
					this.Height = desktopHeight * 0.64;
					return;
				}
			}
			this.SizeToContent = SizeToContent.Height;
		}
		
		private void ActivateIfLoaded(bool isSettingsPage, bool windowIsVisible, bool isPopupMode)
		{
			var pageType = isSettingsPage ? typeof(SettingsView) : typeof(MixerView);
			
			//hide navigation pane if in popup mode 
			if(isPopupMode)
				this.Resources["NavigationViewTopPaneHeight"] = NavPaneHiddenHeight;
			else
				this.Resources["NavigationViewTopPaneHeight"] = NavPaneHeight;
			
			if(windowIsVisible)
				Navigate(pageType);
			else
				NavigateWithoutTransition(pageType);

			if (this.IsLoaded)
				this.Activate();
		}

		private void NavigationView_OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if(args.IsSettingsInvoked)
                Navigate(typeof(SettingsView));
            else if(args.InvokedItemContainer is NavigationViewItem item)
					Navigate(GetPageType(item));
        }
		
		private void Navigate(Type sourcePageType)
		{
			if(ContentFrame.CurrentSourcePageType == sourcePageType)
				return;
			//Subscribe to mixer view CollectionChanged event when it'll start loading
			if(sourcePageType == typeof(MixerView))
				this.ContentFrame.Navigated += OnMixerViewNavigated;
			//Unsubscribe if mixer view is changed to another view
			else
			{
				if(this.ContentFrame.Content is MixerView view)
					view.CollectionChanged -= OnMixerViewCollectionChanged;
			}
			ContentFrame.Navigate(sourcePageType, null, sourcePageType == typeof(SettingsView) ? transitionFromRight : transitionFromLeft);
		}

		/// <summary>
		/// Handles navigating pages without animations which occurs when app is displayed after being minimized.
		/// </summary>
		private void NavigateWithoutTransition(Type sourcePageType)
        {
	        if(ContentFrame.CurrentSourcePageType != sourcePageType)
	        {
		        //Subscribe to mixer view CollectionChanged event when it'll start loading
		        if(sourcePageType == typeof(MixerView))
			        this.ContentFrame.Navigated += OnMixerViewNavigated;
		        //Unsubscribe if mixer view is changed to another view
		        else
		        {
			        if(this.ContentFrame.Content is MixerView view)
				        view.CollectionChanged -= OnMixerViewCollectionChanged;
		        }
		        ContentFrame.Navigate(sourcePageType, null, suppressTransition);
	        }
        }

		/// <summary>
		/// Invokes when a mixer view is loading to display. Subscribes to its event to update window position when its items count changes. 
		/// </summary>
		private void OnMixerViewNavigated(object sender, NavigationEventArgs e)
		{
			this.ContentFrame.Navigated -= OnMixerViewNavigated;
			if(this.ContentFrame.Content is MixerView view)
				view.CollectionChanged += OnMixerViewCollectionChanged;
		}

		/// <summary>
		/// Invokes when items count of the currently displayed mixer view changes. Calls PageSizeChanged handler to update window position.
		/// </summary>
		private void OnMixerViewCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			=> this.OnCurrentPageSizeChanged();

		/// <summary>
		/// Updates window position when the currently displayed page changes its size.
		/// </summary>
		private void OnCurrentPageSizeChanged()
		{
			if(this.Visibility == Visibility.Visible && !this.positionChanged)
			{
				this.LimitWindowHeightIfNecessary();
				this.SetWindowPosition();	
			}
		}

		private Type GetPageType(NavigationViewItem item)
		{
			return item.Tag as Type;
		}

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
	        switch(msg)
	        {
		        case WM_ENTERSIZEMOVE:
		        {
			        this.positionChanged = true;
			        break;
		        }
		        //Disable Minimize menu item in title bar's context menu every time it opens
		        case WM_INITMENU:
		        {
			        var menu = NativeMethods.GetSystemMenu(this.Hwnd, false);
			        NativeMethods.EnableMenuItem(menu, SC_MINIMIZE, MF_GRAYED | MF_BYCOMMAND);
			        NativeMethods.EnableMenuItem(menu, 0xF030, MF_GRAYED | MF_BYCOMMAND);
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
			        HotkeyMessageHandler?.Invoke(lParam.ToInt32());
			        break;
		        }
	        }
	        prevMsg = msg;
	        return IntPtr.Zero;
        }

        private bool isDialogOpened;
        private Queue<ContentDialog> dialogQueue;

        private async Task DisplayContentDialog(ContentDialog dialog)
        {
	        try
	        {
		        if(isDialogOpened)
		        {
			        if(dialogQueue == null)
				        dialogQueue = new Queue<ContentDialog>();
			        dialogQueue.Enqueue(dialog);
			        return;
		        }

		        dialog.Closed += OnDialogClosed;
		        isDialogOpened = true;
		        NativeMethods.FlashWindow(this.Hwnd);
		        await dialog.ShowAsync(ContentDialogPlacement.Popup);
	        }
	        catch(Exception e)
	        {
		        isDialogOpened = false;
		        Logger.Error($"Failed to display ContentDialog of type {dialog.GetType()}", e);
	        }
        }

        private async void OnDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
	        sender.Closed -= OnDialogClosed;
	        isDialogOpened = false;
	        if(dialogQueue != null && dialogQueue.Count > 0)
		        await DisplayContentDialog(dialogQueue.Dequeue());
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
	        //prevent closing or scrolling if any textbox is focused
	        if(Keyboard.FocusedElement is TextBox)
		        return;
	        switch(e.Key)
	        {
		        case Key.Escape:
			        this.Close();
			        break;
		        case Key.Up:
		        {
			        var scrollViewer = this.GetCurrentPageScrollViewer();
			        if(scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
				        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - ScrollStep);
			        break;
		        }
		        case Key.Down:
		        {
			        var scrollViewer = this.GetCurrentPageScrollViewer();
			        if(scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
				        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + ScrollStep);
			        break;
		        }
	        }
        }

        private ScrollViewer GetCurrentPageScrollViewer()
        {
	        ScrollViewer scrollViewer = null;
	        if(this.ContentFrame.Content is MixerView mixer)
		        scrollViewer = mixer.ScrollViewer;
	        else if(this.ContentFrame.Content is SettingsView settings)
		        scrollViewer = settings.ScrollViewer;
	        return scrollViewer;
        }
        
        
        private static void SelectedScreenChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
	        if(d is MainView view)
	        {
		        if(!view.IsVisible)
			        return;
		        view.LimitWindowHeightIfNecessary();
		        view.SetWindowPosition();
	        }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using H.NotifyIcon;
using log4net;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;

namespace Volumey.View;

/// <summary>
/// The mixer window: the popup above the tray in popup mode (no title bar, height fixed when it opens),
/// otherwise a normal resizable window whose size and position are remembered (#80). It also owns the tray icon
/// and the window messages (hotkeys, restart, second start)
/// </summary>
public partial class MainView
{
	private IntPtr Hwnd;

	private readonly WindowMessages messages;

	private const int ScrollStep = 30;
	private const int BorderIndent = 11;

	//popup sizing (mockups FlyoutMin/FlyoutCapped): the height is computed once when it opens and doesn't change while it's
	//visible; new apps go to the end of the scrolling list
	private const double PopupWidth = 368; //WPF-UI's minimum window width (460) × 4/5, owner's decision; MainView.xaml lowers MinWidth to it
	private const double RowHeight = 54;
	private const int MinRows = 3;
	private const double MaxShareOfWorkArea = 0.6;

	//window mode before its size was ever saved
	private const double DefaultWindowWidth = PopupWidth; //one width for both modes (owner)
	private const double DefaultWindowHeight = 540;

	public static readonly DependencyProperty SelectedScreenProperty = DependencyProperty.Register("SelectedScreen", typeof(ScreenInfo), typeof(MainView), new PropertyMetadata(SelectedScreenChangedCallback));

	private ScreenInfo SelectedScreen => (ScreenInfo)GetValue(SelectedScreenProperty);

	private static readonly DependencyProperty TrayIconImageProperty = DependencyProperty.Register("TrayIconImage", typeof(System.Drawing.Icon), typeof(MainView), new PropertyMetadata(OnTrayIconImageChanged));
	private static readonly DependencyProperty TrayToolTipTextProperty = DependencyProperty.Register("TrayToolTipText", typeof(string), typeof(MainView), new PropertyMetadata(OnTrayToolTipTextChanged));

	private ILog logger;
	private ILog Logger => logger ??= LogManager.GetLogger(typeof(MainView));

	private readonly AppBehaviorViewModel vm;
	private SettingsWindow settingsWindow;

	/// <summary>Set while the window mode's size or position is applied, so applying it isn't saved as a user change</summary>
	private bool applyingPlacement;

	/// <summary>
	/// Set once the visible window has been placed: before that, WPF's first position (Windows' default) would be saved
	/// over the remembered one
	/// </summary>
	private bool placed;

	public MainView()
	{
		InitializeComponent();
		WindowBackdropType = ThemeViewModel.Backdrop;
		vm = (AppBehaviorViewModel)DataContext;

		//size and position before the HWND exists, so a window shown at startup appears at its remembered place right away
		ApplyMode();
		if(!vm.DisplayMinimalistic && vm.WindowWidth > 0 && vm.RememberPosition)
		{
			Left = vm.WindowLeft;
			Top = vm.WindowTop;
		}

		//force create HWND of the window
		//since the window is hidden at the startup
		//and HWND of the window is not going to be created until the window is shown at least once
		Hwnd = ForceCreateHwnd();

		//use created hwnd to register window messages handler
		var source = HwndSource.FromHwnd(Hwnd);
		source?.AddHook(WndProc);

		messages = new WindowMessages(Win32.Instance, Hwnd);
		messages.RegisterRestart();

		ContentRendered += (s, a) => ActivateIfLoaded(windowIsVisible: false, isPopupMode: vm.DisplayMinimalistic);
		vm.DisplayAppRequested += ActivateIfLoaded;
		vm.SettingsRequested += ShowSettings;
		vm.PropertyChanged += OnViewModelPropertyChanged;

		SizeChanged += (s, a) => SaveWindowPlacement();
		LocationChanged += (s, a) => SaveWindowPlacement();

		SetUpTrayIcon();
		SetBindings();

		//Load SettingsViewModel from resources so hotkeys could be loaded on app startup instead of waiting when settings view will be opened to load it
		var settingsVm = (SettingsViewModel)TryFindResource("SettingsViewModel");
		messages.HotkeyMessageHandler = settingsVm.GetHotkeyMessageHandler();
		settingsVm.SetWindowHandle(Hwnd);
	}

	private void SetBindings()
	{
		var displayBinding = new Binding
		{
			Source = vm,
			Path = new PropertyPath("SelectedScreen"),
			Mode = BindingMode.OneWay,
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		};
		BindingOperations.SetBinding(this, SelectedScreenProperty, displayBinding);
	}

	private void SetUpTrayIcon()
	{
		//The icon and the tooltip go straight to the shell icon instead of the TaskbarIcon properties:
		//TaskbarIcon.Icon disposes the previous icon, but the icons come from a cache (or belong to the device model),
		//and a failed shell call throws, which mustn't crash the app (e.g. while Explorer restarts).
		//The tooltip is the native text tooltip on every Windows version (a custom WPF tooltip got stuck on Windows 10, #65, #91)
		BindingOperations.SetBinding(this, TrayIconImageProperty, (MultiBinding)TryFindResource("TrayIconBinding"));
		BindingOperations.SetBinding(this, TrayToolTipTextProperty, (MultiBinding)TryFindResource("TrayTextTooltip"));

		//TaskbarIcon creates itself on Loaded, but the window starts hidden, so it may never load.
		//Efficiency mode stays off: it would throttle hotkeys and audio callbacks
		UpdateTrayIcon(tray => tray.ForceCreate(enablesEfficiencyMode: false), "create");
	}

	private static void OnTrayIconImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var icon = e.NewValue as System.Drawing.Icon;
		((MainView)d).UpdateTrayIcon(tray =>
		{
			if(!tray.UpdateIcon(icon))
				throw new InvalidOperationException("Shell_NotifyIcon failed");
		}, "update the icon of");
	}

	private static void OnTrayToolTipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var text = e.NewValue as string ?? string.Empty;
		((MainView)d).UpdateTrayIcon(tray => tray.TrayIcon.UpdateToolTip(text), "update the tooltip of");
	}

	private void UpdateTrayIcon(Action<TaskbarIcon> update, string action)
	{
		try
		{
			update(Tray);
		}
		catch(Exception e) when(e is InvalidOperationException or ObjectDisposedException or Win32Exception)
		{
			//the icon is created again with the last icon and tooltip when the taskbar comes back
			Logger.Warn($"Failed to {action} the tray icon", e);
		}
	}

	private IntPtr ForceCreateHwnd() => Hwnd = new WindowInteropHelper(this).EnsureHandle();

	private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(AppBehaviorViewModel.DisplayMinimalistic))
		{
			ApplyMode();
			//e.g. the tray menu's "Mixer" while the popup is open: it becomes the window, where the window belongs
			if(IsLoaded && IsVisible)
				PlaceWindow();
		}
	}

	/// <summary>
	/// The popup has a fixed width and can't be resized; the window mode is resizable and gets its saved size
	/// </summary>
	private void ApplyMode()
	{
		applyingPlacement = true;
		try
		{
			if(vm.DisplayMinimalistic)
			{
				ResizeMode = ResizeMode.NoResize;
				Width = PopupWidth;
			}
			else
			{
				ResizeMode = ResizeMode.CanResize;
				Width = vm.WindowWidth > 0 ? vm.WindowWidth : DefaultWindowWidth;
				Height = vm.WindowHeight > 0 ? vm.WindowHeight : DefaultWindowHeight;
			}
		}
		finally
		{
			applyingPlacement = false;
		}
	}

	private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		bool isVisible = (bool)e.NewValue;
		if(!isVisible)
			placed = false;
		else if(IsLoaded)
		{
			PlaceWindow();
			Activate();
			Focus();
		}
	}

	private void PlaceWindow()
	{
		if(vm.DisplayMinimalistic)
		{
			SizePopup();
			//update layout before setting windows position to use actual height of the window when calculating its position
			UpdateLayout();
			if(vm.RememberPosition && vm.PopupLeft is { } left && vm.PopupTop is { } top && IsOnScreen(left, top))
				MoveToRemembered(left, top);
			else
				SetWindowPosition();
		}
		else
			RestoreWindowPlacement();
		placed = true;
	}

	/// <summary>Whether the window at this position would still be on a screen (one may have been unplugged)</summary>
	private bool IsOnScreen(double left, double top)
	{
		var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
			SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
		return screen.IntersectsWith(new Rect(left, top, Width, Height));
	}

	private void MoveToRemembered(double left, double top)
	{
		applyingPlacement = true;
		Left = left;
		Top = top;
		applyingPlacement = false;
	}

	/// <summary>
	/// The mixer is dragged by any spot that isn't a control, in both modes, whether its position is remembered or not (owner).
	/// The new position is always noted (window mode: LocationChanged; popup: below) but used for the next opening only while it's remembered.
	/// On the preview: the apps list's ScrollViewer handles the press itself, so the bubbling event never reached the window there.
	/// </summary>
	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		if(e.Handled || e.ButtonState != MouseButtonState.Pressed || IsInControl(e.OriginalSource as DependencyObject))
			return;
		e.Handled = true;
		DragMove();
		if(vm.DisplayMinimalistic)
		{
			vm.PopupLeft = Left;
			vm.PopupTop = Top;
		}
	}

	/// <summary>Whether the pressed element is (inside) something that takes clicks itself</summary>
	private static bool IsInControl(DependencyObject source)
	{
		for(var element = source; element != null;
		    element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
		{
			if(element is ButtonBase or RangeBase or Thumb or Selector or TextBoxBase)
				return true;
		}
		return false;
	}

	/// <summary>
	/// The popup's height: its fixed parts plus the apps list (at least <see cref="MinRows"/> rows),
	/// at most <see cref="MaxShareOfWorkArea"/> of the screen's work area. Computed when it opens only
	/// </summary>
	private void SizePopup()
	{
		var (chrome, apps) = Mixer.MeasureHeights(PopupWidth);
		double workAreaHeight = (SelectedScreen.WorkingAreaBottom - SelectedScreen.WorkingAreaTop) / SelectedScreen.ScaleFactor;
		double max = Math.Max(MinHeight, workAreaHeight * MaxShareOfWorkArea);
		applyingPlacement = true;
		Height = Math.Clamp(chrome + Math.Max(apps, MinRows * RowHeight), MinHeight, max);
		applyingPlacement = false;
	}

	/// <summary>
	/// Window mode: its saved size; the saved position while it's remembered and still on a screen, otherwise next to the tray like the popup
	/// </summary>
	private void RestoreWindowPlacement()
	{
		ApplyMode();
		if(vm.RememberPosition && vm.WindowWidth > 0 && IsOnScreen(vm.WindowLeft, vm.WindowTop))
			MoveToRemembered(vm.WindowLeft, vm.WindowTop);
		else
		{
			UpdateLayout();
			SetWindowPosition();
		}
	}

	/// <summary>
	/// Remembers the window mode's size and position (saved to the settings on exit)
	/// </summary>
	private void SaveWindowPlacement()
	{
		if(applyingPlacement || !placed || vm.DisplayMinimalistic || !IsVisible || WindowState != WindowState.Normal)
			return;
		vm.WindowLeft = Left;
		vm.WindowTop = Top;
		vm.WindowWidth = ActualWidth;
		vm.WindowHeight = ActualHeight;
	}

	private enum TaskBarLocation { TOP, BOTTOM, LEFT, RIGHT }
	private TaskBarLocation GetTaskBarLocation()
	{
		if(SystemParameters.WorkArea.Left > 0)
			return TaskBarLocation.LEFT;
		if(SystemParameters.WorkArea.Top > 0)
			return TaskBarLocation.TOP;
		if(SystemParameters.WorkArea.Left == 0
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

		switch(taskBarLocation)
		{
			case TaskBarLocation.TOP:

				topPos = SelectedScreen.WorkingAreaTop + BorderIndent;
				MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
				break;

			case TaskBarLocation.BOTTOM:

				topPos = SelectedScreen.WorkingAreaBottom - ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
				MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
				break;

			case TaskBarLocation.LEFT:

				topPos = SelectedScreen.WorkingAreaBottom - ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
				MoveTo(SelectedScreen.WorkingAreaLeft + BorderIndent, topPos < 0 ? 0 : topPos);
				break;

			case TaskBarLocation.RIGHT:

				topPos = SelectedScreen.WorkingAreaBottom - ActualHeight * SelectedScreen.ScaleFactor - BorderIndent;
				MoveTo(SelectedScreen.WorkingAreaLeft + SelectedScreen.Width - ActualWidth * SelectedScreen.ScaleFactor - BorderIndent, topPos < 0 ? 0 : topPos);
				break;
		}
	}

	private void MoveTo(double left, double top)
	{
		//move only: WPF sizes the window
		PInvoke.SetWindowPos((HWND)Hwnd, default /*HWND_TOP*/, (int)left, (int)top, 0, 0,
			SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
	}

	/// <summary>
	/// Brings a visible window to the front; a hidden one is placed and activated when it becomes visible
	/// </summary>
	private void ActivateIfLoaded(bool windowIsVisible, bool isPopupMode)
	{
		if(IsLoaded && IsVisible)
			Activate();
	}

	private void ShowSettings()
	{
		settingsWindow ??= new SettingsWindow();
		if(!settingsWindow.IsVisible)
			CenterOnMixerScreen(settingsWindow);
		settingsWindow.Show();
		if(settingsWindow.WindowState == WindowState.Minimized)
			settingsWindow.WindowState = WindowState.Normal;
		settingsWindow.Activate();
	}

	/// <summary>
	/// The settings open centered on the screen the mixer is on (owner's decision), also when the mixer is hidden:
	/// its window keeps its last position. If the screen can't be read, the window's own startup location is used
	/// </summary>
	private void CenterOnMixerScreen(Window window)
	{
		var monitor = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
		if(!PInvoke.GetMonitorInfo(PInvoke.MonitorFromWindow((HWND)Hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST), ref monitor))
			return;
		var work = monitor.rcWork;
		//the target screen's scale gives the window's size there; the window's current DPI converts physical pixels to its Left/Top
		double targetScale = 1;
		foreach(var screen in vm.AllScreens)
			if((int)screen.WorkingAreaLeft == work.left && (int)screen.WorkingAreaTop == work.top)
				targetScale = screen.ScaleFactor;
		double currentScale = VisualTreeHelper.GetDpi(window).DpiScaleX;
		double width = window.Width * targetScale;
		double height = window.Height * targetScale;
		window.WindowStartupLocation = WindowStartupLocation.Manual;
		window.Left = (work.left + Math.Max(0, (work.right - work.left - width) / 2)) / currentScale;
		window.Top = (work.top + Math.Max(0, (work.bottom - work.top - height) / 2)) / currentScale;
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		messages.Handle(msg, wParam, lParam);
		return IntPtr.Zero;
	}

	private bool isDialogOpened;
	private readonly Queue<ContentDialog> dialogQueue = new();

	private async Task DisplayContentDialog(ContentDialog dialog)
	{
		if(isDialogOpened)
		{
			dialogQueue.Enqueue(dialog);
			return;
		}
		isDialogOpened = true;
		try
		{
			dialog.DialogHostEx = DialogHost;
			NativeMethods.FlashWindow(Hwnd);
			await dialog.ShowAsync();
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to display ContentDialog of type {dialog.GetType()}", e);
		}
		finally
		{
			isDialogOpened = false;
		}
		if(dialogQueue.Count > 0)
			DisplayContentDialog(dialogQueue.Dequeue()).Forget(Logger, "Failed to show a queued dialog");
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		//prevent closing or scrolling if any textbox is focused
		if(Keyboard.FocusedElement is TextBox)
			return;
		var scrollViewer = Mixer.ScrollViewer;
		switch(e.Key)
		{
			case Key.Escape:
				Close();
				break;
			case Key.Up:
				if(scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
					scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - ScrollStep);
				break;
			case Key.Down:
				if(scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
					scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + ScrollStep);
				break;
		}
	}

	private static void SelectedScreenChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is MainView view && view.IsVisible && view.vm.DisplayMinimalistic)
		{
			view.SizePopup();
			view.UpdateLayout();
			view.SetWindowPosition();
		}
	}
}

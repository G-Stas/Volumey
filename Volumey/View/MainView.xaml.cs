#nullable enable
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
using System.Windows.Threading;
using System.Windows.Media.Animation;
using H.NotifyIcon;
using log4net;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;

namespace Volumey.View;

/// <summary>
/// The mixer window: the popup above the tray in popup mode (no title bar, height fixed when it opens),
/// otherwise a normal resizable window whose size and position are remembered. It also owns the tray icon
/// and the window messages (hotkeys, restart, second start)
/// </summary>
public sealed partial class MainView
{
	private IntPtr Hwnd;

	private readonly WindowMessages messages;

	private const int ScrollStep = 30;
	private const int BorderIndent = 11;

	//sizing in both modes: the height is computed each time the mixer opens and doesn't change while it's
	//visible; new apps go to the end of the scrolling list
	private const double PopupWidth = 368; //WPF-UI's minimum window width (460) × 4/5; MainView.xaml lowers MinWidth to it
	private const double RowHeight = 54;
	private const int MinRows = 3;
	private const double MaxShareOfWorkArea = 0.6;

	//window mode before its width was ever saved
	private const double DefaultWindowWidth = PopupWidth; //one width for both modes

	public static readonly DependencyProperty SelectedScreenProperty = DependencyProperty.Register("SelectedScreen", typeof(ScreenInfo), typeof(MainView), new PropertyMetadata(SelectedScreenChangedCallback));

	private ScreenInfo SelectedScreen => (ScreenInfo)GetValue(SelectedScreenProperty);

	private static readonly DependencyProperty TrayIconImageProperty = DependencyProperty.Register("TrayIconImage", typeof(System.Drawing.Icon), typeof(MainView), new PropertyMetadata(OnTrayIconImageChanged));
	private static readonly DependencyProperty TrayToolTipTextProperty = DependencyProperty.Register("TrayToolTipText", typeof(string), typeof(MainView), new PropertyMetadata(OnTrayToolTipTextChanged));

	private ILog? logger;
	private ILog Logger => logger ??= LogManager.GetLogger(typeof(MainView));

	private readonly AppBehaviorViewModel vm;
	/// <summary>Creates the settings window the first time it opens (the container keeps the one instance)</summary>
	private readonly Func<SettingsWindow> getSettingsWindow;
	private SettingsWindow? settingsWindow;

	private readonly DeviceProviderViewModel devices;
	private readonly TrayWheelInput trayWheel;
	private readonly INotificationService notifications;

	/// <summary>Set while the window mode's size or position is applied, so applying it isn't saved as a user change</summary>
	private bool applyingPlacement;

	/// <summary>
	/// Set once the visible window has been placed: before that, WPF's first position (Windows' default) would be saved
	/// over the remembered one
	/// </summary>
	private bool placed;

	/// <summary>
	/// Indicates whether the current operating system requires the native window cloaking workaround to prevent white flashes during display.
	/// This evaluates to <see langword="true"/> on systems where the configured window backdrop is not supported (such as Windows 10), 
	/// and <see langword="false"/> on systems that handle it natively without visual glitches (such as Windows 11).
	/// </summary>
	private readonly bool requiresCloak;

	internal MainView(MainViewModel main, HotkeyModeViewModel hotkeyMode, Func<SettingsWindow> getSettingsWindow, INotificationService notifications)
	{
		vm = main.Window;
		devices = main.Devices;
		this.notifications = notifications;
		DataContext = main;
		this.getSettingsWindow = getSettingsWindow;
		InitializeComponent();
		WindowBackdropType = ThemeViewModel.Backdrop;
		requiresCloak = !Wpf.Ui.Controls.WindowBackdrop.IsSupported(ThemeViewModel.Backdrop);

		//size before the HWND exists
		ApplyMode();

		//force create HWND of the window
		//since the window is hidden at the startup
		//and HWND of the window is not going to be created until the window is shown at least once
		Hwnd = ForceCreateHwnd();

		trayWheel = new TrayWheelInput(Hwnd);

		if(requiresCloak)
			SetCloaked(true);

		Wpf.Ui.Appearance.ApplicationThemeManager.Changed += (_, _) =>
		    Dispatcher.BeginInvoke(new Action(UpdateFallbackCompositionBackground));

		UpdateFallbackCompositionBackground();

		//the hidden window goes to its remembered place, so a window shown at startup appears there right away (placed again when it shows)
		if(!vm.DisplayMinimalistic && vm.WindowWidth > 0 && TryGetRememberedPosition(vm.WindowLeft, vm.WindowTop, out var position, out _))
			MoveTo(position.X, position.Y, show: false);

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
		DpiChanged += OnDpiChanged;

		SetUpTrayIcon();
		SetBindings();

		//WM_HOTKEY goes to the blocking hotkey manager; getting the window handle sets up the hotkey manager,
		//and the hotkey features then register their saved hotkeys
		messages.HotkeyMessageHandler = hotkeyMode.GetHotkeyMessageHandler();
		hotkeyMode.SetWindowHandle(Hwnd);

		Tray.TrayMouseMove += OnTrayMouseMove;

		Closed += OnClosed;
	}

	/// <summary>
	/// Explorer sends this while the cursor is over the tray icon. The first move reads the icon's rectangle and
	/// starts reading the mouse wheel through raw input, which stops when the cursor leaves the icon
	/// </summary>
	private void OnTrayMouseMove(object sender, RoutedEventArgs e)
	{
		if(trayWheel.IsActive || !trayWheel.CanStart)
			return;
		if(NativeMethods.TryGetTrayIconRect(Tray.TrayIcon.Id, out RECT rect))
			trayWheel.Start(rect);
	}

	/// <summary>
	/// Changes the default output device volume by the hotkeys' step for each wheel notch over the tray icon
	/// </summary>
	/// <param name="steps">Positive for wheel-up, negative for wheel-down</param>
	private void ChangeOutputVolumeFromTrayWheel(int steps)
	{
		//while the mixer is open, the wheel changes the device it shows, even if it isn't the default one yet
		var device = IsVisible ? devices.SelectedDevice ?? devices.DefaultDevice : devices.DefaultDevice;
		var master = device?.Master;
		if(master == null)
			return;

		for(int i = 0; i < Math.Abs(steps); i++)
			master.ChangeVolumeByStep(up: steps > 0, notify: false);

		//the open mixer window shows the new volume itself; otherwise the tray
		//tooltip that is already shown isn't repainted by Explorer, so the OSD shows it (when OSD notifications are on).
		if(!IsVisible)
			notifications.NotifyAudioStateChange(master);
	}

	/// <summary>Stops reading the mouse wheel and unsubscribes from the tray icon when the window closes</summary>
	private void OnClosed(object? sender, EventArgs e)
	{
		trayWheel.Stop();
		Tray.TrayMouseMove -= OnTrayMouseMove;
		Closed -= OnClosed;
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
		//The tooltip is the native text tooltip on every Windows version (a WPF tooltip popup gets stuck on Windows 10)
		BindingOperations.SetBinding(this, TrayIconImageProperty, (MultiBinding)TryFindResource("TrayIconBinding"));
		BindingOperations.SetBinding(this, TrayToolTipTextProperty, (MultiBinding)TryFindResource("TrayTextTooltip"));

		Tray.PreviewTrayContextMenuOpen += OnTrayMenuOpening;
		CreateTrayIcon();
	}

	/// <summary>
	/// Opens the tray menu at the cursor. TaskbarIcon would convert the cursor's pixels with one DPI for all monitors,
	/// which puts the menu on another monitor and at the wrong scale when the monitors' scales differ;
	/// WPF's mouse placement takes the cursor's pixels as they are
	/// </summary>
	private void OnTrayMenuOpening(object sender, RoutedEventArgs e)
	{
		e.Handled = true;
		if(Tray.ContextMenu is not { } menu)
			return;
		menu.Placement = PlacementMode.Mouse;
		menu.IsOpen = true;
		//the menu has to be the foreground window, or a click elsewhere doesn't close it
		if(PresentationSource.FromVisual(menu) is HwndSource source)
			PInvoke.SetForegroundWindow((HWND)source.Handle);
	}

	/// <summary>Delays of the attempts to create the tray icon again after Explorer refused it, then every minute</summary>
	private static readonly TimeSpan[] TrayRetryDelays =
		{ TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1) };

	private DispatcherTimer? trayRetryTimer;
	private int trayRetry;
	/// <summary>Failed tray calls since the icon last worked; only the first of an outage is logged</summary>
	private int trayFailures;
	private System.Drawing.Icon? trayIconImage;

	/// <summary>
	/// Creates the tray icon (again) with the current icon and tooltip. Explorer can refuse it, e.g. while it starts at sign-in or is busy;
	/// it only tells about its own restarts (H.NotifyIcon creates the icon again then), so a refused icon is retried here.
	/// </summary>
	private void CreateTrayIcon()
	{
		try
		{
			var tray = Tray.TrayIcon;
			//a create that failed halfway may have added the icon; Create deletes a leftover first
			_ = tray.TryRemove();
			//not created: these only keep the values that Create uses
			Tray.UpdateIcon(trayIconImage);
			tray.UpdateToolTip(trayToolTip ?? string.Empty);
			//TaskbarIcon creates itself on Loaded, but the window starts hidden, so it may never load.
			//Efficiency mode stays off: it would throttle hotkeys and audio callbacks
			Tray.ForceCreate(enablesEfficiencyMode: false);
		}
		catch(Exception e) when(e is InvalidOperationException or ObjectDisposedException or Win32Exception)
		{
			OnTrayFailed("create", e);
			return;
		}
		if(trayFailures > 0)
			Logger.Info($"Created the tray icon again after {trayFailures} failed tray calls");
		trayFailures = 0;
		trayRetry = 0;
	}

	/// <summary>A tray call failed: Explorer refused the icon or no longer has it. The icon is created again with growing delays</summary>
	private void OnTrayFailed(string action, Exception e)
	{
		if(trayFailures++ == 0)
			Logger.Warn($"Failed to {action} the tray icon, creating it again", e);
		if(trayRetryTimer is { IsEnabled: true })
			return;
		trayRetryTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
		trayRetryTimer.Interval = TrayRetryDelays[Math.Min(trayRetry++, TrayRetryDelays.Length - 1)];
		trayRetryTimer.Tick -= OnTrayRetryTick;
		trayRetryTimer.Tick += OnTrayRetryTick;
		trayRetryTimer.Start();
	}

	private void OnTrayRetryTick(object? sender, EventArgs e)
	{
		trayRetryTimer?.Stop();
		CreateTrayIcon();
	}

	private static void OnTrayIconImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var view = (MainView)d;
		view.trayIconImage = e.NewValue as System.Drawing.Icon;
		view.UpdateTrayIcon(tray =>
		{
			if(!tray.UpdateIcon(view.trayIconImage))
				throw new InvalidOperationException("Shell_NotifyIcon failed");
		}, "update the icon of");
	}

	private static void OnTrayToolTipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((MainView)d).QueueTrayToolTip(e.NewValue as string ?? string.Empty);

	private static readonly TimeSpan TrayToolTipInterval = TimeSpan.FromMilliseconds(100);
	private DispatcherTimer? trayToolTipTimer;
	private string? pendingTrayToolTip;

	/// <summary>
	/// Each tooltip update is a call into Explorer, and a held volume hotkey changes the text about 30 times a second:
	/// the first change goes out at once, the changes within the next interval go out together after it
	/// </summary>
	private void QueueTrayToolTip(string text)
	{
		pendingTrayToolTip = text;
		if(trayToolTipTimer is { IsEnabled: true })
			return;
		PushTrayToolTip();
		if(trayToolTipTimer == null)
		{
			trayToolTipTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TrayToolTipInterval };
			trayToolTipTimer.Tick += OnTrayToolTipTimerTick;
		}
		trayToolTipTimer.Start();
	}

	private void OnTrayToolTipTimerTick(object? sender, EventArgs e)
	{
		if(pendingTrayToolTip == null)
			trayToolTipTimer?.Stop();
		else
			PushTrayToolTip();
	}

	private void PushTrayToolTip()
	{
		if(pendingTrayToolTip is not { } text)
			return;
		pendingTrayToolTip = null;
		trayToolTip = text;
		UpdateTrayIcon(tray => tray.TrayIcon.UpdateToolTip(text), "update the tooltip of");
	}

	/// <summary>The tooltip last sent, for creating the icon again</summary>
	private string? trayToolTip;

	/// <summary>
	/// Changes the shell icon. While it's being created again only the new value is kept (CreateTrayIcon sends it);
	/// a failed change means Explorer no longer has the icon, so it's created again
	/// </summary>
	private void UpdateTrayIcon(Action<TaskbarIcon> update, string action)
	{
		if(trayFailures > 0)
			return;
		try
		{
			update(Tray);
		}
		catch(Exception e) when(e is InvalidOperationException or ObjectDisposedException or Win32Exception)
		{
			OnTrayFailed(action, e);
		}
	}

	private IntPtr ForceCreateHwnd() => Hwnd = new WindowInteropHelper(this).EnsureHandle();

	/// <summary>Frames WPF must have rendered after the window was shown before the window is uncloaked</summary>
	private const int CloakFrames = 3;

	/// <summary>The cloak never lasts longer than this, in case no frames are rendered (e.g. the window ended up hidden)</summary>
	private static readonly TimeSpan MaxCloakDuration = TimeSpan.FromMilliseconds(400);

	private DispatcherTimer? uncloakTimer;
	private bool cloakFramesHooked;
	private int cloakedFrames;
	private TimeSpan lastCloakFrameTime;

	private DispatcherTimer? cloakOnHideTimer;

	private void StartUncloakSequence()
	{
		if(Hwnd == IntPtr.Zero)
			return;

		// Ensure the window is cloaked immediately if reopened while the fade-out timer is still running
		cloakOnHideTimer?.Stop();
		SetCloaked(true);

		if(FindResource("DisplayFadeIn") is Storyboard fadeIn)
		{
			// Take control of the storyboard to override the XAML trigger during the cloak.
			// The 'true' parameter makes it controllable (pause/resume).
			fadeIn.Begin(this, true);

			// Advance slightly to bypass visual tree culling (preventing the solid frame glitch)
			// and pause it so it doesn't animate while the window is still native-cloaked.
			fadeIn.Seek(this, TimeSpan.FromMilliseconds(1), TimeSeekOrigin.BeginTime);
			fadeIn.Pause(this);
		}

		cloakedFrames = 0;
		lastCloakFrameTime = TimeSpan.MinValue;
		if(!cloakFramesHooked)
		{
			CompositionTarget.Rendering += OnCloakedFrameRendering;
			cloakFramesHooked = true;
		}
		if(uncloakTimer == null)
		{
			var timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = MaxCloakDuration };
			timer.Tick += (s, a) => EndCloak();
			uncloakTimer = timer;
		}
		uncloakTimer.Stop();
		uncloakTimer.Start();
	}

	private void StopUncloakSequence()
	{
		uncloakTimer?.Stop();
		if(cloakFramesHooked)
		{
			CompositionTarget.Rendering -= OnCloakedFrameRendering;
			cloakFramesHooked = false;
		}
	}

	private void EndCloak()
	{
		StopUncloakSequence();
		SetCloaked(false);

		if(FindResource("DisplayFadeIn") is Storyboard fadeIn)
		{
			// Resume the smooth fade-in now that the native cloak is dropped
			fadeIn.Resume(this);
		}
	}

	private void OnCloakedFrameRendering(object? sender, EventArgs e)
	{
		//Rendering can be raised more than once for the same frame
		if(e is not RenderingEventArgs args || args.RenderingTime == lastCloakFrameTime)
			return;
		lastCloakFrameTime = args.RenderingTime;
		if(++cloakedFrames >= CloakFrames)
			EndCloak();
	}

	private unsafe void SetCloaked(bool cloaked)
	{
		if(Hwnd == IntPtr.Zero) return;

		int value = cloaked ? 1 : 0;
		_ = PInvoke.DwmSetWindowAttribute((HWND)Hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, sizeof(int));
	}

	/// <summary>
	/// Sets the composition target's background to the window's current background
	/// when the configured backdrop is not supported by the operating system.
	/// This prevents the unpainted client area from flashing white before WPF renders.
	/// </summary>
	private void UpdateFallbackCompositionBackground()
	{
		if(Wpf.Ui.Controls.WindowBackdrop.IsSupported(ThemeViewModel.Backdrop))
			return;

		if(HwndSource.FromHwnd(Hwnd)?.CompositionTarget is { } compositionTarget
		   && Application.Current.TryFindResource("ApplicationBackgroundColor") is Color backgroundColor)
		{
			compositionTarget.BackgroundColor = backgroundColor;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
	/// The popup has a fixed width and can't be resized; the window mode is resizable and gets its saved width
	/// (the height fits the apps when it opens, see <see cref="FitHeight(double)"/>)
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
		{
			placed = false;
			if(requiresCloak)
			{
				StopUncloakSequence();

				if(cloakOnHideTimer == null)
				{
					TimeSpan fadeOutDuration;
					if(FindResource("HideFadeOut") is Storyboard hideStoryboard
						&& hideStoryboard.Children.Count > 0
						&& hideStoryboard.Children[0].Duration.HasTimeSpan)
					{
						fadeOutDuration = hideStoryboard.Children[0].Duration.TimeSpan;
					}
					else
					{
						fadeOutDuration = TimeSpan.FromMilliseconds(120);
					}

					var timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = fadeOutDuration };
					timer.Tick += (s, a) =>
					{
						cloakOnHideTimer?.Stop();
						if(!IsVisible)
							SetCloaked(true);
					};
					cloakOnHideTimer = timer;
				}

				cloakOnHideTimer.Stop();
				cloakOnHideTimer.Start();
			}
		}
		else
		{
			if(requiresCloak)
			{
				// Windows 10: Manually handle the uncloak and storyboard pause/resume
				StartUncloakSequence();
			}

			// Windows 11 (!requiresCloak) relies entirely on the XAML DataTrigger. No code needed here.
			if(IsLoaded)
			{
				PlaceWindow();
				Activate();
				Focus();
			}
		}
	}

	private void PlaceWindow()
	{
		if(vm.DisplayMinimalistic)
		{
			if(TryGetRememberedPosition(vm.PopupLeft, vm.PopupTop, out var position, out double workAreaHeight))
			{
				FitHeight(workAreaHeight);
				UpdateLayout();
				MoveToRemembered(position);
			}
			else
			{
				FitHeight();
				//update layout before setting windows position to use actual height of the window when calculating its position
				UpdateLayout();
				SetWindowPosition();
			}
		}
		else
			RestoreWindowPlacement();
		placed = true;
	}

	/// <summary>
	/// A remembered position (both modes), while "Remember the mixer's position" is on and the position is on a connected screen,
	/// and the height of that screen's work area in DIPs.
	/// Positions are in physical pixels: WPF converts Left/Top with the scale of the screen the window is on when they're set,
	/// and the hidden window is often on another screen than the one it opens on (on mixed scales it would open hundreds of pixels off),
	/// and they stay right when a screen's scale changes between starts
	/// </summary>
	private bool TryGetRememberedPosition(double? left, double? top, out System.Drawing.Point position, out double workAreaHeight)
	{
		position = default;
		workAreaHeight = 0;
		if(!vm.RememberPosition || left is not { } x || top is not { } y)
			return false;
		position = new System.Drawing.Point((int)Math.Round(x), (int)Math.Round(y));
		var monitor = PInvoke.MonitorFromPoint(position, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
		var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
		if(monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info)
		   || PInvoke.GetDpiForMonitor(monitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpi, out _).Failed)
			return false;
		workAreaHeight = (info.rcWork.bottom - info.rcWork.top) * 96.0 / dpi;
		return true;
	}

	/// <summary>Where the window is being placed (physical pixels), until the move is done</summary>
	private System.Drawing.Point? placementTarget;

	private void MoveToRemembered(System.Drawing.Point position)
	{
		//moving onto a screen with another scale makes WPF resize the window to the new scale (DpiChanged), which can shift it: placed again then;
		//once it's done, a window that sticks out of its screen (e.g. it grew with the screen's scale) is moved back in
		placementTarget = position;
		MoveTo(position.X, position.Y);
		Dispatcher.InvokeAsync(() =>
		{
			placementTarget = null;
			KeepOnScreen();
		}, System.Windows.Threading.DispatcherPriority.ContextIdle);
	}

	private void OnDpiChanged(object sender, DpiChangedEventArgs e)
	{
		if(placementTarget is { } target)
			Dispatcher.InvokeAsync(() => MoveTo(target.X, target.Y));
	}

	/// <summary>Moves the visible window back inside the work area of the screen it's (mostly) on</summary>
	private void KeepOnScreen()
	{
		if(!IsVisible || WindowState != WindowState.Normal || !PInvoke.GetWindowRect((HWND)Hwnd, out RECT rect))
			return;
		var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
		if(!PInvoke.GetMonitorInfo(PInvoke.MonitorFromWindow((HWND)Hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST), ref info))
			return;
		var window = System.Drawing.Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
		var work = System.Drawing.Rectangle.FromLTRB(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom);
		var inside = WindowPlacement.KeepInside(window, work);
		if(inside != window.Location)
			MoveTo(inside.X, inside.Y);
	}

	/// <summary>The popup's position in physical pixels (see <see cref="TryGetRememberedPosition"/>)</summary>
	private void RememberPopupPosition()
	{
		if(PInvoke.GetWindowRect((HWND)Hwnd, out RECT rect))
		{
			vm.PopupLeft = rect.left;
			vm.PopupTop = rect.top;
		}
	}

	/// <summary>
	/// The mixer is dragged by any spot that isn't a control, in both modes, whether its position is remembered or not.
	/// The new position is always noted (window mode: LocationChanged; popup: below) but used for the next opening only while it's remembered.
	/// On the preview: the apps list's ScrollViewer handles the press itself, so the bubbling event never reaches the window there.
	/// A press in a popup (the device combo box's list) reaches the window too, through the popup's owner, but never drags it.
	/// </summary>
	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		if(e.Handled || e.ButtonState != MouseButtonState.Pressed || !IsOnThisWindow(e.OriginalSource as DependencyObject)
		   || IsInControl(e.OriginalSource as DependencyObject))
			return;
		e.Handled = true;
		DragMove();
		if(vm.DisplayMinimalistic)
			RememberPopupPosition();
	}

	/// <summary>
	/// Whether the pressed element is drawn in this window, not in a popup: a popup has its own window and visual tree,
	/// so <see cref="IsInControl"/> can't find the control that opened it
	/// </summary>
	private bool IsOnThisWindow(DependencyObject? source)
		=> source != null && PresentationSource.FromDependencyObject(source)?.RootVisual == this;

	/// <summary>Whether the pressed element is (inside) something that takes clicks itself</summary>
	private static bool IsInControl(DependencyObject? source)
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
	/// The mixer's height in both modes: its fixed parts (and the window mode's title bar) plus the apps list (at least <see cref="MinRows"/> rows),
	/// at most <see cref="MaxShareOfWorkArea"/> of the screen's work area. Computed when it opens only;
	/// a height the user drags the window to lasts until it closes
	/// </summary>
	private void FitHeight() => FitHeight((SelectedScreen.WorkingAreaBottom - SelectedScreen.WorkingAreaTop) / SelectedScreen.ScaleFactor);

	/// <param name="workAreaHeight">The height of the work area of the screen it opens on, in DIPs</param>
	private void FitHeight(double workAreaHeight)
	{
		var (chrome, apps) = Mixer.MeasureHeights(Width);
		if(TitleBar.Visibility == Visibility.Visible)
		{
			TitleBar.Measure(new Size(Width, double.PositiveInfinity));
			chrome += TitleBar.DesiredSize.Height;
		}
		double max = Math.Max(MinHeight, workAreaHeight * MaxShareOfWorkArea);
		applyingPlacement = true;
		Height = Math.Clamp(chrome + Math.Max(apps, MinRows * RowHeight), MinHeight, max);
		applyingPlacement = false;
	}

	/// <summary>
	/// Window mode: its saved width and a height that fits the apps; the saved position while it's remembered and still on a screen,
	/// otherwise next to the tray like the popup
	/// </summary>
	private void RestoreWindowPlacement()
	{
		ApplyMode();
		if(vm.WindowWidth > 0 && TryGetRememberedPosition(vm.WindowLeft, vm.WindowTop, out var position, out double workAreaHeight))
		{
			FitHeight(workAreaHeight);
			UpdateLayout();
			MoveToRemembered(position);
		}
		else
		{
			FitHeight();
			UpdateLayout();
			SetWindowPosition();
		}
	}

	/// <summary>
	/// Remembers the window mode's width (DIPs, so it scales with the screen) and position (physical pixels, see <see cref="TryGetRememberedPosition"/>),
	/// saved to the settings on exit
	/// </summary>
	private void SaveWindowPlacement()
	{
		if(applyingPlacement || !placed || vm.DisplayMinimalistic || !IsVisible || WindowState != WindowState.Normal)
			return;
		if(PInvoke.GetWindowRect((HWND)Hwnd, out RECT rect))
		{
			vm.WindowLeft = rect.left;
			vm.WindowTop = rect.top;
		}
		vm.WindowWidth = ActualWidth;
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

	/// <param name="show">false: a hidden window stays hidden (placed before it's shown)</param>
	private void MoveTo(double left, double top, bool show = true)
	{
		//move only: WPF sizes the window
		var flags = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
		if(show)
			flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
		else
			flags |= SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
		PInvoke.SetWindowPos((HWND)Hwnd, default /*HWND_TOP*/, (int)left, (int)top, 0, 0, flags);
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
		settingsWindow ??= getSettingsWindow();
		if(!settingsWindow.IsVisible)
			CenterOnMixerScreen(settingsWindow);
		settingsWindow.Show();
		if(settingsWindow.WindowState == WindowState.Minimized)
			settingsWindow.WindowState = WindowState.Normal;
		settingsWindow.Activate();
	}

	/// <summary>
	/// The settings open centered on the screen the mixer is on, also when the mixer is hidden:
	/// its window keeps its last position. A window larger than that screen's work area is shrunk to it (the pages scroll).
	/// If the screen can't be read, the window's own startup location is used
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
		double workWidth = (work.right - work.left) / targetScale;
		double workHeight = (work.bottom - work.top) / targetScale;
		if(window.MinWidth > workWidth)
			window.MinWidth = workWidth;
		window.Width = Math.Min(window.Width, workWidth);
		window.Height = Math.Min(window.Height, workHeight);
		double currentScale = VisualTreeHelper.GetDpi(window).DpiScaleX;
		double width = window.Width * targetScale;
		double height = window.Height * targetScale;
		window.WindowStartupLocation = WindowStartupLocation.Manual;
		window.Left = (work.left + Math.Max(0, (work.right - work.left - width) / 2)) / currentScale;
		window.Top = (work.top + Math.Max(0, (work.bottom - work.top - height) / 2)) / currentScale;
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if(msg == (int)PInvoke.WM_INPUT)
		{
			int steps = trayWheel.ReadSteps(lParam);
			if(steps != 0)
				ChangeOutputVolumeFromTrayWheel(steps);
			//not marked as handled: Windows needs its default processing of raw input
			return IntPtr.Zero;
		}
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
			view.FitHeight();
			view.UpdateLayout();
			view.SetWindowPosition();
		}
	}
}

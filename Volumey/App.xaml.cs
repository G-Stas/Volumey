using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using Volumey.Helper;
using Volumey.Services;
using Volumey.View;
using Volumey.ViewModel;

namespace Volumey;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public sealed partial class App
{
	private ILog logger;
	private ILog Logger => logger ??= LogManager.GetLogger(typeof(App));
	private static Stopwatch execTimer;

	public App()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		//settings are saved shortly after a change; write a pending change before the process ends (also on logoff)
		Exit += (s, e) => DataProvider.SettingsProvider.Settings.Flush();
		//sleep and wake drop and bring back audio devices, so mark them in the log
		Microsoft.Win32.SystemEvents.PowerModeChanged += (s, e) => Logger.Info($"Power mode changed: {e.Mode}");
	}

	private ServiceProvider services;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		services = new ServiceCollection().AddVolumey(this).BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

		//the theme and accent before any window is created
		services.GetRequiredService<ThemeViewModel>();
		services.GetRequiredService<AppBehaviorViewModel>();
		//creating it enumerates the audio devices
		services.GetRequiredService<DeviceProviderViewModel>();
		services.GetRequiredService<HotkeyModeViewModel>();
		foreach(var feature in ServiceRegistration.Features)
			services.GetRequiredService(feature);

		//the mixer window and the tray icon; the window shows itself unless the app starts minimized
		MainWindow = services.GetRequiredService<MainView>();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		//raises Exit: its handlers release the devices and write pending settings
		base.OnExit(e);
		services?.Dispose();
		//last, so errors of those are still logged (Release builds send their last logs to Loggly here)
		LogManager.Shutdown();
	}

	private readonly WindowUpdateFailureGuard windowUpdateFailures = new WindowUpdateFailureGuard(() => DateTime.UtcNow);
	private readonly WindowUpdateFailureGuard compositionChangeFailures = new WindowUpdateFailureGuard(() => DateTime.UtcNow);
	private readonly WindowUpdateFailureGuard popupResizeFailures = new WindowUpdateFailureGuard(() => DateTime.UtcNow);
	private readonly WindowUpdateFailureGuard tabletRefreshFailures = new WindowUpdateFailureGuard(() => DateTime.UtcNow);

	/// <summary>
	/// Keeps the app alive on known WPF failures. Only the failed update is skipped;
	/// <see cref="WindowUpdateFailureGuard"/> limits the logging and refreshes the windows afterwards.
	/// <list type="bullet">
	/// <item>Updating a window's render target can fail with "Not enough quota is available to process this command"
	/// (Win32 error 1816), e.g. under a fullscreen game or while the window grows with many apps.</item>
	/// <item>WindowChrome (the windows' own title bar and backdrop) extends the window frame again on WM_DWMCOMPOSITIONCHANGED,
	/// which fails while DWM is briefly unavailable (graphics driver reset, Remote Desktop, sleep and wake): usually "Desktop composition
	/// is disabled" (0x80263001), also other codes (e.g. 0xD0000701), so any COMException of that call.
	/// DWM sends the message again once it's back, and the frame is extended then. When a window gets its chrome
	/// (<c>_ApplyNewCustomChrome</c>, at start), WPF-UI's <c>FluentWindow.SetWindowChrome</c> already ignores it.</item>
	/// <item>A popup (tray menu, combo box drop-down, tooltip) whose window is resized before WPF positioned it: WPF's
	/// <c>Popup.OnWindowResize</c> throws a NullReferenceException on purpose (still in .NET 10).
	/// Skipped, the popup is positioned again the next time it opens.</item>
	/// <item>WPF's pen and touch code (<c>WispLogic.RefreshTablets</c>) changes its tablet list while enumerating it when a pen or
	/// touch device is added ("Collection was modified"). Skipped, a new tablet may not work in Volumey until a restart.</item>
	/// </list>
	/// </summary>
	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		WindowUpdateFailureGuard guard;
		string failure;
		if(IsWindowUpdateQuotaFailure(e.Exception))
			(guard, failure) = (windowUpdateFailures, "failed window update(s) (not enough quota)");
		else if(IsCompositionChangeFailure(e.Exception))
			(guard, failure) = (compositionChangeFailures, "failed window frame update(s) (DWM unavailable)");
		else if(IsPopupResizeFailure(e.Exception))
			(guard, failure) = (popupResizeFailures, "failed popup resize(s)");
		else if(IsTabletRefreshFailure(e.Exception))
			(guard, failure) = (tabletRefreshFailures, "failed pen and touch device refresh(es)");
		else
			return;
		e.Handled = true;
		int count = guard.CountFailure();
		if(count > 0)
			Logger.Warn($"Ignored {count} {failure} since the last report", e.Exception);
		guard.ScheduleRefresh(Dispatcher);
	}

	internal static bool IsWindowUpdateQuotaFailure(Exception exception)
		=> exception is Win32Exception { NativeErrorCode: 1816 } && exception.StackTrace?.Contains("System.Windows.Interop.HwndTarget") == true;

	internal static bool IsCompositionChangeFailure(Exception exception)
		=> exception is COMException && exception.StackTrace?.Contains("System.Windows.Shell.WindowChromeWorker._ExtendGlassFrame") == true;

	/// <summary>Thrown by the popup itself (its first frame), not by app code that runs during the layout</summary>
	internal static bool IsPopupResizeFailure(Exception exception)
		=> exception is NullReferenceException
		   && exception.StackTrace?.TrimStart().StartsWith("at System.Windows.Controls.Primitives.Popup.OnWindowResize(", StringComparison.Ordinal) == true;

	internal static bool IsTabletRefreshFailure(Exception exception)
		=> exception is InvalidOperationException && exception.StackTrace?.Contains("System.Windows.Input.StylusWisp.WispLogic.RefreshTablets") == true;

	internal static void InitializeExecutionTimer()
	{
		execTimer = new Stopwatch();
		execTimer.Start();
	}

	/// <summary>
	/// Logs an exception that ends the app and flushes the log (Release builds send logs to Loggly in batches,
	/// which would be lost when the process goes down). The caller decides how the process ends.
	/// </summary>
	internal static void LogFatal(string source, Exception ex)
	{
		try
		{
			bool isStoreVersion;
#if(STORE)
                isStoreVersion = true;
#else
			isStoreVersion = false;
#endif

			var ts = execTimer?.Elapsed ?? TimeSpan.Zero;
			StringBuilder data = new StringBuilder($"Source: [{source}]\n");
			data.Append($"OS Version: {Environment.OSVersion}\n");
			data.Append($"Is 64 bit OS: {Environment.Is64BitOperatingSystem.ToString()}\n");
			data.Append($"CPU cores count: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}\n");
			data.Append($"Execution time: {ts.Days * 24 + ts.Hours} hrs. {ts.Minutes:00} mins. {ts.Seconds:00} secs.\n");
			data.Append($"Is store version: {isStoreVersion}");
			var log = LogManager.GetLogger(typeof(App));
			log.Fatal($"An unhandled exception has occurred: \n{data}", ex);
			log.Logger.Repository.Shutdown();
		}
		catch
		{
			//nothing else can be done while the app is going down
		}
	}
}

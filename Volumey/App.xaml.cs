using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Threading;
using log4net;
using Volumey.Helper;

namespace Volumey;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
	private ILog logger;
	private ILog Logger => logger ??= LogManager.GetLogger(typeof(App));
	private static Stopwatch execTimer;

	public App()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		//settings are saved shortly after a change; write a pending change before the process ends (also on logoff)
		Exit += (s, e) => DataProvider.SettingsProvider.Settings.Flush();
		//sleep and wake drop and bring back audio devices (#57), so mark them in the log
		Microsoft.Win32.SystemEvents.PowerModeChanged += (s, e) => Logger.Info($"Power mode changed: {e.Mode}");
	}

	protected override void OnExit(System.Windows.ExitEventArgs e)
	{
		//raises Exit: its handlers release the devices and write pending settings
		base.OnExit(e);
		//last, so errors of those are still logged (Release builds send their last logs to Loggly here)
		LogManager.Shutdown();
	}

	private readonly WindowUpdateFailureGuard windowUpdateFailures = new WindowUpdateFailureGuard(() => DateTime.UtcNow);

	/// <summary>
	/// Keeps the app alive on a known WPF failure: updating a window's render target can fail with
	/// "Not enough quota is available to process this command" (Win32 error 1816), e.g. under a fullscreen game
	/// or while the window grows with many apps (GitHub issues #58, #66). Only that one update is skipped;
	/// <see cref="WindowUpdateFailureGuard"/> limits the logging and refreshes the windows afterwards.
	/// </summary>
	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		if(!IsWindowUpdateQuotaFailure(e.Exception))
			return;
		e.Handled = true;
		int count = windowUpdateFailures.CountFailure();
		if(count > 0)
			Logger.Warn($"Ignored {count} failed window update(s) (not enough quota) since the last report", e.Exception);
		windowUpdateFailures.ScheduleRefresh(Dispatcher);
	}

	internal static bool IsWindowUpdateQuotaFailure(Exception exception)
		=> exception is Win32Exception { NativeErrorCode: 1816 } && exception.StackTrace?.Contains("System.Windows.Interop.HwndTarget") == true;

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

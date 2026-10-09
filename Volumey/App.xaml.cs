using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Threading;
using log4net;
using Volumey.Helper;
                             
namespace Volumey
{
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
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
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
            int count = this.windowUpdateFailures.CountFailure();
            if(count > 0)
                Logger.Warn($"Ignored {count} failed window update(s) (not enough quota) since the last report", e.Exception);
            this.windowUpdateFailures.ScheduleRefresh(this.Dispatcher);
        }

        internal static bool IsWindowUpdateQuotaFailure(Exception exception)
            => exception is Win32Exception { NativeErrorCode: 1816 } && exception.StackTrace?.Contains("System.Windows.Interop.HwndTarget") == true;

        internal static void InitializeExecutionTimer()
        {
            execTimer = new Stopwatch();
            execTimer.Start();
        }
        
        internal void LogFatalException(string exceptionType, Exception ex)
        {
            bool isStoreVersion;
            
            #if(STORE)
            isStoreVersion = true;
            #else
            isStoreVersion = false;
            #endif
            
            execTimer.Stop();
            var ts = execTimer.Elapsed;
            StringBuilder data = new StringBuilder($"Exception type: [{exceptionType}]\n");
            data.Append($"OS Version: {Environment.OSVersion}\n");
            data.Append($"Is 64 bit OS: {Environment.Is64BitOperatingSystem.ToString()}\n");
            data.Append($"CPU cores count: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}\n");
            data.Append($"Execution time: {ts.Days * 24 + ts.Hours} hrs. {ts.Minutes:00} mins. {ts.Seconds:00} secs.\n");
            data.Append($"Is store version: {isStoreVersion}");
            Logger.Fatal($"An unhandled exception has occurred: \n{data}", ex);
            Logger.Logger.Repository.Shutdown();
            Environment.Exit(0);
        }
    }
}
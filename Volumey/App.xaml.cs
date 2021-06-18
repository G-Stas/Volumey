using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using log4net;
                             
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

        internal static void InitializeExecutionTimer()
        {
            execTimer = new Stopwatch();
            execTimer.Start();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            #if (!DEBUG)
            AppDomain.CurrentDomain.UnhandledException += (s, exc) => LogFatalException("AppDomain.CurrentDomain.UnhandledException", exc?.ExceptionObject as Exception);
            App.Current.DispatcherUnhandledException += (s, exc) => LogFatalException("App.Current.DispatcherUnhandledException", exc?.Exception);
            TaskScheduler.UnobservedTaskException += (s, exc) => LogFatalException("TaskScheduler.UnobservedTaskException", exc?.Exception);
            #endif
        }

        private void LogFatalException(string exceptionType, Exception ex)
        {
            execTimer.Stop();
            var ts = execTimer.Elapsed;
            StringBuilder data = new StringBuilder($"Exception type: [{exceptionType}]\n");
            data.Append($"OS Version: {Environment.OSVersion}\n");
            data.Append($"Is 64 bit OS: {Environment.Is64BitOperatingSystem.ToString()}\n");
            data.Append($"CPU cores count: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}\n");
            data.Append($"Execution time: {ts.Days * 24 + ts.Hours} hrs. {ts.Minutes:00} mins. {ts.Seconds:00} secs.\n");
            Logger.Fatal($"An unhandled exception has occurred: \n{data}", ex);
            Logger.Logger.Repository.Shutdown();
            Environment.Exit(0);
        }
    }
}
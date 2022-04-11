using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
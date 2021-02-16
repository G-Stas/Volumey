using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel;
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

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			#if(!DEBUG)
			AppDomain.CurrentDomain.UnhandledException += (s, exc) => LogFatalException("AppDomain.CurrentDomain.UnhandledException", exc?.ExceptionObject as Exception);
			App.Current.DispatcherUnhandledException += (s, exc) => LogFatalException("App.Current.DispatcherUnhandledException", exc?.Exception);
			TaskScheduler.UnobservedTaskException += (s, exc) => LogFatalException("TaskScheduler.UnobservedTaskException", exc?.Exception);
			#endif
		}
		
		private void LogFatalException(string exceptionType, Exception ex)
		{
			var packageId = Package.Current?.Id;
			var ver = packageId?.Version ?? new PackageVersion();
			StringBuilder data = new StringBuilder($"Exception type: [{exceptionType}]\n");
			data.Append($"Exception data: {ex?.ToString()}\n");
			data.Append($"OS Version: {Environment.OSVersion}\n");
			data.Append($"Is 64 bit OS: {Environment.Is64BitOperatingSystem.ToString()}\n");
			data.Append($"CPU cores count: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}\n");
			data.Append($"App ver.: {ver.Major.ToString()}.{ver.Minor.ToString()}.{ver.Build.ToString()}.{ver.Revision.ToString()}\n");
			Logger.Fatal($"An unhandled exception has occurred: \n{data}", ex);
			Logger.Logger.Repository.Shutdown();
			Environment.Exit(0);
		}
	}
}
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using log4net;
using log4net.Config;
using Volumey.Helper;

namespace Volumey
{
	static class Startup
	{
		private static Mutex mutex = new Mutex(true, "2A777FD7-2EB6-4D0B-8523-87F23462918B");

		private const int HWND_BROADCAST = 0xffff;
		public static readonly int WM_SHOWME = NativeMethods.RegisterWindowMessage("WM_SHOWME");

		internal static string MinimizedArg { get; } = "-minimized";
		internal static string RestartArg { get; } = "-restart";
		internal static bool StartMinimized { get; private set; }

		[STAThread]
		private static void Main()
		{
			bool isRestarting = false;
			bool mutexAcquired = false;
			foreach(var arg in Environment.GetCommandLineArgs())
			{
				if(!isRestarting && arg.Equals(RestartArg))
				{
					isRestarting = true;
					try
					{
						//try acquiring a mutex with a bigger timeout if the app is restarting because the previous instance might take some time to close itself and release mutex
						mutexAcquired = mutex.WaitOne(TimeSpan.FromSeconds(5), true);
					}
					catch(AbandonedMutexException)
					{
						//AbandonedMutexException is thrown when one thread acquires a Mutex object that another thread has abandoned by exiting without releasing it.
						mutexAcquired = true;
					}
				}

				if(!StartMinimized && arg.Equals(MinimizedArg))
				{
					StartMinimized = true;
				}
			}

			if(isRestarting && mutexAcquired || mutex.WaitOne(TimeSpan.Zero, true))
			{
				App.InitializeExecutionTimer();
				InitializeLoggerConfig();

				#if(STORE)
				if(!StartMinimized)
				{
					try
					{
						var args = AppInstance.GetActivatedEventArgs();
						if(args != null)
						{
							//when the app was launched on system startup, Kind argument will be "StartupTask"
							//otherwise i.e. when the app was launched normal way it will be "Launch"
							if(args.Kind == ActivationKind.StartupTask)
								StartMinimized = true;
						}
					}
					catch { }
				}
				#endif

				#if(!DEBUG)
				try
				{
					App.Main();
				}
				catch(OutOfMemoryException e)
				{
					MessageBox.Show(e.Message, "Fatal exception", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				catch(Exception e)
				{
					(App.Current as App)?.LogFatalException("App.Main unhandled exception", e);
				}
				finally
				{
					mutex.ReleaseMutex();
				}
				#endif
				#if(DEBUG)
				App.Main();
				#endif
				
			}
			else
			{
				NativeMethods.PostMessage((IntPtr) HWND_BROADCAST, WM_SHOWME, IntPtr.Zero,
				                          IntPtr.Zero);
			}
		}
		
		#if(DEBUG)
		private const string PathToDebugLogConfig = @"";
		#endif

		private static void InitializeLoggerConfig()
		{
			#if(!DEBUG)
			try
			{
				var assembly = Assembly.GetExecutingAssembly();
				var assemblyPath = Path.GetDirectoryName(assembly.Location);
				var repo = LogManager.GetRepository(assembly);
				if(assemblyPath != null)
				{
					var file = new FileInfo(Path.Combine(assemblyPath, "log4net.config"));
					XmlConfigurator.Configure(repo, file);
					try
					{
						#if(STORE)
						PackageVersion ver = Package.Current?.Id?.Version ?? new PackageVersion();
						GlobalContext.Properties["AppVer"] =
							$"[ver.: {ver.Major.ToString()}.{ver.Minor.ToString()}.{ver.Build.ToString()}.{ver.Revision.ToString()}]";
						#elif(!DEBUG)
						GlobalContext.Properties["AppVer"] =
							$"[ver.: {assembly?.GetName()?.Version?.ToString()}]";
						#endif
						
					}
					catch { }
				}
			}
			catch { }
			#else
			if(!string.IsNullOrEmpty(PathToDebugLogConfig))
			{
				var file = new FileInfo(PathToDebugLogConfig);
				var repo = LogManager.GetRepository(Assembly.GetExecutingAssembly());
				XmlConfigurator.Configure(repo, file);
			}
			#endif
		}
	}
}
#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Volumey.Helper;
using Volumey.View;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Win32;

namespace Volumey;

static class Startup
{
#if(DEBUG)
	//a separate single-instance scope so a debug build can run next to an installed release build
	internal const string InstanceSuffix = "-dev";
#else
	internal const string InstanceSuffix = "";
#endif

	//not owned on creation: Main takes it with WaitOne, once (owning it here as well would take it twice, so it would be abandoned on every exit)
	private static readonly Mutex mutex = new Mutex(false, "2A777FD7-2EB6-4D0B-8523-87F23462918B" + InstanceSuffix);

	public static readonly int WM_SHOWME = (int)PInvoke.RegisterWindowMessage("WM_SHOWME" + InstanceSuffix);

	internal static string MinimizedArg { get; } = "-minimized";
	internal static string RestartArg { get; } = "-restart";
	internal static bool StartMinimized { get; private set; }

	[STAThread]
	private static void Main()
	{
		EnvironmentArgs.Apply(Environment.GetCommandLineArgs());
		bool isRestarting = false;
		bool mutexAcquired = false;
		foreach(var arg in Environment.GetCommandLineArgs())
		{
			if(!isRestarting && arg.Equals(RestartArg))
			{
				isRestarting = true;
				//a bigger timeout when restarting: the previous instance might take some time to close itself and release the mutex
				mutexAcquired = TryAcquireMutex(TimeSpan.FromSeconds(5));
			}

			if(!StartMinimized && arg.Equals(MinimizedArg))
			{
				StartMinimized = true;
			}
		}

		if(isRestarting && mutexAcquired || TryAcquireMutex(TimeSpan.Zero))
		{
			App.InitializeExecutionTimer();
			InitializeLoggerConfig();
			LogUnhandledExceptions();

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
			bool crashed = false;
			try
			{
				App.Main();
			}
			catch(OutOfMemoryException e)
			{
				crashed = true;
				MessageBox.Show(e.Message, "Fatal exception", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch(Exception e)
			{
				crashed = true;
				App.LogFatal("UI thread", e);
			}
			finally
			{
				mutex.ReleaseMutex();
			}
			//other threads (e.g. the keyboard hook) could keep the process alive; a non-zero code tells Windows it was a crash
			if(crashed)
				Environment.Exit(1);
#endif
#if(DEBUG)
			App.Main();
			mutex.ReleaseMutex();
#endif

		}
		else
		{
			WindowMessages.ShowRunningInstance(Win32.Instance, WM_SHOWME);
		}
	}

#if(DEBUG)
	private const string PathToDebugLogConfig = @"";
#endif

	private static bool TryAcquireMutex(TimeSpan timeout)
	{
		try
		{
			return mutex.WaitOne(timeout);
		}
		catch(AbandonedMutexException)
		{
			//the previous instance ended without releasing it (crashed or killed) while this one already had the mutex open;
			//this thread owns it now
			return true;
		}
	}

	/// <summary>
	/// Logs crashes on every thread in every build. App.Main's try/catch only sees the UI thread, so an exception on an audio (COM),
	/// thread-pool or keyboard hook thread would otherwise end a Release build without a trace.
	/// </summary>
	private static void LogUnhandledExceptions()
	{
		//the process ends after this handler, the runtime sets the exit code
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			App.LogFatal(e.IsTerminating ? "background thread" : "background thread (not terminating)", e.ExceptionObject as Exception);
		//exceptions of tasks nobody awaited (fire-and-forget Task.Run): they don't end the app, but would otherwise vanish
		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			LogManager.GetLogger(typeof(Startup)).Error("Unobserved task exception", e.Exception);
			e.SetObserved();
		};
	}

	private static void InitializeLoggerConfig()
	{
#if(!DEBUG)
		try
		{
			//a build without the Loggly key (e.g. CI) doesn't log
			if(RemoteLogging.BuildInputKey is not string inputKey)
				return;
			var assembly = Assembly.GetExecutingAssembly();
			var repo = LogManager.GetRepository(assembly);
			BasicConfigurator.Configure(repo, RemoteLogging.CreatePipeline(RemoteLogging.CreateAppender(inputKey)));
			//info and above: warnings go out, info only as the context of a crash (devices, sleep and wake); debug stays local
			((Hierarchy)repo).Root.Level = Level.Info;
			try
			{
#if(STORE)
				PackageVersion ver = Package.Current?.Id?.Version ?? new PackageVersion();
				GlobalContext.Properties["AppVer"] =
					$"[ver.: {ver.Major.ToString()}.{ver.Minor.ToString()}.{ver.Build.ToString()}.{ver.Revision.ToString()}]";
#else
				GlobalContext.Properties["AppVer"] = $"[ver.: {assembly.GetName().Version}]";
#endif
			}
			catch { }
		}
		catch { }
#else
		if(!string.IsNullOrEmpty(PathToDebugLogConfig))
		{
			var file = new FileInfo(PathToDebugLogConfig);
			var repo = LogManager.GetRepository(Assembly.GetExecutingAssembly());
			XmlConfigurator.Configure(repo, file);
		}
		else if(Environment.GetEnvironmentVariable(EnvironmentArgs.LogFileVariable) is string logFile && logFile.Length > 0)
		{
			var appender = new FileAppender
			{
				File = logFile,
				AppendToFile = true,
				Layout = new PatternLayout("%date{HH:mm:ss.fff} [%thread] %-5level %logger - %message%newline%exception")
			};
			appender.ActivateOptions();
			BasicConfigurator.Configure(LogManager.GetRepository(Assembly.GetExecutingAssembly()), appender);
			LogManager.GetLogger(typeof(Startup)).Info($"Debug build started, pid {Environment.ProcessId}");
		}
		BindingErrorListener.Attach();
#endif
	}
}

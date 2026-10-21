#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Input;
using log4net;
using Microsoft.Win32;
#if(!STORE)
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.UI.Shell;
#endif

namespace Volumey.Helper;

internal static class SystemIntegrationHelper
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(SystemIntegrationHelper));
#if(!STORE)

	private static readonly string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
	//a debug build has its own names, so a dev run never re-points the autostart entry or the shortcut of the installed app
	private static readonly string StartupRegistryKeyName = "Volumey" + Startup.InstanceSuffix;
	private static readonly string StartMenuLinkName = "Volumey" + Startup.InstanceSuffix + ".lnk";
	private static readonly string? StartupRegistryValue;
	private static readonly string? ExePath = Environment.ProcessPath;

	static SystemIntegrationHelper()
	{
		if(string.IsNullOrEmpty(ExePath))
		{
			Logger.Error("Failed to retrieve .exe path.");
			return;
		}
		StartupRegistryValue = StartupCommandLine(ExePath, EnvironmentArgs.FromEnvironment());
	}

	/// <summary>
	/// The autostart entry's command line. The path is quoted: an unquoted path with spaces is ambiguous to Windows
	/// (it may run another program or nothing)
	/// </summary>
	internal static string StartupCommandLine(string exePath, string environmentArgs)
		=> $"\"{exePath}\" {Startup.MinimizedArg}{environmentArgs}";

	/// <summary>
	/// The autostart entry pointed at <paramref name="exePath"/>, or null when it already starts it minimized. The entry keeps its own
	/// folder overrides rather than taking the running instance's, so a test run with other folders doesn't take over the entry
	/// of the dev run that created it. Older unquoted entries are rewritten.
	/// </summary>
	internal static string? RepointStartupCommandLine(string commandLine, string exePath)
	{
		var current = StartupCommandLine(exePath, "");
		if(commandLine.StartsWith(current, StringComparison.OrdinalIgnoreCase)
		   && (commandLine.Length == current.Length || commandLine[current.Length] == ' '))
			return null;
		return StartupCommandLine(exePath, EnvironmentArgs.FindIn(commandLine));
	}

	private static readonly string StartMenuProgramsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

	internal static bool EnableLaunchAtStartup()
	{
		try
		{
			using(var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
			//created if missing: a fresh user profile has no Run key yet, and opening it then would do nothing while reporting success
			using(var key = baseKey.CreateSubKey(StartupRegistryPath, true))
			{
				if(CheckIfStartupRegistryKeyExists())
					DisableLaunchAtStartup();

				if(string.IsNullOrEmpty(StartupRegistryValue))
					return false;

				key.SetValue(StartupRegistryKeyName, StartupRegistryValue);
				return true;
			}
		}
		//refused (security software, a policy): the General page says so
		catch(Exception e)
		{
			Logger.Error("Failed to enable launch at startup", e);
		}
		return false;
	}

	internal static void DisableLaunchAtStartup()
	{
		try
		{
			using(var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
			using(var key = baseKey.OpenSubKey(StartupRegistryPath, true))
			{
				key?.DeleteValue(StartupRegistryKeyName, throwOnMissingValue: false);
			}
		}
		catch(Exception e)
		{
			Logger.Error("Failed to disable launch at startup", e);
		}
	}

	internal static bool CheckIfStartupRegistryKeyExists()
	{
		try
		{
			using(var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
			using(var key = baseKey.OpenSubKey(StartupRegistryPath, true))
			{
				var value = key?.GetValue(StartupRegistryKeyName)?.ToString();
				if(key != null && !string.IsNullOrEmpty(value))
				{
					//points the entry at the running exe, so a moved build keeps starting
					if(!string.IsNullOrEmpty(ExePath) && RepointStartupCommandLine(value, ExePath) is string repointed)
						key.SetValue(StartupRegistryKeyName, repointed);
					return true;
				}
				return false;
			}
		}
		catch(Exception e)
		{
			Logger.Error("Failed to check for existing key", e);
		}
		return false;
	}

	internal static bool AddToStartMenu()
	{
		try
		{
			if(string.IsNullOrEmpty(ExePath))
				return false;

			SaveShortcut(Path.Combine(StartMenuProgramsPath, StartMenuLinkName), ExePath, EnvironmentArgs.FromEnvironment().TrimStart());
			return true;
		}
		catch(Exception e)
		{
			Logger.Error("Failed to add to Start Menu", e);
		}
		return false;
	}

	internal static void RemoveFromStartMenu()
	{
		try
		{
			File.Delete(Path.Combine(StartMenuProgramsPath, StartMenuLinkName));
		}
		catch { }
	}

	internal static bool CheckIfStartMenuLinkExists()
	{
		try
		{
			if(File.Exists(Path.Combine(StartMenuProgramsPath, StartMenuLinkName)))
			{
				UpdateStartMenuShortcutIfExePathHasChanged();
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static void UpdateStartMenuShortcutIfExePathHasChanged()
	{
		try
		{
			string linkPath = Path.Combine(StartMenuProgramsPath, StartMenuLinkName);
			if(ExePath != null && !ReadShortcutTarget(linkPath).Equals(ExePath))
				SaveShortcut(linkPath, ExePath);
		}
		catch { }
	}

	/// <summary>
	/// Creates a shortcut to <paramref name="targetPath"/>, or points an existing one there (keeping its other settings, e.g. its icon)
	/// </summary>
	/// <param name="linkPath"/>
	/// <param name="targetPath"/>
	/// <param name="arguments">The shortcut's arguments; null keeps an existing shortcut's own, as when a moved exe re-points it</param>
	internal static void SaveShortcut(string linkPath, string targetPath, string? arguments = null)
	{
		var link = (IShellLinkW)new ShellLink();
		try
		{
			var file = (IPersistFile)link;
			if(File.Exists(linkPath))
				file.Load(linkPath, 2 /*STGM_READWRITE*/);
			link.SetPath(targetPath);
			if(arguments != null)
				link.SetArguments(arguments);
			file.Save(linkPath, false);
		}
		//released now rather than when the garbage collector gets to it
		finally { Marshal.FinalReleaseComObject(link); }
	}

	/// <summary>
	/// The path a shortcut points to
	/// </summary>
	internal static string ReadShortcutTarget(string linkPath)
	{
		var link = (IShellLinkW)new ShellLink();
		try
		{
			((IPersistFile)link).Load(linkPath, 0 /*STGM_READ*/);
			//long enough for any path; flags 0: the full path (SLGP_SHORTPATH can give an 8.3 path that differs from the exe path)
			var buffer = new char[32768];
			var data = new WIN32_FIND_DATAW();
			link.GetPath(buffer, ref data, 0).ThrowOnFailure();
			int end = Array.IndexOf(buffer, '\0');
			return new string(buffer, 0, end < 0 ? buffer.Length : end);
		}
		finally { Marshal.FinalReleaseComObject(link); }
	}

	/// <summary>
	/// The arguments a shortcut passes to its target
	/// </summary>
	internal static string ReadShortcutArguments(string linkPath)
	{
		var link = (IShellLinkW)new ShellLink();
		try
		{
			((IPersistFile)link).Load(linkPath, 0 /*STGM_READ*/);
			//INFOTIPSIZE: the longest arguments a shortcut keeps
			var buffer = new char[1024];
			link.GetArguments(buffer);
			int end = Array.IndexOf(buffer, '\0');
			return new string(buffer, 0, end < 0 ? buffer.Length : end);
		}
		finally { Marshal.FinalReleaseComObject(link); }
	}
#endif

	internal static void SimulateKeyPress(Key key)
	{
		try
		{
			NativeMethods.SimulateKeyPress(key);
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to simulate key press: {key.ToString()}", e);
		}
	}
}

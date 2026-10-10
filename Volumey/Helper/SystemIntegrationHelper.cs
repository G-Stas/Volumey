using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;
using System.Windows;
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
	private static readonly string StartupRegistryValue;

	static SystemIntegrationHelper()
	{
		string exePath = Environment.ProcessPath;
		if(string.IsNullOrEmpty(exePath))
		{
			Logger.Error("Failed to retrieve .exe path.");
			return;
		}
		//quoted: an unquoted path with spaces is ambiguous to Windows (it may run another program or nothing).
		//Older unquoted entries get rewritten by CheckIfStartupRegistryKeyExists on the next start
		StartupRegistryValue = $"\"{exePath}\" {Startup.MinimizedArg}";
	}

	private static readonly string StartMenuProgramsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

	internal static bool EnableLaunchAtStartup()
	{
		try
		{
			using(var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
			using(var key = baseKey.OpenSubKey(StartupRegistryPath, true))
			{
				if(CheckIfStartupRegistryKeyExists())
					DisableLaunchAtStartup();

				if(string.IsNullOrEmpty(StartupRegistryValue))
					return false;

				key?.SetValue(StartupRegistryKeyName, StartupRegistryValue);
				return true;
			}
		}
		catch(SecurityException e)
		{
			Logger.Error("Failed to enable launch at startup", e);
			MessageBox.Show(e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
		}
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
				var value = key?.GetValue(StartupRegistryKeyName);
				if(!string.IsNullOrEmpty(value?.ToString()))
				{
					if(StartupRegistryValue == null || StartupRegistryValue.Equals(value))
						return true;
					//Update registry startup path to the currently launched .exe if values are not equals
					key.SetValue(StartupRegistryKeyName, StartupRegistryValue);
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
			var exePath = Environment.ProcessPath;
			if(string.IsNullOrEmpty(exePath))
				return false;

			SaveShortcut(Path.Combine(StartMenuProgramsPath, StartMenuLinkName), exePath);
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
			string exePath = Environment.ProcessPath;
			if(!ReadShortcutTarget(linkPath).Equals(exePath))
				SaveShortcut(linkPath, exePath);
		}
		catch { }
	}

	/// <summary>
	/// Creates a shortcut to <paramref name="targetPath"/>, or points an existing one there (keeping its other settings, e.g. its icon)
	/// </summary>
	internal static void SaveShortcut(string linkPath, string targetPath)
	{
		var link = (IShellLinkW)new ShellLink();
		var file = (IPersistFile)link;
		if(File.Exists(linkPath))
			file.Load(linkPath, 2 /*STGM_READWRITE*/);
		link.SetPath(targetPath);
		file.Save(linkPath, false);
	}

	/// <summary>
	/// The path a shortcut points to
	/// </summary>
	internal static string ReadShortcutTarget(string linkPath)
	{
		var link = (IShellLinkW)new ShellLink();
		((IPersistFile)link).Load(linkPath, 0 /*STGM_READ*/);
		//long enough for any path; flags 0: the full path (SLGP_SHORTPATH, used before, can give an 8.3 path that differs from the exe path)
		var buffer = new char[32768];
		var data = new WIN32_FIND_DATAW();
		link.GetPath(buffer, ref data, 0).ThrowOnFailure();
		int end = Array.IndexOf(buffer, '\0');
		return new string(buffer, 0, end < 0 ? buffer.Length : end);
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

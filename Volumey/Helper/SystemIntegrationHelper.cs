#if(!STORE)
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Windows;
using log4net;
using Microsoft.Win32;

namespace Volumey.Helper
{
	internal static class SystemIntegrationHelper
	{
		private static ILog _logger = LogManager.GetLogger(typeof(SystemIntegrationHelper));
		private static readonly string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
		private static readonly string StartupRegistryKeyName = "Volumey";

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

					var exePath = Process.GetCurrentProcess()?.MainModule?.FileName;
					if(string.IsNullOrEmpty(exePath))
					{
						_logger.Error("Failed to retrieve .exe path.");
						return false;
					}
					key?.SetValue(StartupRegistryKeyName, $"{exePath} {Startup.MinimizedArg}");
					return true;
				}
			}
			catch(SecurityException e)
			{
				_logger.Error("Failed to enable launch at startup", e);
				MessageBox.Show(e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch(Exception e)
			{
				_logger.Error("Failed to enable launch at startup", e);
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
					key?.DeleteValue(StartupRegistryKeyName);
				}
			}
			catch(Exception e)
			{
				_logger.Error("Failed to disable launch at startup", e);
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
					return!string.IsNullOrEmpty(value?.ToString());
				}
			}
			catch(Exception e)
			{
				_logger.Error("Failed to check for existing key", e);
			}
			return false;
		}

		internal static bool AddToStartMenu()
		{
			try
			{
				var link = (NativeMethods.IShellLink)new NativeMethods.ShellLink();

				var exePath = Process.GetCurrentProcess()?.MainModule?.FileName;
				if(string.IsNullOrEmpty(exePath))
					return false;

				link.SetPath(exePath);
				IPersistFile file = (IPersistFile)link;
				file.Save(Path.Combine(StartMenuProgramsPath, "Volumey.lnk"), false);
				return true;
			}
			catch(Exception e)
			{
				_logger.Error("Failed to add to Start Menu", e);
			}
			return false;
		}

		internal static void RemoveFromStartMenu()
		{
			try
			{
				File.Delete(Path.Combine(StartMenuProgramsPath, "Volumey.lnk"));
			}
			catch { }
		}

		internal static bool CheckIfStartMenuLinkExists()
		{
			try
			{
				return File.Exists(Path.Combine(StartMenuProgramsPath, "Volumey.lnk"));
			}
			catch
			{
				return false;
			}
		}
	}
}
#endif
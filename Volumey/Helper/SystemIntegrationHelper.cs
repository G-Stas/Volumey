using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Input;
using log4net;
using Microsoft.Win32;

namespace Volumey.Helper
{
	internal static class SystemIntegrationHelper
	{
		private static ILog _logger = LogManager.GetLogger(typeof(SystemIntegrationHelper));
#if(!STORE)

		private static readonly string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
		private static readonly string StartupRegistryKeyName = "Volumey";
		private static readonly string StartupRegistryValue;
		
		static SystemIntegrationHelper()
		{
			string exePath = null;
			try
			{
				exePath = Process.GetCurrentProcess()?.MainModule?.FileName;
				StartupRegistryValue = $"{exePath} {Startup.MinimizedArg}";
				return;
			}
			catch { }
			if(string.IsNullOrEmpty(exePath))
				_logger.Error("Failed to retrieve .exe path.");
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
					if(!string.IsNullOrEmpty(value?.ToString()))
					{
						if(StartupRegistryValue.Equals(value))
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
				if(File.Exists(Path.Combine(StartMenuProgramsPath, "Volumey.lnk")))
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
				var link = (NativeMethods.IShellLink)new NativeMethods.ShellLink();
				IPersistFile file = (IPersistFile)link;
				
				file.Load(Path.Combine(StartMenuProgramsPath, "Volumey.lnk"), (int)0x00000002L);
				StringBuilder sb = new StringBuilder(260);
				var data = new NativeMethods.WIN32_FIND_DATA();
				link.GetPath(sb, sb.Capacity, out data, 0x1);

				string existingShortcutExePath = sb.ToString();
				string exePath = Process.GetCurrentProcess()?.MainModule?.FileName;

				if(!existingShortcutExePath.Equals(exePath))
				{
					link.SetPath(exePath);
					file.Save(Path.Combine(StartMenuProgramsPath, "Volumey.lnk"), false);
				}
			}
			catch { }
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
				_logger.Error($"Failed to simulate key press: {key.ToString()}", e);
			}
		}
	}
}
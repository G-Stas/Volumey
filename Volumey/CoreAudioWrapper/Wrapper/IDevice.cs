﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Localization;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IDevice
	{
		IMMDevice device { get; }
		
		object Activate(Guid id);

		string GetId();

		string GetFriendlyName();

		string GetDeviceDesc();

		ImageSource GetIconSource();

		Icon GetIcon();

		WAVEFORMATEX? GetDeviceFormat();
	}

	internal static class IDeviceExtensions
	{
		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(IDevice));
		
		internal static OutputDeviceModel GetOutputDeviceModel(this IDevice device, IDeviceStateNotificationHandler deviceStateNotificationHandler)
		{
			try
			{
				var sessionManager =
					(IAudioSessionManager2) device.Activate(new Guid(GuidValue.External.IAudioSessionManager2));
				sessionManager.GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
		
				var processes = device.GetCurrentProcessList(sessionEnum);
				var sessionProvider = new AudioSessionProvider(sessionManager);
				var master = device.GetMaster();
		
				return new OutputDeviceModel(device, deviceStateNotificationHandler, sessionProvider, master, processes);
			}
			catch(Exception e)
			{
				Logger.Error("Failed to create output device model", e);
				return null;
			}
		}
		
		private static MasterSessionModel GetMaster(this IDevice device)
		{
			try
			{
				var endpointVolume =
					(IAudioEndpointVolume) device.Activate(new Guid(GuidValue.External.IAudioEndpointVolume));
		
				var volumeHandler = new MasterVolumeNotificationHandler(endpointVolume);
				volumeHandler.RegisterMVolumeNotifications();
		
				var masterVolume = new MasterAudioSessionVolume(endpointVolume);
				float volume = masterVolume.GetVolume();
				bool muteState = masterVolume.GetMute();
				Icon icon = device.GetIcon();
				string deviceName = device.GetFriendlyName();
				string deviceDesc = device.GetDeviceDesc();
				string id = device.GetId();
		
				return new MasterSessionModel(deviceName, deviceDesc, Convert.ToInt32(volume * 100), muteState, id, icon,
				                              masterVolume,
				                              volumeHandler);
			}
			catch(Exception e)
			{
				Logger.Error("Failed to create master session", e);
				throw;
			}
		}
		
		private static List<AudioProcessModel> GetCurrentProcessList(this IDevice device, IAudioSessionEnumerator sessionEnum)
		{
			Dictionary<string, AudioProcessModel> processes = new Dictionary<string, AudioProcessModel>();
			var list = new List<AudioProcessModel>();
		
			if(sessionEnum == null)
				return list;
		
			sessionEnum.GetCount(out int sessionCount);
			for(int i = 0; i < sessionCount; i++)
			{
				try
				{
					sessionEnum.GetSession(i, out IAudioSessionControl sessionControl);
					var session = sessionControl.GetAudioSessionModel();

					if(session == null)
						continue;
					
					if(processes.TryGetValue(session.FilePath, out AudioProcessModel process))
					{
						session.Name = process.Name;
						process.AddSession(session);
					}
					else
					{
						AudioProcessModel model = session.GetProcessModelFromSessionModel((IAudioSessionControl2)sessionControl, out bool isSystemSounds);
						
						if(model == null)
							continue;
						
						model.AddSession(session);
						session.Name = model.Name;
						processes.Add(session.FilePath, model);

						//put system sounds model on top of the session list
						if(isSystemSounds)
							list.Insert(0, model);
						else
							list.Add(model);
					}
				}
				catch { }
			}
			return list;
		}

		private static AudioProcessModel GetProcessModelFromSessionModel(this AudioSessionModel session, IAudioSessionControl sControl, out bool isSystemSounds)
		{
			Process proc;
			isSystemSounds = false;

			try
			{
				proc = Process.GetProcessById((int)session.ProcessId);
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get session process", e);
				return null;
			}

			Icon icon = null;
			string processName;

			try
			{
				FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(proc.MainModule.FileName);
				if(!string.IsNullOrEmpty(fileInfo.FileDescription))
					processName = fileInfo.FileDescription;
				else
					processName = proc.ProcessName;
			}
			catch
			{
				processName = proc.ProcessName;
			}

			try
			{
				if(((IAudioSessionControl2)sControl).IsSystemSounds())
				{
					isSystemSounds = true;
					//check if the system language was changed to get a new localized name for the system sounds session
					var systemLanguage = TranslationSource.GetSystemLanguage();
					if(SettingsProvider.Settings.SystemSoundsName == null ||
					   SettingsProvider.Settings.SystemLanguage != systemLanguage)
					{
						//returns resources "DllPath,index"
						processName = sControl.GetSystemSoundsName();
						SettingsProvider.Settings.SystemLanguage = systemLanguage;
						SettingsProvider.Settings.SystemSoundsName = processName;
						SettingsProvider.SaveSettings().GetAwaiter().GetResult();
					}
					else
						processName = SettingsProvider.Settings.SystemSoundsName;

					try
					{
						App.Current.Dispatcher.Invoke(() =>
						{
							icon =
								Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\AudioSrv.dll"));
						});
					}
					catch(Exception e)
					{
						Logger.Error($"Failed to extract system sounds icon from AudioSrv.dll.", e);
					}
				}
				else
				{
					App.Current.Dispatcher.Invoke(() => icon = ExtractSessionIcon(proc, sControl));
				}
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to create process model from session model, process: [{processName}]", e);
				return null;
			}
			AudioProcessModel model = new AudioProcessModel(session.Volume, session.IsMuted, processName, session.ProcessId, session.FilePath, icon, proc);
			return model;
		}

		internal static AudioProcessModel GetProcessModelFromSessionModel(this AudioSessionModel session, IAudioSessionControl sControl)
		{
			return session.GetProcessModelFromSessionModel(sControl, out bool systemSounds);
		}

		internal static AudioSessionModel GetAudioSessionModel(this IAudioSessionControl sControl)
		{
			var sessionControl = (IAudioSessionControl2) sControl;
			sessionControl.GetProcessId(out uint processId);

			string filePath = processId.ToString();

			try
			{
				filePath = Process.GetProcessById((int)processId).MainModule.FileName;
			}
			catch { }
			
			try
			{
				var sessionVolume = new AudioSessionVolume(sessionControl);
				var muteState = sessionVolume.GetMute();
				var volume = sessionVolume.GetVolume();
				sessionControl.GetSessionIdentifier(out var sessionId);

				AudioSessionStateNotifications sessionStateNotifications = new AudioSessionStateNotifications(sessionControl);

				try { sessionStateNotifications.RegisterNotifications(); }
				catch { }

				AudioSessionModel session = new AudioSessionModel(muteState, Convert.ToInt32(volume * 100), sessionId, processId,
				                                                  filePath,
				                                                  sessionVolume,
				                                                  sessionStateNotifications);
				return session;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to create audio session model", e);
			}
			return null;
		}

		private static Icon ExtractSessionIcon(Process proc, IAudioSessionControl sc)
		{
			Icon icon = null;
			try
			{
				icon = IconHelper.GetFromProcess(proc);
			}
			catch(Win32Exception)
			{
				//"Win32Exception occurs when a 32-bit process is trying to access the modules of a 64-bit process"
				//occurs when the exe is a system process or it was launched via admin rights
				
				//it's possible to get system process icon from its IAudioSessionControl interface
				sc.GetIconPath(out var iconPath);
				if(string.IsNullOrEmpty(iconPath))
				{
					//the icon path will be empty if the exe was launched with admin rights
					//looks like its impossible to get an icon of an exe with admin rights so we return generic windows exe icon
					return IconHelper.GenericExeIcon;
				}

				try
				{
					//"The format of an icon resource specifier is "executable-file-path,resource-identifier"
					//where executable-file-path contains the fully qualified path of the file on a computer that contains the icon resource
					//and resource-identifier specifies an integer that identifies the resource."
					string[] resource = iconPath.Split(',');
					if(resource.Length < 2)
						throw new Exception();

					//extract the icon from dll
					icon = IconHelper.GetFromDll(filePath: resource[0],
					                             resourceId: int.Parse(resource[1], CultureInfo.InvariantCulture));
				}
				catch { }

				if(icon == null)
				{
					try
					{
						icon = IconHelper.GetFromFilePath(iconPath);
					}
					catch(Exception e)
					{
						Logger.Error($"Failed to extract session icon from the file path, process: [{proc.ProcessName}] file path: [{iconPath}]", e);
					}
				}
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to extract icon from the process: [{proc.ProcessName}]", e);
			}
			return icon ?? IconHelper.GenericExeIcon;
		}

		private static bool IsSystemSounds(this IAudioSessionControl2 sc) => sc.IsSystemSoundsSession().Equals(0x0);

		private static string GetSystemSoundsName(this IAudioSessionControl sc)
		{
			string systemSoundsName = string.Empty;
			string resourcePath = string.Empty;
			try
			{
				sc.GetDisplayName(out resourcePath);
				if(!string.IsNullOrEmpty(resourcePath))
				{
					var index = int.Parse(resourcePath.Split(',')[1], CultureInfo.InvariantCulture);
					systemSoundsName = NativeMethods.ExtractStringFromSystemDLL("AudioSrv.dll",
					                                                       index < 0 ? -index : index);
				}
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get system sounds session name, path: [{resourcePath}]", e);
			}
			return systemSoundsName;
		}
	}
}
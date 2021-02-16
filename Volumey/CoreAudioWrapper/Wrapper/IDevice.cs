using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using log4net;
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
		
				var sessions = device.GetCurrentSessionList(sessionEnum);
				var sessionProvider = new AudioSessionProvider(sessionManager);
				var master = device.GetMaster();
		
				return new OutputDeviceModel(device, deviceStateNotificationHandler, sessionProvider, master, sessions);
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
				ImageSource icon = device.GetIconSource();
				string deviceName = device.GetFriendlyName();
				string deviceDesc = device.GetDeviceDesc();
		
				return new MasterSessionModel(deviceName, deviceDesc, Convert.ToInt32(volume * 100), muteState, icon,
				                              masterVolume,
				                              volumeHandler);
			}
			catch(Exception e)
			{
				Logger.Error("Failed to create master session", e);
				throw;
			}
		}
		
		private static ObservableCollection<AudioSessionModel> GetCurrentSessionList(this IDevice device, IAudioSessionEnumerator sessionEnum)
		{
			var list = new ObservableCollection<AudioSessionModel>();
		
			if(sessionEnum == null)
				return list;
		
			sessionEnum.GetCount(out int sessionCount);
			for(int i = 0; i < sessionCount; i++)
			{
				try
				{
					sessionEnum.GetSession(i, out IAudioSessionControl sessionControl);
					var session = sessionControl.GetAudioSessionModel(out bool isSystemSession);
					if(session != null)
					{
						//put system session on top of the session list
						if(isSystemSession)
							list.Insert(0, session);
						else
							list.Add(session);
					}
				}
				catch { }
			}
			return list;
		}

		internal static AudioSessionModel GetAudioSessionModel(this IAudioSessionControl sControl, out bool isSystemSession)
		{
			if(sControl == null)
			{
				isSystemSession = false;
				return null;
			}
			var sessionControl = (IAudioSessionControl2) sControl;
			Process process;

			try
			{
				Marshal.ThrowExceptionForHR(sessionControl.GetProcessId(out var processId));
				process = Process.GetProcessById((int) processId);
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get session process", e);
				isSystemSession = false;
				return null;
			}

			try
			{
				ImageSource iconImageSource = null;
				string sessionName = string.Empty;

				try
				{
					sessionName = process.ProcessName;
				}
				catch { }

				if(sessionControl.IsNotSystemSounds())
				{
					App.Current.Dispatcher.Invoke(() => iconImageSource = ExtractSessionIcon(process, sControl));
					isSystemSession = false;
				}
				else
				{
					isSystemSession = true;
					//check if the system language was changed to get a new localized name for the system sounds session
					var systemLanguage = TranslationSource.GetSystemLanguage();
					if(SettingsProvider.Settings.SystemSoundsName == null ||
					   SettingsProvider.Settings.SystemLanguage != systemLanguage)
					{
						//returns resources "DllPath,index"
						sessionName = sControl.GetSystemSoundsName();
						SettingsProvider.Settings.SystemLanguage = systemLanguage;
						SettingsProvider.Settings.SystemSoundsName = sessionName;
						SettingsProvider.SaveSettings().GetAwaiter().GetResult();
					}
					else
						sessionName = SettingsProvider.Settings.SystemSoundsName;

					try
					{
						App.Current.Dispatcher.Invoke(() =>
						{
							Icon icon = 
								Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\AudioSrv.dll"));
							iconImageSource = IconHelper.GetImageSourceFromIcon(icon);
						});
					}
					catch(Exception e)
					{
						Logger.Error($"Failed to extract system sounds icon from AudioSrv.dll.", e);
					}
				}

				var sessionVolume = new AudioSessionVolume(sessionControl);
				var muteState = sessionVolume.GetMute();
				var volume = sessionVolume.GetVolume();

				AudioSessionStateNotifications sessionStateNotifications =
					new AudioSessionStateNotifications(sessionControl);

				try
				{
					sessionStateNotifications.RegisterNotifications();
				}
				catch(Exception e)
				{
					Logger.Error($"Failed to register audio session notifications, process: [{sessionName}]", e);
				}

				AudioSessionModel session =
					new AudioSessionModel(muteState, Convert.ToInt32(volume * 100), sessionName, iconImageSource,
					                      sessionVolume,
					                      sessionStateNotifications);
				return session;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to create audio session model, process: [{process.ProcessName}]", e);
			}
			isSystemSession = false;
			return null;
		}

		private static ImageSource ExtractSessionIcon(Process proc, IAudioSessionControl sc)
		{
			ImageSource icon = null;
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

		private static bool IsNotSystemSounds(this IAudioSessionControl2 sc) => sc.IsSystemSoundsSession().Equals(0x1);

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
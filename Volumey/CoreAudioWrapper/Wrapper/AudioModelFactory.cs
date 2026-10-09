using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using log4net;
using NAudio.CoreAudioApi;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Localization;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using Windows.Win32.Foundation;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Builds the device, master, process and session models from NAudio objects.
/// </summary>
internal static class AudioModelFactory
{
	private static ILog logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(AudioModelFactory));

	internal static OutputDeviceModel CreateOutputDeviceModel(AudioDevice device, IDeviceStateNotificationHandler deviceStateNotificationHandler, IHotkeyService hotkeys)
	{
		try
		{
			var sessionManager = device.Device.AudioSessionManager;
			var processes = GetCurrentProcessList(sessionManager.Sessions, hotkeys);
			var sessionProvider = new AudioSessionProvider(sessionManager, hotkeys);
			var master = CreateMaster(device, hotkeys);

			return new OutputDeviceModel(device, deviceStateNotificationHandler, sessionProvider, master, processes);
		}
		catch(Exception e)
		{
			Logger.Error("Failed to create output device model", e);
			return null;
		}
	}

	private static MasterSessionModel CreateMaster(AudioDevice device, IHotkeyService hotkeys)
	{
		try
		{
			var endpointVolume = device.Device.AudioEndpointVolume;

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
										  volumeHandler,
										  hotkeys);
		}
		catch(Exception e)
		{
			Logger.Error("Failed to create master session", e);
			throw;
		}
	}

	private static List<AudioProcessModel> GetCurrentProcessList(SessionCollection sessions, IHotkeyService hotkeys)
	{
		List<AudioProcessModel> processes = new List<AudioProcessModel>();

		if(sessions == null)
			return processes;

		int sessionCount = sessions.Count;
		for(int i = 0; i < sessionCount; i++)
		{
			try
			{
				AudioSessionControl sessionControl = sessions[i];
				AudioSessionModel session = CreateSessionModel(sessionControl);

				if(session == null)
				{
					sessionControl.Dispose();
					continue;
				}

				if(processes.FirstOrDefault(p => p.GroupingParam.Equals(session.GroupingParam) || p.FilePath.Equals(session.FilePath)) is AudioProcessModel process)
				{
					session.Name = process.Name;
					process.AddSession(session);
				}
				else
				{
					AudioProcessModel newProcess = CreateProcessModel(session, sessionControl, hotkeys, out bool isSystemSounds);

					if(newProcess == null)
						continue;

					newProcess.AddSession(session);
					session.Name = newProcess.Name;

					//put system sounds process on top of the processes list
					if(isSystemSounds)
						processes.Insert(0, newProcess);
					else
						processes.Add(newProcess);
				}
			}
			catch { }
		}
		return processes;
	}

	internal static AudioProcessModel CreateProcessModel(AudioSessionModel session, AudioSessionControl sControl, IHotkeyService hotkeys, out bool isSystemSounds)
	{
		Process proc = null;
		isSystemSounds = false;
		string processName = session.Name;

		try
		{
			proc = Process.GetProcessById((int)session.ProcessId);
			FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(proc.MainModule.FileName);
			if(!string.IsNullOrEmpty(fileInfo.FileDescription))
				processName = fileInfo.FileDescription;
			else
				processName = proc.ProcessName;
		}
		catch { }

		Icon icon = null;

		try
		{
			if(sControl.IsSystemSoundsSession)
			{
				isSystemSounds = true;
				//check if the system language was changed to get a new localized name for the system sounds session
				var systemLanguage = TranslationSource.GetSystemLanguage();
				var settings = SettingsProvider.Settings;
				string systemSoundsName = settings.General.SystemSoundsName;
				if(systemSoundsName == null || settings.General.SystemSoundsLanguage != systemLanguage)
				{
					//returns resources "DllPath,index"
					processName = GetSystemSoundsName(sControl);
					settings.General.SystemSoundsLanguage = systemLanguage;
					settings.General.SystemSoundsName = processName;
				}
				else
					processName = systemSoundsName;

				try
				{
					icon = Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\AudioSrv.dll"));
				}
				catch(Exception e)
				{
					Logger.Error($"Failed to extract system sounds icon from AudioSrv.dll.", e);
				}
			}
			else
			{
				//on the calling (COM) thread: icon extraction doesn't need the UI thread, and a COM callback must not wait for it
				icon = ExtractSessionIcon(proc, sControl);
			}
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to create process model from session model, process: [{processName}]", e);
			return null;
		}
		AudioProcessModel model = new AudioProcessModel(session.Volume, session.IsMuted, processName, session.ProcessId, session.FilePath, icon, proc, hotkeys);
		return model;
	}

	/// <summary>
	/// Creates the session model. The model takes ownership of the session control.
	/// </summary>
	internal static AudioSessionModel CreateSessionModel(AudioSessionControl sControl)
	{
		uint processId = sControl.GetProcessID;

		string filePath = processId.ToString();
		string name = filePath;

		try
		{
			//disposed: every new session opened a process handle that was never closed
			using Process proc = Process.GetProcessById((int)processId);
			name = proc.ProcessName;
			filePath = proc.MainModule.FileName;
		}
		catch { }

		try
		{
			var sessionVolume = new AudioSessionVolume(sControl.SimpleAudioVolume);
			var muteState = sessionVolume.GetMute();
			var volume = sessionVolume.GetVolume();
			var sessionId = sControl.GetSessionIdentifier;
			var groupingParam = sControl.GetGroupingParam();

			AudioSessionStateNotifications sessionStateNotifications = new AudioSessionStateNotifications(sControl, sessionVolume.EchoFilter);

			try { sessionStateNotifications.RegisterNotifications(); }
			catch { }

			AudioSessionModel session = new AudioSessionModel(muteState, AudioSessionVolume.ToPercent(volume), sessionId, processId,
															  name,
															  filePath,
															  groupingParam,
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

	private static Icon ExtractSessionIcon(Process proc, AudioSessionControl sc)
	{
		Icon icon = null;
		try
		{
			string path = NativeMethods.GetProcessImagePath(proc.Id, out int error);
			if(path != null)
				icon = Icon.ExtractAssociatedIcon(path);
			else if(error == (int)WIN32_ERROR.ERROR_ACCESS_DENIED)
				//expected for elevated processes; the session's icon path is tried next
				Logger.Info($"No access to the process [{proc.ProcessName}] to read its path");
			else
				Logger.Error($"Failed to read the path of the process [{proc.ProcessName}], error code {error}");

			icon ??= TryExtractIconAlternatively(proc, sc);
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to extract icon from the process: [{proc?.ProcessName}]", e);
			icon = TryExtractIconAlternatively(proc, sc);
		}
		return icon ?? IconHelper.CreateGenericExeIcon();
	}

	private static Icon TryExtractIconAlternatively(Process proc, AudioSessionControl sc)
	{
		//it's possible to get system process icon from its IAudioSessionControl interface
		string iconPath = null;
		try { iconPath = sc.IconPath; }
		catch { }
		if(string.IsNullOrEmpty(iconPath))
		{
			//the icon path will be empty if the exe was launched with admin rights
			//looks like its impossible to get an icon of an exe with admin rights so we return generic windows exe icon
			return IconHelper.CreateGenericExeIcon();
		}

		try
		{
			//"executable-file-path,resource-identifier", or a plain file path
			return IconHelper.GetFromResourcePath(iconPath);
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to extract the session icon, process: [{proc?.ProcessName}] icon path: [{iconPath}]", e);
			return null;
		}
	}

	private static string GetSystemSoundsName(AudioSessionControl sc)
	{
		string systemSoundsName = string.Empty;
		string resourcePath = string.Empty;
		try
		{
			resourcePath = sc.DisplayName;
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

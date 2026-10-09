using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using log4net;

namespace Volumey.Helper
{
	internal static class SystemSoundUtilities
	{
		private static readonly ILog Logger = LogManager.GetLogger(typeof(SystemSoundUtilities));

		internal static void StartSoundControlPanel()
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe"),
					Arguments = "shell32.dll,Control_RunDLL mmsys.cpl,,0"
				};
				Process.Start(psi)?.Dispose();
			}
			catch(Exception e)
			{
				Logger.Error("Failed to start sound control panel", e);
			}
		}

		internal static void StartSoundSettings()
			=> LaunchSoundSettingsAsync().Forget(Logger, "Failed to open the sound settings");

		private static async Task LaunchSoundSettingsAsync()
		{
			if(!await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:sound")))
				Logger.Warn("Windows didn't open the sound settings");
		}
	}
}

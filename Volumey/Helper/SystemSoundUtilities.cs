using System;
using System.Diagnostics;
using System.Threading.Tasks;
using log4net;

namespace Volumey.Helper
{
	internal static class SystemSoundUtilities
	{
		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(SystemSoundUtilities));
		
		internal static async void StartSoundControlPanel()
		{
			try
			{
				await Task.Run(() =>
				{
					ProcessStartInfo psi = new ProcessStartInfo
					{
						FileName = $@"{Environment.GetFolderPath(Environment.SpecialFolder.System)}\rundll32.exe",
						Arguments = "shell32.dll,Control_RunDLL mmsys.cpl,,0"
					};
					Process.Start(psi);
				}).ConfigureAwait(false);
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to start sound control panel", e);
			}
		}

		internal static async void StartSoundSettings()
		{
			try
			{
				await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
			}
			catch {}
		}
	}
}
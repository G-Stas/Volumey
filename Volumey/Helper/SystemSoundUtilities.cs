#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using log4net;

namespace Volumey.Helper;

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
				Arguments = "shell32.dll,Control_RunDLL mmsys.cpl,,0",
				//through the shell, so the panel doesn't inherit Volumey's handles (e.g. a redirected output that would stay open while it runs)
				UseShellExecute = true
			};
			Process.Start(psi)?.Dispose();
		}
		catch(Exception e)
		{
			Logger.Error("Failed to start sound control panel", e);
		}
	}

	private static readonly Uri SoundSettingsUri = new("ms-settings:sound");

	/// <summary>
	/// Whether an app can open the sound settings: false when the Settings app is removed or broken (some trimmed-down Windows builds).
	/// True when the check itself fails, so the sound settings stay offered
	/// </summary>
	internal static async Task<bool> CanOpenSoundSettingsAsync()
	{
		try
		{
			var support = await Windows.System.Launcher.QueryUriSupportAsync(SoundSettingsUri, Windows.System.LaunchQuerySupportType.Uri);
			if(support == Windows.System.LaunchQuerySupportStatus.Available)
				return true;
			Logger.Info($"Windows can't open the sound settings: {support}");
			return false;
		}
		catch(Exception e)
		{
			Logger.Warn("Failed to check whether Windows can open the sound settings", e);
			return true;
		}
	}

	/// <param name="fallBackToControlPanel">Opens the Sound control panel when Windows doesn't open the sound settings</param>
	internal static void StartSoundSettings(bool fallBackToControlPanel)
		=> LaunchSoundSettingsAsync(fallBackToControlPanel).Forget(Logger, "Failed to open the sound settings");

	private static async Task LaunchSoundSettingsAsync(bool fallBackToControlPanel)
	{
		if(await Windows.System.Launcher.LaunchUriAsync(SoundSettingsUri))
			return;
		if(!fallBackToControlPanel)
		{
			Logger.Warn("Windows didn't open the sound settings");
			return;
		}
		Logger.Warn("Windows didn't open the sound settings, opening the Sound control panel");
		StartSoundControlPanel();
	}
}

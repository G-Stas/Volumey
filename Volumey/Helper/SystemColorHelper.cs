using System;
using log4net;
using Microsoft.Win32;
using Volumey.DataProvider;

namespace Volumey.Helper
{
	static class SystemColorHelper
	{
        internal static Action<AppTheme> WindowsThemeChanged;
        internal static AppTheme WindowsTheme { get; private set; }

        private static ILog _logger;
        private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(SystemColorHelper));

		static SystemColorHelper()
		{
			SystemEvents.UserPreferenceChanged += OnUserPreferencesChanged;
			WindowsTheme = GetWindowsTheme();
		}

		/// <summary>
		/// Checks whether Windows has light or dark color theme
		/// </summary>
		/// <returns></returns>
		private static AppTheme GetWindowsTheme()
		{
			try
			{
				var systemUsesLightTheme = (int) Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\", "SystemUsesLightTheme", -1);
				if(systemUsesLightTheme != -1)
					return systemUsesLightTheme == 1 ? AppTheme.Light : AppTheme.Dark;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get SystemUsesLightTheme key from registry", e);
			}
			try
			{
				var appsUseLightTheme = (int) Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\", "AppsUseLightTheme", -1);
				if(appsUseLightTheme != -1)
					return appsUseLightTheme == 1 ? AppTheme.Light : AppTheme.Dark;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to get AppsUseLightTheme key from registry", e);
			}
			return AppTheme.Light;
		}
		
		private static void OnUserPreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			// Theme change event categorizes the change as a UserPreferenceCategory.General type change
			// that is the default when the old logic does not know what has changed
			if(e.Category == UserPreferenceCategory.General)
			{
                var newTheme = GetWindowsTheme();
				if(WindowsTheme != newTheme)
				{
                    WindowsTheme = newTheme;
					WindowsThemeChanged?.Invoke(newTheme);
                }
			}
		}
	}
}
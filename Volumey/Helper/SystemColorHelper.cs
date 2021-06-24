using System;
using System.Globalization;
using Windows.UI.ViewManagement;
using Microsoft.Win32;
using Volumey.DataProvider;

namespace Volumey.Helper
{
	static class SystemColorHelper
	{
        internal static Action<AppTheme> WindowsThemeChanged;
        internal static AppTheme WindowsTheme { get; private set; }

		private static readonly UISettings UiSettings = new UISettings();
		
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
			var color = UiSettings.GetColorValue(UIColorType.Background);
			if(color.ToString(CultureInfo.InvariantCulture).Equals("#FF000000", StringComparison.InvariantCultureIgnoreCase))
				return AppTheme.Dark;
			return AppTheme.Light;
		}
		
		private static void OnUserPreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			// This event categorizes the change as a UserPreferenceCategory.General type change
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
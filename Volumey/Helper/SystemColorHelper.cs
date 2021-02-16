using System;
using System.Globalization;
using System.Windows.Media;
using Windows.UI.ViewManagement;
using Microsoft.Win32;
using Volumey.DataProvider;

namespace Volumey.Helper
{
	static class SystemColorHelper
	{
		internal static Action<Color> AccentColorChanged;
        internal static Action<AppTheme> WindowsThemeChanged;
        internal static AppTheme WindowsTheme { get; private set; }

		private static readonly UISettings UiSettings = new UISettings();
		private static Color AccentColor = GetAccentColor();
		
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

		/// <summary>
		/// Creates color with corrected brightness.
		/// </summary>
		/// <param name="color">Color to correct.</param>
		/// <param name="correctionFactor">The brightness correction factor. Must be between -1 and 1. 
		/// Negative values produce darker colors.</param>
		internal static Color ChangeColorBrightness(Color color, float correctionFactor)
		{
			float red = color.R;
			float green = color.G;
			float blue = color.B;

			if (correctionFactor < 0)
			{
				correctionFactor = 1 + correctionFactor;
				red *= correctionFactor;
				green *= correctionFactor;
				blue *= correctionFactor;
			}
			else
			{
				red = (255 - red) * correctionFactor + red;
				green = (255 - green) * correctionFactor + green;
				blue = (255 - blue) * correctionFactor + blue;
			}
			return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
		}
		
		/// <summary>
		/// Gets Windows accent color
		/// </summary>
		/// <returns></returns>
		internal static Color GetAccentColor()
		{
			Windows.UI.Color c = UiSettings.GetColorValue(UIColorType.Accent);
			return Color.FromArgb(c.A, c.R, c.G, c.B);
		}
		
		private static void OnUserPreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			// This event categorizes the change as a UserPreferenceCategory.General type change
			// that is the default when the old logic does not know what has changed
			if(e.Category == UserPreferenceCategory.General)
			{
				Color accent = GetAccentColor();
				if(AccentColor.ToString(CultureInfo.InvariantCulture) != accent.ToString(CultureInfo.InvariantCulture))
				{
					AccentColor = accent;
					AccentColorChanged.Invoke(AccentColor);
				}

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
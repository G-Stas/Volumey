using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Volumey.DataProvider;

namespace Volumey.View.Converters
{
	public class ButtonIconThemeConverter : IMultiValueConverter
	{
		private BitmapImage mug;
		private BitmapImage MugIcon => mug ??= App.Current.FindResource("Mug") as BitmapImage;
		
		private BitmapImage mugEmpty;
		private BitmapImage MugEmptyIcon => mugEmpty ??= App.Current.FindResource("MugEmpty") as BitmapImage;

		private BitmapImage mugDark;
		private BitmapImage MugIconDark => mugDark ??= App.Current.FindResource("MugDark") as BitmapImage;
		
		private BitmapImage mugEmptyDark;
		private BitmapImage MugEmptyIconDark => mugEmptyDark ??= App.Current.FindResource("MugEmptyDark") as BitmapImage;

		private BitmapImage lightGit;
		private BitmapImage LightGitIcon => lightGit ??= App.Current.FindResource("GitHub") as BitmapImage;

		private BitmapImage darkGit;
		private BitmapImage DarkGitIcon => darkGit ??= App.Current.FindResource("GitHubDark") as BitmapImage;

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				var theme = AppTheme.Light;
				if(values[0] is AppTheme selectedTheme)
				{
					if(selectedTheme == AppTheme.System && values[1] is AppTheme windowsTheme)
						theme = windowsTheme;
					else
						theme = selectedTheme;
				}

				if(parameter.Equals("Mug") && values[2] is bool isMouseOver)
					return GetMugIcon(theme, isMouseOver);
				if(parameter.Equals("Git"))
					return theme == AppTheme.Dark ? LightGitIcon : DarkGitIcon;
			}
			catch { }
			return Binding.DoNothing;
		}

		private BitmapImage GetMugIcon(AppTheme theme, bool isMouseOver)
			=> theme == AppTheme.Dark ? GetLightMugIcon(isMouseOver) : GetDarkMugIcon(isMouseOver);

		private BitmapImage GetDarkMugIcon(bool isMouseOver)
			=> isMouseOver ? MugIconDark : MugEmptyIconDark;

		private BitmapImage GetLightMugIcon(bool isMouseOver)
			=> isMouseOver ? MugIcon : MugEmptyIcon;

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> new object[] { };
	}
}
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Volumey.DataProvider;

namespace Volumey.View.Converters
{
	public class MutedIconConverter : IMultiValueConverter
	{
		private BitmapImage darkMute;
		private BitmapImage DarkMuteIcon => darkMute ??= App.Current.FindResource("DarkMute") as BitmapImage;

		private BitmapImage darkVolume;
		private BitmapImage DarkVolumeIcon => darkVolume ??= App.Current.FindResource("DarkVolume") as BitmapImage;

		private BitmapImage lightMute;
		private BitmapImage LightMuteIcon => lightMute ??= App.Current.FindResource("LightMute") as BitmapImage;

		private BitmapImage lightVolume;
		private BitmapImage LightVolumeIcon => lightVolume ??= App.Current.FindResource("LightVolume") as BitmapImage;
		
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[0] is bool isMuted && values[1] is AppTheme newTheme)
			{
				switch(newTheme)
				{
					case AppTheme.Light: { return isMuted ? DarkMuteIcon : DarkVolumeIcon; }
					case AppTheme.Dark: { return isMuted ? LightMuteIcon : LightVolumeIcon; }
				}
			}
			return Binding.DoNothing;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
	}
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class MutedToGlyphConverter : IValueConverter
	{
		private const string MuteGlyphCode = "E74F";
		private const string VolumeGlyphCode = "E995";

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if(value is bool isMuted)
			{
				var result = isMuted ? MuteGlyphCode : VolumeGlyphCode;
				return (char)System.Convert.ToInt32(result, 16);
			}
			return string.Empty;
		}

		public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture) => null;
	}
}
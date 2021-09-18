using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Volumey.View.Converters
{
	public class BrushToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if(value is SolidColorBrush brush)
				return brush.Color;
			return Binding.DoNothing;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
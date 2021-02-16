using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class WindowVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if(value is bool windowIsVisible)
				return windowIsVisible ? Visibility.Visible : Visibility.Hidden;
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
			=> Visibility.Visible;
	}
}
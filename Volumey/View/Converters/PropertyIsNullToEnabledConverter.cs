using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class PropertyIsNullToEnabledConverter : IValueConverter
	{
		public object Convert(object property, Type targetType, object parameter, CultureInfo culture) =>
			property != null;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			Binding.DoNothing;
	}
}
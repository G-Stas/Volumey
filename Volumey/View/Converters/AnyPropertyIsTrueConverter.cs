using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class AnyPropertyIsTrueConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			foreach(var value in values)
				if(value is true)
					return true;
			return false;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
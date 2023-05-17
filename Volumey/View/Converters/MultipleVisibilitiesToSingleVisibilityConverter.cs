using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class MultipleVisibilityToSingleVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			foreach(object val in values)
			{
				//If any of the values is equals to Collapsed, we return Collapsed
				if(val is Visibility visibility && visibility == Visibility.Collapsed)
					return Visibility.Collapsed;
			}
			//Otherwise we return Visibile
			return Visibility.Visible;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
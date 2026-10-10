using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters;

public class InverseBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if(value is bool val)
			return !val;
		return Binding.DoNothing;
	}

	//two-way: e.g. the second radio button of a two-choice setting
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> Convert(value, targetType, parameter, culture);
}

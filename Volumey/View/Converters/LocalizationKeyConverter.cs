using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Localization;

namespace Volumey.View.Converters
{
	public class LocalizationKeyConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[0] is string key)
				return TranslationSource.Instance[key];
			return null;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
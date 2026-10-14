using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider;
using Volumey.Localization;

namespace Volumey.View.Converters;

/// <summary>
/// The translated name of a theme ("Theme_Light", "Theme_Dark", "Theme_System").
/// The second value is the current language, only bound so the name changes with it
/// </summary>
public sealed class ThemeNameConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values is { Length: > 0 } && values[0] is AppTheme theme)
			return TranslationSource.Instance["Theme_" + theme];
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

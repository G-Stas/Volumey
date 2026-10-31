#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Localization;

namespace Volumey.View.Converters;

/// <summary>
/// The translated name of an enum value: the key is the converter parameter followed by the value ("Theme_" + Dark).
/// The second value is the current language, only bound so the name changes with it
/// </summary>
public sealed class EnumNameConverter : IMultiValueConverter
{
	public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values is { Length: > 0 } && values[0] is Enum value && parameter is string prefix)
			return TranslationSource.Instance[prefix + value];
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

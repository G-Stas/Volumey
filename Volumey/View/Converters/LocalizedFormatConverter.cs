#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Volumey.Localization;

namespace Volumey.View.Converters;

/// <summary>
/// Fills the localized format string named by the parameter (e.g. "Remove {0}") with the values, e.g. for screen reader names.
/// Bind the current language as the last value, so the text follows it.
/// </summary>
public sealed class LocalizedFormatConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		string? format = parameter is string key ? TranslationSource.Instance[key] : null;
		if(string.IsNullOrEmpty(format))
			return string.Empty;
		return string.Format(format, values.Select(v => v?.ToString() ?? string.Empty).ToArray<object>()).Trim();
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

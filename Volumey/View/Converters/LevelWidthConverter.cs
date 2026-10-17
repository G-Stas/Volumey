#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters;

/// <summary>
/// The width of a slider's level bar: the fill's width times the level (0..1)
/// </summary>
public sealed class LevelWidthConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values.Length < 2 || values[0] is not double width || values[1] is not double level || double.IsNaN(width))
			return 0d;
		return width * Math.Clamp(level, 0, 1);
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

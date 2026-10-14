using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters;

/// <summary>
/// Whether an enum value is the one named by the parameter, for one radio button per value; a checked button gives that value back
/// </summary>
public sealed class EqualsParameterConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value?.ToString() == parameter as string;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}

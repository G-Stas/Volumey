using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.View.Converters;

/// <summary>
/// The name of a spatial sound format (<see cref="SpatialSoundNames"/>).
/// Values: the format, and the current language (only bound so the name follows it)
/// </summary>
public class SpatialFormatNameConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		=> values is { Length: >= 1 } && values[0] is SpatialSoundFormat format ? SpatialSoundNames.Of(format) : string.Empty;

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

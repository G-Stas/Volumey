using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Localization;
using Volumey.Model;

namespace Volumey.View.Converters;

/// <summary>
/// A screen as the screen picker shows it:"DISPLAY1 · 2560 × 1440 (main)".
/// The second value is the current language, only bound so "(main)" follows it
/// </summary>
public sealed class ScreenNameConverter : IMultiValueConverter
{
	private const string DevicePrefix = @"\\.\";

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values is not { Length: > 0 } || values[0] is not ScreenInfo screen)
			return string.Empty;
		string name = screen.Name ?? string.Empty;
		if(name.StartsWith(DevicePrefix, StringComparison.Ordinal))
			name = name[DevicePrefix.Length..];
		string text = $"{name} · {screen.NativeWidth:0} × {screen.NativeHeight:0}";
		return screen.IsPrimary ? $"{text} {TranslationSource.Instance["MixerPage_MainScreen"]}" : text;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

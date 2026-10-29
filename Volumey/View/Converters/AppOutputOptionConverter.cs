#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Volumey.Localization;
using Volumey.ViewModel;

namespace Volumey.View.Converters;

/// <summary>
/// The parts of an item of an app's device box (the value), by the parameter: <c>Default</c> ("Default device"),
/// <c>DefaultOrNameless</c> ("Default device" or a device Windows can't name, which the closed box shows as "Default device": Windows
/// plays the app there meanwhile), <c>Named</c> (a device with a name), <c>Nameless</c> (a device Windows can't name);
/// as a <see cref="Visibility"/>. As a multi-value converter (the item, then the language) it gives the item's name for screen readers
/// </summary>
public sealed class AppOutputOptionConverter : IValueConverter, IMultiValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if(value is not AppOutputOption option)
			return Visibility.Collapsed;
		bool nameless = option.DeviceId != null && option.Name.Length == 0;
		bool visible = (parameter as string) switch
		{
			"Default" => option.DeviceId == null,
			"DefaultOrNameless" => option.DeviceId == null || nameless,
			"Named" => option.DeviceId != null && !nameless,
			"Nameless" => nameless,
			_ => false
		};
		return visible ? Visibility.Visible : Visibility.Collapsed;
	}

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		=> values is [AppOutputOption option, ..]
			? option.DeviceId == null ? TranslationSource.Instance["Mixer_DefaultDevice"] ?? string.Empty : option.Name
			: string.Empty;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

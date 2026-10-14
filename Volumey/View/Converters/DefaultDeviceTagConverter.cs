using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Volumey.Model;

namespace Volumey.View.Converters;

/// <summary>
/// The default device's mark in a device list, from the item (first value) and the default device (second value):
/// for a <see cref="Visibility"/>, visible for the default device only; for a string, the tag text (the third value)
/// for the default device and empty for the others
/// </summary>
public sealed class DefaultDeviceTagConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		bool isDefault = values is [OutputDeviceModel device, OutputDeviceModel defaultDevice, ..] && device == defaultDevice;
		if(targetType == typeof(Visibility))
			return isDefault ? Visibility.Visible : Visibility.Collapsed;
		return isDefault && values is [_, _, string tag, ..] ? tag : string.Empty;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

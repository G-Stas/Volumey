#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters;

/// <summary>
/// Visible when the app's row opens: from whether the app is System Sounds (first value) and its session count (second value).
/// Any other app opens for its volume lock (and its device box); System Sounds, which can be neither locked nor moved, only
/// when it has several sessions
/// </summary>
public sealed class AppOpensConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		=> values is [false, ..] || values is [_, int count, ..] && count > 1 ? Visibility.Visible : Visibility.Collapsed;

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

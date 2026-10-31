#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters;

/// <summary>
/// Visible when the app can be moved to another device: from whether apps can be moved (first value) and whether the app is
/// System Sounds (second value), which Windows can't move. With a third value (the app's session count), also visible for an app
/// with several sessions: the app opens for either
/// </summary>
public sealed class AppCanMoveConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		bool canMove = values is [true, false, ..];
		bool severalSessions = values is [_, _, int count, ..] && count > 1;
		return canMove || severalSessions ? Visibility.Visible : Visibility.Collapsed;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

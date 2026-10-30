#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider.Settings;
using Volumey.Localization;

namespace Volumey.View.Converters;

public sealed class TrayMiddleClickActionConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values.Length > 0 && values[0] is TrayMiddleClickAction action)
		{
			string key = action switch
			{
				TrayMiddleClickAction.ToggleMute => "TrayMiddleClick_ToggleMute",
				TrayMiddleClickAction.SwitchDevice => "TrayMiddleClick_SwitchDevice",
				_ => "TrayMiddleClick_None"
			};
			return TranslationSource.Instance[key] ?? action.ToString();
		}
		return string.Empty;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

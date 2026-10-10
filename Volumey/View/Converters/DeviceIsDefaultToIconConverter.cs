using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Model;
using Wpf.Ui.Controls;

namespace Volumey.View.Converters;

/// <summary>
/// The checkmark of the default device in the tray menu's device list
/// </summary>
public class DeviceIsDefaultToIconConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		//a new element each time: an element can have only one parent, and a shared one could still belong to the previous
		//default device's menu item when the new one takes it, which throws
		if(values != null && values[0] is OutputDeviceModel model && values[1] is OutputDeviceModel defaultDevice && model == defaultDevice)
			return new SymbolIcon(SymbolRegular.Checkmark24);
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		return new object[] { };
	}
}

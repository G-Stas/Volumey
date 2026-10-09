using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ModernWpf.Controls;
using Volumey.Model;

namespace Volumey.View.Converters
{
	public class DeviceIsDefaultToIconConverter : IMultiValueConverter
	{
		private static readonly FontFamily IconFont = new FontFamily("Segoe MDL2 Assets");

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			//a new element each time: an element can have only one parent, and a shared one could still belong to the previous
			//default device's menu item when the new one takes it, which throws
			if(values != null && values[0] is OutputDeviceModel model && values[1] is OutputDeviceModel defaultDevice && model == defaultDevice)
				return new FontIcon { Glyph = "\xE73E", FontFamily = IconFont };
			return null;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
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
		private static readonly FontIcon CurrentDefaultDeviceIcon;

		static DeviceIsDefaultToIconConverter()
		{
			CurrentDefaultDeviceIcon = new FontIcon { Glyph = "\xE73E", FontFamily = new FontFamily("Segoe MDL2 Assets") };
			
		}
		
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[0] is OutputDeviceModel model && values[1] is OutputDeviceModel defaultDevice)
			{
				if(model == defaultDevice)
					return CurrentDefaultDeviceIcon;
			}
			return null;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
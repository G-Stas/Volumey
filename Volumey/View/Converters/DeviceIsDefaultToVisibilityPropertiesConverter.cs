using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Volumey.Model;

namespace Volumey.View.Converters
{
	public class DeviceIsDefaultToVisibilityPropertiesConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[0] is OutputDeviceModel selectedDevice && values[1] is OutputDeviceModel currentDefaultDevice)
			{
				if(targetType == typeof(bool))
					return selectedDevice != currentDefaultDevice;
				if(targetType == typeof(Visibility))
					return selectedDevice == currentDefaultDevice ? Visibility.Collapsed : Visibility.Visible;
			}
			return Binding.DoNothing;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
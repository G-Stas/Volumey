using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Model;

namespace Volumey.View.Converters
{
	public class DeviceIsDefaultToEnabledConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[0] is OutputDeviceModel selectedDevice && values[1] is OutputDeviceModel currentDefaultDevice)
				return selectedDevice != currentDefaultDevice;
			return false;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
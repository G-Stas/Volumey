using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class DeviceViewPositionToDockConverter : IValueConverter
	{
		public object Convert(object deviceViewAtTheBottom, Type targetType, object parameter, CultureInfo culture)
		{
			if(deviceViewAtTheBottom is true)
				return Dock.Bottom;
			return Dock.Top;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
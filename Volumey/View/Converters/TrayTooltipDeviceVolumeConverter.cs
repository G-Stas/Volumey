using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class TrayTooltipDeviceVolumeConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo cltr)
		{
			if(values == null)
				return Binding.DoNothing;

			if(values[0] is true) //bool DeviceProvider.NoOutputDevices
				return string.Empty;

			if(values[1] is int volume)
				return $"{volume.ToString(CultureInfo.InvariantCulture)}%";

			return Binding.DoNothing;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo cltr)
			=> null;
	}
}
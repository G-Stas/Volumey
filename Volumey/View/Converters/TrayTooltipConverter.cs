using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class TrayTooltipConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values == null)
				return Binding.DoNothing;

			if(values[2] is true) //bool NoOutputDevice
				return"No output device";

			var deviceFriendlyName = values[0] as string;

			int deviceVolume = 0;
			try { deviceVolume = (int) values[1]; }
			catch { }

			return$"{deviceFriendlyName} - {deviceVolume.ToString(CultureInfo.InvariantCulture)}%";
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
	}
}
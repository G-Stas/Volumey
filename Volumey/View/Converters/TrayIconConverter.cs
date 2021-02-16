using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter : IMultiValueConverter
	{
		private enum IconType { High, Mid, Low, Mute, NoDevice, None}
		private IconType currentIconType = IconType.None;
		private TrayIconProvider currentIconProvider;
		
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values != null && values[3] is AppTheme theme)
			{
				if(theme is AppTheme.Dark)
				{
					if(!(currentIconProvider is LightIconProvider))
						currentIconProvider = new LightIconProvider();
				}
				else
				{
					if(!(currentIconProvider is DarkIconProvider))
						currentIconProvider = new DarkIconProvider();
				}
				return this.GetIcon(values);
			}
			return Binding.DoNothing;
		}

		private object GetIcon(object[] values)
		{
			var type = IconType.None;
			if(values[0] is true) //NoOutputDevices
				type = IconType.NoDevice;
			else
			{
				if(values[2] is true) //isMuted
					type = IconType.Mute;
				else if(values[1] is int newVolume)
					type = newVolume > 65 ? IconType.High : (newVolume < 35) ? IconType.Low : IconType.Mid;
			}
			if(this.currentIconType == type || type == IconType.None) return Binding.DoNothing;
			this.currentIconType = type;
			return this.currentIconProvider.GetIcon(type);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
	}
}
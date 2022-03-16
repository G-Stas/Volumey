using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter : IMultiValueConverter
	{
		internal enum IconType
		{
			High,
			Mid,
			Low,
			Mute,
			NoDevice,
			DeviceIcon,
			None
		}

		private IconType currentIconType = IconType.None;
		private TrayIconProvider currentIconProvider;
		private Icon curDeviceIcon;

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if(values[4] is bool displayDeviceIconAtTray && displayDeviceIconAtTray && values[5] is Icon defaultDeviceIcon)
			{
				if(currentIconType == IconType.DeviceIcon && curDeviceIcon == defaultDeviceIcon)
					return Binding.DoNothing;
				currentIconType = IconType.DeviceIcon;
				curDeviceIcon = defaultDeviceIcon;
				return defaultDeviceIcon;
			}
			
			if(values[3] is AppTheme theme)
			{
				bool newIconProvider = false;
				if(theme is AppTheme.Dark)
				{
					if(!(currentIconProvider is LightIconProvider))
					{
						currentIconProvider = new LightIconProvider();
						newIconProvider = true;
					}
				}
				else
				{
					if(!(currentIconProvider is DarkIconProvider))
					{
						currentIconProvider = new DarkIconProvider();
						newIconProvider = true;
					}
				}
				return this.GetIcon(values, newIconProvider);
			}
			return Binding.DoNothing;
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <param name="newIconProvider">Flag used to indicate that the color of the current icon has to change</param>
		/// <returns></returns>
		private object GetIcon(object[] values, bool newIconProvider)
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
			if((this.currentIconType == type || type == IconType.None) && !newIconProvider) 
				return Binding.DoNothing;
			this.currentIconType = type;
			return this.currentIconProvider.GetIcon(type);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
	}
}
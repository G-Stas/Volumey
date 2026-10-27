#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider;

namespace Volumey.View.Converters;

public sealed partial class TrayIconConverter : IMultiValueConverter
{
	internal enum IconType
	{
		High,
		Mid,
		Low,
		Mute,
		NoDevice,
		DeviceIcon,
		VolumeyIcon,
		None
	}

	private IconType currentIconType = IconType.None;
	private TrayIconProvider? currentIconProvider;
	private Icon? curDeviceIcon;
	private Icon? volumeyIcon;
	private bool triedGettingVolumeyIcon;
	//one provider per theme: a provider caches its icons, a new one on every theme switch would leak them
	private LightIconProvider? lightIconProvider;
	private DarkIconProvider? darkIconProvider;

	public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values[6] is true && GetVolumeyIcon() is Icon appIcon)
		{
			if(currentIconType == IconType.VolumeyIcon)
				return Binding.DoNothing;
			currentIconType = IconType.VolumeyIcon;
			return appIcon;
		}

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
				if(currentIconProvider is not LightIconProvider)
				{
					currentIconProvider = lightIconProvider ??= new LightIconProvider();
					newIconProvider = true;
				}
			}
			else
			{
				if(currentIconProvider is not DarkIconProvider)
				{
					currentIconProvider = darkIconProvider ??= new DarkIconProvider();
					newIconProvider = true;
				}
			}
			return GetIcon(values, currentIconProvider, newIconProvider);
		}
		return Binding.DoNothing;
	}

	private Icon? GetVolumeyIcon()
	{
		if(!triedGettingVolumeyIcon)
		{
			triedGettingVolumeyIcon = true;
			volumeyIcon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
		}
		return volumeyIcon;
	}

	/// <summary></summary>
	/// <param name="values"></param>
	/// <param name="iconProvider">The provider of the current theme</param>
	/// <param name="newIconProvider">Flag used to indicate that the color of the current icon has to change</param>
	/// <returns></returns>
	private object? GetIcon(object[] values, TrayIconProvider iconProvider, bool newIconProvider)
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
		if((currentIconType == type || type == IconType.None) && !newIconProvider)
			return Binding.DoNothing;
		currentIconType = type;
		return iconProvider.GetIcon(type);
	}

	public object[]? ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}

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
		None
	}

	private IconType currentIconType = IconType.None;
	private TrayIconProvider? currentIconProvider;
	private Icon? curDeviceIcon;
	//one provider per theme: a provider caches its icons, a new one on every theme switch would leak them
	private LightIconProvider? lightIconProvider;
	private DarkIconProvider? darkIconProvider;

	public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		var defaultDeviceIcon = values[5] as Icon;
		var type = Pick(values[4] is true, values[0] is true, values[1] as int?, values[2] is true, defaultDeviceIcon != null);

		if(type == IconType.DeviceIcon)
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
			return GetIcon(type, currentIconProvider, newIconProvider);
		}
		return Binding.DoNothing;
	}

	/// <param name="type">A volume glyph or <see cref="IconType.None"/></param>
	/// <param name="iconProvider">The provider of the current theme</param>
	/// <param name="newIconProvider">The theme changed, so the current glyph has to be sent again in the new color</param>
	private object? GetIcon(IconType type, TrayIconProvider iconProvider, bool newIconProvider)
	{
		if((currentIconType == type || type == IconType.None) && !newIconProvider)
			return Binding.DoNothing;
		currentIconType = type;
		return iconProvider.GetIcon(type);
	}

	/// <summary>What the tray shows; Convert then loads the icon and skips repeats</summary>
	internal static IconType Pick(bool displayDeviceIcon, bool noOutputDevices, int? volume, bool muted, bool hasDeviceIcon)
	{
		if(displayDeviceIcon && hasDeviceIcon)
			return IconType.DeviceIcon;
		if(noOutputDevices)
			return IconType.NoDevice;
		if(muted)
			return IconType.Mute;
		if(volume is not { } v)
			return IconType.None;
		return v > 65 ? IconType.High : v < 35 ? IconType.Low : IconType.Mid;
	}

	public object[]? ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}

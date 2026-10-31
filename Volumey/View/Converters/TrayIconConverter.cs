#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.View.Converters;

public sealed partial class TrayIconConverter : IMultiValueConverter
{
	internal enum IconType
	{
		High,
		Mid,
		Low,
		Zero,
		Mute,
		NoDevice,
		DeviceIcon,
		AppIcon,
		None
	}

	private IconType currentIconType = IconType.None;
	private TrayIconProvider? currentIconProvider;
	private Icon? curDeviceIcon;
	//one provider per theme: a provider caches its icons, a new one on every theme switch would leak them
	private LightIconProvider? lightIconProvider;
	private DarkIconProvider? darkIconProvider;

	//read once, also when it fails: it's part of the app and won't appear later
	private readonly Lazy<Icon?> appIcon;

	public TrayIconConverter() : this(IconExtensions.LoadAppTrayIcon) { }

	/// <summary>Reads the system glyph of a volume state; the file of Windows' own icons by default</summary>
	private readonly Func<IconType, Icon?>? loadSystemIcon;
	private SystemIconProvider? systemIconProvider;

	internal TrayIconConverter(Func<Icon?> loadAppIcon, Func<IconType, Icon?>? loadSystemIcon = null)
	{
		appIcon = new Lazy<Icon?>(loadAppIcon);
		this.loadSystemIcon = loadSystemIcon;
	}

	public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		var defaultDeviceIcon = values[5] as Icon;
		var kind = values[4] is TrayIconKind trayKind ? trayKind : TrayIconKind.VolumeLevel;
		var type = Pick(kind, values[0] is true, values[1] as int?, values[2] is true, defaultDeviceIcon != null);

		if(type == IconType.AppIcon)
		{
			if(currentIconType == IconType.AppIcon || appIcon.Value is not { } icon)
				return Binding.DoNothing;
			currentIconType = IconType.AppIcon;
			return icon;
		}

		if(type == IconType.DeviceIcon)
		{
			if(currentIconType == IconType.DeviceIcon && curDeviceIcon == defaultDeviceIcon)
				return Binding.DoNothing;
			currentIconType = IconType.DeviceIcon;
			curDeviceIcon = defaultDeviceIcon;
			return defaultDeviceIcon;
		}

		if(kind == TrayIconKind.SystemIcons && type != IconType.None)
		{
			var systemProvider = systemIconProvider ??= loadSystemIcon == null ? new SystemIconProvider() : new SystemIconProvider(loadSystemIcon);
			//a glyph that Windows' file doesn't give is shown in Volumey's own, in the theme's color
			if(systemProvider.GetIcon(type) != null)
			{
				bool newSystemProvider = currentIconProvider != systemProvider;
				currentIconProvider = systemProvider;
				//the system glyphs have their own colors: the theme doesn't matter
				return GetIcon(type, systemProvider, newSystemProvider);
			}
		}

		//Volumey's own glyphs have none for a volume of zero
		if(type == IconType.Zero)
			type = IconType.Low;

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
	internal static IconType Pick(TrayIconKind kind, bool noOutputDevices, int? volume, bool muted, bool hasDeviceIcon)
	{
		if(kind == TrayIconKind.AppIcon)
			return IconType.AppIcon;
		if(kind == TrayIconKind.DeviceIcon && hasDeviceIcon)
			return IconType.DeviceIcon;
		if(noOutputDevices)
			return IconType.NoDevice;
		if(muted)
			return IconType.Mute;
		if(volume is not { } v)
			return IconType.None;
		//Windows has a glyph of its own for a volume of zero
		if(v == 0 && kind == TrayIconKind.SystemIcons)
			return IconType.Zero;
		return v > 65 ? IconType.High : v < 35 ? IconType.Low : IconType.Mid;
	}

	public object[]? ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}

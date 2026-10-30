#nullable enable
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Resources;
using log4net;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.View.Converters;

public sealed partial class TrayIconConverter
{
	internal abstract class TrayIconProvider
	{
		protected virtual Icon? High => null;
		protected virtual Icon? Mid => null;
		protected virtual Icon? Low => null;
		protected virtual Icon? Mute => null;
		protected virtual Icon? NoDevice => null;
		protected virtual ILog? Logger => null;

		public Icon? GetIcon(IconType type)
		{
			try
			{
				return type switch
				{
					IconType.High => High,
					IconType.Mid => Mid,
					IconType.Low => Low,
					IconType.Mute => Mute,
					IconType.NoDevice => NoDevice,
					_ => null
				};
			}
			catch(Exception e)
			{
				Logger?.Error($"Failed to get icon of type {type.ToString()}", e);
				return null;
			}
		}
	}
}

public static class IconExtensions
{
	private static readonly Uri AppIconUri = new Uri("pack://application:,,,/Resources/Icons/icon.ico");

	/// <summary>
	/// The app icon at the tray's size for the primary screen's scale (the notification area is on the primary taskbar),
	/// so Windows doesn't have to scale a bigger frame down. Null if it can't be read (logged)
	/// </summary>
	internal static Icon? LoadAppTrayIcon()
	{
		try
		{
			StreamResourceInfo? streamInfo = Application.GetResourceStream(AppIconUri);
			if(streamInfo == null)
				throw new InvalidOperationException("The app icon isn't in the resources");
			using var stream = streamInfo.Stream;
			int size = GetTrayIconSize();
			return new Icon(stream, size, size);
		}
		catch(Exception e)
		{
			LogManager.GetLogger(typeof(IconExtensions)).Error("Failed to read the app icon for the tray", e);
			return null;
		}
	}

	private static int GetTrayIconSize()
	{
		var primary = PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
		if(PInvoke.GetDpiForMonitor(primary, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpi, out _).Failed)
			dpi = 96;
		int size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
		return size > 0 ? size : 16;
	}

	internal static Icon? GetAsIcon(this ImageSource? src)
	{
		if(src == null) return null;
		Uri uri = new Uri(src.ToString());
		StreamResourceInfo? streamInfo = Application.GetResourceStream(uri);

		if(streamInfo == null)
			return null;

		try
		{
			return new Icon(streamInfo.Stream);
		}
		finally
		{
			streamInfo.Stream.Close();
			streamInfo.Stream.Dispose();
		}
	}
}

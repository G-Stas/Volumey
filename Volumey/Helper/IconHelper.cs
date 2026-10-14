using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.Helper;

/// <summary>
/// Icon extraction. Every <see cref="Icon"/> returned here is a new one owned by the caller, who releases it with <see cref="Icon.Dispose"/>
/// only (an owned icon frees its handle itself; calling DestroyIcon as well frees it twice).
/// None of this needs the UI thread, so audio callbacks never have to wait for it.
/// </summary>
static class IconHelper
{
	public static Icon GetFromDll(string filePath, int resourceId, bool largeIcon = true)
	{
		if(filePath == null)
			return null;

		Span<HICON> extracted = stackalloc HICON[1];
		if(largeIcon)
			PInvoke.ExtractIconEx(filePath, resourceId, extracted, default);
		else
			PInvoke.ExtractIconEx(filePath, resourceId, default, extracted);

		HICON hIcon = extracted[0];
		if(hIcon.IsNull)
			return null;
		//Icon.FromHandle doesn't own the handle: keep an owned copy and free the extracted one
		using var borrowed = Icon.FromHandle(hIcon);
		var owned = (Icon)borrowed.Clone();
		PInvoke.DestroyIcon(hIcon);
		return owned;
	}

	/// <summary>
	/// Extracts an icon from a resource specifier ("file-path,resource-index", e.g. a device's or a session's icon path),
	/// or from the file itself if it's a plain path. Null if neither works.
	/// </summary>
	public static Icon GetFromResourcePath(string iconPath)
	{
		if(string.IsNullOrEmpty(iconPath))
			return null;

		int comma = iconPath.LastIndexOf(',');
		if(comma > 0 && int.TryParse(iconPath.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int resourceId))
		{
			var icon = GetFromDll(Environment.ExpandEnvironmentVariables(iconPath[..comma]), resourceId);
			if(icon != null)
				return icon;
		}
		return GetFromFilePath(Environment.ExpandEnvironmentVariables(iconPath));
	}

	public static ImageSource GetAsImageSource(this Icon icon)
	{
		try
		{
			var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			if(source.CanFreeze)
				source.Freeze();
			return source;
		}
		catch { return null; }
	}

	public static Icon GetFromFilePath(string filePath)
	{
		if(File.Exists(filePath))
			return Icon.ExtractAssociatedIcon(filePath);
		return null;
	}

	/// <summary>
	/// The apps' icons as frozen images, by the file or resource path they were read from. A frozen image can be shared by every
	/// model and thread and needs no releasing, so each icon is read from its file once per run
	/// </summary>
	private static readonly ConcurrentDictionary<string, ImageSource> iconSources = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The icon at <paramref name="path"/> as a frozen image, read with <paramref name="extract"/> the first time; null if it can't be read
	/// (not remembered: the next app with this path tries again)
	/// </summary>
	internal static ImageSource GetIconSource(string path, Func<string, Icon> extract)
	{
		if(iconSources.TryGetValue(path, out var cached))
			return cached;
		using var icon = extract(path);
		var source = icon?.GetAsImageSource();
		return source == null ? null : iconSources.GetOrAdd(path, source);
	}

	private static ImageSource genericExeIconSource;

	/// <summary>Windows' generic program icon as a frozen image, for apps whose own icon can't be read</summary>
	internal static ImageSource GenericExeIconSource
	{
		get
		{
			if(genericExeIconSource == null)
			{
				using var icon = GetGenericExeIcon();
				genericExeIconSource = icon?.GetAsImageSource();
			}
			return genericExeIconSource;
		}
	}

	private static Icon GetGenericExeIcon()
	{
		//the icon is located in this file since win 10 1903 by index 11
		string imageres = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SystemResources\imageres.dll.mun");
		return GetFromDll(imageres, 11);
	}
}

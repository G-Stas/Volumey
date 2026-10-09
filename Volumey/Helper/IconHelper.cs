using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

		IntPtr hIcon;
		if(largeIcon)
			NativeMethods.ExtractIconEx(filePath, resourceId, out hIcon, IntPtr.Zero, 1);
		else
			NativeMethods.ExtractIconEx(filePath, resourceId, IntPtr.Zero, out hIcon, 1);

		if(hIcon == IntPtr.Zero)
			return null;
		//Icon.FromHandle doesn't own the handle: keep an owned copy and free the extracted one
		using var borrowed = Icon.FromHandle(hIcon);
		var owned = (Icon)borrowed.Clone();
		NativeMethods.DestroyIcon(hIcon);
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

	private static Icon genericExeIcon;

	/// <summary>
	/// A new copy of Windows' generic program icon, for apps whose own icon can't be read. The cached original is never handed out,
	/// because models dispose their icons (it used to be, and the first app that closed destroyed it for all others).
	/// </summary>
	internal static Icon CreateGenericExeIcon()
	{
		genericExeIcon ??= GetGenericExeIcon();
		return genericExeIcon == null ? null : (Icon)genericExeIcon.Clone();
	}

	private static Icon GetGenericExeIcon()
	{
		//the icon is located in this file since win 10 1903 by index 11
		string imageres = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SystemResources\imageres.dll.mun");
		return GetFromDll(imageres, 11);
	}
}

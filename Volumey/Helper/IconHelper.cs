#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Volumey.Helper;

/// <summary>
/// Icon extraction. Every <see cref="Icon"/> returned here is a new one owned by the caller, who releases it with <see cref="Icon.Dispose"/>
/// only (an owned icon frees its handle itself; calling DestroyIcon as well frees it twice).
/// None of this needs the UI thread, so audio callbacks never have to wait for it.
/// </summary>
static class IconHelper
{
	public static Icon? GetFromDll(string? filePath, int resourceId, bool largeIcon = true)
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
	/// Like <see cref="GetFromDll"/> but at exactly <paramref name="size"/> x <paramref name="size"/>, the size a caller needs
	/// instead of the small or large icon of the system: the file's own frame of that size, else Windows scales the nearest one
	/// </summary>
	public static unsafe Icon? GetFromDllOfSize(string? filePath, int resourceId, int size)
	{
		if(filePath == null)
			return null;

		HICON hIcon = default;
		uint count;
		//like in ExtractIconEx, a negative index is a resource ID
		fixed(char* path = filePath)
			count = PInvoke.PrivateExtractIcons(path, resourceId, size, size, &hIcon, null, 1, 0);
		if(count != 1 || hIcon.IsNull)
			return null;
		//Icon.FromHandle doesn't own the handle: keep an owned copy and free the extracted one
		using var borrowed = Icon.FromHandle(hIcon);
		var owned = (Icon)borrowed.Clone();
		PInvoke.DestroyIcon(hIcon);
		return owned;
	}

	/// <summary>The lowest value of a color channel that still counts as white</summary>
	private const byte WhiteMinimum = 0xE0;

	/// <summary>Only the pixels at least this opaque decide the color: the soft edge of a glyph isn't reliable</summary>
	private const byte OpaqueMinimum = 0x80;

	/// <summary>A pixel whose color channels differ by at least this much has a color of its own, it isn't white, gray or black</summary>
	private const byte ColorSpread = 0x40;

	/// <summary>
	/// The icon is a white glyph, like the glyphs Windows draws for a dark taskbar: what can be reached from its edge without
	/// crossing a part that has a color of its own (like the red badge of the no-device icon) is white. An icon with colors of
	/// its own isn't
	/// </summary>
	public static bool IsWhiteGlyph(Icon icon)
	{
		using var bitmap = icon.ToBitmap();
		byte[] pixels = ReadPixels(bitmap);
		bool[] glyph = ReachFromEdge(pixels, bitmap.Width, bitmap.Height);
		bool anyOpaque = false;
		for(int pixel = 0; pixel < glyph.Length; pixel++)
		{
			int i = pixel * 4;
			if(!glyph[pixel] || pixels[i + 3] < OpaqueMinimum)
				continue;
			anyOpaque = true;
			if(pixels[i] < WhiteMinimum || pixels[i + 1] < WhiteMinimum || pixels[i + 2] < WhiteMinimum)
				return false;
		}
		return anyOpaque;
	}

	/// <summary>
	/// A new icon, owned by the caller, with the colors of the glyph of <paramref name="icon"/> inverted and its transparency kept.
	/// Only what can be reached from the edge without crossing a part with a color of its own is inverted: that part, and whatever
	/// it encloses, stay as they are
	/// </summary>
	public static Icon Inverted(Icon icon)
	{
		using var source = icon.ToBitmap();
		byte[] pixels = ReadPixels(source);
		bool[] glyph = ReachFromEdge(pixels, source.Width, source.Height);
		for(int pixel = 0; pixel < glyph.Length; pixel++)
		{
			int i = pixel * 4;
			if(!glyph[pixel] || pixels[i + 3] == 0)
				continue;
			pixels[i] = (byte)(byte.MaxValue - pixels[i]);
			pixels[i + 1] = (byte)(byte.MaxValue - pixels[i + 1]);
			pixels[i + 2] = (byte)(byte.MaxValue - pixels[i + 2]);
		}

		using var inverted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
		var area = new Rectangle(0, 0, inverted.Width, inverted.Height);
		var data = inverted.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try
		{
			Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
		}
		finally
		{
			inverted.UnlockBits(data);
		}

		var hIcon = new HICON(inverted.GetHicon());
		using var borrowed = Icon.FromHandle(hIcon);
		var owned = (Icon)borrowed.Clone();
		PInvoke.DestroyIcon(hIcon);
		return owned;
	}

	/// <summary>
	/// The pixels (one flag each, row after row) that a flood fill reaches from the edge of the image through the pixels
	/// without a color of its own (transparent ones included), moving up, down, left and right
	/// </summary>
	private static bool[] ReachFromEdge(byte[] pixels, int width, int height)
	{
		var reached = new bool[width * height];
		var pending = new Stack<int>();

		void Visit(int x, int y)
		{
			int pixel = y * width + x;
			if(reached[pixel])
				return;
			int i = pixel * 4;
			byte brightest = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
			byte darkest = Math.Min(pixels[i], Math.Min(pixels[i + 1], pixels[i + 2]));
			if(brightest - darkest >= ColorSpread)
				return;
			reached[pixel] = true;
			pending.Push(pixel);
		}

		for(int x = 0; x < width; x++)
		{
			Visit(x, 0);
			Visit(x, height - 1);
		}
		for(int y = 0; y < height; y++)
		{
			Visit(0, y);
			Visit(width - 1, y);
		}
		while(pending.Count > 0)
		{
			int pixel = pending.Pop();
			int x = pixel % width;
			int y = pixel / width;
			if(x > 0)
				Visit(x - 1, y);
			if(x < width - 1)
				Visit(x + 1, y);
			if(y > 0)
				Visit(x, y - 1);
			if(y < height - 1)
				Visit(x, y + 1);
		}
		return reached;
	}

	/// <summary>The pixels as BGRA, four bytes each, row after row</summary>
	private static byte[] ReadPixels(Bitmap bitmap)
	{
		var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
		var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			byte[] pixels = new byte[bitmap.Width * bitmap.Height * 4];
			Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
			return pixels;
		}
		finally
		{
			bitmap.UnlockBits(data);
		}
	}

	/// <summary>
	/// Extracts an icon from a resource specifier ("file-path,resource-index", e.g. a device's or a session's icon path),
	/// or from the file itself if it's a plain path. Null if neither works.
	/// </summary>
	public static Icon? GetFromResourcePath(string? iconPath)
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

	/// <summary>
	/// Like <see cref="GetFromResourcePath"/> but at exactly <paramref name="size"/> x <paramref name="size"/> (see <see cref="GetFromDllOfSize"/>);
	/// null if the path doesn't name an icon resource or a .ico file
	/// </summary>
	public static Icon? GetFromResourcePathOfSize(string? iconPath, int size)
	{
		if(string.IsNullOrEmpty(iconPath))
			return null;

		int comma = iconPath.LastIndexOf(',');
		if(comma > 0 && int.TryParse(iconPath.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int resourceId))
			return GetFromDllOfSize(Environment.ExpandEnvironmentVariables(iconPath[..comma]), resourceId, size);
		string path = Environment.ExpandEnvironmentVariables(iconPath);
		if(!path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
			return null;
		try
		{
			//the file's frame closest to the size
			return new Icon(path, size, size);
		}
		catch
		{
			return null;
		}
	}

	public static ImageSource? GetAsImageSource(this Icon icon)
	{
		try { return ToImageSource(icon.Handle); }
		catch { return null; }
	}

	private static ImageSource ToImageSource(IntPtr hIcon)
	{
		var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		if(source.CanFreeze)
			source.Freeze();
		return source;
	}

	/// <summary>
	/// A file's icon (by its index among the file's icons) as a frozen image, straight from the extracted handle;
	/// null if the file has no such icon
	/// </summary>
	private static ImageSource? ReadIconSource(string filePath, int index)
	{
		Span<HICON> extracted = stackalloc HICON[1];
		PInvoke.ExtractIconEx(filePath, index, extracted, default);
		if(extracted[0].IsNull)
			return null;
		try { return ToImageSource(extracted[0]); }
		finally { PInvoke.DestroyIcon(extracted[0]); }
	}

	public static Icon? GetFromFilePath(string filePath)
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
	internal static ImageSource? GetIconSource(string path, Func<string, Icon?> extract)
		=> GetCached(path, p =>
		{
			using var icon = extract(p);
			return icon?.GetAsImageSource();
		});

	/// <summary>
	/// An app's icon from its executable as a frozen image: the file's first icon, or the first one that shows something when the first
	/// is empty (some programs have an empty first icon and give their window another one, e.g. Warcraft III's war3.exe).
	/// Null if none of them shows anything
	/// </summary>
	internal static ImageSource? GetAppIconSource(string exePath) => GetCached(exePath, ReadAppIcon);

	/// <summary>
	/// The icon of an app's main window as a frozen image (see <see cref="NativeMethods.GetAppWindowIcon"/>); null if it has none
	/// that shows something. Not cached: one executable can give its window different icons (war3.exe shows The Frozen Throne's
	/// icon when it runs that expansion)
	/// </summary>
	internal static ImageSource? GetWindowIconSource(uint processId)
	{
		IntPtr icon = NativeMethods.GetAppWindowIcon(processId);
		if(icon == IntPtr.Zero)
			return null;
		try
		{
			var source = ToImageSource(icon);
			return HasVisiblePixels(source) ? source : null;
		}
		//the app can destroy its icon at any moment
		catch { return null; }
	}

	private static ImageSource? GetCached(string path, Func<string, ImageSource?> read)
	{
		if(iconSources.TryGetValue(path, out var cached))
			return cached;
		var source = read(path);
		return source == null ? null : iconSources.GetOrAdd(path, source);
	}

	//an empty first icon is rare: the file's other icons are looked at only then, and only the first few
	private const int MaxIconsTried = 8;

	//ExtractIconEx reads the exe's own icons, about three times faster than Icon.ExtractAssociatedIcon, which goes through the shell's
	//file associations; an exe without icons gets the session's icon or the generic one from the caller instead
	private static ImageSource? ReadAppIcon(string exePath)
	{
		var first = ReadIconSource(exePath, 0);
		if(first == null || HasVisiblePixels(first))
			return first;
		//the number of icons in the file
		int count = (int)Math.Min(PInvoke.ExtractIconEx(exePath, -1, default, default), MaxIconsTried);
		for(int index = 1; index < count; index++)
		{
			var source = ReadIconSource(exePath, index);
			if(source != null && HasVisiblePixels(source))
				return source;
		}
		return null;
	}

	/// <summary>Whether any pixel of the image isn't fully transparent</summary>
	internal static bool HasVisiblePixels(ImageSource source)
	{
		if(source is not BitmapSource bitmap)
			return true;
		if(bitmap.Format != PixelFormats.Bgra32)
			bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
		int stride = bitmap.PixelWidth * 4;
		var pixels = new byte[stride * bitmap.PixelHeight];
		bitmap.CopyPixels(pixels, stride, 0);
		for(int alpha = 3; alpha < pixels.Length; alpha += 4)
		{
			if(pixels[alpha] != 0)
				return true;
		}
		return false;
	}

	private static ImageSource? genericExeIconSource;

	/// <summary>Windows' generic program icon as a frozen image, for apps whose own icon can't be read</summary>
	internal static ImageSource? GenericExeIconSource
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

	private static Icon? GetGenericExeIcon()
	{
		//the icon is located in this file since win 10 1903 by index 11
		string imageres = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SystemResources\imageres.dll.mun");
		return GetFromDll(imageres, 11);
	}
}

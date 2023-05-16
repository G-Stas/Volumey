using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Volumey.Helper
{
	static class IconHelper
	{
		public static Icon GetFromDll(string filePath, int resourceId, bool largeIcon = true)
		{
			Icon icon = null;
			
			if(filePath == null)
				return null;
			
			App.Current.Dispatcher.Invoke(() =>
			{
				IntPtr hIcon;
				if(largeIcon)
					NativeMethods.ExtractIconEx(filePath, resourceId, out hIcon, IntPtr.Zero, 1);
				else
					NativeMethods.ExtractIconEx(filePath, resourceId, IntPtr.Zero, out hIcon, 1);

				if(hIcon != IntPtr.Zero)
				{
					icon = Icon.FromHandle(hIcon);
				}
			});
			return icon;
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
		internal static Icon GenericExeIcon => genericExeIcon ??= GetGenericExeIcon();

		private static Icon GetGenericExeIcon()
		{
			//the icon is located in this file since win 10 1903 @"C:\Windows\SystemResources\imageres.dll.mun" by index 11
			return GetFromDll(@"C:\Windows\SystemResources\imageres.dll.mun", 11);
		}
	}
}
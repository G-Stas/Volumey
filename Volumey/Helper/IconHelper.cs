using System;
using System.Diagnostics;
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
		public static ImageSource GetFromProcess(Process process)
		{
			if(process == null)
				return null;
			ImageSource iSource = null;
			App.Current.Dispatcher.Invoke(() =>
			{
				//get application icon by full path to its exe file
				var fileName = process.GetMainModuleFileName();
				Icon icon = Icon.ExtractAssociatedIcon(fileName);
				if(icon != null)
				{
					iSource = GetImageSourceFromIcon(icon);
					NativeMethods.DestroyIcon(icon.Handle);
					icon.Dispose();
				}
			});
			return iSource;
		}

		public static ImageSource GetFromDll(string filePath, int resourceId, bool largeIcon = true)
		{
			ImageSource iSource = null;
			
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
					Icon icon = Icon.FromHandle(hIcon);
					iSource = GetImageSourceFromIcon(icon);

					NativeMethods.DestroyIcon(icon.Handle);
					icon.Dispose();
				}
			});
			return iSource;
		}

		public static ImageSource GetImageSourceFromIcon(Icon icon)
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
		
		public static ImageSource GetFromFilePath(string filePath)
		{
			if(File.Exists(filePath))
				return GetImageSourceFromIcon(Icon.ExtractAssociatedIcon(filePath));
			return null;
		}

		private static ImageSource genericExeIcon;
		internal static ImageSource GenericExeIcon => genericExeIcon ??= GetGenericExeIcon();

		private static ImageSource GetGenericExeIcon()
		{
			//the icon is located in this file since win 10 1903 @"C:\Windows\SystemResources\imageres.dll.mun" by index 11
			return GetFromDll(@"C:\Windows\SystemResources\imageres.dll.mun", 11);
		}
	}
}
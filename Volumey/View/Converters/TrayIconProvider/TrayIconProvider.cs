using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Resources;
using log4net;

namespace Volumey.View.Converters;

public partial class TrayIconConverter
{
	internal abstract class TrayIconProvider
	{
		protected virtual Icon High => null;
		protected virtual Icon Mid => null;
		protected virtual Icon Low => null;
		protected virtual Icon Mute => null;
		protected virtual Icon NoDevice => null;
		protected virtual ILog Logger => null;

		public Icon GetIcon(IconType type)
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
	internal static Icon GetAsIcon(this ImageSource src)
	{
		if(src == null) return null;
		Uri uri = new Uri(src.ToString());
		StreamResourceInfo streamInfo = Application.GetResourceStream(uri);

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

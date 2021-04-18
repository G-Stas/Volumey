using System.Windows.Media.Imaging;
using System;
using log4net;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter
	{
		internal abstract class TrayIconProvider
		{
			protected virtual BitmapImage High => null;
			protected virtual BitmapImage Mid => null;
			protected virtual BitmapImage Low => null;
			protected virtual BitmapImage Mute => null;
			protected virtual BitmapImage NoDevice => null;
			protected virtual ILog Logger => null;
			
			public BitmapImage GetIcon(IconType type)
			{
				try
				{
					return type switch
					           {
						           IconType.High => this.High,
						           IconType.Mid => this.Mid,
						           IconType.Low => this.Low,
						           IconType.Mute => this.Mute,
						           IconType.NoDevice => this.NoDevice,
						           _ => null
					           };
				}
				catch(Exception e)
				{
					this.Logger?.Error($"Failed to get icon of type {type.ToString()}", e);
					return null;
				}
			}
		}
	}
	
}
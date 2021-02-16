using System.Windows.Media.Imaging;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter
	{
		private abstract class TrayIconProvider
		{
			protected virtual BitmapImage High => null;
			protected virtual BitmapImage Mid => null;
			protected virtual BitmapImage Low => null;
			protected virtual BitmapImage Mute => null;
			protected virtual BitmapImage NoDevice => null;
			
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
				catch
				{
					return null;
				}
			}
		}
	}
	
}
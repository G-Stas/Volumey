using System.Windows.Media.Imaging;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter
	{
		private class DarkIconProvider : TrayIconProvider
		{
			private BitmapImage trayHigh;
			protected override BitmapImage High => trayHigh ??= App.Current.FindResource("TrayHighDark") as BitmapImage;

			private BitmapImage trayMid;
			protected override BitmapImage Mid => trayMid ??= App.Current.FindResource("TrayMidDark") as BitmapImage;

			private BitmapImage trayLow;
			protected override BitmapImage Low => trayLow ??= App.Current.FindResource("TrayLowDark") as BitmapImage;

			private BitmapImage trayMute;
			protected override BitmapImage Mute => trayMute ??= App.Current.FindResource("TrayMuteDark") as BitmapImage;

			private BitmapImage trayNoDevice;
			protected override BitmapImage NoDevice => trayNoDevice ??= App.Current.FindResource("TrayNoDeviceDark") as BitmapImage;
		}
	}
}
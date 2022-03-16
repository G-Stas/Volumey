using System.Drawing;
using System.Windows.Media.Imaging;
using log4net;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter
	{
		internal class DarkIconProvider : TrayIconProvider
		{
			private Icon trayHigh;
			protected override Icon High => trayHigh ??= (App.Current.FindResource("TrayHighDark") as BitmapImage).GetAsIcon();

			private Icon trayMid;
			protected override Icon Mid => trayMid ??= (App.Current.FindResource("TrayMidDark") as BitmapImage).GetAsIcon();

			private Icon trayLow;
			protected override Icon Low => trayLow ??= (App.Current.FindResource("TrayLowDark") as BitmapImage).GetAsIcon();

			private Icon trayMute;
			protected override Icon Mute => trayMute ??= (App.Current.FindResource("TrayMuteDark") as BitmapImage).GetAsIcon();

			private Icon trayNoDevice;
			protected override Icon NoDevice => trayNoDevice ??= (App.Current.FindResource("TrayNoDeviceDark") as BitmapImage).GetAsIcon();

			private ILog _logger;
			protected override ILog Logger => _logger ??= LogManager.GetLogger(typeof(DarkIconProvider));
		}
	}
}
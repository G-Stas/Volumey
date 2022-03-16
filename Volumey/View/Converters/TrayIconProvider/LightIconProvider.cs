using System.Drawing;
using System.Windows.Media.Imaging;
using log4net;

namespace Volumey.View.Converters
{
	public partial class TrayIconConverter
	{
		internal class LightIconProvider : TrayIconProvider
		{
			private Icon trayHigh;
			protected override Icon High => trayHigh ??= (App.Current.FindResource("TrayHigh") as BitmapImage).GetAsIcon();

			private Icon trayMid;
			protected override Icon Mid => trayMid ??= (App.Current.FindResource("TrayMid") as BitmapImage).GetAsIcon();

			private Icon trayLow;
			protected override Icon Low => trayLow ??= (App.Current.FindResource("TrayLow") as BitmapImage).GetAsIcon();

			private Icon trayMute;
			protected override Icon Mute => trayMute ??= (App.Current.FindResource("TrayMute") as BitmapImage).GetAsIcon();

			private Icon trayNoDevice;
			protected override Icon NoDevice => trayNoDevice ??= (App.Current.FindResource("TrayNoDevice") as BitmapImage).GetAsIcon();
			
			private ILog _logger;
			protected override ILog Logger => _logger ??= LogManager.GetLogger(typeof(LightIconProvider));
		}
	}

}
using System;
using System.Windows;
using System.Windows.Controls;
using log4net;

namespace Volumey.View.DialogContent
{
	public partial class UpdateDialog : DialogContent
	{ 
		public override event Action<DialogContent> DialogResult;
		
		public UpdateDialog() => InitializeComponent();

		private ILog logger = LogManager.GetLogger(typeof(UpdateDialog));

		private async void OnResultButtonClick(object sender, RoutedEventArgs e)
		{
			try
			{
				if(sender is Button btn)
				{
					if(btn.Tag.Equals("Y"))
						await Windows.System.Launcher
						             .LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9MZCQ03MX0S3"));
					logger.Info($"Displaying an update request. Result: [{btn.Tag}]");
				}
			}
			catch(Exception exc) { logger.Error("An exception occurred during update dialog result processing", exc); }
			this.DialogResult?.Invoke(this);
		}
	}
}
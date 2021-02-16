using System;
using System.Windows;
using System.Windows.Controls;
using log4net;
using Volumey.DataProvider;

namespace Volumey.View.DialogContent
{
	public partial class ReviewDialog : DialogContent
	{
		public override event Action<DialogContent> DialogResult;

		public ReviewDialog() => InitializeComponent();

		private ILog logger = LogManager.GetLogger(typeof(ReviewDialog));
		
		private async void OnResultButtonClick(object sender, RoutedEventArgs e)
		{
			try
			{
				if(sender is Button btn)
				{
					switch(btn.Tag)
					{
						case"Y": //yes
						{
							await Windows.System.Launcher
							             .LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9MZCQ03MX0S3"));
							SettingsProvider.Settings.UserHasRated = true;
							break;
						}
						case"N": //no
						{
							SettingsProvider.Settings.UserHasRated = true;
							break;
						}
						case"L": //later
						{
							//postpone a review request
							SettingsProvider.Settings.FirstLaunchDate = DateTime.Today;
							break;
						}
					}

					await SettingsProvider.SaveSettings().ConfigureAwait(false);
					logger.Info($"Displaying a review request. Result: [{btn.Tag}]");
				}
			}
			catch(Exception exc) { logger.Error("An exception occurred during review dialog result processing", exc); }
			DialogResult?.Invoke(this);
		}
	}
}
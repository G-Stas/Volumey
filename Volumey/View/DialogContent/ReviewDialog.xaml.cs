using System;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;

namespace Volumey.View.DialogContent
{
	public partial class ReviewDialog
	{
		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(ReviewDialog));

		public ICommand PrimaryCommand { get; }
		public ICommand SecondaryCommand { get; }
		public ICommand CloseCommand { get; }

		public ReviewDialog()
		{
			this.PrimaryCommand = new ActionCommand(() => {OnReviewResult("Y");});
			this.SecondaryCommand = new ActionCommand(() => {OnReviewResult("L");});
			this.CloseCommand = new ActionCommand(() => {OnReviewResult("N");});

			InitializeComponent();
		}

		private async void OnReviewResult(string param)
		{
			try
			{
				switch(param)
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

				Task.Run(() =>
				{
					_ = SettingsProvider.SaveSettings();
					Logger.Info($"Displaying a review request. Result: [{param}]");
				});
			}
			catch(Exception exc) { Logger.Error("An exception occurred during review dialog result processing", exc); }
		}
	}
}
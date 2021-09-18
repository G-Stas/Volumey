using log4net;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;

namespace Volumey.View.DialogContent
{
	public partial class UpdateDialog
	{
		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(UpdateDialog));

		public ICommand PrimaryCommand { get; }
		public ICommand CloseCommand { get; }

		public UpdateDialog()
		{
			this.PrimaryCommand = new ActionCommand(() => { OnReviewResult("Y"); });
			this.CloseCommand = new ActionCommand(() => { OnReviewResult("L"); });
			InitializeComponent();
		}

		private async void OnReviewResult(string param)
		{
			try
			{
				if(param.Equals("Y"))
					await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9MZCQ03MX0S3"));

				Task.Run(() =>
				{
					Logger.Info($"Displaying an update request. Result: [{param}]");
				});
			}
			catch(Exception exc) { Logger.Error("An exception occurred during update dialog result processing", exc); }
		}
	}
}
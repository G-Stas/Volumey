using System;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Helper;

namespace Volumey.View.DialogContent;

public partial class UpdateDialog
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(UpdateDialog));

	public ICommand PrimaryCommand { get; }
	public ICommand CloseCommand { get; }

	public UpdateDialog()
	{
		PrimaryCommand = new ActionCommand(() => OnReviewResult("Y").Forget(Logger, "Failed to process the update dialog result"));
		CloseCommand = new ActionCommand(() => OnReviewResult("L").Forget(Logger, "Failed to process the update dialog result"));
		InitializeComponent();
	}

	private async Task OnReviewResult(string param)
	{
		if(param.Equals("Y"))
			await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9MZCQ03MX0S3"));
		Logger.Info($"Displaying an update request. Result: [{param}]");
	}
}

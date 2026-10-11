using System;
using System.Threading.Tasks;
using log4net;
using Volumey.Helper;
using Wpf.Ui.Controls;

namespace Volumey.View.DialogContent;

/// <summary>
/// Store build: offers the Store page when an update is available
/// </summary>
public partial class UpdateDialog
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(UpdateDialog));

	public UpdateDialog()
	{
		//WPF-UI's dialog style is implicit, which doesn't reach a subclass (without it: no dialog chrome, no buttons)
		SetResourceReference(StyleProperty, typeof(ContentDialog));
		InitializeComponent();
	}

	private void OnButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		=> OnReviewResult(args.Button == ContentDialogButton.Primary ? "Y" : "L").Forget(Logger, "Failed to process the update dialog result");

	private static async Task OnReviewResult(string param)
	{
		if(param.Equals("Y"))
			await Windows.System.Launcher.LaunchUriAsync(new Uri(UpdateHelper.StorePage));
		Logger.Info($"Displaying an update request. Result: [{param}]");
	}
}

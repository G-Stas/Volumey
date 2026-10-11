using System;
using System.Threading.Tasks;
using System.Windows;
using Volumey.Helper;
using Volumey.View.DialogContent;

namespace Volumey.View;

public partial class MainView
{
	private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
	{
		try
		{
			PlaceWindow();
		}
		catch(Exception e)
		{
			Logger.Error("Failed to position the window", e);
		}
#if(STORE)
		CheckForStoreUpdate().Forget(Logger, "Failed to check for update");
#else
		CheckForUpdate();
#endif
	}

	private void CheckForUpdate()
	{
		try
		{
			UpdateHelper.StartAutoUpdater(userAsked: false);
		}
		catch(Exception e)
		{
			Logger.Error("Failed to check for update", e);
		}
	}

	//runs on the UI thread: the awaits resume there, which the dialog needs
	private async Task CheckForStoreUpdate()
	{
		if(await UpdateHelper.CheckIfUpdateIsAvailable())
			await DisplayContentDialog(new UpdateDialog());
	}
}

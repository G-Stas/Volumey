#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
using Volumey.Helper;
using Volumey.View.DialogContent;

namespace Volumey.View;

public sealed partial class MainView
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
#if(DEBUG)
		//the Store build's update dialog, to look at it in a dev run (its OK button opens the Store page)
		if(Environment.GetEnvironmentVariable("VOLUMEY_SHOW_UPDATE_DIALOG") == "1")
			DisplayContentDialog(new UpdateDialog()).Forget(Logger, "Failed to show the update dialog");
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

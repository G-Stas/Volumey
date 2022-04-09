using System;
using System.Threading.Tasks;
using System.Windows;
using AutoUpdaterDotNET;
using Volumey.Helper;
using Volumey.View.DialogContent;

namespace Volumey.View
{
	public partial class MainView
	{
		private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
		{
			SetWindowPosition();
			try
			{
				#if(STORE)
					await CheckForStoreUpdate();
				#else
					await CheckForUpdate();
				#endif
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to check for update", e);
			}
		}

		private async Task CheckForUpdate()
		{
			AutoUpdater.ShowSkipButton = false;
			AutoUpdater.ReportErrors = false;
			AutoUpdater.LetUserSelectRemindLater = false;
			AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
			AutoUpdater.RemindLaterAt = 1;
			await App.Current.Dispatcher.InvokeAsync(() => AutoUpdater.Start("https://raw.githubusercontent.com/G-Stas/Volumey/main/LatestUpdateInfo.xml"));
		}

		private async Task CheckForStoreUpdate()
		{
			var updateIsAvailable = await UpdateHelper.CheckIfUpdateIsAvailable().ConfigureAwait(false);
			if(updateIsAvailable)
				await App.Current.Dispatcher.InvokeAsync(async () => await DisplayContentDialog(new UpdateDialog()));
		}
	}
}
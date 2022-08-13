using System.Windows;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class AudioProcessStateNotificationMediator
	{
		internal void NotifyAudioStateChange(IManagedMasterAudioSession sender)
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.ShowNotification(sender);
			});
		}

		internal void NotifyOfDisposing(IManagedMasterAudioSession sender)
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.CloseNotification(sender);
			});
		}
	}
}
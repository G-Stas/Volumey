using System.Windows;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class AudioProcessStateNotificationMediator : IAudioProcessStateMediator
	{
		public void NotifyAudioStateChange(IManagedMasterAudioSession sender)
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.ShowNotification(sender);
			});
		}

		public void NotifyOfDisposing(IManagedMasterAudioSession sender)
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.CloseNotification(sender);
			});
		}
	}
}
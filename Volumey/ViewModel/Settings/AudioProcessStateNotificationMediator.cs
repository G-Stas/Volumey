using System.Windows;
using System.Windows.Threading;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class AudioProcessStateNotificationMediator : IAudioProcessStateMediator
	{
		private static Dispatcher UiDispatcher => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		public void NotifyAudioStateChange(IManagedMasterAudioSession sender)
		{
			UiDispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.ShowNotification(sender);
			});
		}

		public void NotifyOfDisposing(IManagedMasterAudioSession sender)
		{
			UiDispatcher.InvokeAsync(() =>
			{
				NotificationManagerHelper.CloseNotification(sender);
			});
		}
	}
}
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class AudioSessionStateNotificationMediator
	{
		internal void NotifyAudioStateChange(IManagedAudioSession sender)
		{
			NotificationManagerHelper.ShowNotification(sender);
		}

		internal void NotifyOfDisposing(IManagedAudioSession sender)
		{
			NotificationManagerHelper.CloseNotification(sender);
		}
	}
}
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class AudioProcessStateNotificationMediator
	{
		internal void NotifyAudioStateChange(IManagedMasterAudioSession sender)
		{
			NotificationManagerHelper.ShowNotification(sender);
		}

		internal void NotifyOfDisposing(IManagedMasterAudioSession sender)
		{
			NotificationManagerHelper.CloseNotification(sender);
		}
	}
}
using System.Windows;
using System.Windows.Threading;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public class AudioProcessStateNotificationMediator : IAudioProcessStateMediator
{
	private static Dispatcher UiDispatcher => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	private readonly INotificationService notifications;

	public AudioProcessStateNotificationMediator(INotificationService notifications)
	{
		this.notifications = notifications;
	}

	public void NotifyAudioStateChange(IManagedMasterAudioSession sender)
	{
		UiDispatcher.InvokeAsync(() =>
		{
			notifications.ShowNotification(sender);
		});
	}

	public void NotifyOfDisposing(IManagedMasterAudioSession sender)
	{
		UiDispatcher.InvokeAsync(() =>
		{
			notifications.CloseNotification(sender);
		});
	}
}

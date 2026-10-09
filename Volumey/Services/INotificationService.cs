using System.Windows.Media;
using Notification.Wpf.Controls;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Services;

/// <summary>
/// The on-screen volume popups (OSD). As the models' <see cref="IAudioProcessStateMediator"/> it shows and closes them on the UI thread.
/// </summary>
public interface INotificationService : IAudioProcessStateMediator
{
	void ShowNotification(IManagedMasterAudioSession session);

	void CloseNotification(IManagedMasterAudioSession session);

	void ShowNotificationExample();

	void CloseNotificationExample();

	void SetWindowWorkArea(double left, double top, double width, double height);

	void ChangePosition(NotificationPosition newPosition);

	void SetVerticalIndent(int indent);

	void SetHorizontalIndent(int indent);

	void SetNotificationDisplayTime(int timeInSeconds);

	void UpdateColors(Brush background, Brush foreground, Brush hover);
}

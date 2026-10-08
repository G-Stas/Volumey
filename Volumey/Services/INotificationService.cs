using System.Windows.Media;
using Notification.Wpf.Controls;
using Volumey.Model;

namespace Volumey.Services
{
	/// <summary>
	/// The on-screen volume popups (OSD).
	/// </summary>
	public interface INotificationService
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
}

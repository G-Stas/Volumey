using System.Windows.Media;
using Notification.Wpf.Controls;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.Services
{
	/// <summary>
	/// <see cref="INotificationService"/> over <see cref="NotificationManagerHelper"/> (the Notification.Wpf fork).
	/// </summary>
	internal sealed class NotificationService : INotificationService
	{
		public void ShowNotification(IManagedMasterAudioSession session) => NotificationManagerHelper.ShowNotification(session);

		public void CloseNotification(IManagedMasterAudioSession session) => NotificationManagerHelper.CloseNotification(session);

		public void ShowNotificationExample() => NotificationManagerHelper.ShowNotificationExample();

		public void CloseNotificationExample() => NotificationManagerHelper.CloseNotificationExample();

		public void SetWindowWorkArea(double left, double top, double width, double height)
			=> NotificationManagerHelper.SetWindowWorkArea(left, top, width, height);

		public void ChangePosition(NotificationPosition newPosition) => NotificationManagerHelper.ChangePosition(newPosition);

		public void SetVerticalIndent(int indent) => NotificationManagerHelper.SetVerticalIndent(indent);

		public void SetHorizontalIndent(int indent) => NotificationManagerHelper.SetHorizontalIndent(indent);

		public void SetNotificationDisplayTime(int timeInSeconds) => NotificationManagerHelper.SetNotificationDisplayTime(timeInSeconds);

		public void UpdateColors(Brush background, Brush foreground, Brush hover) => NotificationManagerHelper.UpdateColors(background, foreground, hover);
	}
}

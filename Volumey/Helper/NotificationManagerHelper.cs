using System;
using log4net;
using Notification.Wpf;
using Notification.Wpf.Controls;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;
using Volumey.View.Controls;
using Brush = System.Windows.Media.Brush;

namespace Volumey.Helper;

internal static class NotificationManagerHelper
{
	private static NotificationManager _notificationManager;
	private static TimeSpan NotificationDisplayTime;

	private const string _exampleNotificationId = "E7D8A20F-94C6-4027-A53A-267938F9FD90";
	public const int MinIndent = 15;
	public const int MaxIndent = 500;

	private static ILog _logger;
	private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(NotificationManagerHelper));

	static NotificationManagerHelper()
	{
		_notificationManager = new NotificationManager();
		var settings = SettingsProvider.Settings;
		_notificationManager.ChangeNotificationAreaPosition(settings.Notifications.Position);
		_notificationManager.SetNotificationAreaHorizontalIndent(settings.Notifications.HorizontalIndent);
		_notificationManager.SetNotificationAreaVerticalIndent(settings.Notifications.VerticalIndent);
		_notificationManager.SetNotificationsMaxDisplayCount(3);

		NotificationDisplayTime = TimeSpan.FromSeconds(settings.Notifications.DisplayTimeSeconds);
	}

	internal static void ShowNotification(IManagedMasterAudioSession session)
	{
		try
		{
			if(_notificationManager.IsDisplayed(session.Id, resetDisplayTimer: true))
				return;
			var content = new VolumeNotificationContent(session);
			_notificationManager.ShowNotification(content, session.Id, true, NotificationDisplayTime);
		}
		catch(ObjectDisposedException) { }
		catch(Exception e)
		{
			Logger.Error("Failed to display notification", e);
		}
	}

	internal static void CloseNotification(IManagedMasterAudioSession session)
	{
		try
		{
			_notificationManager.CloseNotification(session.Id);
		}
		catch(ObjectDisposedException) { }
		catch(Exception e)
		{
			Logger.Error("Failed to close notification", e);
		}
	}

	internal static void ShowNotificationExample()
	{
		try
		{
			IManagedMasterAudioSession dummy = new AudioProcessModel(50, false, "Application", default, string.Empty, IconHelper.CreateGenericExeIcon(), null, AppServices.Current.Hotkeys);
			var content = new VolumeNotificationContent(dummy);
			_notificationManager.ShowNotification(content, _exampleNotificationId, false, TimeSpan.MaxValue);
		}
		catch(ObjectDisposedException) { }
		catch(Exception e)
		{
			Logger.Error("Failed to display example notification", e);
		}
	}

	internal static void CloseNotificationExample()
	{
		try
		{
			_notificationManager.CloseNotification(_exampleNotificationId);
		}
		catch(ObjectDisposedException) { }
		catch(Exception e)
		{
			Logger.Error("Failed to close example notification", e);
		}
	}

	internal static void SetWindowWorkArea(double left, double top, double width, double height)
	{
		_notificationManager.SetWorkArea(left, top, width, height);
	}

	internal static void ChangePosition(NotificationPosition newPosition)
	{
		_notificationManager.ChangeNotificationAreaPosition(newPosition);

	}

	internal static void SetVerticalIndent(int indent)
	{
		_notificationManager.SetNotificationAreaVerticalIndent(indent);
	}

	internal static void SetHorizontalIndent(int indent)
	{
		_notificationManager.SetNotificationAreaHorizontalIndent(indent);
	}

	internal static void UpdateColors(Brush background, Brush foreground, Brush hover)
	{
		_notificationManager.UpdateColors(background, foreground, hover);
	}

	internal static void SetNotificationDisplayTime(int timeInSeconds)
	{
		NotificationDisplayTime = TimeSpan.FromSeconds(timeInSeconds);
	}
}

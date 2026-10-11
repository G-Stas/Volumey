using System;
using System.Windows;
using System.Windows.Threading;
using log4net;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;
using Volumey.View;

namespace Volumey.Services;

/// <summary>
/// <see cref="INotificationService"/> over the app's OSD window (<see cref="OsdWindow"/>), created on first use on the UI thread.
/// </summary>
internal sealed class NotificationService : INotificationService
{
	private const string ExampleKey = "E7D8A20F-94C6-4027-A53A-267938F9FD90";

	private static readonly ILog Logger = LogManager.GetLogger(typeof(NotificationService));
	private static Dispatcher UiDispatcher => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	private OsdWindow window;
	private TimeSpan displayTime;
	private IManagedMasterAudioSession example;

	private OsdWindow Window
	{
		get
		{
			if(window == null)
			{
				var settings = SettingsProvider.Settings.Notifications;
				window = new OsdWindow();
				window.SetPosition(settings.Position);
				window.SetIndents(settings.HorizontalIndent, settings.VerticalIndent);
				displayTime = TimeSpan.FromSeconds(settings.DisplayTimeSeconds);
			}
			return window;
		}
	}

	//the models call these from audio callbacks, on any thread
	public void NotifyAudioStateChange(IManagedMasterAudioSession sender)
	{
		//the "OSD notifications" switch: a model can still have the mediator from before it was turned off
		if(!SettingsProvider.Settings.Notifications.Enabled)
			return;
		UiDispatcher.InvokeAsync(() => ShowNotification(sender));
	}

	public void NotifyOfDisposing(IManagedMasterAudioSession sender)
		=> UiDispatcher.InvokeAsync(() => CloseNotification(sender));

	public void ShowNotification(IManagedMasterAudioSession session)
		=> Try(() => Window.Show(session.Id, session, displayTime), "Failed to display notification");

	public void CloseNotification(IManagedMasterAudioSession session)
		=> Try(() => window?.Close(session.Id), "Failed to close notification");

	public void ShowNotificationExample()
		=> Try(() =>
		{
			example ??= new AudioProcessModel(50, false, "Application", default, string.Empty, IconHelper.CreateGenericExeIcon(), null, AppServices.Current.Hotkeys);
			Window.Show(ExampleKey, example, null);
		}, "Failed to display example notification");

	public void CloseNotificationExample() => Try(() => window?.Close(ExampleKey), "Failed to close example notification");

	public void SetWindowWorkArea(double left, double top, double width, double height) => Window.SetWorkArea(left, top, width, height);

	public void ChangePosition(NotificationPosition newPosition) => Window.SetPosition(newPosition);

	public void SetIndents(int horizontal, int vertical) => Window.SetIndents(horizontal, vertical);

	public void SetNotificationDisplayTime(int timeInSeconds)
	{
		_ = Window;
		displayTime = TimeSpan.FromSeconds(timeInSeconds);
	}

	private static void Try(Action action, string failure)
	{
		try
		{
			action();
		}
		catch(Exception e)
		{
			Logger.Error(failure, e);
		}
	}
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using Notification.Wpf.Controls;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public class NotificationViewModel : INotifyPropertyChanged
{
	public NotificationPosition[] Positions { get; } =
	{
		NotificationPosition.TopLeft,
		NotificationPosition.TopCenter,
		NotificationPosition.TopRight,
		NotificationPosition.BottomLeft,
		NotificationPosition.BottomCenter,
		NotificationPosition.BottomRight,
	};

	private NotificationPosition _selectedPosition;
	public NotificationPosition SelectedPosition
	{
		get => _selectedPosition;
		set
		{
			if(_selectedPosition == value)
				return;
			_selectedPosition = value;

			NotificationsPositionIsCentered = value == NotificationPosition.TopCenter || value == NotificationPosition.BottomCenter;
			OnPropertyChanged(nameof(NotificationsPositionIsCentered));

			_notifications.ChangePosition(value);
			OnPropertyChanged();

			//on the UI thread, so the settings' listeners run there too; the file is written later on the thread pool
			SettingsProvider.Settings.Notifications.Position = value;
		}
	}

	public bool NotificationsPositionIsCentered { get; set; }

	private bool _notificationsEnabled;
	public bool NotificationsEnabled
	{
		get => _notificationsEnabled;
		set
		{
			_notificationsEnabled = value;
			OnPropertyChanged();

			//on the UI thread: the hotkey view models react to it and change their lists of apps
			SettingsProvider.Settings.Notifications.Enabled = value;
		}
	}

	private bool _reactToAllChanges;
	public bool ReactToAllVolumeChanges
	{
		get => _reactToAllChanges;
		set
		{
			if(_reactToAllChanges == value)
				return;

			_reactToAllChanges = value;
			OnPropertyChanged();

			if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges != value)
				SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = value;

			//on the UI thread, like the default device changes it reacts to
			(value ? SetStateMediatorForDefaultDeviceProcesses() : ResetStateMediatorForDefaultDeviceProcesses())
				.Forget(Logger, "Failed to set/reset mediator for default device processes");
		}
	}

	private bool _previewIsOn;
	public bool PreviewIsOn
	{
		get => _previewIsOn;
		set
		{
			_previewIsOn = value;
			if(value)
				_notifications.ShowNotificationExample();
			else
				_notifications.CloseNotificationExample();
			OnPropertyChanged();
		}
	}

	private int _verticalIndent;
	public int VerticalIndent
	{
		get => _verticalIndent;
		set
		{
			if(value < MinIndent || value > MaxIndent)
			{
				_verticalIndent = MinIndent;
				OnPropertyChanged();
				return;
			}
			_verticalIndent = value;
			SettingsProvider.Settings.Notifications.VerticalIndent = value;
			_notifications.SetVerticalIndent(value);
			OnPropertyChanged();
		}
	}

	private int _horizontalIndent;
	public int HorizontalIndent
	{
		get => _horizontalIndent;
		set
		{
			if(value < MinIndent || value > MaxIndent)
			{
				_horizontalIndent = MinIndent;
				OnPropertyChanged();
				return;
			}
			_horizontalIndent = value;
			SettingsProvider.Settings.Notifications.HorizontalIndent = value;
			_notifications.SetHorizontalIndent(value);
			OnPropertyChanged();
		}
	}

	private int _displayTime;
	public int DisplayTime
	{
		get => _displayTime;
		set
		{
			if(value < MinDisplayTime || value > MaxDisplayTime)
				_displayTime = 3;
			else
				_displayTime = value;
			SettingsProvider.Settings.Notifications.DisplayTimeSeconds = _displayTime;
			_notifications.SetNotificationDisplayTime(_displayTime);
			OnPropertyChanged();
		}
	}

	public int MaxDisplayTime { get; } = 10;
	public int MinDisplayTime { get; } = 1;

	public ICommand UnloadedCommand { get; }
	public ICommand LoadedCommand { get; }

	public static int MinIndent => NotificationManagerHelper.MinIndent;
	public static int MaxIndent => NotificationManagerHelper.MaxIndent;

	private readonly ScreenSelection screens;

	/// <summary>
	/// The screen the OSD is shown on
	/// </summary>
	public ScreenInfo SelectedScreen
	{
		get => screens.Selected;
		set => screens.Select(value);
	}

	private void OnScreenShown(ScreenInfo screen)
	{
		OnPropertyChanged(nameof(SelectedScreen));
		//the OSD window takes device-independent units of the screen it's on: the screen's values are physical pixels,
		//so all four are divided by its scale factor (only the size was before, which put the OSD off a scaled screen, #76)
		double scale = screen.ScaleFactor > 0 ? screen.ScaleFactor : 1;
		_notifications.SetWindowWorkArea(screen.WorkingAreaLeft / scale, screen.WorkingAreaTop / scale, screen.Width / scale, screen.Height / scale);
	}

	public ObservableCollection<ScreenInfo> AllScreens => screens.All;

	private readonly IDeviceProvider _deviceProvider;
	private readonly INotificationService _notifications;
	private OutputDeviceModel _currentDefaultDevice;

	private static ILog _logger;
	private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(NotificationViewModel));

	internal NotificationViewModel(IDeviceProvider deviceProvider, INotificationService notifications, IScreenInfoProvider screenInfoProvider)
	{
		_deviceProvider = deviceProvider;
		_notifications = notifications;
		screens = new ScreenSelection(screenInfoProvider, () => SettingsProvider.Settings.Notifications.ScreenName,
			name => SettingsProvider.Settings.Notifications.ScreenName = name, () => App.Current?.Dispatcher, OnScreenShown, Logger);
		screens.ScreensChanging += () =>
		{
			if(PreviewIsOn)
				PreviewIsOn = false;
		};
		NotificationsEnabled = SettingsProvider.Settings.Notifications.Enabled;
		HorizontalIndent = SettingsProvider.Settings.Notifications.HorizontalIndent;
		VerticalIndent = SettingsProvider.Settings.Notifications.VerticalIndent;
		SelectedPosition = SettingsProvider.Settings.Notifications.Position;
		ReactToAllVolumeChanges = SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges;

		UnloadedCommand = new ActionCommand(() => PreviewIsOn = false);
		LoadedCommand = new ActionCommand(screens.Refresh);

		DisplayTime = SettingsProvider.Settings.Notifications.DisplayTimeSeconds;

		screens.Load();

		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
	}

	//e.g. a game switching the resolution, or a monitor that turns on late (#92)
	private void OnDisplaySettingsChanged(object sender, EventArgs e) => screens.Refresh();

	private async Task SetStateMediatorForDefaultDeviceProcesses()
	{

		_deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;

		_currentDefaultDevice = _deviceProvider.DefaultDevice;
		if(_currentDefaultDevice == null)
			return;

		_currentDefaultDevice.ProcessCreated += OnProcessCreated;
		_currentDefaultDevice.SetStateNotificationMediator(_notifications);

		foreach(AudioProcessModel proc in await _currentDefaultDevice.GetImmutableProcessesAsync())
			proc.SetStateMediator(_notifications);
	}

	private async Task ResetStateMediatorForDefaultDeviceProcesses()
	{

		_deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;

		if(_currentDefaultDevice == null)
			return;

		if(!_currentDefaultDevice.Master.AnyHotkeyRegistered)
			_currentDefaultDevice.ResetStateNotificationMediator();
		_currentDefaultDevice.ProcessCreated -= OnProcessCreated;

		foreach(AudioProcessModel proc in await _currentDefaultDevice.GetImmutableProcessesAsync())
		{
			if(!proc.AnyHotkeyRegistered)
				proc.ResetStateMediator();
		}

		_currentDefaultDevice = null;
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		=> SwitchDefaultDeviceAsync(newDevice).Forget(Logger, "Failed to move the OSD notifications to the new default device");

	private async Task SwitchDefaultDeviceAsync(OutputDeviceModel newDevice)
	{
		//swap before the first await: another switch may come in while this one waits
		var oldDevice = _currentDefaultDevice;
		_currentDefaultDevice = newDevice;

		if(oldDevice != null)
		{
			oldDevice.ProcessCreated -= OnProcessCreated;
			oldDevice.ResetStateNotificationMediator(force: true);
			//empty if the old device is already disposed (removed before the switch, e.g. on wake from sleep)
			foreach(AudioProcessModel proc in await oldDevice.GetImmutableProcessesAsync())
				proc.ResetStateMediator(force: true);
		}

		if(newDevice == null)
			return;

		newDevice.ProcessCreated += OnProcessCreated;
		newDevice.SetStateNotificationMediator(_notifications);
		foreach(AudioProcessModel proc in await newDevice.GetImmutableProcessesAsync())
			proc.SetStateMediator(_notifications);
	}

	private void OnProcessCreated(AudioProcessModel newProcess)
	{
		newProcess.SetStateMediator(_notifications);
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	~NotificationViewModel()
	{
		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
	}
}

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using log4net;
using Microsoft.Win32;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public partial class NotificationViewModel : ObservableObject
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
			OnPropertyChanged(nameof(SelectedPositionKey));

			_notifications.ChangePosition(value);
			OnPropertyChanged();

			//on the UI thread, so the settings' listeners run there too; the file is written later on the thread pool
			SettingsProvider.Settings.Notifications.Position = value;
		}
	}

	public bool NotificationsPositionIsCentered { get; set; }

	/// <summary>
	/// The localization key of the position's name ("Top right")
	/// </summary>
	public string SelectedPositionKey => $"Position_{SelectedPosition}";

	[ObservableProperty]
	public partial bool NotificationsEnabled { get; set; }

	//on the UI thread: the hotkey view models react to it and change their lists of apps
	partial void OnNotificationsEnabledChanged(bool value) => SettingsProvider.Settings.Notifications.Enabled = value;

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

	/// <summary>
	/// The sample app the page's preview and "Show on screen" show
	/// </summary>
	public IManagedMasterAudioSession PreviewSession => _notifications.Example;

	/// <summary>
	/// Shows the sample OSD where the real one would show, for the display time
	/// </summary>
	[RelayCommand]
	private void ShowPreview() => _notifications.ShowNotificationExample();

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
			_notifications.SetIndents(_horizontalIndent, value);
			OnPropertyChanged();
			ShowIndentPreview();
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
			_notifications.SetIndents(value, _verticalIndent);
			OnPropertyChanged();
			ShowIndentPreview();
		}
	}

	/// <summary>Set once the saved settings are loaded: only a distance the user changes shows the preview</summary>
	private bool _loaded;

	/// <summary>
	/// A changed distance (slider or box) shows the sample OSD where it now lands; each change restarts its display time
	/// </summary>
	private void ShowIndentPreview()
	{
		if(_loaded)
			_notifications.ShowNotificationExample();
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
	/// <summary>
	/// The display times to choose from, in seconds
	/// </summary>
	public int[] DisplayTimes { get; } = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
	public int MinDisplayTime { get; } = 1;

	public static int MinIndent => NotificationSettings.MinIndent;
	public static int MaxIndent => NotificationSettings.MaxIndent;

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
		//the screen's work area in physical pixels; the OSD window places itself with them (in WPF units
		//the OSD would be off when it moves between screens with different scales)
		_notifications.SetWindowWorkArea((int)screen.WorkingAreaLeft, (int)screen.WorkingAreaTop, (int)screen.Width, (int)screen.Height);
	}

	public ObservableCollection<ScreenInfo> AllScreens => screens.All;

	private readonly IDeviceProvider _deviceProvider;
	private readonly INotificationService _notifications;
	private OutputDeviceModel _currentDefaultDevice;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(NotificationViewModel));

	internal NotificationViewModel(IDeviceProvider deviceProvider, INotificationService notifications, IScreenInfoProvider screenInfoProvider)
	{
		_deviceProvider = deviceProvider;
		_notifications = notifications;
		screens = new ScreenSelection(screenInfoProvider, () => SettingsProvider.Settings.Notifications.ScreenName,
			name => SettingsProvider.Settings.Notifications.ScreenName = name, () => App.Current?.Dispatcher, OnScreenShown, Logger);
		screens.ScreensChanging += () =>
		{
			_notifications.CloseNotificationExample();
		};
		NotificationsEnabled = SettingsProvider.Settings.Notifications.Enabled;
		HorizontalIndent = SettingsProvider.Settings.Notifications.HorizontalIndent;
		VerticalIndent = SettingsProvider.Settings.Notifications.VerticalIndent;
		SelectedPosition = SettingsProvider.Settings.Notifications.Position;
		ReactToAllVolumeChanges = SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges;

		DisplayTime = SettingsProvider.Settings.Notifications.DisplayTimeSeconds;

		screens.Load();

		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		_loaded = true;
	}

	//e.g. a game switching the resolution, or a monitor that turns on late
	private void OnDisplaySettingsChanged(object sender, EventArgs e) => screens.Refresh();

	[RelayCommand]
	private void Loaded() => screens.Refresh();

	[RelayCommand]
	private void Unloaded() => _notifications.CloseNotificationExample();

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
}

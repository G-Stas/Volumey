using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using Notification.Wpf.Controls;
using Volumey.Helper;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings
{
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
			get => this._selectedPosition;
			set
			{
				if(this._selectedPosition == value)
					return;
				this._selectedPosition = value;

				NotificationsPositionIsCentered = value == NotificationPosition.TopCenter || value == NotificationPosition.BottomCenter;
				OnPropertyChanged(nameof(NotificationsPositionIsCentered));
				
				this._notifications.ChangePosition(value);
				OnPropertyChanged();
				
				if(SettingsProvider.Settings.Notifications.Position == value)
					return;
				var notificationSettings = SettingsProvider.Settings.Notifications;
				Task.Run(() =>
				{
					notificationSettings.Position = value;
				});
			}
		}
		
		public bool NotificationsPositionIsCentered { get; set; }

		private bool _notificationsEnabled;
		public bool NotificationsEnabled
		{
			get => this._notificationsEnabled;
			set
			{
				this._notificationsEnabled = value;
				OnPropertyChanged();
				
				if(SettingsProvider.Settings.Notifications.Enabled == value)
					return;
				var notificationSettings = SettingsProvider.Settings.Notifications;
				Task.Run(() =>
				{
					notificationSettings.Enabled = value;
				});
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

				Task.Run(async () =>
				{
					if(value)
						await SetStateMediatorForDefaultDeviceProcesses();
					else 
						await ResetStateMediatorForDefaultDeviceProcesses();
				}).ContinueWith(task =>
				{
					Logger.Error("Failed to set/reset mediator for default device processes", task?.Exception?.Flatten());
				}, TaskContinuationOptions.OnlyOnFaulted);
				
			}
		}

		private bool _previewIsOn;
		public bool PreviewIsOn
		{
			get => this._previewIsOn;
			set
			{
				this._previewIsOn = value;
				if(value)
					this._notifications.ShowNotificationExample();
				else
					this._notifications.CloseNotificationExample();
				OnPropertyChanged();
			}
		}

		private int _verticalIndent;
		public int VerticalIndent
		{
			get => this._verticalIndent;
			set
			{
				if(value < MinIndent || value > MaxIndent)
				{
					this._verticalIndent = MinIndent;
					OnPropertyChanged();
					return;
				}
				this._verticalIndent = value;
				SettingsProvider.Settings.Notifications.VerticalIndent = value;
				this._notifications.SetVerticalIndent(value);
				OnPropertyChanged();
			}
		}

		private int _horizontalIndent;
		public int HorizontalIndent
		{
			get => this._horizontalIndent;
			set
			{
				if(value < MinIndent || value > MaxIndent)
				{
					this._horizontalIndent = MinIndent;
					OnPropertyChanged();
					return;
				}
				this._horizontalIndent = value;
				SettingsProvider.Settings.Notifications.HorizontalIndent = value;
				this._notifications.SetHorizontalIndent(value);
				OnPropertyChanged();
			}
		}

		private int _displayTime;
		public int DisplayTime
		{
			get => this._displayTime;
			set
			{
				if(value < MinDisplayTime || value > MaxDisplayTime)
					this._displayTime = 3;
				else
					this._displayTime = value;
				SettingsProvider.Settings.Notifications.DisplayTimeSeconds = this._displayTime;
				this._notifications.SetNotificationDisplayTime(this._displayTime);
				OnPropertyChanged();
			}
		}

		public int MaxDisplayTime { get; } = 10;
		public int MinDisplayTime { get; } = 1;
		
		public ICommand UnloadedCommand { get; }  
		public ICommand LoadedCommand { get; }

		public static int MinIndent => NotificationManagerHelper.MinIndent;
		public static int MaxIndent => NotificationManagerHelper.MaxIndent;

		private ScreenInfo selectedScreen;
		public ScreenInfo SelectedScreen
		{
			get => selectedScreen;
			set
			{
				//only a listed screen is the user's choice: the combo box writes back an empty value while the list is replaced
				if(!AllScreens.Contains(value))
					return;
				ShowScreen(value);
				SettingsProvider.Settings.Notifications.ScreenName = value.Name;
			}
		}

		/// <summary>
		/// Uses the screen without remembering it, so a fallback screen doesn't replace the user's choice (#92)
		/// </summary>
		private void ShowScreen(ScreenInfo screen)
		{
			this.selectedScreen = screen;
			OnPropertyChanged(nameof(SelectedScreen));
			//the OSD window takes device-independent units of the screen it's on: the screen's values are physical pixels,
			//so all four are divided by its scale factor (only the size was before, which put the OSD off a scaled screen, #76)
			double scale = screen.ScaleFactor > 0 ? screen.ScaleFactor : 1;
			this._notifications.SetWindowWorkArea(screen.WorkingAreaLeft / scale, screen.WorkingAreaTop / scale, screen.Width / scale, screen.Height / scale);
		}

		public ObservableCollection<ScreenInfo> AllScreens { get; set; } = new ObservableCollection<ScreenInfo>();

		private readonly IScreenInfoProvider _screenInfoProvider;
		private readonly IDeviceProvider _deviceProvider;
		private readonly INotificationService _notifications;
		private OutputDeviceModel _currentDefaultDevice;
		private readonly AudioProcessStateNotificationMediator StateMediator;

		private static ILog _logger;
		private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(NotificationViewModel));

		internal NotificationViewModel(IDeviceProvider deviceProvider, INotificationService notifications, IScreenInfoProvider screenInfoProvider)
		{
			this._deviceProvider = deviceProvider;
			this._notifications = notifications;
			this._screenInfoProvider = screenInfoProvider;
			this.StateMediator = new AudioProcessStateNotificationMediator(notifications);
			this.NotificationsEnabled = SettingsProvider.Settings.Notifications.Enabled;
			this.HorizontalIndent = SettingsProvider.Settings.Notifications.HorizontalIndent;
			this.VerticalIndent = SettingsProvider.Settings.Notifications.VerticalIndent;
			this.SelectedPosition = SettingsProvider.Settings.Notifications.Position;
			this.ReactToAllVolumeChanges = SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges;
			
			this.UnloadedCommand = new ActionCommand(() => this.PreviewIsOn = false);
			this.LoadedCommand = new ActionCommand(this.CheckIfSelectedScreenIsAvailable);

			this.DisplayTime = SettingsProvider.Settings.Notifications.DisplayTimeSeconds;
			
			SetSelectedScreen();
			
			SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		}

		private void OnDisplaySettingsChanged(object sender, EventArgs e)
		{
			CheckIfSelectedScreenIsAvailable();
		}

		private void SetSelectedScreen()
		{
			foreach(var screen in _screenInfoProvider.GetAllScreensInfo())
				AllScreens.Add(screen);

			ShowScreen(ScreenInfo.Find(AllScreens, SettingsProvider.Settings.Notifications.ScreenName));
		}
		
		private void CheckIfSelectedScreenIsAvailable()
		{
			Task.Run(() =>
			{
				var screens = _screenInfoProvider.GetAllScreensInfo().ToList();
		
				if(screens.Count != AllScreens.Count || screens.Except(AllScreens).Any())
				{
					App.Current.Dispatcher.Invoke(() =>
					{
						if(PreviewIsOn)
							PreviewIsOn = false;
						AllScreens.Clear();
						foreach(var screen in screens)
							AllScreens.Add(screen);
					});
				}
		
				//picks the remembered screen again once it's connected, and updates the selection in the UI, which is lost when AllScreens is replaced
				ShowScreen(ScreenInfo.Find(screens, SettingsProvider.Settings.Notifications.ScreenName));
			}).ContinueWith(task =>
			{
				if(task.Exception != null)
				{
					Logger.Error("Failed to check the screen for availability", task.Exception.Flatten());
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}

		private async Task SetStateMediatorForDefaultDeviceProcesses()
		{
			
			_deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;

			_currentDefaultDevice = _deviceProvider.DefaultDevice;
			if(_currentDefaultDevice == null)
				return;

			_currentDefaultDevice.ProcessCreated += OnProcessCreated;
			_currentDefaultDevice.SetStateNotificationMediator(StateMediator);

			foreach(AudioProcessModel proc in await _currentDefaultDevice.GetImmutableProcessesAsync())
				proc.SetStateMediator(StateMediator);
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

		private async void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(_currentDefaultDevice != null)
			{
				foreach(AudioProcessModel proc in await _currentDefaultDevice.GetImmutableProcessesAsync())
				{
					proc.ResetStateMediator(force: true);
				}

				_currentDefaultDevice.ProcessCreated -= OnProcessCreated;
				_currentDefaultDevice.ResetStateNotificationMediator(force: true);
			}

			_currentDefaultDevice = newDevice;

			if(_currentDefaultDevice == null)
				return;
			
			_currentDefaultDevice.ProcessCreated += OnProcessCreated;
			_currentDefaultDevice.SetStateNotificationMediator(StateMediator);
			
			foreach(AudioProcessModel proc in await _currentDefaultDevice.GetImmutableProcessesAsync())
				proc.SetStateMediator(StateMediator);
		}

		private void OnProcessCreated(AudioProcessModel newProcess)
		{
			newProcess.SetStateMediator(StateMediator);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		~NotificationViewModel()
		{
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
		}
	}
}
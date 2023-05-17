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
				
				NotificationManagerHelper.ChangePosition(value);
				OnPropertyChanged();
				
				if(SettingsProvider.NotificationsSettings.Position == value)
					return;
				Task.Run(() =>
				{
					SettingsProvider.NotificationsSettings.Position = value;
					_ = SettingsProvider.SaveSettings();
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
				
				if(SettingsProvider.NotificationsSettings.Enabled == value)
					return;
				Task.Run(() =>
				{
					SettingsProvider.NotificationsSettings.Enabled = value;
					_ = SettingsProvider.SaveSettings();
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

				if(SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges != value)
				{
					SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges = value;
					Task.Run(async () =>
					{
						await SettingsProvider.SaveSettings();
					});
				}

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
					NotificationManagerHelper.ShowNotificationExample();
				else
					NotificationManagerHelper.CloseNotificationExample();
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
				SettingsProvider.NotificationsSettings.VerticalIndent = value;
				NotificationManagerHelper.SetVerticalIndent(value);
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
				SettingsProvider.NotificationsSettings.HorizontalIndent = value;
				NotificationManagerHelper.SetHorizontalIndent(value);
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
				SettingsProvider.NotificationsSettings.DisplayTimeInSeconds = this._displayTime;
				NotificationManagerHelper.SetNotificationDisplayTime(this._displayTime);
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
				selectedScreen = value;
				OnPropertyChanged();
				
				NotificationManagerHelper.SetWindowWorkArea(value.WorkingAreaLeft, value.WorkingAreaTop, value.Width / value.ScaleFactor, value.Height / value.ScaleFactor);
		        
				int index = AllScreens.IndexOf(value);
				if(SettingsProvider.NotificationsSettings.SelectedScreenIndex != index)
				{
					SettingsProvider.NotificationsSettings.SelectedScreenIndex = index;
					_ = SettingsProvider.SaveSettings();
				}
			}
		}

		public ObservableCollection<ScreenInfo> AllScreens { get; set; } = new ObservableCollection<ScreenInfo>();

		private IScreenInfoProvider _screenInfoProvider = new ScreenInfoProvider();
		private IDeviceProvider _deviceProvider;
		private OutputDeviceModel _currentDefaultDevice;
		private AudioProcessStateNotificationMediator StateMediator = new AudioProcessStateNotificationMediator();

		private static ILog _logger;
		private static ILog Logger => _logger ??= LogManager.GetLogger(typeof(NotificationViewModel));

		public NotificationViewModel()
		{
			this.NotificationsEnabled = SettingsProvider.NotificationsSettings.Enabled;
			this.HorizontalIndent = SettingsProvider.NotificationsSettings.HorizontalIndent;
			this.VerticalIndent = SettingsProvider.NotificationsSettings.VerticalIndent;
			this.SelectedPosition = SettingsProvider.NotificationsSettings.Position;
			this.ReactToAllVolumeChanges = SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges;
			
			this.UnloadedCommand = new ActionCommand(() => this.PreviewIsOn = false);
			this.LoadedCommand = new ActionCommand(this.CheckIfSelectedScreenIsAvailable);

			this.DisplayTime = SettingsProvider.NotificationsSettings.DisplayTimeInSeconds;
			
			SetSelectedScreen();
			
			SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		}

		private void OnDisplaySettingsChanged(object? sender, EventArgs e)
		{
			CheckIfSelectedScreenIsAvailable();
		}

		private void SetSelectedScreen()
		{
			foreach(var screen in _screenInfoProvider.GetAllScreensInfo())
				AllScreens.Add(screen);

			int selectedScreenIndex = SettingsProvider.NotificationsSettings.SelectedScreenIndex;
			if(AllScreens.Count - 1 < selectedScreenIndex || selectedScreenIndex < 0)
				SelectedScreen = _screenInfoProvider.GetPrimaryScreenInfo();
			else
				SelectedScreen = AllScreens[selectedScreenIndex];
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
		
				if(!screens.Contains(SelectedScreen))
				{
					SelectedScreen = AllScreens.FirstOrDefault(s => s.IsPrimary);
				}
				//Update the SelectedScreen property in the UI as it will be set to none if the AllScreens collection would change
				else
					OnPropertyChanged(nameof(SelectedScreen));
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
			if(_deviceProvider == null)
				_deviceProvider = DeviceProvider.GetInstance();
			
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
			if(_deviceProvider == null)
				_deviceProvider = DeviceProvider.GetInstance();

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
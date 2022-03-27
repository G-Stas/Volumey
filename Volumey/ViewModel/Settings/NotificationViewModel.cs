using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Notification.Wpf.Controls;
using Volumey.Helper;
using Volumey.DataProvider;

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
				NotificationManagerHelper.ChangePosition(value);
				OnPropertyChanged();
				Task.Run(() =>
				{
					if(SettingsProvider.NotificationsSettings.Position == value)
						return;
					SettingsProvider.NotificationsSettings.Position = value;
					_ = SettingsProvider.SaveSettings();
				});
			}
		}

		private bool _notificationsEnabled;
		public bool NotificationsEnabled
		{
			get => this._notificationsEnabled;
			set
			{
				this._notificationsEnabled = value;
				OnPropertyChanged();
				Task.Run(() =>
				{
					if(SettingsProvider.NotificationsSettings.Enabled == value)
						return;
					SettingsProvider.NotificationsSettings.Enabled = value;
					_ = SettingsProvider.SaveSettings();
				});
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

		public static int MinIndent => NotificationManagerHelper.MinIndent;
		public static int MaxIndent => NotificationManagerHelper.MaxIndent;

		public NotificationViewModel()
		{
			this.NotificationsEnabled = SettingsProvider.NotificationsSettings.Enabled;
			this.HorizontalIndent = SettingsProvider.NotificationsSettings.HorizontalIndent;
			this.VerticalIndent = SettingsProvider.NotificationsSettings.VerticalIndent;
			this.SelectedPosition = SettingsProvider.NotificationsSettings.Position;
			
			this.UnloadedCommand = new ActionCommand(() => this.PreviewIsOn = false);

			this.DisplayTime = SettingsProvider.NotificationsSettings.DisplayTimeInSeconds;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
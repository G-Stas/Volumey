using System.ComponentModel;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ModernWpf;
using Volumey.DataProvider;
using Volumey.Helper;

namespace Volumey.ViewModel
{
	public sealed class ThemeViewModel : INotifyPropertyChanged
	{
		public AppTheme[] AppThemes { get; } = {AppTheme.Light, AppTheme.Dark, AppTheme.System};

		private AppTheme selectedTheme;
		public AppTheme SelectedTheme
		{
			get => selectedTheme;
			set
			{
				selectedTheme = value;
				this.SetTheme(value);
				OnPropertyChanged();
			}
		}

		private AppTheme windowsTheme;
		/// <summary>
		/// Used to display tray icon with appropriate to current windows theme colors
		/// </summary>
		public AppTheme WindowsTheme
		{
			get => windowsTheme;
			set
			{
				windowsTheme = value;
				OnPropertyChanged();
			}
		}

		private Brush _darkBackground;
		private Brush _lightBackground;
		
		private Brush _darkForeground;
		private Brush _lightForeground;

		private Brush _buttonHoverDarkBackground;
		private Brush _lightButtonHoverBackground;

		private const string BackgroundKey = "SystemChromeMediumLowColorBrush";
		private const string ButtonHoverKey = "SystemControlHighlightListLowBrush";
		private const double BackgroundOpacity = 0.94;

		public ThemeViewModel()
		{
			this.WindowsTheme = SystemColorHelper.WindowsTheme;
			this.SelectedTheme = SettingsProvider.Settings.CurrentAppTheme;
			SystemColorHelper.WindowsThemeChanged += WindowsThemeChanged;
		}

		private void WindowsThemeChanged(AppTheme newTheme)
		{
			this.WindowsTheme = newTheme;
			if(this.selectedTheme == AppTheme.System)
				this.SetTheme(AppTheme.System);
		}

		private void SetTheme(AppTheme newTheme)
		{
			if(newTheme == AppTheme.System)
				newTheme = windowsTheme;

			if(newTheme == AppTheme.Dark && ThemeManager.Current.ApplicationTheme != ApplicationTheme.Dark)
			{
				ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
				UpdateNotificationColors(AppTheme.Dark);
			}
			else if(newTheme == AppTheme.Light && ThemeManager.Current.ApplicationTheme != ApplicationTheme.Light)
			{
				ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
				UpdateNotificationColors(AppTheme.Light);
			}
			
			Task.Run(() =>
			{
				if(SettingsProvider.Settings.CurrentAppTheme != this.selectedTheme)
				{
					SettingsProvider.Settings.CurrentAppTheme = this.selectedTheme;
					_ = SettingsProvider.SaveSettings();
				}
			});
		}

		private void UpdateNotificationColors(AppTheme newTheme)
		{
			if(newTheme == AppTheme.Dark)
			{
				if(_darkBackground == null)
				{
					if(App.Current.TryFindResource(BackgroundKey) is Brush brush)
					{
						this._darkBackground = brush.CloneCurrentValue();
						this._darkBackground.Opacity = BackgroundOpacity;
						this._darkBackground.Freeze();
					}
				}
				if(this._buttonHoverDarkBackground == null)
				{
					if(App.Current.TryFindResource(ButtonHoverKey) is Brush brush)
					{
						this._buttonHoverDarkBackground = brush.CloneCurrentValue();
						this._buttonHoverDarkBackground.Freeze();
					}
				}
				if(_darkForeground == null)
					this._darkForeground = Brushes.White;
				NotificationManagerHelper.UpdateColors(this._darkBackground, this._darkForeground, this._buttonHoverDarkBackground);
			}
			else if(newTheme == AppTheme.Light)
			{
				if(_lightBackground == null)
				{
					if(App.Current.TryFindResource(BackgroundKey) is Brush brush)
					{
						this._lightBackground = brush.CloneCurrentValue();
						this._lightBackground.Opacity = BackgroundOpacity;
						this._lightBackground.Freeze();
					}
				}
				if(this._lightButtonHoverBackground == null)
				{
					if(App.Current.TryFindResource(ButtonHoverKey) is Brush brush)
					{
						this._lightButtonHoverBackground = brush.CloneCurrentValue();
						this._lightButtonHoverBackground.Freeze();
					}
				}
				if(_lightForeground == null)
					this._lightForeground = Brushes.Black;
				NotificationManagerHelper.UpdateColors(this._lightBackground, this._lightForeground, this._lightButtonHoverBackground);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
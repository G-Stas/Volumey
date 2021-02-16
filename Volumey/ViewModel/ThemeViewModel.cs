using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ControlzEx.Theming;
using Volumey.DataProvider;
using Volumey.Helper;

namespace Volumey.ViewModel
{
	public sealed class ThemeViewModel : INotifyPropertyChanged
	{
		public AppTheme[] AppThemes { get; } = {AppTheme.Light, AppTheme.Dark};

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
		public AppTheme WindowsTheme
		{
			get => windowsTheme;
			set
			{
				windowsTheme = value;
				OnPropertyChanged();
			}
		}

		private SolidColorBrush accentBrush;
		public SolidColorBrush AccentBrush
		{
			get => accentBrush;
			set
			{
				accentBrush = value;
				OnPropertyChanged();
			}
		}

		private SolidColorBrush lightAccentBrush;
		public SolidColorBrush LightAccentBrush
		{
			get => lightAccentBrush;
			set
			{
				lightAccentBrush = value;
				OnPropertyChanged();
			}
		}

		private SolidColorBrush baseBackground;
		public SolidColorBrush BaseBackgroundBrush
		{
			get => baseBackground;
			set
			{
				baseBackground = value;
				OnPropertyChanged();
			}
		}

		private SolidColorBrush baseForeground;
		public SolidColorBrush BaseForegroundBrush
		{
			get => baseForeground;
			set
			{
				baseForeground = value;
				OnPropertyChanged();
			}
		}

		private SolidColorBrush darkBackground;
		private SolidColorBrush darkForeground;

		private SolidColorBrush lightBackground;
		private SolidColorBrush lightForeground;

		public ThemeViewModel()
		{
			Color accent = SystemColorHelper.GetAccentColor();
			this.AccentBrush = new SolidColorBrush(accent);
			if(AccentBrush.CanFreeze)
				AccentBrush.Freeze();
			this.LightAccentBrush = new SolidColorBrush(SystemColorHelper.ChangeColorBrightness(accent, 0.55f));
			if(LightAccentBrush.CanFreeze)
				LightAccentBrush.Freeze();
			this.SelectedTheme = SettingsProvider.Settings.CurrentAppTheme;
            this.WindowsTheme = SystemColorHelper.WindowsTheme;

			SystemColorHelper.AccentColorChanged += newColor =>
			{
				this.AccentBrush = new SolidColorBrush(newColor);
				if(AccentBrush.CanFreeze)
					AccentBrush.Freeze();
				this.LightAccentBrush = new SolidColorBrush(SystemColorHelper.ChangeColorBrightness(newColor, 0.55f));
				if(LightAccentBrush.CanFreeze)
					LightAccentBrush.Freeze();
			};
			
			SystemColorHelper.WindowsThemeChanged += newTheme => this.WindowsTheme = newTheme;
		}
		
		private void SetTheme(AppTheme newTheme)
		{
			Theme theme;
			if (newTheme == AppTheme.Dark)
			{
				theme = ThemeManager.Current.ChangeTheme(App.Current, "Dark.Steel");
				if(this.darkBackground != null && this.darkForeground != null)
				{
					this.BaseBackgroundBrush = darkBackground;
					this.BaseForegroundBrush = darkForeground;
				}
				else
				{
					this.BaseBackgroundBrush = this.darkBackground = new SolidColorBrush((Color)theme.Resources["MahApps.Colors.ThemeBackground"]);
					if(darkBackground.CanFreeze)
						darkBackground.Freeze();
					this.BaseForegroundBrush = this.darkForeground = new SolidColorBrush((Color)theme.Resources["MahApps.Colors.ThemeForeground"]);
					if(darkForeground.CanFreeze)
						darkForeground.Freeze();
				}
			}
			else
			{
				theme = ThemeManager.Current.ChangeTheme(App.Current, "Light.Steel");
				if(this.lightBackground != null && this.lightForeground != null)
				{
					this.BaseBackgroundBrush = lightBackground;
					this.BaseForegroundBrush = lightForeground;
				}
				else
				{
					this.BaseBackgroundBrush = this.lightBackground = new SolidColorBrush((Color)theme.Resources["MahApps.Colors.ThemeBackground"]);
					if(lightBackground.CanFreeze)
						lightBackground.Freeze();
					this.BaseForegroundBrush = this.lightForeground = new SolidColorBrush((Color)theme.Resources["MahApps.Colors.ThemeForeground"]);
					if(lightForeground.CanFreeze)
						lightForeground.Freeze();
				}
			}

			if(SettingsProvider.Settings.CurrentAppTheme != newTheme)
			{
				SettingsProvider.Settings.CurrentAppTheme = newTheme;
				SettingsProvider.SaveSettings().GetAwaiter().GetResult();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
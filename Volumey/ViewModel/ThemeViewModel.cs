using System.ComponentModel;
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
				ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
			else if(newTheme == AppTheme.Light && ThemeManager.Current.ApplicationTheme != ApplicationTheme.Light)
				ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

			Task.Run(() =>
			{
				if(SettingsProvider.Settings.CurrentAppTheme != this.selectedTheme)
				{
					SettingsProvider.Settings.CurrentAppTheme = this.selectedTheme;
					_ = SettingsProvider.SaveSettings();
				}
			});
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
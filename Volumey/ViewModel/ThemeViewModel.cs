using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ModernWpf;
using Volumey.DataProvider;
using Volumey.Helper;

namespace Volumey.ViewModel;

public sealed class ThemeViewModel : INotifyPropertyChanged
{
	public AppTheme[] AppThemes { get; } = { AppTheme.Light, AppTheme.Dark, AppTheme.System };

	private AppTheme selectedTheme;
	public AppTheme SelectedTheme
	{
		get => selectedTheme;
		set
		{
			selectedTheme = value;
			SetTheme(value);
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
		WindowsTheme = SystemColorHelper.WindowsTheme;
		SelectedTheme = SettingsProvider.Settings.General.Theme;
		SystemColorHelper.WindowsThemeChanged += WindowsThemeChanged;
	}

	private void WindowsThemeChanged(AppTheme newTheme)
	{
		WindowsTheme = newTheme;
		if(selectedTheme == AppTheme.System)
			SetTheme(AppTheme.System);
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

		SettingsProvider.Settings.General.Theme = selectedTheme;
	}

	private void UpdateNotificationColors(AppTheme newTheme)
	{
		if(newTheme == AppTheme.Dark)
		{
			if(_darkBackground == null)
			{
				if(App.Current.TryFindResource(BackgroundKey) is Brush brush)
				{
					_darkBackground = brush.CloneCurrentValue();
					_darkBackground.Opacity = BackgroundOpacity;
					_darkBackground.Freeze();
				}
			}
			if(_buttonHoverDarkBackground == null)
			{
				if(App.Current.TryFindResource(ButtonHoverKey) is Brush brush)
				{
					_buttonHoverDarkBackground = brush.CloneCurrentValue();
					_buttonHoverDarkBackground.Freeze();
				}
			}
			if(_darkForeground == null)
				_darkForeground = Brushes.White;
			NotificationManagerHelper.UpdateColors(_darkBackground, _darkForeground, _buttonHoverDarkBackground);
		}
		else if(newTheme == AppTheme.Light)
		{
			if(_lightBackground == null)
			{
				if(App.Current.TryFindResource(BackgroundKey) is Brush brush)
				{
					_lightBackground = brush.CloneCurrentValue();
					_lightBackground.Opacity = BackgroundOpacity;
					_lightBackground.Freeze();
				}
			}
			if(_lightButtonHoverBackground == null)
			{
				if(App.Current.TryFindResource(ButtonHoverKey) is Brush brush)
				{
					_lightButtonHoverBackground = brush.CloneCurrentValue();
					_lightButtonHoverBackground.Freeze();
				}
			}
			if(_lightForeground == null)
				_lightForeground = Brushes.Black;
			NotificationManagerHelper.UpdateColors(_lightBackground, _lightForeground, _lightButtonHoverBackground);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Volumey.DataProvider;
using Volumey.Helper;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

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

	/// <summary>
	/// The backdrop of the app's windows (Windows 11; older versions get the solid background)
	/// </summary>
	public static WindowBackdropType Backdrop => WindowBackdropType.Mica;

	private readonly Windows.UI.ViewManagement.UISettings uiSettings = new();
	private ApplicationTheme? appliedTheme;

	public ThemeViewModel()
	{
		WindowsTheme = SystemColorHelper.WindowsTheme;
		SelectedTheme = SettingsProvider.Settings.General.Theme;
		SystemColorHelper.WindowsThemeChanged += WindowsThemeChanged;
		//an accent change in Windows' settings (raised on a background thread)
		uiSettings.ColorValuesChanged += (_, _) => Application.Current?.Dispatcher.InvokeAsync(ApplicationAccentColorManager.ApplySystemAccent);
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

		var theme = newTheme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
		//tracked here: App.xaml starts WPF-UI in the dark theme, but nothing (accent) has been applied yet
		if(appliedTheme != theme)
		{
			appliedTheme = theme;
			//without updateAccent: WPF-UI derives the accent shades by shifting the colorization color, which turns a muted
			//accent gray; ApplySystemAccent takes Windows' own shades (AccentLight1-3 in dark, AccentDark1-3 in light), like WinUI
			ApplicationThemeManager.Apply(theme, Backdrop, updateAccent: false);
			ApplicationAccentColorManager.ApplySystemAccent();
		}

		SettingsProvider.Settings.General.Theme = selectedTheme;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

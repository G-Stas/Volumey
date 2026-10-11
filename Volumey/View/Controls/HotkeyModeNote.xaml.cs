using System.Windows;
using Volumey.View.SettingsPage;

namespace Volumey.View.Controls;

/// <summary>
/// The note about the hotkey mode under a settings page's hotkeys
/// </summary>
public partial class HotkeyModeNote
{
	public HotkeyModeNote()
	{
		InitializeComponent();
	}

	private void OnOpenGeneralClick(object sender, RoutedEventArgs e)
	{
		if(Window.GetWindow(this) is SettingsWindow window)
			window.Navigation.Navigate(typeof(GeneralPage));
	}
}

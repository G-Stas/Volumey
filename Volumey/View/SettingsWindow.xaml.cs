using System.ComponentModel;
using System.Windows;
using Volumey.View.SettingsPage;
using Volumey.ViewModel;

namespace Volumey.View;

/// <summary>
/// The settings, a window of their own (the mixer window only has the mixer)
/// </summary>
public partial class SettingsWindow
{
	public SettingsWindow()
	{
		InitializeComponent();
		WindowBackdropType = ThemeViewModel.Backdrop;
		Loaded += (_, _) => Navigation.Navigate(typeof(GeneralPage));
	}

	//the support links are on the About page (they come from GitHub, there may be several)
	private void OnSupportClick(object sender, RoutedEventArgs e) => Navigation.Navigate(typeof(AboutPage));

	//closing only hides it: it keeps its page and position for the next time
	private void OnClosing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		Hide();
	}
}

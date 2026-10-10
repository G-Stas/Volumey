using System.ComponentModel;
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

	//closing only hides it: it keeps its page and position for the next time
	private void OnClosing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		Hide();
	}
}

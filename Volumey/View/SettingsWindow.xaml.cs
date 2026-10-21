#nullable enable
using System.ComponentModel;
using Volumey.View.SettingsPage;
using Volumey.ViewModel;
using Wpf.Ui.Abstractions;

namespace Volumey.View;

/// <summary>
/// The settings, a window of their own (the mixer window only has the mixer). Each page brings its own view model
/// </summary>
public sealed partial class SettingsWindow
{
	internal SettingsWindow(INavigationViewPageProvider pages)
	{
		InitializeComponent();
		WindowBackdropType = ThemeViewModel.Backdrop;
		Navigation.SetPageProviderService(pages);
		//only the bar: the pages bring their own view models
		SaveErrorBar.DataContext = new SettingsSaveErrorViewModel(Dispatcher);
		Loaded += (_, _) => Navigation.Navigate(typeof(GeneralPage));
	}

	//closing only hides it: it keeps its page and position for the next time
	private void OnClosing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		Hide();
	}
}

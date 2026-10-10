using System;
using System.Windows;
using log4net;
using Volumey.Helper;
using Volumey.View.DialogContent;
using Volumey.ViewModel;

namespace Volumey.View.SettingsPage;

public partial class AppHotkeysPage
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(AppHotkeysPage));

	public AppHotkeysPage()
	{
		InitializeComponent();
	}

	private void OnAddAppClick(object sender, RoutedEventArgs e)
	{
		if(Window.GetWindow(this) is not SettingsWindow window)
			return;
		var vm = ((SettingsViewModel)DataContext).HotkeysViewModel;
		vm.PrepareAddApp();
		new AddAppDialog(vm) { DialogHostEx = window.DialogHost }.ShowAsync().Forget(Logger, "Failed to show the add app dialog");
	}

	private void OnOpenGeneralClick(object sender, RoutedEventArgs e)
	{
		if(Window.GetWindow(this) is SettingsWindow window)
			window.Navigation.Navigate(typeof(GeneralPage));
	}
}

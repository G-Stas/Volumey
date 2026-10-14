#nullable enable
using System.Windows;
using log4net;
using Volumey.Helper;
using Volumey.View.DialogContent;
using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public sealed partial class AppHotkeysPage
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(AppHotkeysPage));

	private readonly AppHotkeysPageViewModel vm;

	internal AppHotkeysPage(AppHotkeysPageViewModel vm)
	{
		this.vm = vm;
		DataContext = vm;
		InitializeComponent();
	}

	private void OnAddAppClick(object sender, RoutedEventArgs e)
	{
		if(Window.GetWindow(this) is not SettingsWindow window)
			return;
		vm.AppHotkeys.PrepareAddApp();
		new AddAppDialog(vm.AppHotkeys) { DialogHostEx = window.DialogHost }.ShowAsync().Forget(Logger, "Failed to show the add app dialog");
	}
}

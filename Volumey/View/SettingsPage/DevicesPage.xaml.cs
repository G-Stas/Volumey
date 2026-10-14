using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public partial class DevicesPage
{
	internal DevicesPage(DevicesPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
		//the settings window is hidden, not closed, so Unloaded doesn't come when it closes
		IsVisibleChanged += (_, e) => vm.DefaultDevice.SetPageShown((bool)e.NewValue);
	}
}

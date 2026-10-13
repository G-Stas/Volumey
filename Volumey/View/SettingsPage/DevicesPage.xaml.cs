using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public partial class DevicesPage
{
	internal DevicesPage(DevicesPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
	}
}

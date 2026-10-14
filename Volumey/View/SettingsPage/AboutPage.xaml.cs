using Volumey.ViewModel.Settings;

namespace Volumey.View.SettingsPage;

public sealed partial class AboutPage
{
	internal AboutPage(AboutViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
	}
}

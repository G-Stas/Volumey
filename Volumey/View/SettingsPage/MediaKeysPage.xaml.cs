using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public partial class MediaKeysPage
{
	internal MediaKeysPage(MediaKeysPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
	}
}

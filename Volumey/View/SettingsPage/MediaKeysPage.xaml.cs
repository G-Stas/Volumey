using Volumey.ViewModel.Settings;

namespace Volumey.View.SettingsPage;

public partial class MediaKeysPage
{
	internal MediaKeysPage(SystemHotkeysViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
	}
}

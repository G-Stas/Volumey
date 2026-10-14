using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public sealed partial class MixerPage
{
	internal MixerPage(MixerPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
		//read the screens again when the page opens
		Loaded += (_, _) => vm.Window.LoadedCommand.Execute(null);
	}
}

using Volumey.ViewModel;

namespace Volumey.View.SettingsPage;

public partial class MixerPage
{
	public MixerPage()
	{
		InitializeComponent();
		//read the screens again when the page opens
		Loaded += (_, _) => ((AppBehaviorViewModel)FindResource("AppBehaviorViewModel")).LoadedCommand.Execute(null);
	}
}

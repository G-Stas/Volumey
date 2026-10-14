using Volumey.ViewModel.Settings;

namespace Volumey.View.SettingsPage;

public sealed partial class NotificationsPage
{
	internal NotificationsPage(NotificationViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
		//read the screens again when the page opens, and close the sample OSD when it's left
		Loaded += (_, _) => vm.LoadedCommand.Execute(null);
		Unloaded += (_, _) => vm.UnloadedCommand.Execute(null);
	}
}

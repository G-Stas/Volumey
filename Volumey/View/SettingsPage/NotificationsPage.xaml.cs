using Volumey.ViewModel.Settings;

namespace Volumey.View.SettingsPage;

public partial class NotificationsPage
{
	public NotificationsPage()
	{
		InitializeComponent();
		//as before: read the screens again when the page opens, and close the sample OSD when it's left
		Loaded += (_, _) => ((NotificationViewModel)DataContext).LoadedCommand.Execute(null);
		Unloaded += (_, _) => ((NotificationViewModel)DataContext).UnloadedCommand.Execute(null);
	}
}

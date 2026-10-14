#nullable enable
using Volumey.ViewModel.Pages;

namespace Volumey.View.SettingsPage;

public sealed partial class MediaKeysPage
{
	internal MediaKeysPage(MediaKeysPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();
	}
}

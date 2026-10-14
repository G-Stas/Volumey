using System.Windows;
using Volumey.ViewModel.Pages;
using Wpf.Ui.Controls;

namespace Volumey.View.SettingsPage;

public sealed partial class GeneralPage
{
	internal GeneralPage(GeneralPageViewModel vm)
	{
		DataContext = vm;
		InitializeComponent();

#if(STORE)
		NonStoreSection.Visibility = Visibility.Collapsed;
#endif
	}

	private void OnNumberBoxValueChanged(object sender, NumberBoxValueChangedEventArgs args)
	{
		//prevent leaving number boxes empty
		if(args.NewValue == null && args.OldValue != null && sender is NumberBox box)
			box.Value = args.OldValue;
	}
}

using System.Windows;
using Wpf.Ui.Controls;

namespace Volumey.View.SettingsPage;

public partial class GeneralPage
{
	public GeneralPage()
	{
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

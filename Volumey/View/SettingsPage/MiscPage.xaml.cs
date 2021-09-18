using ModernWpf.Controls;

namespace Volumey.View.SettingsPage
{
	public partial class MiscPage
	{
		public MiscPage()
		{
			InitializeComponent();
		}
		
		private void NumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
		{
			//prevent leaving number boxes empty
			if (double.IsNaN(args.NewValue))
				sender.Value = args.OldValue;
		}
	}
}
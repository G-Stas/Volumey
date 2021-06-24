using ModernWpf.Controls;

namespace Volumey.View
{
    public partial class SettingsView
    {
        public SettingsView()
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
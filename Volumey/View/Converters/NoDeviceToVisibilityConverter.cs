using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters
{
    public class NoDeviceToVisibilityConverter : IValueConverter
    {
        public object Convert(object noOutputDevices, Type targetType, object parameter, CultureInfo culture)
        {
            //set Visibility property of MasterSession container & "no devices" label
            //based on ViewModel.NoOutputDevices bool property it's binded to
            if (parameter != null && noOutputDevices is bool noDevices)
            {
                switch (parameter.ToString())
                {
                    case "Master":
                    case "Separator":
                    {
                        return noDevices ? Visibility.Collapsed : Visibility.Visible;
                    }
                    case "Label":
                    {
                        return noDevices ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
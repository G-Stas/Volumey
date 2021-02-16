using System;
using System.Globalization;
using System.Windows.Data;

namespace Volumey.View.Converters
{
    public class HotkeysRegisteredToEnabledConverter : IValueConverter
    {
        public object Convert(object hotkeysRegistered, Type targetType, object parameter, CultureInfo culture)
        {
            //set IsEnabled property of HotkeyBox
            //based on SettingsViewModel.MusicHotkeysRegistered/OpenHotkeyRegistered property it's binded to
            if (parameter?.ToString() != null && parameter.ToString().Equals("HotkeyBox", StringComparison.InvariantCulture) && hotkeysRegistered is bool registered)
                return !registered;
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
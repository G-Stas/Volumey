using System;
using System.Globalization;
using System.Windows.Data;
using Volumey.Localization;

namespace Volumey.View.Converters;

/// <summary>
/// The name a screen reader reads for a mute button: "Mute Spotify" / "Unmute Spotify".
/// Values: is muted, the app's or device's name, and the current language (only bound so the name follows it)
/// </summary>
public class MuteButtonNameConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if(values is not { Length: >= 2 } || values[0] is not bool isMuted)
			return string.Empty;
		string format = TranslationSource.Instance[isMuted ? "Mixer_Unmute" : "Mixer_Mute"] ?? "{0}";
		return string.Format(format, values[1] as string ?? string.Empty).Trim();
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
}

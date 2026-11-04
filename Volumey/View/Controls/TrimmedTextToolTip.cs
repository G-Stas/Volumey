#nullable enable
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:TrimmedTextToolTip.IsEnabled="True"</c> on a TextBlock that ends in "…" when it doesn't fit: its full text shows in a
/// tooltip, only while it's cut off
/// </summary>
public static class TrimmedTextToolTip
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(TrimmedTextToolTip), new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not TextBlock text)
			return;
		if((bool)e.NewValue)
		{
			//a tooltip has to be set for ToolTipOpening to be raised
			text.ToolTip = string.Empty;
			text.ToolTipOpening += OnToolTipOpening;
		}
		else
		{
			text.ToolTipOpening -= OnToolTipOpening;
			text.ClearValue(FrameworkElement.ToolTipProperty);
		}
	}

	private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
	{
		var text = (TextBlock)sender;
		if(IsTrimmed(text))
			text.ToolTip = text.Text;
		else
			e.Handled = true;
	}

	private static bool IsTrimmed(TextBlock text)
	{
		var full = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
			new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black,
			VisualTreeHelper.GetDpi(text).PixelsPerDip);
		return full.WidthIncludingTrailingWhitespace > text.ActualWidth + 0.5;
	}
}

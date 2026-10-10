using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:DigitsOnly.IsEnabled="True"</c> on a text box (e.g. a WPF-UI NumberBox for a positive whole number):
/// only digits can be typed or pasted. WPF-UI's NumberBox accepts any text and only clamps it to its range on Enter
/// or when it loses focus, so "-1" stayed in the box until then
/// </summary>
public static class DigitsOnly
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(DigitsOnly), new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	internal static bool IsAllowed(string text) => !string.IsNullOrEmpty(text) && text.All(char.IsAsciiDigit);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not TextBox box)
			return;
		box.PreviewTextInput -= OnPreviewTextInput;
		box.PreviewKeyDown -= OnPreviewKeyDown;
		DataObject.RemovePastingHandler(box, OnPasting);
		if(e.NewValue is true)
		{
			box.PreviewTextInput += OnPreviewTextInput;
			box.PreviewKeyDown += OnPreviewKeyDown;
			DataObject.AddPastingHandler(box, OnPasting);
		}
	}

	private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		if(!IsAllowed(e.Text))
			e.Handled = true;
	}

	//a space doesn't come through PreviewTextInput in a TextBox
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if(e.Key == Key.Space)
			e.Handled = true;
	}

	private static void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		if(e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !IsAllowed(text.Trim()))
			e.CancelCommand();
	}
}

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:NumberBoxCommit.IsEnabled="True"</c> on a WPF-UI NumberBox fixes two things about committing a typed value
/// (WPF-UI clamps it to the range and updates the binding on Enter and on LostFocus):
/// <list type="bullet">
/// <item>switching to another window only takes the keyboard focus (the box keeps the window's logical focus), so a typed
/// value stayed unclamped and unsaved: the logical focus is taken away then, so WPF raises LostFocus;</item>
/// <item>when the clamped value equals the current one (e.g. 150 typed while the value is 100), WPF-UI doesn't update the
/// text, so "150" stayed in the box: the text is set to the value after each commit.</item>
/// </list>
/// </summary>
public static class NumberBoxCommit
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(NumberBoxCommit), new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not NumberBox box)
			return;
		box.LostKeyboardFocus -= OnLostKeyboardFocus;
		box.LostFocus -= OnCommitted;
		box.RemoveHandler(UIElement.PreviewKeyUpEvent, (KeyEventHandler)OnPreviewKeyUp);
		if(e.NewValue is true)
		{
			box.LostKeyboardFocus += OnLostKeyboardFocus;
			box.LostFocus += OnCommitted;
			//WPF-UI marks Enter handled
			box.AddHandler(UIElement.PreviewKeyUpEvent, (KeyEventHandler)OnPreviewKeyUp, handledEventsToo: true);
		}
	}

	private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		//the keyboard focus went to nothing of this app (its window was deactivated); a move inside the window moves the
		//logical focus too, which commits by itself
		if(e.NewFocus == null)
			TakeLogicalFocus((NumberBox)sender);
	}

	private static void OnPreviewKeyUp(object sender, KeyEventArgs e)
	{
		if(e.Key == Key.Enter)
			OnCommitted(sender, e);
	}

	//LostFocus handlers run before the box validates (its OnLostFocus raises the event first), so the text is fixed afterwards
	private static void OnCommitted(object sender, RoutedEventArgs e)
	{
		var box = (NumberBox)sender;
		box.Dispatcher.InvokeAsync(() => ShowValue(box), DispatcherPriority.Input);
	}

	/// <summary>
	/// Takes the logical focus away, so WPF raises LostFocus and the box commits its value
	/// (raising LostFocus here wouldn't reach the box: its OnLostFocus raises that event, it doesn't handle it)
	/// </summary>
	internal static void TakeLogicalFocus(NumberBox box)
	{
		if(!box.IsFocused)
			return;
		var scope = FocusManager.GetFocusScope(box);
		if(scope != null)
			FocusManager.SetFocusedElement(scope, null);
	}

	/// <summary>
	/// Sets the text to the (clamped) value, formatted as the box formats it
	/// </summary>
	internal static void ShowValue(NumberBox box)
	{
		if(box.Value is not double value || box.NumberFormatter is null)
			return;
		string text = box.NumberFormatter.FormatDouble(Math.Round(value, box.MaxDecimalPlaces));
		if(box.Text != text)
			box.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, text);
	}
}

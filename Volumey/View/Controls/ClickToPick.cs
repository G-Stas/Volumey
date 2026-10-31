#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:ClickToPick.IsEnabled="True"</c> on a ComboBox: an item is picked only by a click inside the open list.
/// WPF picks the item under the pointer on any button release, so when the list opens over the box, releasing the
/// press that opened it picks whatever item lies under the pointer
/// </summary>
public static class ClickToPick
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(ClickToPick), new PropertyMetadata(false, OnIsEnabledChanged));

	//set while the list is open and no press has landed in it yet
	private static readonly DependencyProperty AwaitingPressProperty = DependencyProperty.RegisterAttached("AwaitingPress",
		typeof(bool), typeof(ClickToPick), new PropertyMetadata(false));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not ComboBox box)
			return;
		box.DropDownOpened -= OnDropDownOpened;
		box.DropDownClosed -= OnDropDownClosed;
		box.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
		box.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
		if(e.NewValue is not true)
			return;
		box.DropDownOpened += OnDropDownOpened;
		box.DropDownClosed += OnDropDownClosed;
		box.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
		box.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
	}

	private static void OnDropDownOpened(object? sender, EventArgs e) => ((ComboBox)sender!).SetValue(AwaitingPressProperty, true);

	private static void OnDropDownClosed(object? sender, EventArgs e) => ((ComboBox)sender!).SetValue(AwaitingPressProperty, false);

	//the list's items raise their mouse events through the box (the popup is its logical child)
	private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if(((ComboBox)sender).IsDropDownOpen)
			((ComboBox)sender).SetValue(AwaitingPressProperty, false);
	}

	private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		var box = (ComboBox)sender;
		if(!(bool)box.GetValue(AwaitingPressProperty) || !IsInItem(e.OriginalSource as DependencyObject))
			return;
		box.SetValue(AwaitingPressProperty, false);
		e.Handled = true;
	}

	private static bool IsInItem(DependencyObject? element)
	{
		for(; element != null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
			if(element is ComboBoxItem)
				return true;
		return false;
	}
}

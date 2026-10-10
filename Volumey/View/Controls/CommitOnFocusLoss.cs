using System.Windows;
using System.Windows.Input;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:CommitOnFocusLoss.IsEnabled="True"</c> on a WPF-UI NumberBox: it also commits its value (clamps it to its range
/// and updates the binding) when its window loses the focus. The box does that on Enter and on LostFocus, but switching to
/// another window only takes the keyboard focus (the box keeps the window's logical focus), so a typed value stayed
/// unclamped and unsaved
/// </summary>
public static class CommitOnFocusLoss
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(CommitOnFocusLoss), new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not UIElement element)
			return;
		element.LostKeyboardFocus -= OnLostKeyboardFocus;
		if(e.NewValue is true)
			element.LostKeyboardFocus += OnLostKeyboardFocus;
	}

	private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		//the keyboard focus went to nothing of this app (its window was deactivated); a move inside the window moves the
		//logical focus too, which commits by itself
		if(e.NewFocus == null)
			Commit((UIElement)sender);
	}

	/// <summary>
	/// Takes the logical focus away, so WPF raises LostFocus and the box commits its value (WPF-UI's NumberBox validates there;
	/// OnLostFocus raises that event, so raising it here wouldn't reach the box)
	/// </summary>
	internal static void Commit(UIElement element)
	{
		if(!element.IsFocused)
			return;
		var scope = FocusManager.GetFocusScope(element);
		if(scope != null)
			FocusManager.SetFocusedElement(scope, null);
	}
}

#nullable enable
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace Volumey.Controls;

/// <summary>
/// <c>controls:ExpanderButtonName.IsEnabled="True"</c> on a WPF-UI CardExpander: its header button (the template part
/// ExpanderToggleButton, which expands and collapses the card) gets the card's AutomationProperties.Name. WPF-UI leaves
/// that button unnamed, so a screen reader announces only "button"
/// </summary>
public static class ExpanderButtonName
{
	private const string HeaderButtonPart = "ExpanderToggleButton";

	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
		typeof(bool), typeof(ExpanderButtonName), new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(d is not Control expander)
			return;
		expander.Loaded -= OnLoaded;
		if(e.NewValue is true)
			expander.Loaded += OnLoaded;
	}

	//the template is applied by Loaded; a page shown again loads again and finds the binding already there
	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		var expander = (Control)sender;
		if(expander.Template?.FindName(HeaderButtonPart, expander) is FrameworkElement button)
			button.SetBinding(AutomationProperties.NameProperty,
				new Binding { Source = expander, Path = new PropertyPath(AutomationProperties.NameProperty) });
	}
}

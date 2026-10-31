#nullable enable
using System.Windows;
using System.Windows.Controls;

namespace Volumey.View.Converters;

/// <summary>
/// The templates of an app's device box: <see cref="InList"/> for the items of the open list (their presenter is in a
/// <see cref="ComboBoxItem"/>'s template), <see cref="Closed"/> for the closed box (its presenter is in the combo box's template)
/// </summary>
public sealed class OutputOptionTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Closed { get; set; }
	public DataTemplate? InList { get; set; }

	public override DataTemplate? SelectTemplate(object item, DependencyObject container)
		=> container is FrameworkElement { TemplatedParent: ComboBoxItem } ? InList : Closed;
}

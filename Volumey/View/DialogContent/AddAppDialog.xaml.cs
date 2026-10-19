#nullable enable
using System.Windows;
using System.Windows.Data;
using Volumey.ViewModel.Settings;
using Wpf.Ui.Controls;

namespace Volumey.View.DialogContent;

/// <summary>
/// App hotkeys page: picks a running app and its hotkeys. Add only closes the dialog when the app was added.
/// </summary>
public sealed partial class AddAppDialog
{
	private readonly AppsHotkeysViewModel vm;

	internal AddAppDialog(AppsHotkeysViewModel vm)
	{
		this.vm = vm;
		DataContext = vm;
		//WPF-UI's dialog style is implicit, which doesn't reach a subclass (without it: no dialog chrome, no buttons)
		SetResourceReference(StyleProperty, typeof(ContentDialog));
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		//Add is disabled until an app and at least one hotkey are set, as in Windows. Not through IsPrimaryButtonEnabled:
		//WPF-UI hides the primary button when that's false, Windows (WinUI) only disables it
		if(GetTemplateChild("PrimaryButton") is UIElement add)
			BindingOperations.SetBinding(add, IsEnabledProperty, new Binding(nameof(AppsHotkeysViewModel.CanAdd)));
	}

	protected override void OnButtonClick(ContentDialogButton button)
	{
		//a rejected app (a hotkey in use, F12...) keeps the dialog open with the error
		if(button == ContentDialogButton.Primary && !vm.TryAddApp())
			return;
		base.OnButtonClick(button);
	}
}

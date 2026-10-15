#nullable enable
using System.Windows;
using Volumey.View.SettingsPage;

namespace Volumey.View.Controls;

/// <summary>
/// The note about the hotkey mode under a settings page's hotkeys
/// </summary>
public sealed partial class HotkeyModeNote
{
	/// <summary>True: hotkeys keep their keys to Volumey; false: other apps get the keys too</summary>
	public static readonly DependencyProperty KeysBlockedProperty
		= DependencyProperty.Register(nameof(KeysBlocked), typeof(bool), typeof(HotkeyModeNote), new PropertyMetadata(true));

	public bool KeysBlocked
	{
		get => (bool)GetValue(KeysBlockedProperty);
		set => SetValue(KeysBlockedProperty, value);
	}

	public HotkeyModeNote()
	{
		InitializeComponent();
	}

	private void OnOpenGeneralClick(object sender, RoutedEventArgs e)
	{
		if(Window.GetWindow(this) is SettingsWindow window)
			window.Navigation.Navigate(typeof(GeneralPage));
	}
}

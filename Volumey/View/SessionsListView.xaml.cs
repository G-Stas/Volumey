#nullable enable
using System.Windows;

namespace Volumey.View;

public sealed partial class SessionsListView
{
	public static readonly DependencyProperty ShowHiddenSourcesProperty = DependencyProperty.Register(
		nameof(ShowHiddenSources), typeof(bool), typeof(SessionsListView), new PropertyMetadata(false));
	public bool ShowHiddenSources
	{
		get => (bool)GetValue(ShowHiddenSourcesProperty);
		set => SetValue(ShowHiddenSourcesProperty, value);
	}

	public SessionsListView()
	{
		InitializeComponent();
	}
}

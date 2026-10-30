#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Volumey.Localization;
using Volumey.ViewModel;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Volumey.View;

public sealed partial class MixerView
{
	private static readonly TimeSpan NoticeTimeout = TimeSpan.FromSeconds(5);

	private DeviceProviderViewModel? devices;

	public MixerView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => Watch((DataContext as MainViewModel)?.Devices);
	}

	private void Watch(DeviceProviderViewModel? newDevices)
	{
		if(devices != null)
			devices.PropertyChanged -= OnDevicesPropertyChanged;
		devices = newDevices;
		if(devices != null)
			devices.PropertyChanged += OnDevicesPropertyChanged;
	}

	private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(DeviceProviderViewModel.Notice))
			ShowNotice(devices?.Notice);
	}

	/// <summary>The notice replaces the one shown; with Undo for a move</summary>
	private async void ShowNotice(MixerNotice? notice)
	{
		await Notices.HideCurrent();
		if(notice == null || devices == null)
			return;
		var content = new DockPanel();
		if(notice.CanUndo)
		{
			var undo = new Wpf.Ui.Controls.Button
			{
				Content = TranslationSource.Instance["Mixer_Undo"],
				Appearance = ControlAppearance.Transparent,
				Padding = new Thickness(10, 4, 10, 4),
				Command = devices.UndoMoveCommand
			};
			AutomationProperties.SetAutomationId(undo, "UndoMoveButton");
			DockPanel.SetDock(undo, Dock.Right);
			content.Children.Add(undo);
		}
		content.Children.Add(new TextBlock { Text = notice.Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
		var snackbar = new Snackbar(Notices)
		{
			Content = content,
			Appearance = ControlAppearance.Secondary,
			Timeout = NoticeTimeout,
			IsCloseButtonEnabled = true
		};
		AutomationProperties.SetAutomationId(snackbar, "MovedSnackbar");
		AutomationProperties.SetName(snackbar, notice.Text);
		snackbar.Show();
	}

	/// <summary>
	/// The height the mixer needs for its fixed parts (device area, header, footer) and for the apps list,
	/// measured at the given width
	/// </summary>
	internal (double chrome, double apps) MeasureHeights(double width)
	{
		var unbounded = new Size(width, double.PositiveInfinity);
		SessionsList.Measure(unbounded);
		Measure(unbounded);
		//the scroll viewer takes the list's height when unbounded, so the rest is the fixed part
		double apps = SessionsList.DesiredSize.Height + ScrollViewer.Padding.Top + ScrollViewer.Padding.Bottom;
		return (DesiredSize.Height - apps, apps);
	}
}

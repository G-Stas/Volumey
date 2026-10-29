#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Volumey.Model;
using Volumey.ViewModel;

namespace Volumey.View;

public sealed partial class SessionsListView
{
	//the apps' device boxes on screen, with the app each one shows
	private readonly Dictionary<ComboBox, AudioProcessModel> outputBoxes = new();
	private DeviceProviderViewModel? watched;
	private bool filling;

	public SessionsListView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => Watch(DataContext as DeviceProviderViewModel);
	}

	private DeviceProviderViewModel? Devices => DataContext as DeviceProviderViewModel;

	//the boxes list the active devices and name the default device
	private void Watch(DeviceProviderViewModel? devices)
	{
		if(watched != null)
		{
			watched.PropertyChanged -= OnDevicesPropertyChanged;
			watched.DeviceProvider.ActiveDevices.CollectionChanged -= OnActiveDevicesChanged;
		}
		watched = devices;
		if(devices != null)
		{
			devices.PropertyChanged += OnDevicesPropertyChanged;
			devices.DeviceProvider.ActiveDevices.CollectionChanged += OnActiveDevicesChanged;
		}
	}

	private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(DeviceProviderViewModel.DefaultDevice))
			FillAll();
	}

	private void OnActiveDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => FillAll();

	private void FillAll()
	{
		foreach(var box in outputBoxes.Keys)
			Fill(box);
	}

	private void OnOutputBoxLoaded(object sender, RoutedEventArgs e)
	{
		if(sender is not ComboBox { DataContext: AudioProcessModel app } box || outputBoxes.ContainsKey(box))
			return;
		outputBoxes[box] = app;
		app.PropertyChanged += OnAppPropertyChanged;
		Fill(box);
	}

	private void OnOutputBoxUnloaded(object sender, RoutedEventArgs e)
	{
		if(sender is ComboBox box && outputBoxes.Remove(box, out var app))
			app.PropertyChanged -= OnAppPropertyChanged;
	}

	//the app's route changed: after a move, or when Windows was read again
	private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName is not (nameof(AudioProcessModel.Route) or nameof(AudioProcessModel.RouteDeviceName)))
			return;
		foreach(var (box, app) in outputBoxes)
			if(app == sender)
				Fill(box);
	}

	private void Fill(ComboBox box)
	{
		if(Devices == null || !outputBoxes.TryGetValue(box, out var app))
			return;
		var (options, selected) = Devices.OutputOptionsOf(app);
		filling = true;
		try
		{
			box.ItemsSource = options;
			box.SelectedIndex = selected;
		}
		finally
		{
			filling = false;
		}
	}

	private async void OnOutputPicked(object sender, SelectionChangedEventArgs e)
	{
		if(filling || Devices == null || sender is not ComboBox { SelectedItem: AppOutputOption option } box
		   || !outputBoxes.TryGetValue(box, out var app))
			return;
		try
		{
			await Devices.PickOutputAsync(app, option);
		}
		catch(OperationCanceledException)
		{
			//the app is closing
		}
		//a refused move leaves the route as it was: the box shows the choice that holds
		Fill(box);
	}
}

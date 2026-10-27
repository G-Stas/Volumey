#nullable enable
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.ViewModel;

namespace Volumey.View;

public sealed partial class SessionsListView
{
	private DeviceProviderViewModel? deviceProvider;
	private INotifyCollectionChanged? observedProcesses;
	private AudioProcessModel? dragSource;
	private Point dragStart;
	private bool applyingSavedOrder;

	public SessionsListView()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
		Loaded += (_, _) => SetDeviceProvider(DataContext as DeviceProviderViewModel);
		Unloaded += (_, _) => SetDeviceProvider(null);
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		=> SetDeviceProvider(e.NewValue as DeviceProviderViewModel);

	private void SetDeviceProvider(DeviceProviderViewModel? value)
	{
		if(deviceProvider != null)
			deviceProvider.PropertyChanged -= OnDeviceProviderPropertyChanged;
		if(observedProcesses != null)
			observedProcesses.CollectionChanged -= OnProcessesChanged;
		deviceProvider = value;
		if(deviceProvider != null)
			deviceProvider.PropertyChanged += OnDeviceProviderPropertyChanged;
		ObserveSelectedProcesses();
	}

	private void OnDeviceProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(DeviceProviderViewModel.SelectedDevice))
			ObserveSelectedProcesses();
	}

	private void ObserveSelectedProcesses()
	{
		if(observedProcesses != null)
			observedProcesses.CollectionChanged -= OnProcessesChanged;
		observedProcesses = deviceProvider?.SelectedDevice?.Processes;
		if(observedProcesses != null)
			observedProcesses.CollectionChanged += OnProcessesChanged;
		ApplySavedOrder();
	}

	private void OnProcessesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplySavedOrder();

	private void ApplySavedOrder()
	{
		if(applyingSavedOrder || deviceProvider?.SelectedDevice is not { } device)
			return;
		try
		{
			applyingSavedOrder = true;
			MixerProcessOrder.Apply(device.Processes, process => process.OrderKey, SettingsProvider.Settings.Mixer.ProcessOrder);
		}
		finally
		{
			applyingSavedOrder = false;
		}
	}

	private void OnDragHandleMouseDown(object sender, MouseButtonEventArgs e)
	{
		dragSource = (sender as FrameworkElement)?.DataContext as AudioProcessModel;
		dragStart = e.GetPosition(this);
	}

	private void OnDragHandleMouseMove(object sender, MouseEventArgs e)
	{
		if(e.LeftButton != MouseButtonState.Pressed || dragSource == null ||
		   (Math.Abs(e.GetPosition(this).X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
		    Math.Abs(e.GetPosition(this).Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance))
			return;
		DragDrop.DoDragDrop((DependencyObject)sender, dragSource, DragDropEffects.Move);
		dragSource = null;
	}

	private void OnProcessRowDragOver(object sender, DragEventArgs e)
	{
		bool canMove = e.Data.GetDataPresent(typeof(AudioProcessModel)) &&
		               sender is ContentPresenter target && target.DataContext is AudioProcessModel destination &&
		               deviceProvider?.SelectedDevice?.Processes.Contains(destination) == true;
		e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnProcessRowDrop(object sender, DragEventArgs e)
	{
		if(sender is not ContentPresenter target || target.DataContext is not AudioProcessModel destination ||
		   e.Data.GetData(typeof(AudioProcessModel)) is not AudioProcessModel source ||
		   deviceProvider?.SelectedDevice is not { } device)
			return;

		int from = device.Processes.IndexOf(source);
		int to = device.Processes.IndexOf(destination);
		if(from < 0 || to < 0 || from == to)
			return;

		var reordered = device.Processes.ToList();
		reordered.RemoveAt(from);
		reordered.Insert(to, source);
		var settings = SettingsProvider.Settings.Mixer;
		settings.ProcessOrder = MixerProcessOrder.SaveOrder(reordered, process => process.OrderKey, settings.ProcessOrder);
		device.Processes.Move(from, to);
		e.Handled = true;
	}
}

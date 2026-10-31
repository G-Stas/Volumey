#nullable enable
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel;

public sealed partial class DeviceProviderViewModel : ObservableObject
{
	[ObservableProperty]
	public partial OutputDeviceModel? SelectedDevice { get; set; }

	[ObservableProperty]
	public partial OutputDeviceModel? DefaultDevice { get; set; }

	public IDeviceProvider DeviceProvider { get; }

	private Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	internal DeviceProviderViewModel(IDeviceProvider deviceProvider, IAppShell shell)
	{
		DeviceProvider = deviceProvider;
		DeviceProvider.DeviceDisabled += OnDeviceDisabled;
		DeviceProvider.DefaultDeviceChanged += OnDefaultDeviceSwitched;
		DeviceProvider.DeviceReplaced += OnDeviceReplaced;

		if(!DeviceProvider.NoOutputDevices)
			SelectedDevice = DefaultDevice = DeviceProvider.DefaultDevice;

		shell.Exit += () => DeviceProvider.Dispose();
	}

	private void OnDefaultDeviceSwitched(OutputDeviceModel? newDevice)
	{
		//update default device property and selected device property if default device was the selected device in app
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == DefaultDevice)
			{
				SelectedDevice = DefaultDevice = newDevice;
				return;
			}
			DefaultDevice = newDevice;
		});
	}

	private void OnDeviceReplaced(OutputDeviceModel oldDevice, OutputDeviceModel newDevice)
	{
		//the default device is handled by OnDefaultDeviceSwitched, which comes first
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == oldDevice)
				SelectedDevice = newDevice;
		});
	}

	private void OnDeviceDisabled(OutputDeviceModel device)
	{
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == device)
			{
				if(device != DefaultDevice)
					SelectedDevice = DefaultDevice;

				//set both properties to null
				//if defaultDevice was the selected device in application
				//and OnDeviceProviderPropertyChanged event handler will set reference of a new default device object to both properties
				else
					SelectedDevice = DefaultDevice = null;
			}
		});
	}

	/// <summary>Shows the default device in the mixer, e.g. when its device picker is hidden</summary>
	internal void ShowDefaultDevice() => SelectedDevice = DefaultDevice;

	[RelayCommand]
	private void SetDefaultDevice()
	{
		if(SelectedDevice == null)
			return;
		DeviceProvider.SetDefaultDevice(SelectedDevice.Id);
	}

	[RelayCommand]
	private void SetDefaultDeviceTray(object param)
	{
		if(param is OutputDeviceModel model)
			SetAsDefaultDevice(model.Id);
	}

	private void SetAsDefaultDevice(string deviceId)
	{
		if(string.IsNullOrEmpty(deviceId))
			return;

		if(DeviceProvider.DefaultDevice != null && DeviceProvider.DefaultDevice.CompareId(deviceId))
			return;

		DeviceProvider.SetDefaultDevice(deviceId);
	}

	/// <summary>
	/// Switches the default output device to the one after it in the active devices list. It counts from a switch
	/// Windows hasn't reported yet, so quick repeats keep moving on
	/// </summary>
	internal void SwitchToNextDevice()
	{
		var activeDevices = DeviceProvider.ActiveDevices;
		if(activeDevices.Count <= 1)
			return;

		string? currentId = DeviceProvider.RequestedDefaultDeviceId ?? DeviceProvider.DefaultDevice?.Id;
		var current = currentId == null ? null : activeDevices.FirstOrDefault(d => d.CompareId(currentId));
		//IndexOf(null) is -1: without a known default device the first one is next
		int nextIndex = (activeDevices.IndexOf(current!) + 1) % activeDevices.Count;
		SetAsDefaultDevice(activeDevices[nextIndex].Id);
	}
}

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
	public partial OutputDeviceModel SelectedDevice { get; set; }

	[ObservableProperty]
	public partial OutputDeviceModel DefaultDevice { get; set; }

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

	private void OnDefaultDeviceSwitched(OutputDeviceModel newDevice)
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
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel;

public class DeviceProviderViewModel : INotifyPropertyChanged
{
	private OutputDeviceModel selectedDevice;
	public OutputDeviceModel SelectedDevice
	{
		get => selectedDevice;
		set
		{
			selectedDevice = value;
			OnPropertyChanged();
		}
	}

	private OutputDeviceModel defaultDevice;
	public OutputDeviceModel DefaultDevice
	{
		get => defaultDevice;
		set
		{
			defaultDevice = value;
			OnPropertyChanged();
		}
	}

	public IDeviceProvider DeviceProvider { get; }

	private Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	public DeviceProviderViewModel() : this(AppServices.Current.Devices, AppServices.Current.Shell) { }

	internal DeviceProviderViewModel(IDeviceProvider deviceProvider, IAppShell shell)
	{
		DeviceProvider = deviceProvider;
		DeviceProvider.DeviceDisabled += OnDeviceDisabled;
		DeviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
		DeviceProvider.DeviceReplaced += OnDeviceReplaced;

		if(!DeviceProvider.NoOutputDevices)
			SelectedDevice = DefaultDevice = DeviceProvider.DefaultDevice;

		shell.Exit += () => DeviceProvider.Dispose();

		SetDefaultDeviceCommand = new ActionCommand(() =>
		{
			if(SelectedDevice == null)
				return;
			DeviceProvider.SetDefaultDevice(SelectedDevice.Id);
		});

		SetDefaultDeviceTrayCommand = new ActionCommand((param) =>
		{
			if(param is OutputDeviceModel model)
				SetAsDefaultDevice(model.Id);
		});
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		//update default device property and selected device property if default device was the selected device in app
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == defaultDevice)
			{
				SelectedDevice = DefaultDevice = newDevice;
				return;
			}
			DefaultDevice = newDevice;
		});
	}

	private void OnDeviceReplaced(OutputDeviceModel oldDevice, OutputDeviceModel newDevice)
	{
		//the default device is handled by OnDefaultDeviceChanged, which comes first
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
				if(device != defaultDevice)
					SelectedDevice = DefaultDevice;

				//set both properties to null
				//if defaultDevice was the selected device in application
				//and OnDeviceProviderPropertyChanged event handler will set reference of a new default device object to both properties
				else
					SelectedDevice = DefaultDevice = null;
			}
		});
	}

	public ICommand SetDefaultDeviceCommand { get; }
	public ICommand SetDefaultDeviceTrayCommand { get; }

	private void SetAsDefaultDevice(string deviceId)
	{
		if(string.IsNullOrEmpty(deviceId))
			return;

		if(DeviceProvider.DefaultDevice != null && DeviceProvider.DefaultDevice.CompareId(deviceId))
			return;

		DeviceProvider.SetDefaultDevice(deviceId);
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string prop = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
	}
}

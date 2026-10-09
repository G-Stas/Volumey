using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

public sealed class AppVolumeSyncViewModel : INotifyPropertyChanged
{
	private int MasterVolume { get; set; } = 100;

	private bool isOn;
	public bool IsOn
	{
		get => isOn;
		set
		{
			if(isOn == value)
				return;
			isOn = value;
			if(isOn)
			{
				deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
				SetDevice(deviceProvider.DefaultDevice);
			}
			else
			{
				deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;
				SetDevice(null);
			}
			var general = SettingsProvider.Settings.General;
			if(general.AppVolumeSyncEnabled != value)
			{
				if(value)
					Logger.Info("Synchronising app volume to master volume on change.");
				general.AppVolumeSyncEnabled = value;
			}
			OnPropertyChanged();
		}
	}

	private readonly IDeviceProvider deviceProvider;
	private OutputDeviceModel defaultDevice;

	private ILog _logger;
	private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppVolumeSyncViewModel));

	internal AppVolumeSyncViewModel(IDeviceProvider deviceProvider)
	{
		this.deviceProvider = deviceProvider;
		IsOn = SettingsProvider.Settings.General.AppVolumeSyncEnabled;
	}

	/// <summary>
	/// Follows the volume and the new processes of the device, and stops following the previous one.
	/// </summary>
	private void SetDevice(OutputDeviceModel device)
	{
		if(defaultDevice != null)
		{
			defaultDevice.ProcessCreated -= OnProcessCreated;
			defaultDevice.Master.PropertyChanged -= OnMasterPropertyChanged;
		}
		defaultDevice = device;
		if(device != null)
		{
			MasterVolume = device.Master.Volume;
			device.ProcessCreated += OnProcessCreated;
			device.Master.PropertyChanged += OnMasterPropertyChanged;
		}
	}

	private async Task SyncSessionVolume()
	{
		if(IsOn && defaultDevice is OutputDeviceModel device)
		{
			foreach(var process in await device.GetImmutableProcessesAsync())
			{
				if(process.Volume != MasterVolume)
				{
					process.Volume = MasterVolume;
				}
			}
		}
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		SetDevice(newDevice);
		if(newDevice != null)
			SyncSessionVolume().Forget(Logger, "Failed to sync the app volumes with the new default device");
	}

	private void OnProcessCreated(AudioProcessModel newProcess)
	{
		if(IsOn && newProcess.Volume != MasterVolume)
			newProcess.Volume = MasterVolume;
	}

	private void OnMasterPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if(sender is MasterSessionModel masterSession && e.PropertyName == nameof(MasterSessionModel.Volume))
		{
			MasterVolume = masterSession.Volume;
			SyncSessionVolume().Forget(Logger, "Failed to sync the app volumes with the device volume");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

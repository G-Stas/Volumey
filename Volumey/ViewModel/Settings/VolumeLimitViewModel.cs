using CommunityToolkit.Mvvm.ComponentModel;
using log4net;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

public sealed partial class VolumeLimitViewModel : ObservableObject
{
	[ObservableProperty]
	public partial int VolumeLimit { get; set; }

	partial void OnVolumeLimitChanged(int value) => SettingsProvider.Settings.General.VolumeLimit = value;

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
				defaultDevice = deviceProvider.DefaultDevice;
				if(defaultDevice != null)
				{
					defaultDevice.ProcessCreated += OnProcessCreated;
				}
			}
			else
			{
				deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;
				if(defaultDevice != null)
				{
					defaultDevice.ProcessCreated -= OnProcessCreated;
				}
				defaultDevice = null;
			}
			var general = SettingsProvider.Settings.General;
			if(general.VolumeLimitEnabled != value)
			{
				if(value)
					Logger.Info($"Volume limit enabled, vol: [{VolumeLimit}]");
				general.VolumeLimitEnabled = value;
			}
			OnPropertyChanged();
		}
	}

	private readonly IDeviceProvider deviceProvider;
	private OutputDeviceModel defaultDevice;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(VolumeLimitViewModel));

	internal VolumeLimitViewModel(IDeviceProvider deviceProvider)
	{
		this.deviceProvider = deviceProvider;
		VolumeLimit = SettingsProvider.Settings.General.VolumeLimit;
		IsOn = SettingsProvider.Settings.General.VolumeLimitEnabled;
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		if(defaultDevice != null)
			defaultDevice.ProcessCreated -= OnProcessCreated;
		defaultDevice = newDevice;
		if(IsOn && newDevice != null)
		{
			newDevice.ProcessCreated += OnProcessCreated;
		}
	}

	private void OnProcessCreated(AudioProcessModel newProcess)
	{
		if(newProcess.Volume == 100)
			newProcess.Volume = VolumeLimit;
	}
}

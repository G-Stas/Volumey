using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class VolumeLimitViewModel : INotifyPropertyChanged
	{
		private int volumeLimit = 50;
		public int VolumeLimit
		{
			get => volumeLimit;
			set
			{
				volumeLimit = value;
				SettingsProvider.Settings.VolumeLimit = value;
				OnPropertyChanged();
			}
		}
		
		private bool isOn;
		public bool IsOn
		{
			get => isOn;
			set
			{
				isOn = value;
				if(isOn)
				{
					this.deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
					this.defaultDevice = deviceProvider.DefaultDevice;
					if(this.defaultDevice != null)
					{
						this.defaultDevice.ProcessCreated += OnProcessCreated;
					}
				}
				else
				{
					this.deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;
					if(this.defaultDevice != null)
					{
						this.defaultDevice.ProcessCreated -= OnProcessCreated;
					}
					this.defaultDevice = null;
				}
				if(SettingsProvider.Settings.VolumeLimitIsOn != value)
				{
					Task.Run(() =>
					{
						if(isOn)
							Logger.Info($"Volume limit enabled, vol: [{this.VolumeLimit}]");
						SettingsProvider.Settings.VolumeLimitIsOn = value;
						_ = SettingsProvider.SaveSettings();
					});
				}
				OnPropertyChanged();
			}
		}

		private readonly IDeviceProvider deviceProvider;
		private OutputDeviceModel defaultDevice;

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(VolumeLimitViewModel));

		public VolumeLimitViewModel()
		{
			this.deviceProvider = DeviceProvider.GetInstance();
			this.VolumeLimit = SettingsProvider.Settings.VolumeLimit;
			this.IsOn = SettingsProvider.Settings.VolumeLimitIsOn;
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.defaultDevice != null)
				this.defaultDevice.ProcessCreated -= OnProcessCreated;
			this.defaultDevice = newDevice;
			if(this.IsOn && newDevice != null)
			{
				newDevice.ProcessCreated += OnProcessCreated;
			}
		}

		private void OnProcessCreated(AudioProcessModel newProcess)
		{
			if(newProcess.Volume == 100)
				newProcess.Volume = this.VolumeLimit;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
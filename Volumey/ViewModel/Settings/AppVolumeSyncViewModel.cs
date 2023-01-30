using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class AppVolumeSyncViewModel : INotifyPropertyChanged
	{
		private int masterVolume = 100;
		public int MasterVolume
		{
			get => masterVolume;
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
                        this.masterVolume = this.defaultDevice.Master.Volume;
                        this.defaultDevice.ProcessCreated += OnProcessCreated;
						this.defaultDevice.Master.PropertyChanged += OnMasterPropertyChanged;
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
				if(SettingsProvider.Settings.SyncAppVolumeIsOn != value)
				{
					Task.Run(() =>
					{
						if(isOn)
							Logger.Info($"Synchronising Application volume to Master volume on change.");
						SettingsProvider.Settings.SyncAppVolumeIsOn = value;
						_ = SettingsProvider.SaveSettings();
					});
				}
				OnPropertyChanged();
			}
		}

		private readonly IDeviceProvider deviceProvider;
		private OutputDeviceModel defaultDevice;

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppVolumeSyncViewModel));

		public AppVolumeSyncViewModel()
		{
			this.deviceProvider = DeviceProvider.GetInstance();
			this.IsOn = SettingsProvider.Settings.SyncAppVolumeIsOn;
		}

        private void SyncSessionVolume()
        {
			if(this.IsOn)
			{
				foreach (var process in this.defaultDevice.Processes)
				{
					if(process.Volume != this.MasterVolume)
					{
						process.Volume = this.MasterVolume;
					}
				}
			}
        }

        private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.defaultDevice != null)
				this.defaultDevice.ProcessCreated -= OnProcessCreated;
			this.defaultDevice = newDevice;
			if(this.IsOn && newDevice != null)
			{
				newDevice.ProcessCreated += OnProcessCreated;
				masterVolume = newDevice.Master.Volume;
				SyncSessionVolume();
			}
		}

		private void OnProcessCreated(AudioProcessModel newProcess)
		{
			if(this.IsOn && newProcess.Volume != this.MasterVolume)
				newProcess.Volume = this.MasterVolume;
		}

        private void OnMasterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
			if(sender is MasterSessionModel masterSession && e.PropertyName == nameof(MasterSessionModel.Volume))
			{
				this.masterVolume = masterSession.Volume;
				SyncSessionVolume();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
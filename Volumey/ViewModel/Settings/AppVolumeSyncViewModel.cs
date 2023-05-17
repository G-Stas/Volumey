using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class AppVolumeSyncViewModel : INotifyPropertyChanged
	{
		private int MasterVolume { get; set; } = 100;

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
                        this.MasterVolume = this.defaultDevice.Master.Volume;
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
							Logger.Info("Synchronising app volume to master volume on change.");
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

        private async Task SyncSessionVolume()
        {
			if(this.IsOn)
			{
				foreach (var process in await this.defaultDevice.GetImmutableProcessesAsync())
				{
					if(process.Volume != this.MasterVolume)
					{
						process.Volume = this.MasterVolume;
					}
				}
			}
        }

        private async void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.defaultDevice != null)
				this.defaultDevice.ProcessCreated -= OnProcessCreated;
			this.defaultDevice = newDevice;
			if(this.IsOn && newDevice != null)
			{
				newDevice.ProcessCreated += OnProcessCreated;
				MasterVolume = newDevice.Master.Volume;
				await SyncSessionVolume();
			}
		}

		private void OnProcessCreated(AudioProcessModel newProcess)
		{
			if(this.IsOn && newProcess.Volume != this.MasterVolume)
				newProcess.Volume = this.MasterVolume;
		}

        private async void OnMasterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
			if(sender is MasterSessionModel masterSession && e.PropertyName == nameof(MasterSessionModel.Volume))
			{
				this.MasterVolume = masterSession.Volume;
				await SyncSessionVolume();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
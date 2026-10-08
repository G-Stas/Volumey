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
				if(isOn == value)
					return;
				isOn = value;
				if(isOn)
				{
					this.deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
					this.SetDevice(this.deviceProvider.DefaultDevice);
				}
				else
				{
					this.deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;
					this.SetDevice(null);
				}
				//take the settings now: they may be replaced before the task runs
				var general = SettingsProvider.Settings.General;
				if(general.AppVolumeSyncEnabled != value)
				{
					Task.Run(() =>
					{
						if(value)
							Logger.Info("Synchronising app volume to master volume on change.");
						general.AppVolumeSyncEnabled = value;
					});
				}
				OnPropertyChanged();
			}
		}

		private readonly IDeviceProvider deviceProvider;
		private OutputDeviceModel defaultDevice;

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppVolumeSyncViewModel));

		public AppVolumeSyncViewModel() : this(DeviceProvider.GetInstance()) { }

		internal AppVolumeSyncViewModel(IDeviceProvider deviceProvider)
		{
			this.deviceProvider = deviceProvider;
			this.IsOn = SettingsProvider.Settings.General.AppVolumeSyncEnabled;
		}

		/// <summary>
		/// Follows the volume and the new processes of the device, and stops following the previous one.
		/// </summary>
		private void SetDevice(OutputDeviceModel device)
		{
			if(this.defaultDevice != null)
			{
				this.defaultDevice.ProcessCreated -= OnProcessCreated;
				this.defaultDevice.Master.PropertyChanged -= OnMasterPropertyChanged;
			}
			this.defaultDevice = device;
			if(device != null)
			{
				this.MasterVolume = device.Master.Volume;
				device.ProcessCreated += OnProcessCreated;
				device.Master.PropertyChanged += OnMasterPropertyChanged;
			}
		}

		private async Task SyncSessionVolume()
		{
			if(this.IsOn && this.defaultDevice is OutputDeviceModel device)
			{
				foreach (var process in await device.GetImmutableProcessesAsync())
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
			this.SetDevice(newDevice);
			if(newDevice != null)
				await SyncSessionVolume();
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
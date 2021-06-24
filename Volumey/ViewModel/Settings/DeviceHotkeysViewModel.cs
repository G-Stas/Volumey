using System.Threading.Tasks;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class DeviceHotkeysViewModel : HotkeyViewModel
	{
		private OutputDeviceModel defaultDevice;

		private HotKey volumeUp;
		public HotKey VolumeUp
		{
			get => volumeUp;
			set
			{
				volumeUp = value;
				OnPropertyChanged();
			}
		}
		
		private HotKey volumeDown;
		public HotKey VolumeDown
		{
			get => volumeDown;
			set
			{
				volumeDown = value;
				OnPropertyChanged();
			}
		}

		private bool isOn;

		public bool IsOn
		{
			get => this.isOn;
			set
			{
				if(value)
				{
					if(!this.hotkeysRegistered)
						this.isOn = this.hotkeysRegistered = SaveHotkeys();
				}
				else if(this.hotkeysRegistered)
				{
					ResetHotkeys();
					this.isOn = this.hotkeysRegistered = false;
				}
				OnPropertyChanged();
			}
		}

		private bool hotkeysRegistered;

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(DeviceHotkeysViewModel));

		public DeviceHotkeysViewModel()
		{
			var deviceProvider = DeviceProvider.GetInstance();
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.defaultDevice = deviceProvider.DefaultDevice;
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			var hotkeysSettings = SettingsProvider.HotkeysSettings;
			if(hotkeysSettings.DeviceVolumeUp is HotKey volUp && hotkeysSettings.DeviceVolumeDown is HotKey volDown)
			{
				this.VolumeUp = volUp;
				this.VolumeDown = volDown;
				this.hotkeysRegistered = this.isOn = true;
				if(HotkeysControl.IsActive)
					this.RegisterLoadedHotkeys();
				else
					HotkeysControl.Activated += RegisterLoadedHotkeys;
			}
		}

		private void RegisterLoadedHotkeys()
		{
			if(this.defaultDevice != null)
			{
				this.defaultDevice.SetHotkeys(this.VolumeUp, this.VolumeDown);
				this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
		}

		private bool SaveHotkeys()
		{
			var up = this.VolumeUp;
			var down = this.VolumeDown;

			if(HotkeysControl.HotkeysAreValid(up, down) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}

			if(this.defaultDevice != null)
			{
				if(this.defaultDevice.SetHotkeys(up, down))
					this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
				else
				{
					SetErrorMessage(ErrorMessageType.VolumeReg);
					return false;
				}
			}

			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.HotkeysSettings.DeviceVolumeUp = this.VolumeUp;
			SettingsProvider.HotkeysSettings.DeviceVolumeDown = this.VolumeDown;
			Task.Run(() =>
			{
				Logger.Info($"Registered device hotkeys, +vol: [{this.VolumeUp}], -vol: [{this.VolumeDown}]");
				_ = SettingsProvider.SaveSettings();
			});
			return true;
		}

		private void ResetHotkeys()
		{
			this.defaultDevice?.ResetHotkeys();
			
			SettingsProvider.HotkeysSettings.DeviceVolumeUp = SettingsProvider.HotkeysSettings.DeviceVolumeDown = null;
			_ = SettingsProvider.SaveSettings().ConfigureAwait(false);
		}

		private void OnDefaultDeviceDisabled(OutputDeviceModel disabledDevice)
		{
			disabledDevice.ResetHotkeys();
			disabledDevice.Disabled -= OnDefaultDeviceDisabled;
			this.defaultDevice = null;
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(hotkeysRegistered)
			{
				if(this.defaultDevice != null)
				{
					this.defaultDevice.ResetHotkeys();
					this.defaultDevice.Disabled -= OnDefaultDeviceDisabled;
				}
				if(newDevice != null)
				{
					newDevice.SetHotkeys(this.VolumeUp, this.VolumeDown);
					newDevice.Disabled += OnDefaultDeviceDisabled;
				}
			}
			this.defaultDevice = newDevice;
			if(HotkeysControl.VolumeHotkeysState == HotkeysState.Disabled)
				HotkeysControl.SetHotkeysState(HotkeysState.Enabled);
		}
	}
}
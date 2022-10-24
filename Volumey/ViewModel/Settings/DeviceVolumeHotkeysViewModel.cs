using System.ComponentModel;
using System.Threading.Tasks;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class DeviceVolumeHotkeysViewModel : HotkeyViewModel
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

		private HotKey muteKey;
		public HotKey MuteKey
		{
			get => this.muteKey;
			set
			{
				this.muteKey = value;
				OnPropertyChanged();
			}
		}

		private bool volumeHotkeysRegistered;
		public bool VolumeHotkeysRegistered
		{
			get => this.volumeHotkeysRegistered;
			set
			{
				if(value)
				{
					this.volumeHotkeysRegistered = this.SaveVolumeHotkeys();
				}
				else
				{
					this.volumeHotkeysRegistered = false;
					this.ResetVolumeHotkeys();
				}
				OnPropertyChanged();
			}
		}

		private bool muteHotkeyRegistered;
		public bool MuteHotkeyRegistered
		{
			get => this.muteHotkeyRegistered;
			set
			{
				if(value)
				{
					this.muteHotkeyRegistered = this.SaveMuteHotkey();
				}
				else
				{
					this.muteHotkeyRegistered = false;
					this.ResetMuteHotkey();
				}
				OnPropertyChanged();
			}
		}
		
		private bool _preventResettingVolumeBalance;
		public bool PreventResettingVolumeBalance
		{
			get => _preventResettingVolumeBalance;
			set
			{
				_preventResettingVolumeBalance = value;
				OnPropertyChanged();
				if(SettingsProvider.HotkeysSettings.PreventResettingVolumeBalance != value)
				{
					SettingsProvider.HotkeysSettings.PreventResettingVolumeBalance = value;
					_ = Task.Run(async () => { await SettingsProvider.SaveSettings(); });
				}
			}
		}

		private AudioProcessStateNotificationMediator _dMediator;
		private AudioProcessStateNotificationMediator DeviceMediator
		{
			get
			{
				this._dMediator ??= new AudioProcessStateNotificationMediator();
				return this._dMediator;
			}
		}
		
		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(DeviceVolumeHotkeysViewModel));

		public DeviceVolumeHotkeysViewModel()
		{
			var deviceProvider = DeviceProvider.GetInstance();
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.defaultDevice = deviceProvider.DefaultDevice;
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			var hotkeysSettings = SettingsProvider.HotkeysSettings;
			PreventResettingVolumeBalance = hotkeysSettings.PreventResettingVolumeBalance;
			var anyHotkeysRegistered = false;
			
			//set volume hotkeys if they are exist in settings
			if(hotkeysSettings.DeviceVolumeUp is HotKey volUp && hotkeysSettings.DeviceVolumeDown is HotKey volDown)
			{
				anyHotkeysRegistered = true;
				this.VolumeUp = volUp;
				this.VolumeDown = volDown;
				this.volumeHotkeysRegistered = true;
			}

			if(hotkeysSettings.DeviceMute is HotKey mute)
			{
				anyHotkeysRegistered = true;
				this.muteKey = mute;
				this.muteHotkeyRegistered = true;
			}

			//register hotkeys if there are any if the hotkey manager is set or wait for activation via event
			if(anyHotkeysRegistered)
			{
				if(HotkeysControl.IsActive)
					this.RegisterLoadedHotkeys();
				else
					HotkeysControl.Activated += this.RegisterLoadedHotkeys;
			}
			SettingsProvider.NotificationsSettings.PropertyChanged += OnSettingsPropertyChanged;
		}


		private void RegisterLoadedHotkeys()
		{
			if(this.defaultDevice != null)
			{
				if(this.muteHotkeyRegistered)
				{
					this.defaultDevice.SetMuteHotkey(this.muteKey);
					if(SettingsProvider.NotificationsSettings.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				if(this.volumeHotkeysRegistered)
				{
					this.defaultDevice.SetVolumeHotkeys(this.volumeUp, this.volumeDown);
					if(SettingsProvider.NotificationsSettings.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
		}

		private bool SaveVolumeHotkeys()
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
				if(this.defaultDevice.SetVolumeHotkeys(up, down))
				{
					if(SettingsProvider.NotificationsSettings.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
					this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
				}
				else
				{
					SetErrorMessage(ErrorMessageType.VolumeReg);
					return false;
				}
			}

			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.HotkeysSettings.DeviceVolumeUp = this.VolumeUp;
			SettingsProvider.HotkeysSettings.DeviceVolumeDown = this.VolumeDown;
			_ = SettingsProvider.SaveSettings().ConfigureAwait(false);
			Logger.Info($"Registered device hotkeys, +vol: [{this.VolumeUp}], -vol: [{this.VolumeDown}]");
			return true;
		}

		private void ResetVolumeHotkeys()
		{
			if(this.defaultDevice != null)
			{
				this.defaultDevice.ResetVolumeHotkeys();
				if(!this.muteHotkeyRegistered)
				{
					this.defaultDevice.Disabled -= OnDefaultDeviceDisabled;
					this.defaultDevice.ResetStateNotificationMediator();
				}
			}
			SettingsProvider.HotkeysSettings.DeviceVolumeUp = SettingsProvider.HotkeysSettings.DeviceVolumeDown = null;
			_ = SettingsProvider.SaveSettings().ConfigureAwait(false);
		}

		private bool SaveMuteHotkey()
		{
			var key = this.muteKey;
			if(HotkeysControl.HotkeyIsValid(key) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}

			if(this.defaultDevice != null)
			{
				if(this.defaultDevice.SetMuteHotkey(key))
				{
					this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
					if(SettingsProvider.NotificationsSettings.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				else
				{
					SetErrorMessage(ErrorMessageType.OpenReg);
					return false;
				}
			}
			
			this.SetErrorMessage(ErrorMessageType.None);

			SettingsProvider.HotkeysSettings.DeviceMute = key;
			_ = SettingsProvider.SaveSettings().ConfigureAwait(false);
			Logger.Info($"Registered device mute hotkey: [{key}]");
			return true;
		}

		private void ResetMuteHotkey()
		{
			if(this.defaultDevice != null)
			{
				this.defaultDevice.ResetMuteHotkeys();
				if(!this.volumeHotkeysRegistered)
					this.defaultDevice.Disabled -= OnDefaultDeviceDisabled;
			}
			SettingsProvider.HotkeysSettings.DeviceMute = null;
			_ = SettingsProvider.SaveSettings().ConfigureAwait(false);
		}

		private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if(e.PropertyName.Equals(nameof(SettingsProvider.NotificationsSettings.Enabled)))
			{
				if(SettingsProvider.NotificationsSettings.Enabled)
					this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				else
					this.defaultDevice.ResetStateNotificationMediator();
			}
		}

		private void OnDefaultDeviceDisabled(OutputDeviceModel disabledDevice)
		{
			if(volumeHotkeysRegistered)
				disabledDevice.ResetVolumeHotkeys();
			if(muteHotkeyRegistered)
				disabledDevice.ResetMuteHotkeys();
			disabledDevice.ResetStateNotificationMediator();
			disabledDevice.Disabled -= OnDefaultDeviceDisabled;
			this.defaultDevice = null;
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.defaultDevice != null)
			{
				if(volumeHotkeysRegistered)
					this.defaultDevice.ResetVolumeHotkeys();
				if(muteHotkeyRegistered)
					this.defaultDevice.ResetMuteHotkeys();
				this.defaultDevice.ResetStateNotificationMediator();
				this.defaultDevice.Disabled -= OnDefaultDeviceDisabled;
			}

			if(newDevice != null)
			{
				if(volumeHotkeysRegistered)
				{
					newDevice.SetVolumeHotkeys(this.volumeUp, this.volumeDown);
					if(SettingsProvider.NotificationsSettings.Enabled)
						newDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				if(muteHotkeyRegistered)
				{
					newDevice.SetMuteHotkey(this.muteKey);
					if(SettingsProvider.NotificationsSettings.Enabled)
						newDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				newDevice.Disabled += OnDefaultDeviceDisabled;
			}
			this.defaultDevice = newDevice;
		}
	}
}
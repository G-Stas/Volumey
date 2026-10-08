using System.ComponentModel;
using System.Threading.Tasks;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
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
				if(SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance != value)
				{
					SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance = value;
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

		public DeviceVolumeHotkeysViewModel() : this(DeviceProvider.GetInstance()) { }

		internal DeviceVolumeHotkeysViewModel(IDeviceProvider deviceProvider)
		{
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.defaultDevice = deviceProvider.DefaultDevice;
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			var settings = SettingsProvider.Settings;
			PreventResettingVolumeBalance = settings.Hotkeys.PreventResettingVolumeBalance;
			var anyHotkeysRegistered = false;
			
			//set volume hotkeys if they are exist in settings
			if(settings.Hotkeys.DeviceVolumeUp is HotKey volUp && settings.Hotkeys.DeviceVolumeDown is HotKey volDown)
			{
				anyHotkeysRegistered = true;
				this.VolumeUp = volUp;
				this.VolumeDown = volDown;
				this.volumeHotkeysRegistered = true;
			}

			if(settings.Hotkeys.DeviceMute is HotKey mute)
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
			SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
		}


		private void RegisterLoadedHotkeys()
		{
			if(this.defaultDevice != null)
			{
				if(this.muteHotkeyRegistered)
				{
					this.defaultDevice.SetMuteHotkey(this.muteKey);
					if(SettingsProvider.Settings.Notifications.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				if(this.volumeHotkeysRegistered)
				{
					this.defaultDevice.SetVolumeHotkeys(this.volumeUp, this.volumeDown);
					if(SettingsProvider.Settings.Notifications.Enabled)
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
					if(SettingsProvider.Settings.Notifications.Enabled)
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
			SettingsProvider.Settings.Hotkeys.DeviceVolumeUp = this.VolumeUp;
			SettingsProvider.Settings.Hotkeys.DeviceVolumeDown = this.VolumeDown;
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
			SettingsProvider.Settings.Hotkeys.DeviceVolumeUp = null;
			SettingsProvider.Settings.Hotkeys.DeviceVolumeDown = null;
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
					if(SettingsProvider.Settings.Notifications.Enabled)
						this.defaultDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				else
				{
					SetErrorMessage(ErrorMessageType.OpenReg);
					return false;
				}
			}
			
			this.SetErrorMessage(ErrorMessageType.None);

			SettingsProvider.Settings.Hotkeys.DeviceMute = key;
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
			SettingsProvider.Settings.Hotkeys.DeviceMute = null;
		}

		private void OnNotificationSettingsChanged(object sender, PropertyChangedEventArgs e)
		{
			//no device (none is active, or the default one was disabled): the next default device gets the mediator when it's set
			if(e.PropertyName == nameof(NotificationSettings.Enabled) && this.defaultDevice != null)
			{
				if(SettingsProvider.Settings.Notifications.Enabled)
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
					if(SettingsProvider.Settings.Notifications.Enabled)
						newDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				if(muteHotkeyRegistered)
				{
					newDevice.SetMuteHotkey(this.muteKey);
					if(SettingsProvider.Settings.Notifications.Enabled)
						newDevice.SetStateNotificationMediator(this.DeviceMediator);
				}
				newDevice.Disabled += OnDefaultDeviceDisabled;
			}
			this.defaultDevice = newDevice;
		}
	}
}
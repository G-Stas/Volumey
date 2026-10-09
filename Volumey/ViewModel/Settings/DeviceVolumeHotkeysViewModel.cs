using System.ComponentModel;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

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
		get => muteKey;
		set
		{
			muteKey = value;
			OnPropertyChanged();
		}
	}

	private bool volumeHotkeysRegistered;
	public bool VolumeHotkeysRegistered
	{
		get => volumeHotkeysRegistered;
		set
		{
			if(value)
			{
				volumeHotkeysRegistered = SaveVolumeHotkeys();
			}
			else
			{
				volumeHotkeysRegistered = false;
				ResetVolumeHotkeys();
			}
			OnPropertyChanged();
		}
	}

	private bool muteHotkeyRegistered;
	public bool MuteHotkeyRegistered
	{
		get => muteHotkeyRegistered;
		set
		{
			if(value)
			{
				muteHotkeyRegistered = SaveMuteHotkey();
			}
			else
			{
				muteHotkeyRegistered = false;
				ResetMuteHotkey();
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
			_dMediator ??= new AudioProcessStateNotificationMediator(notifications);
			return _dMediator;
		}
	}

	private ILog _logger;
	private ILog Logger => _logger ??= LogManager.GetLogger(typeof(DeviceVolumeHotkeysViewModel));

	private readonly IHotkeyService hotkeys;
	private readonly INotificationService notifications;

	internal DeviceVolumeHotkeysViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys, INotificationService notifications)
	{
		this.hotkeys = hotkeys;
		this.notifications = notifications;
		deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
		defaultDevice = deviceProvider.DefaultDevice;

		var settings = SettingsProvider.Settings;
		PreventResettingVolumeBalance = settings.Hotkeys.PreventResettingVolumeBalance;
		var anyHotkeysRegistered = false;

		//set volume hotkeys if they are exist in settings
		if(settings.Hotkeys.DeviceVolumeUp is HotKey volUp && settings.Hotkeys.DeviceVolumeDown is HotKey volDown)
		{
			anyHotkeysRegistered = true;
			VolumeUp = volUp;
			VolumeDown = volDown;
			volumeHotkeysRegistered = true;
		}

		if(settings.Hotkeys.DeviceMute is HotKey mute)
		{
			anyHotkeysRegistered = true;
			muteKey = mute;
			muteHotkeyRegistered = true;
		}

		//register hotkeys if there are any if the hotkey manager is set or wait for activation via event
		if(anyHotkeysRegistered)
		{
			if(this.hotkeys.IsActive)
				RegisterLoadedHotkeys();
			else
				this.hotkeys.Activated += RegisterLoadedHotkeys;
		}
		SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
	}


	private void RegisterLoadedHotkeys()
	{
		if(defaultDevice != null)
		{
			if(muteHotkeyRegistered)
			{
				defaultDevice.SetMuteHotkey(muteKey);
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(DeviceMediator);
			}
			if(volumeHotkeysRegistered)
			{
				defaultDevice.SetVolumeHotkeys(volumeUp, volumeDown);
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(DeviceMediator);
			}
			defaultDevice.Disabled += OnDefaultDeviceDisabled;
		}
	}

	private bool SaveVolumeHotkeys()
	{
		var up = VolumeUp;
		var down = VolumeDown;

		if(hotkeys.HotkeysAreValid(up, down) is var error && error != ErrorMessageType.None)
		{
			SetErrorMessage(error);
			return false;
		}

		if(defaultDevice != null)
		{
			if(defaultDevice.SetVolumeHotkeys(up, down))
			{
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(DeviceMediator);
				defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
			else
			{
				SetErrorMessage(ErrorMessageType.VolumeReg);
				return false;
			}
		}

		SetErrorMessage(ErrorMessageType.None);
		SettingsProvider.Settings.Hotkeys.DeviceVolumeUp = VolumeUp;
		SettingsProvider.Settings.Hotkeys.DeviceVolumeDown = VolumeDown;
		Logger.Info($"Registered device hotkeys, +vol: [{VolumeUp}], -vol: [{VolumeDown}]");
		return true;
	}

	private void ResetVolumeHotkeys()
	{
		if(defaultDevice != null)
		{
			defaultDevice.ResetVolumeHotkeys();
			if(!muteHotkeyRegistered)
			{
				defaultDevice.Disabled -= OnDefaultDeviceDisabled;
				defaultDevice.ResetStateNotificationMediator();
			}
		}
		SettingsProvider.Settings.Hotkeys.DeviceVolumeUp = null;
		SettingsProvider.Settings.Hotkeys.DeviceVolumeDown = null;
	}

	private bool SaveMuteHotkey()
	{
		var key = muteKey;
		if(hotkeys.HotkeyIsValid(key) is var error && error != ErrorMessageType.None)
		{
			SetErrorMessage(error);
			return false;
		}

		if(defaultDevice != null)
		{
			if(defaultDevice.SetMuteHotkey(key))
			{
				defaultDevice.Disabled += OnDefaultDeviceDisabled;
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(DeviceMediator);
			}
			else
			{
				SetErrorMessage(ErrorMessageType.OpenReg);
				return false;
			}
		}

		SetErrorMessage(ErrorMessageType.None);

		SettingsProvider.Settings.Hotkeys.DeviceMute = key;
		Logger.Info($"Registered device mute hotkey: [{key}]");
		return true;
	}

	private void ResetMuteHotkey()
	{
		if(defaultDevice != null)
		{
			defaultDevice.ResetMuteHotkeys();
			if(!volumeHotkeysRegistered)
				defaultDevice.Disabled -= OnDefaultDeviceDisabled;
		}
		SettingsProvider.Settings.Hotkeys.DeviceMute = null;
	}

	private void OnNotificationSettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		//no device (none is active, or the default one was disabled): the next default device gets the mediator when it's set
		if(e.PropertyName == nameof(NotificationSettings.Enabled) && defaultDevice != null)
		{
			if(SettingsProvider.Settings.Notifications.Enabled)
				defaultDevice.SetStateNotificationMediator(DeviceMediator);
			else
				defaultDevice.ResetStateNotificationMediator();
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
		defaultDevice = null;
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		if(defaultDevice != null)
		{
			if(volumeHotkeysRegistered)
				defaultDevice.ResetVolumeHotkeys();
			if(muteHotkeyRegistered)
				defaultDevice.ResetMuteHotkeys();
			defaultDevice.ResetStateNotificationMediator();
			defaultDevice.Disabled -= OnDefaultDeviceDisabled;
		}

		if(newDevice != null)
		{
			if(volumeHotkeysRegistered)
			{
				newDevice.SetVolumeHotkeys(volumeUp, volumeDown);
				if(SettingsProvider.Settings.Notifications.Enabled)
					newDevice.SetStateNotificationMediator(DeviceMediator);
			}
			if(muteHotkeyRegistered)
			{
				newDevice.SetMuteHotkey(muteKey);
				if(SettingsProvider.Settings.Notifications.Enabled)
					newDevice.SetStateNotificationMediator(DeviceMediator);
			}
			newDevice.Disabled += OnDefaultDeviceDisabled;
		}
		defaultDevice = newDevice;
	}
}

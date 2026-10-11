using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public sealed partial class DeviceVolumeHotkeysViewModel : HotkeyViewModel
{
	private OutputDeviceModel defaultDevice;

	[ObservableProperty]
	public partial HotKey VolumeUp { get; set; }

	[ObservableProperty]
	public partial HotKey VolumeDown { get; set; }

	[ObservableProperty]
	public partial HotKey MuteKey { get; set; }

	/// <summary>
	/// Why turning the volume hotkeys on failed, under the box that has the problem; a box's error goes away when it changes
	/// </summary>
	public HotkeyPairErrors VolumeErrors { get; } = new HotkeyPairErrors();

	partial void OnVolumeUpChanged(HotKey value) => VolumeErrors.ClearUp();

	partial void OnVolumeDownChanged(HotKey value) => VolumeErrors.ClearDown();

	/// <summary>
	/// Why turning the mute hotkey on failed (its own error, so it isn't mistaken for the volume hotkeys')
	/// </summary>
	[ObservableProperty]
	public partial string MuteError { get; private set; }

	private ErrorMessageType muteErrorType = ErrorMessageType.None;

	private void SetMuteError(ErrorMessageType type)
	{
		muteErrorType = type;
		MuteError = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
	}

	partial void OnMuteKeyChanged(HotKey value) => SetMuteError(ErrorMessageType.None);

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

	[ObservableProperty]
	public partial bool PreventResettingVolumeBalance { get; set; }

	partial void OnPreventResettingVolumeBalanceChanged(bool value)
		=> SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance = value;

	//shows the OSD when a hotkey changes a volume (the models' state mediator)
	private readonly INotificationService notifications;

	internal DeviceVolumeHotkeysViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys, INotificationService notifications)
		: base(hotkeys)
	{
		this.notifications = notifications;
		ErrorDictionary.LanguageChanged += () =>
		{
			VolumeErrors.Refresh();
			SetMuteError(muteErrorType);
		};
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
			MuteKey = mute;
			muteHotkeyRegistered = true;
		}

		//register hotkeys if there are any if the hotkey manager is set or wait for activation via event
		if(anyHotkeysRegistered)
			RegisterWhenHotkeysActive(RegisterLoadedHotkeys);
		SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
	}


	private void RegisterLoadedHotkeys()
	{
		if(defaultDevice != null)
		{
			if(muteHotkeyRegistered)
			{
				defaultDevice.SetMuteHotkey(MuteKey);
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(notifications);
			}
			if(volumeHotkeysRegistered)
			{
				defaultDevice.SetVolumeHotkeys(VolumeUp, VolumeDown);
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(notifications);
			}
			defaultDevice.Disabled += OnDefaultDeviceDisabled;
		}
	}

	private bool SaveVolumeHotkeys()
	{
		var up = VolumeUp;
		var down = VolumeDown;

		VolumeErrors.Set(hotkeys.CheckHotkeysPair(up, down));
		if(VolumeErrors.Any)
			return false;

		if(defaultDevice != null)
		{
			if(defaultDevice.SetVolumeHotkeys(up, down, out var failed))
			{
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(notifications);
				defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
			else
			{
				//the hotkey service logs why
				VolumeErrors.SetFor(failed, up, ErrorMessageType.VolumeReg);
				return false;
			}
		}

		SettingsProvider.Settings.Hotkeys.DeviceVolumeUp = VolumeUp;
		SettingsProvider.Settings.Hotkeys.DeviceVolumeDown = VolumeDown;
		Logger.Info($"Registered device hotkeys, +vol: [{VolumeUp}], -vol: [{VolumeDown}]");
		return true;
	}

	private void ResetVolumeHotkeys()
	{
		VolumeErrors.Clear();
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
		var key = MuteKey;
		var error = key == null ? ErrorMessageType.Missing : hotkeys.HotkeyIsValid(key);
		SetMuteError(error);
		if(error != ErrorMessageType.None)
			return false;

		if(defaultDevice != null)
		{
			if(defaultDevice.SetMuteHotkey(key))
			{
				defaultDevice.Disabled += OnDefaultDeviceDisabled;
				if(SettingsProvider.Settings.Notifications.Enabled)
					defaultDevice.SetStateNotificationMediator(notifications);
			}
			else
			{
				SetMuteError(ErrorMessageType.OpenReg);
				return false;
			}
		}

		SettingsProvider.Settings.Hotkeys.DeviceMute = key;
		Logger.Info($"Registered device mute hotkey: [{key}]");
		return true;
	}

	private void ResetMuteHotkey()
	{
		SetMuteError(ErrorMessageType.None);
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
				defaultDevice.SetStateNotificationMediator(notifications);
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
				newDevice.SetVolumeHotkeys(VolumeUp, VolumeDown);
				if(SettingsProvider.Settings.Notifications.Enabled)
					newDevice.SetStateNotificationMediator(notifications);
			}
			if(muteHotkeyRegistered)
			{
				newDevice.SetMuteHotkey(MuteKey);
				if(SettingsProvider.Settings.Notifications.Enabled)
					newDevice.SetStateNotificationMediator(notifications);
			}
			newDevice.Disabled += OnDefaultDeviceDisabled;
		}
		defaultDevice = newDevice;
	}
}

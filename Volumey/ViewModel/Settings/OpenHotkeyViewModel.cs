using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.DataProvider;

namespace Volumey.ViewModel.Settings;

public partial class OpenHotkeyViewModel : HotkeyViewModel
{
	[ObservableProperty]
	public partial HotKey Hotkey { get; set; }

	private bool isOn;
	public bool IsOn
	{
		get => isOn;
		set
		{
			if(value)
			{
				if(!hotkeyRegistered)
					isOn = hotkeyRegistered = SaveHotkey();

			}
			else if(hotkeyRegistered)
			{
				ResetHotkey();
				isOn = false;
			}
			OnPropertyChanged();
		}
	}

	private bool hotkeyRegistered;

	internal OpenHotkeyViewModel(IHotkeyService hotkeys) : base(hotkeys)
	{
		if(SettingsProvider.Settings.Hotkeys.OpenMixer is HotKey openHotkey)
		{
			hotkeyRegistered = isOn = true;
			Hotkey = openHotkey;
			RegisterWhenHotkeysActive(RegisterLoadedHotkey);
		}
	}

	private void RegisterLoadedHotkey()
	{
		//shows an error instead of silently doing nothing; it stays saved and is tried again on the next start
		if(!hotkeys.RegisterOpenMixerHotkey(Hotkey))
			SetErrorMessage(ErrorMessageType.OpenReg);
	}

	private bool SaveHotkey()
	{
		if(!IsValid(hotkeys.HotkeyIsValid(Hotkey)))
			return false;
		SetErrorMessage(ErrorMessageType.None);
		if(hotkeys.RegisterOpenMixerHotkey(Hotkey))
		{
			SettingsProvider.Settings.Hotkeys.OpenMixer = Hotkey;
			Logger.Info($"Registered open mixer hotkey: [{Hotkey}]");
			return true;
		}
		//the hotkey service logs why
		SetErrorMessage(ErrorMessageType.OpenReg);
		return false;
	}

	private void ResetHotkey()
	{
		try
		{
			hotkeyRegistered = false;
			hotkeys.UnregisterOpenMixerHotkey(Hotkey);
		}
		catch { }
		SetErrorMessage(ErrorMessageType.None);
		SettingsProvider.Settings.Hotkeys.OpenMixer = null;
	}
}

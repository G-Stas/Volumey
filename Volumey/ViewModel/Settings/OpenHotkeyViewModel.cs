using log4net;
using Volumey.Controls;
using Volumey.DataProvider;

namespace Volumey.ViewModel.Settings;

public class OpenHotkeyViewModel : HotkeyViewModel
{
	private HotKey hotkey;
	public HotKey Hotkey
	{
		get => hotkey;
		set
		{
			hotkey = value;
			OnPropertyChanged();
		}
	}

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

	private ILog _logger;
	private ILog Logger => _logger ??= LogManager.GetLogger(typeof(OpenHotkeyViewModel));

	private readonly IHotkeyService hotkeys;

	internal OpenHotkeyViewModel(IHotkeyService hotkeys)
	{
		this.hotkeys = hotkeys;

		if(SettingsProvider.Settings.Hotkeys.OpenMixer is HotKey openHotkey)
		{
			hotkeyRegistered = isOn = true;
			Hotkey = openHotkey;
			if(this.hotkeys.IsActive)
				RegisterLoadedHotkey();
			else
				this.hotkeys.Activated += RegisterLoadedHotkey;
		}
	}

	private void RegisterLoadedHotkey()
	{
		//shows an error instead of silently doing nothing; it stays saved and is tried again on the next start
		if(!hotkeys.RegisterOpenMixerHotkey(Hotkey))
			SetErrorMessage(ErrorMessageType.OpenReg);
	}

	private bool SaveHotkey(object param = null)
	{
		if(hotkeys.HotkeyIsValid(hotkey) is var error && error != ErrorMessageType.None)
		{
			SetErrorMessage(error);
			return false;
		}
		SetErrorMessage(ErrorMessageType.None);
		if(hotkeys.RegisterOpenMixerHotkey(hotkey))
		{
			SettingsProvider.Settings.Hotkeys.OpenMixer = hotkey;
			Logger.Info($"Registered open mixer hotkey: [{hotkey}]");
			return true;
		}
		//the hotkey service logs why
		SetErrorMessage(ErrorMessageType.OpenReg);
		return false;
	}

	private void ResetHotkey(object param = null)
	{
		try
		{
			hotkeyRegistered = false;
			hotkeys.UnregisterOpenMixerHotkey(hotkey);
		}
		catch { }
		SetErrorMessage(ErrorMessageType.None);
		SettingsProvider.Settings.Hotkeys.OpenMixer = null;
	}
}

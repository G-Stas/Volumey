using System;
using System.Threading.Tasks;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;

namespace Volumey.ViewModel.Settings
{
	public class OpenHotkeyViewModel : HotkeyViewModel
	{
		private HotKey hotkey;
		public HotKey Hotkey
		{
			get => hotkey;
			set
			{
				this.hotkey = value;
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
					if(!this.hotkeyRegistered)
						this.isOn = this.hotkeyRegistered = SaveHotkey();
					
				}
				else if(this.hotkeyRegistered)
				{
					ResetHotkey();
					this.isOn = false;
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
				this.hotkeyRegistered = this.isOn = true;
				this.Hotkey = openHotkey;
				if(this.hotkeys.IsActive)
					this.RegisterLoadedHotkey();
				else
					this.hotkeys.Activated += RegisterLoadedHotkey;
			}
		}

		private void RegisterLoadedHotkey()
		{
			//shows an error instead of silently doing nothing; it stays saved and is tried again on the next start
			if(!this.hotkeys.RegisterOpenMixerHotkey(this.Hotkey))
				this.SetErrorMessage(ErrorMessageType.OpenReg);
		}
		
		private bool SaveHotkey(object param = null)
		{
			if(this.hotkeys.HotkeyIsValid(this.hotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			if(this.hotkeys.RegisterOpenMixerHotkey(this.hotkey))
			{
				SettingsProvider.Settings.Hotkeys.OpenMixer = this.hotkey;
				Logger.Info($"Registered open mixer hotkey: [{this.hotkey}]");
				return true;
			}
			//the hotkey service logs why
			this.SetErrorMessage(ErrorMessageType.OpenReg);
			return false;
		}

		private void ResetHotkey(object param = null)
		{
			try
			{
				this.hotkeyRegistered = false;
				this.hotkeys.UnregisterOpenMixerHotkey(this.hotkey);
			}
			catch { }
			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.Settings.Hotkeys.OpenMixer = null;
		}
	}
}
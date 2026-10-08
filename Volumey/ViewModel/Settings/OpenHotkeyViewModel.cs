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
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

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
			try
			{
				this.hotkeys.RegisterOpenMixerHotkey(this.Hotkey);
			}
			catch { }
		}
		
		private bool SaveHotkey(object param = null)
		{
			if(this.hotkeys.HotkeyIsValid(this.hotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			try
			{
				if(this.hotkeys.RegisterOpenMixerHotkey(this.hotkey))
				{
					SettingsProvider.Settings.Hotkeys.OpenMixer = this.hotkey;
					Task.Run(() =>
					{
						Logger.Info($"Registered open mixer hotkey: [{this.hotkey}]");
					});	
					return true;
				}
			}
			catch(Exception e)
			{
				Task.Run(() =>
				{
					Logger.Error($"Failed to register open mixer hotkey, hotkey: [{this.hotkey}]", e);
				});
			}
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
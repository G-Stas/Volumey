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
		
		internal OpenHotkeyViewModel()
		{
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			if(SettingsProvider.HotkeysSettings.OpenMixer is HotKey openHotkey)
			{
				this.hotkeyRegistered = this.isOn = true;
				this.Hotkey = openHotkey;
				if(HotkeysControl.IsActive)
					this.RegisterLoadedHotkey();
				else
					HotkeysControl.Activated += RegisterLoadedHotkey;
			}
		}

		private void RegisterLoadedHotkey()
		{
			try
			{
				HotkeysControl.RegisterOpenMixerHotkey(this.Hotkey);
			}
			catch { }
		}
		
		private bool SaveHotkey(object param = null)
		{
			if(HotkeysControl.HotkeyIsValid(this.hotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			try
			{
				if(HotkeysControl.RegisterOpenMixerHotkey(this.hotkey))
				{
					SettingsProvider.HotkeysSettings.OpenMixer = this.hotkey;
					Task.Run(() =>
					{
						Logger.Info($"Registered open mixer hotkey: [{this.hotkey}]");
						_ = SettingsProvider.SaveSettings();
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
				HotkeysControl.UnregisterOpenMixerHotkey(this.hotkey);
			}
			catch { }
			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.HotkeysSettings.OpenMixer = null;
			_ = SettingsProvider.SaveSettings();
		}
	}
}
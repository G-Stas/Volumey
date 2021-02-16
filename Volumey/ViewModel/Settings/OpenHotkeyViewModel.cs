using System;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors.Core;
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

		private bool openHotkeyRegistered;
		public bool OpenHotkeyRegistered
		{
			get => openHotkeyRegistered;
			set
			{
				this.openHotkeyRegistered = value;
				OnPropertyChanged();
			}
		}
		
		public ICommand ToggleOpenHotkeyCommand { get; }

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(OpenHotkeyViewModel));
		
		internal OpenHotkeyViewModel()
		{
			ToggleOpenHotkeyCommand = new ActionCommand(OnToggleOpenHotkey);
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			if(SettingsProvider.HotkeysSettings.OpenMixer is HotKey openHotkey)
			{
				this.OpenHotkeyRegistered = true;
				this.Hotkey = openHotkey;
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

		private async void OnToggleOpenHotkey(object param)
		{
			if(param is bool isToggled)
			{
				if(isToggled)
					await SaveOpenMixerHotkey().ConfigureAwait(false);
				else
					await ResetOpenMixerHotkey().ConfigureAwait(false);
			}
		}

		private async Task SaveOpenMixerHotkey(object param = null)
		{
			if(HotkeysControl.HotkeyIsValid(this.hotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				this.OpenHotkeyRegistered = false;
				return;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			try
			{
				if(HotkeysControl.RegisterOpenMixerHotkey(this.hotkey))
				{
					SettingsProvider.HotkeysSettings.OpenMixer = this.hotkey;
					await SettingsProvider.SaveSettings().ConfigureAwait(false);
					Logger.Info($"Registered open mixer hotkey: [{this.hotkey}]");
					return;
				}
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to register open mixer hotkey, hotkey: [{this.hotkey}]", e);
			}
			this.SetErrorMessage(ErrorMessageType.OpenReg);
			this.OpenHotkeyRegistered = false;
		}

		private async Task ResetOpenMixerHotkey(object param = null)
		{
			try
			{
				this.OpenHotkeyRegistered = false;
				HotkeysControl.UnregisterOpenMixerHotkey(this.hotkey);
			}
			catch { }
			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.HotkeysSettings.OpenMixer = null;
			await SettingsProvider.SaveSettings().ConfigureAwait(false);
		}
	}
}
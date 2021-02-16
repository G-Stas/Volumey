using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class DeviceHotkeysViewModel : HotkeyViewModel
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

		private bool hotkeysAreRegistered;
		public bool HotkeysAreRegistered
		{
			get => hotkeysAreRegistered;
			set
			{
				hotkeysAreRegistered = value;
				OnPropertyChanged();
			}
		}

		public ICommand ToggleDeviceHotkeysCommand { get; }
		
		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(DeviceHotkeysViewModel));

		public DeviceHotkeysViewModel()
		{
			var deviceProvider = DeviceProvider.GetInstance();
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.defaultDevice = deviceProvider.DefaultDevice;
			this.ToggleDeviceHotkeysCommand = new ActionCommand(OnToggleMusicHotkeys);
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			var hotkeysSettings = SettingsProvider.HotkeysSettings;
			if(hotkeysSettings.DeviceVolumeUp is HotKey volUp && hotkeysSettings.DeviceVolumeDown is HotKey volDown)
			{
				this.VolumeUp = volUp;
				this.VolumeDown = volDown;
				this.HotkeysAreRegistered = true;
				HotkeysControl.Activated += RegisterLoadedHotkeys;
			}
		}

		private void RegisterLoadedHotkeys()
		{
			if(this.defaultDevice != null)
			{
				this.defaultDevice.SetHotkeys(this.VolumeUp, this.VolumeDown);
				this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
		}
		
		private async void OnToggleMusicHotkeys(object param)
		{
			if(param is bool isToggled)
			{
				if(isToggled)
					await SaveDeviceHotkeys().ConfigureAwait(false);
				else
					await ResetDeviceHotkeys().ConfigureAwait(false);
			}
		}

		private async Task SaveDeviceHotkeys()
		{
			if(this.defaultDevice == null)
				return;
			var up = this.VolumeUp;
			var down = this.VolumeDown;

			if(HotkeysControl.HotkeysAreValid(up, down) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				this.HotkeysAreRegistered = false;
				return;
			}

			if(this.defaultDevice.SetHotkeys(up, down))
			{
				this.defaultDevice.Disabled += OnDefaultDeviceDisabled;
			}
			else
			{
				SetErrorMessage(ErrorMessageType.VolumeReg);
				this.HotkeysAreRegistered = false;
				return;
			}

			this.HotkeysAreRegistered = true;
			this.SetErrorMessage(ErrorMessageType.None);
			try
			{
				SettingsProvider.HotkeysSettings.DeviceVolumeUp = this.VolumeUp;
				SettingsProvider.HotkeysSettings.DeviceVolumeDown = this.VolumeDown;
				await SettingsProvider.SaveSettings().ConfigureAwait(false);

				Logger.Info($"Registered device hotkeys, +vol: [{this.VolumeUp}], -vol: [{this.VolumeDown}]");
			}
			catch { }
		}

		private async Task ResetDeviceHotkeys()
		{
			this.defaultDevice?.ResetHotkeys();
			this.HotkeysAreRegistered = false;
			
			SettingsProvider.HotkeysSettings.DeviceVolumeUp = SettingsProvider.HotkeysSettings.DeviceVolumeDown = null;
			await SettingsProvider.SaveSettings().ConfigureAwait(false);
		}

		private void OnDefaultDeviceDisabled(OutputDeviceModel disabledDevice)
		{
			disabledDevice.ResetHotkeys();
			disabledDevice.Disabled -= OnDefaultDeviceDisabled;
			this.defaultDevice = null;
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(hotkeysAreRegistered)
			{
				if(this.defaultDevice != null)
				{
					this.defaultDevice.ResetHotkeys();
					this.defaultDevice.Disabled -= OnDefaultDeviceDisabled;
				}
				if(newDevice != null)
				{
					newDevice.SetHotkeys(this.VolumeUp, this.VolumeDown);
					newDevice.Disabled += OnDefaultDeviceDisabled;
				}
			}
			this.defaultDevice = newDevice;
			if(HotkeysControl.VolumeHotkeysState == HotkeysState.Disabled)
				HotkeysControl.SetHotkeysState(HotkeysState.Enabled);
		}
	}
}
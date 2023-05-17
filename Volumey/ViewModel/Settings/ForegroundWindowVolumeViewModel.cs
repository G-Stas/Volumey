using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class ForegroundWindowVolumeViewModel : HotkeyViewModel
	{
		private HotKey _volumeUpHotkey;
		public HotKey VolumeUpHotkey
		{
			get => this._volumeUpHotkey;
			set
			{
				this._volumeUpHotkey = value;
				OnPropertyChanged();
			}
		}

		private HotKey _volumeDownHotkey;
		public HotKey VolumeDownHotkey
		{
			get => this._volumeDownHotkey;
			set
			{
				this._volumeDownHotkey = value;
				OnPropertyChanged();
			}
		}

		private bool _hotkeysRegistered;
		public bool HotkeysRegistered
		{
			get => this._hotkeysRegistered;
			set
			{
				if(value)
				{
					if(!_hotkeysRegistered)
					{
						this._hotkeysRegistered = SaveHotkeys();
						OnPropertyChanged();
						return;
					}
				}
				else if(_hotkeysRegistered)
				{
					ResetHotkeys();
				}
				this._hotkeysRegistered = value;
				OnPropertyChanged();
			}
		}

		private AudioProcessStateNotificationMediator _mediator;
		private AudioProcessStateNotificationMediator StateMediator => _mediator ??= new AudioProcessStateNotificationMediator();

		private OutputDeviceModel _defaultDevice;

		private ILog _logger;
		private ILog Logger => _logger ??=LogManager.GetLogger(typeof(ForegroundWindowVolumeViewModel));

		internal ForegroundWindowVolumeViewModel()
		{
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			var deviceProvider = DeviceProvider.GetInstance();
			_defaultDevice = deviceProvider.DefaultDevice;
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;

			if(SettingsProvider.HotkeysSettings.ForegroundVolumeUp is HotKey volumeUp && SettingsProvider.HotkeysSettings.ForegroundVolumeDown is HotKey volumeDown)
			{
				this._hotkeysRegistered = true;
				this.VolumeUpHotkey = volumeUp;
				this.VolumeDownHotkey = volumeDown;
				if(HotkeysControl.IsActive)
					RegisterLoadedHotkeys();
				else
					HotkeysControl.Activated += RegisterLoadedHotkeys;
			}
		}

		private void RegisterLoadedHotkeys()
		{
			try
			{
				HotkeysControl.RegisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey);
				HotkeysControl.HotkeyPressed += OnHotkeyPressed;
			}
			catch { }
		}

		private bool SaveHotkeys()
		{
			if(HotkeysControl.HotkeysAreValid(this._volumeUpHotkey, this._volumeDownHotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			try
			{
				if(HotkeysControl.RegisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey))
				{
					SettingsProvider.HotkeysSettings.ForegroundVolumeUp = this._volumeUpHotkey;
					SettingsProvider.HotkeysSettings.ForegroundVolumeDown = this._volumeDownHotkey;
					HotkeysControl.HotkeyPressed += OnHotkeyPressed;
					Task.Run(() =>
					{
						Logger.Info($"Registered foreground hotkeys: [{this._volumeUpHotkey}] [{this._volumeDownHotkey}]");
						_ = SettingsProvider.SaveSettings();
					});	
					return true;
				}
			}
			catch(Exception e)
			{
				Task.Run(() =>
				{
					Logger.Error($"Failed to register foreground hotkeys: [{this._volumeUpHotkey}, {this._volumeDownHotkey}]", e);
				});
			}
			this.SetErrorMessage(ErrorMessageType.VolumeReg);
			return false;
		}

		private void ResetHotkeys()
		{
			try
			{
				HotkeysControl.UnregisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey);
			}
			catch { }
			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.HotkeysSettings.ForegroundVolumeUp = null;
			SettingsProvider.HotkeysSettings.ForegroundVolumeDown = null;
			HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
			_ = SettingsProvider.SaveSettings();
		}

		private async Task<(bool succeed, AudioProcessModel? foundProcess)> TryGetForegroundAudioProcess()
		{
			if(_defaultDevice == null)
			{
				return (false, null);
			}

			uint foregroundWindowProcId = GetForegroundWindowProcessId();
			if(foregroundWindowProcId == uint.MinValue)
			{
				return (false, null);
			}

			Process process = null;
			string filePath = string.Empty;
			try
			{
				process = Process.GetProcessById((int)foregroundWindowProcId);
				filePath = process.MainModule.FileName;
			}
			catch { }

			AudioProcessModel foundProcess;
			try
			{
				IImmutableList<AudioProcessModel> curDeviceProcesses = await _defaultDevice.GetImmutableProcessesAsync();
				foundProcess = curDeviceProcesses.First(p => p.ProcessId == foregroundWindowProcId || p.RawProcessName.Equals(process?.ProcessName) || p.FilePath.Equals(filePath)); 
			}
			catch
			{
				return (false, null);
			}
			return (true, foundProcess);
		}

		private uint GetForegroundWindowProcessId()
		{
			try
			{
				return NativeMethods.GetForegroundWindowProcessId();
			}
			catch(Exception e)
			{
				Logger.Error("Failed to get foreground window process id", e);
			}
			return uint.MinValue;
		}

		private async void OnHotkeyPressed(HotKey hotkey)
		{
			if(!_hotkeysRegistered)
				return;
			if(_defaultDevice == null)
				return;

			if(hotkey.Equals(this._volumeUpHotkey))
			{
				if(await TryGetForegroundAudioProcess() is (bool succeed, AudioProcessModel foundProcess) && succeed)
				{
					if(foundProcess.StateNotificationMediator == null)
						foundProcess.SetStateMediator(this.StateMediator);
					foundProcess.SetVolume(foundProcess.Volume + HotkeysControl.VolumeStep, notify: true, ref GuidValue.Internal.Empty);
				}
			}
			else if(hotkey.Equals(this._volumeDownHotkey))
			{
				if(await TryGetForegroundAudioProcess() is (bool succeed, AudioProcessModel foundProcess) && succeed)
				{
					if(foundProcess.StateNotificationMediator == null)
						foundProcess.SetStateMediator(this.StateMediator);
					foundProcess.SetVolume(foundProcess.Volume - HotkeysControl.VolumeStep, notify: true, ref GuidValue.Internal.Empty);
				}
			}
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDefaultDevice)
		{
			_defaultDevice = newDefaultDevice;
		}
	}
}
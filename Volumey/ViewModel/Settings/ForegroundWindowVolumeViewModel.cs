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
using Volumey.Services;

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
		private AudioProcessStateNotificationMediator StateMediator => _mediator ??= new AudioProcessStateNotificationMediator(this.notifications);

		private OutputDeviceModel _defaultDevice;

		private ILog _logger;
		private ILog Logger => _logger ??=LogManager.GetLogger(typeof(ForegroundWindowVolumeViewModel));

		private readonly IHotkeyService hotkeys;
		private readonly INotificationService notifications;

		internal ForegroundWindowVolumeViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys, INotificationService notifications)
		{
			this.hotkeys = hotkeys;
			this.notifications = notifications;

			_defaultDevice = deviceProvider.DefaultDevice;
			deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;

			if(SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp is HotKey volumeUp && SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown is HotKey volumeDown)
			{
				this._hotkeysRegistered = true;
				this.VolumeUpHotkey = volumeUp;
				this.VolumeDownHotkey = volumeDown;
				if(this.hotkeys.IsActive)
					RegisterLoadedHotkeys();
				else
					this.hotkeys.Activated += RegisterLoadedHotkeys;
			}
		}

		private void RegisterLoadedHotkeys()
		{
			//a saved pair that fails now (e.g. another app took the combination) shows an error instead of silently doing nothing;
			//it stays saved and is tried again on the next start
			if(this.hotkeys.RegisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey))
				this.hotkeys.HotkeyPressed += OnHotkeyPressed;
			else
				this.SetErrorMessage(ErrorMessageType.VolumeReg);
		}

		private bool SaveHotkeys()
		{
			if(this.hotkeys.HotkeysAreValid(this._volumeUpHotkey, this._volumeDownHotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return false;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			if(this.hotkeys.RegisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey))
			{
				SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp = this._volumeUpHotkey;
				SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown = this._volumeDownHotkey;
				this.hotkeys.HotkeyPressed += OnHotkeyPressed;
				Logger.Info($"Registered foreground hotkeys: [{this._volumeUpHotkey}] [{this._volumeDownHotkey}]");
				return true;
			}
			//the hotkey service logs why
			this.SetErrorMessage(ErrorMessageType.VolumeReg);
			return false;
		}

		private void ResetHotkeys()
		{
			try
			{
				this.hotkeys.UnregisterHotkeysPair(this._volumeUpHotkey, this._volumeDownHotkey);
			}
			catch { }
			this.SetErrorMessage(ErrorMessageType.None);
			SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp = null;
			SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown = null;
			this.hotkeys.HotkeyPressed -= OnHotkeyPressed;
		}

		private async Task<(bool succeed, AudioProcessModel foundProcess)> TryGetForegroundAudioProcess()
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

			long started = Stopwatch.GetTimestamp();
			Process process = null;
			string filePath = string.Empty;
			Exception pathError = null;
			try
			{
				process = Process.GetProcessById((int)foregroundWindowProcId);
				filePath = process.MainModule.FileName;
			}
			catch(Exception e)
			{
				//e.g. a game protected by anti-cheat doesn't let other processes read its modules
				pathError = e;
			}

			AudioProcessModel foundProcess = null;
			IImmutableList<AudioProcessModel> curDeviceProcesses = ImmutableList<AudioProcessModel>.Empty;
			try
			{
				curDeviceProcesses = await _defaultDevice.GetImmutableProcessesAsync();
				foundProcess = curDeviceProcesses.FirstOrDefault(p => p.ProcessId == foregroundWindowProcId || p.RawProcessName.Equals(process?.ProcessName) || p.FilePath.Equals(filePath));
			}
			catch { }
			HotkeyDiagnostics.ForegroundLookup(foregroundWindowProcId, SafeName(process), filePath, pathError, curDeviceProcesses, foundProcess,
				Stopwatch.GetElapsedTime(started).TotalMilliseconds);
			return (foundProcess != null, foundProcess);
		}

		private static string SafeName(Process process)
		{
			try { return process?.ProcessName; }
			catch { return null; }
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

		private void OnHotkeyPressed(HotKey hotkey)
		{
			if(!_hotkeysRegistered || _defaultDevice == null)
				return;

			int direction = hotkey.Equals(this._volumeUpHotkey) ? 1 : hotkey.Equals(this._volumeDownHotkey) ? -1 : 0;
			if(direction != 0)
				ChangeForegroundVolumeAsync(hotkey, direction).Forget(Logger, "Failed to change the volume of the foreground app");
		}

		private async Task ChangeForegroundVolumeAsync(HotKey hotkey, int direction)
		{
			HotkeyDiagnostics.ForegroundHotkeyPressed(hotkey);
			if(await TryGetForegroundAudioProcess() is (true, AudioProcessModel foundProcess))
			{
				if(foundProcess.StateNotificationMediator == null)
					foundProcess.SetStateMediator(this.StateMediator);
				foundProcess.SetVolume(foundProcess.Volume + direction * this.hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
				HotkeyDiagnostics.ForegroundVolumeSet(foundProcess, foundProcess.Volume);
			}
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDefaultDevice)
		{
			_defaultDevice = newDefaultDevice;
		}
	}
}
#nullable enable
using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public sealed partial class ForegroundWindowVolumeViewModel : HotkeyViewModel
{
	//each hotkey is optional; turning it on needs at least one
	[ObservableProperty]
	public partial HotKey? VolumeUpHotkey { get; set; }

	[ObservableProperty]
	public partial HotKey? VolumeDownHotkey { get; set; }

	[ObservableProperty]
	public partial HotKey? MuteHotkey { get; set; }

	/// <summary>
	/// Why turning it on failed, under the hotkey box that has the problem; a box's error goes away when it changes
	/// </summary>
	public HotkeyPairErrors Errors { get; } = new HotkeyPairErrors();

	partial void OnVolumeUpHotkeyChanged(HotKey? value) => Errors.ClearUp();

	partial void OnVolumeDownHotkeyChanged(HotKey? value) => Errors.ClearDown();

	partial void OnMuteHotkeyChanged(HotKey? value) => Errors.ClearMute();

	private HotKey?[] AllHotkeys => new[] { VolumeUpHotkey, VolumeDownHotkey, MuteHotkey };

	private bool _hotkeysRegistered;
	public bool HotkeysRegistered
	{
		get => _hotkeysRegistered;
		set
		{
			if(value)
			{
				if(!_hotkeysRegistered)
				{
					_hotkeysRegistered = SaveHotkeys();
					OnPropertyChanged();
					return;
				}
			}
			else if(_hotkeysRegistered)
			{
				ResetHotkeys();
			}
			_hotkeysRegistered = value;
			OnPropertyChanged();
		}
	}

	private OutputDeviceModel? _defaultDevice;

	private readonly INotificationService notifications;

	internal ForegroundWindowVolumeViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys, INotificationService notifications)
		: base(hotkeys)
	{
		this.notifications = notifications;
		ErrorDictionary.LanguageChanged += Errors.Refresh;

		_defaultDevice = deviceProvider.DefaultDevice;
		deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;

		var saved = SettingsProvider.Settings.Hotkeys;
		if(saved.ForegroundVolumeUp != null || saved.ForegroundVolumeDown != null || saved.ForegroundMute != null)
		{
			_hotkeysRegistered = true;
			VolumeUpHotkey = saved.ForegroundVolumeUp;
			VolumeDownHotkey = saved.ForegroundVolumeDown;
			MuteHotkey = saved.ForegroundMute;
			RegisterWhenHotkeysActive(RegisterLoadedHotkeys);
		}
	}

	private void RegisterLoadedHotkeys()
	{
		//saved hotkeys that fail now (e.g. another app took the combination) show an error instead of silently doing nothing;
		//they stay saved and are tried again on the next start
		if(hotkeys.RegisterHotkeys(AllHotkeys, out var failed))
			hotkeys.HotkeyPressed += OnHotkeyPressed;
		else
			Errors.SetFor(failed, VolumeUpHotkey, VolumeDownHotkey, ErrorMessageType.VolumeReg);
	}

	private bool SaveHotkeys()
	{
		Errors.Set(hotkeys.CheckAppHotkeys(VolumeUpHotkey, VolumeDownHotkey, MuteHotkey));
		if(Errors.Any)
			return false;
		if(hotkeys.RegisterHotkeys(AllHotkeys, out var failed))
		{
			SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp = VolumeUpHotkey;
			SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown = VolumeDownHotkey;
			SettingsProvider.Settings.Hotkeys.ForegroundMute = MuteHotkey;
			hotkeys.HotkeyPressed += OnHotkeyPressed;
			Logger.Info($"Registered foreground hotkeys: [{VolumeUpHotkey}] [{VolumeDownHotkey}] mute: [{MuteHotkey}]");
			return true;
		}
		//the hotkey service logs why
		Errors.SetFor(failed, VolumeUpHotkey, VolumeDownHotkey, ErrorMessageType.VolumeReg);
		return false;
	}

	private void ResetHotkeys()
	{
		try
		{
			hotkeys.UnregisterHotkeys(AllHotkeys);
		}
		catch { }
		Errors.Clear();
		SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp = null;
		SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown = null;
		SettingsProvider.Settings.Hotkeys.ForegroundMute = null;
		hotkeys.HotkeyPressed -= OnHotkeyPressed;
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

		long started = Stopwatch.GetTimestamp();
		AudioProcessModel? foundProcess = null;
		IImmutableList<AudioProcessModel> curDeviceProcesses = ImmutableList<AudioProcessModel>.Empty;
		string? processName = null;
		string filePath = string.Empty;
		Exception? pathError = null;
		try
		{
			curDeviceProcesses = await _defaultDevice.GetImmutableProcessesAsync();
			//the process id matches most of the time; the process is only opened when it doesn't
			foundProcess = FindByProcessId(curDeviceProcesses, foregroundWindowProcId);
			if(foundProcess == null)
			{
				(processName, filePath, pathError) = GetProcessNameAndPath(foregroundWindowProcId);
				foundProcess = FindByNameOrPath(curDeviceProcesses, processName, filePath);
			}
		}
		catch { }
		HotkeyDiagnostics.ForegroundLookup(foregroundWindowProcId, processName, filePath, pathError, curDeviceProcesses, foundProcess,
			Stopwatch.GetElapsedTime(started).TotalMilliseconds);
		return (foundProcess != null, foundProcess);
	}

	private static AudioProcessModel? FindByProcessId(IImmutableList<AudioProcessModel> processes, uint processId)
	{
		foreach(var process in processes)
		{
			if(process.ProcessId == processId)
				return process;
		}
		return null;
	}

	private static AudioProcessModel? FindByNameOrPath(IImmutableList<AudioProcessModel> processes, string? processName, string filePath)
	{
		foreach(var process in processes)
		{
			if(process.RawProcessName.Equals(processName) || process.FilePath.Equals(filePath))
				return process;
		}
		return null;
	}

	/// <summary>
	/// Reads the path from the process or the system's process list (<see cref="NativeMethods.GetProcessPath"/>), which also works for
	/// elevated apps and games guarded by anti-cheat (they don't let other processes read their modules).
	/// The name falls back to the process list when the path can't be read.
	/// </summary>
	private static (string? name, string path, Exception? pathError) GetProcessNameAndPath(uint processId)
	{
		string? path = NativeMethods.GetProcessPath((int)processId, out int error);
		if(path != null)
			return (NativeMethods.ProcessNameFromPath(path), path, null);

		string? name = null;
		try
		{
			using var process = Process.GetProcessById((int)processId);
			name = process.ProcessName;
		}
		catch { }
		return (name, string.Empty, new Win32Exception(error));
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

		int direction = hotkey.Equals(VolumeUpHotkey) ? 1 : hotkey.Equals(VolumeDownHotkey) ? -1 : 0;
		if(direction != 0)
			ChangeForegroundVolumeAsync(hotkey, direction).Forget(Logger, "Failed to change the volume of the foreground app");
		else if(hotkey.Equals(MuteHotkey))
			ChangeForegroundVolumeAsync(hotkey, 0).Forget(Logger, "Failed to mute the foreground app");
	}

	/// <param name="direction">1 up, -1 down by the volume step; 0 mutes or unmutes the whole app</param>
	private async Task ChangeForegroundVolumeAsync(HotKey hotkey, int direction)
	{
		HotkeyDiagnostics.ForegroundHotkeyPressed(hotkey);
		if(await TryGetForegroundAudioProcess() is (true, AudioProcessModel foundProcess))
		{
			//like the app hotkeys: the OSD only while notifications are on
			if(SettingsProvider.Settings.Notifications.Enabled && foundProcess.StateNotificationMediator == null)
				foundProcess.SetStateMediator(notifications);
			if(direction == 0)
			{
				foundProcess.SetMute(!foundProcess.IsMuted, notify: true, GuidValue.Internal.Empty);
				return;
			}
			foundProcess.SetVolume(foundProcess.Volume + direction * hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
			HotkeyDiagnostics.ForegroundVolumeSet(foundProcess, foundProcess.Volume);
		}
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel? newDefaultDevice)
	{
		_defaultDevice = newDefaultDevice;
	}
}

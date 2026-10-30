#nullable enable
using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
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

	private readonly IAppRouting appRouting;

	private readonly INotificationService notifications;

	internal ForegroundWindowVolumeViewModel(IHotkeyService hotkeys, INotificationService notifications, IAppRouting appRouting)
		: base(hotkeys)
	{
		this.notifications = notifications;
		this.appRouting = appRouting;
		ErrorDictionary.LanguageChanged += Errors.Refresh;

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

	/// <summary>The hotkeys registered now, released when it's turned off</summary>
	private HotKey?[] registered = Array.Empty<HotKey?>();

	private void RegisterLoadedHotkeys()
	{
		//each on its own: a saved hotkey that fails now (e.g. another app took the combination) shows an error under its box
		//and doesn't keep the others from working; it stays saved and is tried again on the next start
		var failed = AllHotkeys.Where(hotkey => hotkey != null && !hotkeys.RegisterHotkey(hotkey)).Select(hotkey => hotkey!).ToList();
		registered = AllHotkeys.Where(hotkey => hotkey != null && !failed.Contains(hotkey)).ToArray();
		hotkeys.HotkeyPressed += OnHotkeyPressed;
		Errors.SetFailed(failed, VolumeUpHotkey, VolumeDownHotkey, MuteHotkey, ErrorMessageType.VolumeReg);
	}

	private bool SaveHotkeys()
	{
		Errors.Set(hotkeys.CheckAppHotkeys(VolumeUpHotkey, VolumeDownHotkey, MuteHotkey));
		if(Errors.Any)
			return false;
		if(hotkeys.RegisterHotkeys(AllHotkeys, out var failed))
		{
			registered = AllHotkeys;
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
			hotkeys.UnregisterHotkeys(registered);
		}
		catch { }
		registered = Array.Empty<HotKey?>();
		Errors.Clear();
		SettingsProvider.Settings.Hotkeys.ForegroundVolumeUp = null;
		SettingsProvider.Settings.Hotkeys.ForegroundVolumeDown = null;
		SettingsProvider.Settings.Hotkeys.ForegroundMute = null;
		hotkeys.HotkeyPressed -= OnHotkeyPressed;
	}

	/// <summary>The home model of the foreground window's app on any device; null if it has no audio. On the UI thread</summary>
	private AudioProcessModel? FindForegroundApp()
	{
		uint foregroundWindowProcId = GetForegroundWindowProcessId();
		if(foregroundWindowProcId == uint.MinValue)
			return null;

		long started = Stopwatch.GetTimestamp();
		AudioProcessModel? foundProcess = null;
		IImmutableList<AudioProcessModel> apps = ImmutableList<AudioProcessModel>.Empty;
		string? processName = null;
		string filePath = string.Empty;
		Exception? pathError = null;
		try
		{
			//the process id matches most of the time (also a process that only has a session on another device);
			//the process is only opened when it doesn't
			foundProcess = appRouting.FindApp(foregroundWindowProcId);
			if(foundProcess == null)
			{
				apps = appRouting.GetApps();
				(processName, filePath, pathError) = GetProcessNameAndPath(foregroundWindowProcId);
				foundProcess = FindByNameOrPath(apps, processName, filePath);
			}
		}
		catch { }
		HotkeyDiagnostics.ForegroundLookup(foregroundWindowProcId, processName, filePath, pathError, apps, foundProcess,
			Stopwatch.GetElapsedTime(started).TotalMilliseconds);
		return foundProcess;
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
		if(!_hotkeysRegistered)
			return;

		int direction = hotkey.Equals(VolumeUpHotkey) ? 1 : hotkey.Equals(VolumeDownHotkey) ? -1 : 0;
		if(direction != 0 || hotkey.Equals(MuteHotkey))
			ChangeForegroundVolume(hotkey, direction);
	}

	/// <param name="direction">1 up, -1 down by the volume step; 0 mutes or unmutes the whole app. On every device the app has a model on</param>
	private void ChangeForegroundVolume(HotKey hotkey, int direction)
	{
		HotkeyDiagnostics.ForegroundHotkeyPressed(hotkey);
		if(FindForegroundApp() is not { } foundProcess)
			return;
		//like the app hotkeys: the OSD only while notifications are on
		if(SettingsProvider.Settings.Notifications.Enabled && foundProcess.StateNotificationMediator == null)
			foundProcess.SetStateMediator(notifications);
		foundProcess.ApplyHotkey(direction, appRouting.GetModels(foundProcess));
		if(direction != 0)
			HotkeyDiagnostics.ForegroundVolumeSet(foundProcess, foundProcess.Volume);
	}
}

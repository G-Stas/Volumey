using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

public partial class SystemHotkeysViewModel : HotkeyViewModel
{
	[ObservableProperty]
	public partial SystemHotkey SelectedHotkey { get; set; }

	[ObservableProperty]
	public partial SystemHotkey SelectedRegisteredHotkey { get; set; }

	[ObservableProperty]
	public partial HotKey ReplacementHotKey { get; set; }

	public ObservableCollection<SystemHotkey> Hotkeys { get; set; } = new ObservableCollection<SystemHotkey>();
	public ObservableCollection<SystemHotkey> RegisteredHotkeys { get; set; }

	internal SystemHotkeysViewModel(IHotkeyService hotkeys) : base(hotkeys)
	{
		var mediaHotkeys = GetMediaHotkeys();

		RegisteredHotkeys = new ObservableCollection<SystemHotkey>(SettingsProvider.Settings.Hotkeys.System.Select(h => new SystemHotkey(h.SystemKey, h.Replacement)));

		foreach(var hotkey in mediaHotkeys)
		{
			if(!RegisteredHotkeys.Contains(hotkey))
				Hotkeys.Add(hotkey);
		}

		if(RegisteredHotkeys.Count > 0)
			RegisterWhenHotkeysActive(RegisterHotkeys);
	}

	private void RegisterHotkeys()
	{
		//shows an error instead of silently doing nothing; the hotkeys stay saved and are tried again on the next start
		bool allRegistered = true;
		foreach(var hotkey in RegisteredHotkeys)
		{
			allRegistered &= hotkeys.RegisterHotkey(hotkey.Replacement);
			hotkey.RegisterHotkeyHandler(hotkeys);
		}
		if(!allRegistered)
			SetErrorMessage(ErrorMessageType.OpenReg);
	}

	[RelayCommand]
	private void AddHotkey()
	{
		//the lists below are bound to the selection, so it's read once
		var selected = SelectedHotkey;
		var replacement = ReplacementHotKey;
		if(selected == null || replacement == null)
			return;

		if(!IsValid(hotkeys.HotkeyIsValid(replacement)))
			return;

		if(!hotkeys.RegisterHotkey(replacement))
		{
			SetErrorMessage(ErrorMessageType.VolumeReg);
			return;
		}

		SetErrorMessage(ErrorMessageType.None);

		selected.Replacement = replacement;
		selected.RegisterHotkeyHandler(hotkeys);

		RegisteredHotkeys.Add(selected);

		SaveRegisteredHotkeys();

		Hotkeys.Remove(selected);

		SelectedHotkey = null;
		ReplacementHotKey = null;

	}

	[RelayCommand]
	private void RemoveHotkey()
	{
		if(SelectedRegisteredHotkey == null)
			return;

		var hotkey = SelectedRegisteredHotkey;

		hotkeys.UnregisterHotkey(hotkey.Replacement);
		RegisteredHotkeys.Remove(hotkey);
		SaveRegisteredHotkeys();

		hotkey.UnregisterHotkeyHandler(hotkeys);

		hotkey.Replacement = null;
		Hotkeys.Add(hotkey);

	}

	private void SaveRegisteredHotkeys()
	{
		var entries = RegisteredHotkeys
					  .Select(h => new SystemHotkeyEntry { SystemKey = h.SystemKey, Replacement = h.Replacement })
					  .ToArray();
		SettingsProvider.Settings.Hotkeys.System = entries;
	}

	private static List<SystemHotkey> GetMediaHotkeys()
	{
		return new List<SystemHotkey>
		{
			new SystemHotkey(Key.MediaPlayPause),
			new SystemHotkey(Key.MediaPreviousTrack),
			new SystemHotkey(Key.MediaNextTrack),
			new SystemHotkey(Key.MediaStop)
		};
	}
}


using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The Media keys page: one hotkey per media key, which Volumey answers by pressing that media key
/// </summary>
public sealed class SystemHotkeysViewModel : HotkeyViewModel
{
	private static readonly Key[] MediaKeys = { Key.MediaPlayPause, Key.MediaPreviousTrack, Key.MediaNextTrack, Key.MediaStop };

	/// <summary>
	/// Every media key in a fixed order, with or without a hotkey
	/// </summary>
	public IReadOnlyList<MediaKeyItem> Keys { get; }

	internal SystemHotkeysViewModel(IHotkeyService hotkeys) : base(hotkeys)
	{
		var saved = SettingsProvider.Settings.Hotkeys.System;
		Keys = MediaKeys
			   .Select(key => new MediaKeyItem(this, new SystemHotkey(key, saved.FirstOrDefault(h => h.SystemKey == key)?.Replacement)))
			   .ToList();

		ErrorDictionary.LanguageChanged += () =>
		{
			foreach(var item in Keys)
				item.RefreshTexts();
		};

		if(Keys.Any(k => k.Hotkey != null))
			RegisterWhenHotkeysActive(RegisterHotkeys);
	}

	private void RegisterHotkeys()
	{
		//shows an error instead of silently doing nothing; the hotkeys stay saved and are tried again on the next start
		foreach(var item in Keys.Where(k => k.Hotkey != null))
		{
			item.IsRegistered = hotkeys.RegisterHotkey(item.Hotkey);
			item.Model.RegisterHotkeyHandler(hotkeys);
			if(!item.IsRegistered)
				item.SetError(ErrorMessageType.OpenReg);
		}
	}

	/// <summary>
	/// Takes a hotkey typed into a media key's box (null: cleared); a rejected one leaves the old hotkey in place
	/// </summary>
	internal void ChangeHotkey(MediaKeyItem item, HotKey hotkey)
	{
		var old = item.Hotkey;
		if(Equals(hotkey, old))
			return;

		if(hotkey != null)
		{
			var error = hotkeys.HotkeyIsValid(hotkey);
			if(error == ErrorMessageType.None && !hotkeys.RegisterHotkey(hotkey))
				error = ErrorMessageType.OpenReg;
			if(error != ErrorMessageType.None)
			{
				//the box shows the old hotkey again, so the error names the rejected one
				item.SetError(error, hotkey);
				item.SetHotkey(old);
				return;
			}
		}

		if(old != null && item.IsRegistered)
			hotkeys.UnregisterHotkey(old);
		item.IsRegistered = hotkey != null;
		if(old == null)
			item.Model.RegisterHotkeyHandler(hotkeys);
		else if(hotkey == null)
			item.Model.UnregisterHotkeyHandler(hotkeys);

		item.SetError(ErrorMessageType.None);
		item.SetHotkey(hotkey);
		Logger.Info($"Changed media key hotkey. Key: [{item.MediaKey}] Hotkey: [{hotkey}]");
		SaveHotkeys();
	}

	private void SaveHotkeys()
	{
		SettingsProvider.Settings.Hotkeys.System = Keys
												   .Where(k => k.Hotkey != null)
												   .Select(k => new SystemHotkeyEntry { SystemKey = k.MediaKey, Replacement = k.Hotkey })
												   .ToArray();
	}
}

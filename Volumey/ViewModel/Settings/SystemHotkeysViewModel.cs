using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public class SystemHotkeysViewModel : HotkeyViewModel
	{
		private SystemHotkey _selectedHotkey;
		public SystemHotkey SelectedHotkey
		{
			get => _selectedHotkey;
			set
			{
				_selectedHotkey = value;
				OnPropertyChanged();
			}
		}

		private SystemHotkey _selectedRegisteredHotkey;
		public SystemHotkey SelectedRegisteredHotkey
		{
			get => _selectedRegisteredHotkey;
			set
			{
				_selectedRegisteredHotkey = value;
				OnPropertyChanged();
			} 
		}

		private HotKey _replacementHotkey;
		public HotKey ReplacementHotKey
		{
			get => _replacementHotkey;
			set
			{
				_replacementHotkey = value;
				OnPropertyChanged();
			} }

		public ObservableCollection<SystemHotkey> Hotkeys { get; set; } = new ObservableCollection<SystemHotkey>();
		public ObservableCollection<SystemHotkey> RegisteredHotkeys { get; set; }

		public ICommand AddHotkeyCommand { get; set; }
		public ICommand RemoveHotkeyCommand { get; set; }

		public SystemHotkeysViewModel()
		{
			var hotkeys = GetMediaHotkeys();

			AddHotkeyCommand = new ActionCommand(AddHotkey);
			RemoveHotkeyCommand = new ActionCommand(RemoveHotkey);

			RegisteredHotkeys = new ObservableCollection<SystemHotkey>(SettingsProvider.HotkeysSettings.RegisteredSystemHotkeys);

			foreach(var hotkey in hotkeys)
			{
				if(!RegisteredHotkeys.Contains(hotkey))
					Hotkeys.Add(hotkey);
			}

			if(RegisteredHotkeys.Count > 0)
			{
				if(HotkeysControl.IsActive)
					this.RegisterHotkeys();
				else
					HotkeysControl.Activated += this.RegisterHotkeys;
			}
		}

		private void RegisterHotkeys()
		{
			foreach(var hotkey in RegisteredHotkeys)
			{
				HotkeysControl.RegisterHotkey(hotkey.Replacement);
				hotkey.RegisterHotkeyHandler();
			}
		}

		private void AddHotkey()
		{
			if(_selectedHotkey == null || _replacementHotkey == null)
				return;

			if(HotkeysControl.HotkeyIsValid(_replacementHotkey) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return;
			}
			
			if(!HotkeysControl.RegisterHotkey(_replacementHotkey))
			{
				this.SetErrorMessage(ErrorMessageType.VolumeReg);
				return;
			}
			
			this.SetErrorMessage(ErrorMessageType.None);
			
			_selectedHotkey.Replacement = _replacementHotkey;
			_selectedHotkey.RegisterHotkeyHandler();
			
			RegisteredHotkeys.Add(_selectedHotkey);
			
			if(SettingsProvider.HotkeysSettings.RegisteredSystemHotkeys.Contains(_selectedHotkey))
				SettingsProvider.HotkeysSettings.RegisteredSystemHotkeys.Remove(_selectedHotkey);
			SettingsProvider.HotkeysSettings.RegisteredSystemHotkeys.Add(_selectedHotkey);

			Hotkeys.Remove(_selectedHotkey);

			SelectedHotkey = null;
			ReplacementHotKey = null;

			SettingsProvider.SaveSettings();
		}

		private void RemoveHotkey()
		{
			if(_selectedRegisteredHotkey == null)
				return;

			var hotkey = _selectedRegisteredHotkey;
			
			HotkeysControl.UnregisterHotkey(hotkey.Replacement);
			RegisteredHotkeys.Remove(hotkey);
			SettingsProvider.HotkeysSettings.RegisteredSystemHotkeys.Remove(hotkey);
			
			hotkey.UnregisterHotkeyHandler();

			hotkey.Replacement = null;
			Hotkeys.Add(hotkey);
			
			SettingsProvider.SaveSettings();
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

	
}
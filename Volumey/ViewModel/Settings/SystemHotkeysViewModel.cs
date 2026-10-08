using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
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

			RegisteredHotkeys = new ObservableCollection<SystemHotkey>(SettingsProvider.Settings.Hotkeys.System.Select(h => new SystemHotkey(h.SystemKey, h.Replacement)));

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
			
			SaveRegisteredHotkeys();

			Hotkeys.Remove(_selectedHotkey);

			SelectedHotkey = null;
			ReplacementHotKey = null;

		}

		private void RemoveHotkey()
		{
			if(_selectedRegisteredHotkey == null)
				return;

			var hotkey = _selectedRegisteredHotkey;
			
			HotkeysControl.UnregisterHotkey(hotkey.Replacement);
			RegisteredHotkeys.Remove(hotkey);
			SaveRegisteredHotkeys();
			
			hotkey.UnregisterHotkeyHandler();

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

	
}
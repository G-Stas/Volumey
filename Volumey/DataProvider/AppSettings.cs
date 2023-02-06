using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Windows.Input;
using log4net;
using Notification.Wpf.Controls;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.DataProvider
{
	[Serializable]
	public enum AppTheme
	{
		Dark,
		Light,
		System
	};
	
	[Serializable]
	public class AppSettings
	{
		private readonly HotkeysAppSettings hotkeysSettings = new HotkeysAppSettings();
		public HotkeysAppSettings HotkeysSettings => hotkeysSettings;
		
		[OptionalField]
		private NotificationsAppSettings notificationSettings;
		public NotificationsAppSettings NotificationsSettings => notificationSettings ??= new NotificationsAppSettings();

		private AppTheme currentAppTheme = AppTheme.Light;
		public AppTheme CurrentAppTheme
		{
			get => currentAppTheme;
			set => currentAppTheme = value;
		}

		private int volumeStep = 1;
		public int VolumeStep
		{
			get => volumeStep;
			set => volumeStep = value;
		}

		private string appLanguage;
		public string AppLanguage
		{
			get => appLanguage;
			set => appLanguage = value;
		}

		private string systemLanguage;
		public string SystemLanguage
		{
			get => systemLanguage;
			set => systemLanguage = value;
		}

		private string systemSoundsName;
		public string SystemSoundsName
		{
			get => systemSoundsName;
			set => systemSoundsName = value;
		}

		[OptionalField]
		private bool volumeLimitIsOn;
		public bool VolumeLimitIsOn
		{
			get => volumeLimitIsOn;
			set => volumeLimitIsOn = value;
		}
        [OptionalField]
        private bool syncAppVolumeIsOn;
        public bool SyncAppVolumeIsOn
        {
			get => syncAppVolumeIsOn;
			set => syncAppVolumeIsOn = value;
		}

        [OptionalField]
		private int volumeLimit = 50;
		public int VolumeLimit
		{
			get => volumeLimit;
			set => volumeLimit = value;
		}

		[OptionalField]
		private bool userHasRated;
		public bool UserHasRated
		{
			get => userHasRated;
			set => userHasRated = value;
		}
		
		[OptionalField]
		private DateTime firstLaunchDate;
		public DateTime FirstLaunchDate
		{
			get => firstLaunchDate;
			set => firstLaunchDate = value;
		}

		[OptionalField]
		private int launchCount = 1;
		public int LaunchCount
		{
			get => launchCount;
			set => launchCount = value;
		}

		[OptionalField]
		private bool popupEnabled;
		public bool PopupEnabled
		{
			get => popupEnabled;
			set => popupEnabled = value;
		}

		[OptionalField]
		private bool alwaysOnTop;
		public bool AlwaysOnTop
		{
			get => alwaysOnTop;
			set => alwaysOnTop = value;
		}

		[OptionalField]
		private bool deviceViewAtTheBottom;
		public bool DeviceViewAtTheBottom
		{
			get => deviceViewAtTheBottom;
			set => deviceViewAtTheBottom = value;
		}

		[OptionalField]
		private bool displayDeviceIconAtTray;
		public bool DisplayDeviceIconAtTray
		{
			get => displayDeviceIconAtTray;
			set => displayDeviceIconAtTray = value;
		}

		[OptionalField]
		private double windowTop;
		public double WindowTop
		{
			get => windowTop;
			set => windowTop = value;
		}
		
		[OptionalField]
		private double windowLeft;
		public double WindowLeft
		{
			get => windowLeft;
			set => windowLeft = value;
		}
		
		[OptionalField]
		private bool rememberLastPosition;
		public bool RememberLastPosition
		{
			get => rememberLastPosition;
			set => rememberLastPosition = value;
		}

		[OptionalField]
		private int selectedScreenIndex;
		public int SelectedScreenIndex
		{
			get => selectedScreenIndex;
			set => selectedScreenIndex = value;
		}
		
		// [OptionalField]
		// private bool blockHotkeys = true;
		// public bool BlockHotkeys
		// {
		// 	get => blockHotkeys;
		// 	set => blockHotkeys = value;
		// }
		//
		// [OptionalField]
		// private bool allowDuplicates = false;
		// public bool AllowDuplicates
		// {
		// 	get => allowDuplicates;
		// 	set => allowDuplicates = value;
		// }

		internal bool HotkeyExists(HotKey key) => this.hotkeysSettings.HotkeyExists(key);
		internal bool HotkeysExist(HotKey key, HotKey key2) => this.hotkeysSettings.HotkeysExist(key, key2);
		
		[Serializable]
		public class HotkeysAppSettings
		{
			private string MusicAppName { get; set; }

			private bool VolumeUpIsEmpty => VolumeUpKey == Key.None && VolumeUpModifiers == ModifierKeys.None;
			private bool VolumeDownIsEmpty => VolumeDownKey == Key.None && VolumeDownModifiers == ModifierKeys.None;

			private Key VolumeUpKey;
			private ModifierKeys VolumeUpModifiers;

			private Key VolumeDownKey;
			private ModifierKeys VolumeDownModifiers;

			private Key OpenMixerKey;
			private ModifierKeys OpenMixerModifiers;
			
			[OptionalField]
			private Key DeviceMuteKey;
			[OptionalField]
			private ModifierKeys DeviceMuteModifiers;

			[OptionalField]
			private Key ForegroundVolumeUpKey;
			[OptionalField]
			private ModifierKeys ForegroundVolumeUpModifiers;
			
			[OptionalField]
			private Key ForegroundVolumeDownKey;
			[OptionalField]
			private ModifierKeys ForegroundVolumeDownModifiers;
			
			/// <summary>
			/// Default output device increase volume hotkey. Returns null if hotkey is not set. 
			/// </summary>
			internal HotKey DeviceVolumeUp
			{
				get
				{
					if(VolumeUpKey != Key.None || VolumeUpModifiers != ModifierKeys.None)
						return new HotKey(VolumeUpKey, VolumeUpModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						this.VolumeUpKey = Key.None;
						this.VolumeUpModifiers = ModifierKeys.None;
					}
					else
					{
						this.VolumeUpKey = value.Key;
						this.VolumeUpModifiers = value.ModifierKeys;
					}
				}
			}

			/// <summary>
			/// Default output device decrease volume hotkey. Returns null if hotkey is not set. 
			/// </summary>
			internal HotKey DeviceVolumeDown
			{
				get
				{
					if(VolumeDownKey != Key.None || VolumeDownModifiers != ModifierKeys.None)
						return new HotKey(VolumeDownKey, VolumeDownModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						this.VolumeDownKey = Key.None;
						this.VolumeDownModifiers = ModifierKeys.None;
					}
					else
					{
						this.VolumeDownKey = value.Key;
						this.VolumeDownModifiers = value.ModifierKeys;
					}
				}
			}

			/// <summary>
			/// Default output device mute/unmute hotkey. Returns null if hotkey is not set. 
			/// </summary>
			internal HotKey DeviceMute
			{
				get
				{
					if(DeviceMuteKey != Key.None || DeviceMuteModifiers != ModifierKeys.None)
						return new HotKey(DeviceMuteKey, DeviceMuteModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						DeviceMuteKey = Key.None;
						DeviceMuteModifiers = ModifierKeys.None;
					}
					else
					{
						DeviceMuteKey = value.Key;
						DeviceMuteModifiers = value.ModifierKeys;
					}
				}
			}

			internal HotKey OpenMixer
			{
				get
				{
					if(OpenMixerKey != Key.None || OpenMixerModifiers != ModifierKeys.None)
						return new HotKey(OpenMixerKey, OpenMixerModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						this.OpenMixerKey = Key.None;
						this.OpenMixerModifiers = ModifierKeys.None;
					}
					else
					{
						this.OpenMixerKey = value.Key;
						this.OpenMixerModifiers = value.ModifierKeys;
					}
				}
			}

			internal HotKey ForegroundVolumeUp
			{
				get
				{
					if(ForegroundVolumeUpKey != Key.None || ForegroundVolumeUpModifiers != ModifierKeys.None)
						return new HotKey(ForegroundVolumeUpKey, ForegroundVolumeUpModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						this.ForegroundVolumeUpKey = Key.None;
						this.ForegroundVolumeUpModifiers = ModifierKeys.None;
					}
					else
					{
						this.ForegroundVolumeUpKey = value.Key;
						this.ForegroundVolumeUpModifiers = value.ModifierKeys;
					}
				}
			}
			
			internal HotKey ForegroundVolumeDown
			{
				get
				{
					if(ForegroundVolumeDownKey != Key.None || ForegroundVolumeDownModifiers != ModifierKeys.None)
						return new HotKey(ForegroundVolumeDownKey, ForegroundVolumeDownModifiers);
					return null;
				}
				set
				{
					if(value == null)
					{
						this.ForegroundVolumeDownKey = Key.None;
						this.ForegroundVolumeDownModifiers = ModifierKeys.None;
					}
					else
					{
						this.ForegroundVolumeDownKey = value.Key;
						this.ForegroundVolumeDownModifiers = value.ModifierKeys;
					}
				}
			}

			[OptionalField]
			public bool PreventResettingVolumeBalance;

			[OptionalField]
			public List<SystemHotkey> RegisteredSystemHotkeys = new List<SystemHotkey>();

			[OptionalField]
			private Dictionary<string, (SerializableHotkey up, SerializableHotkey down)> serializableRegisteredSessions;

			internal ObservableConcurrentDictionary<string, VolumeHotkeysPair> GetRegisteredProcesses()
			{
				var dictionary = new ObservableConcurrentDictionary<string, VolumeHotkeysPair>();
				if(this.serializableRegisteredSessions == null)
				{
					this.serializableRegisteredSessions = new Dictionary<string, (SerializableHotkey, SerializableHotkey)>();
					return dictionary;
				}
				if(this.serializableRegisteredSessions.Count == 0)
					return dictionary;
				
				foreach(var (key, (up, down)) in this.serializableRegisteredSessions)
					dictionary.Add(key, new VolumeHotkeysPair(up.ToHotKey(), down.ToHotKey()));
				
				return dictionary;
			}
			
			internal bool HotkeyExists(HotKey key)
			{
				if(DeviceVolumeUp is HotKey up && key.Equals(up))
					return true;
				if(DeviceVolumeDown is HotKey down && key.Equals(down))
					return true;
				if(OpenMixer is HotKey open && key.Equals(open))
					return true;
				if(DeviceMute is HotKey mute && key.Equals(mute))
					return true;
				foreach(var session in this.serializableRegisteredSessions)
				{
					if(session.Value.up.ToHotKey().Equals(key) || session.Value.down.ToHotKey().Equals(key))
						return true;
				}
				foreach(var value in this.serializableRegisteredDevices)
				{
					var hotkey = value.Key.ToHotKey();
					if(hotkey.Equals(key))
						return true;
				}
				return false;
			}
			
			internal bool HotkeysExist(HotKey key, HotKey key2)
			{
				if(DeviceVolumeUp is HotKey volUp && (key.Equals(volUp) || key2.Equals(volUp)))
					return true;
				if(DeviceVolumeDown is HotKey volDown && (key.Equals(volDown) || key2.Equals(volDown)))
					return true;
				if(OpenMixer is HotKey open && (key.Equals(open) || key2.Equals(open)))
					return true;
				if(DeviceMute is HotKey mute && (key.Equals(mute) || key2.Equals(mute)))
					return true;
				foreach(var (_, hotkeys) in this.serializableRegisteredSessions)
				{
					var up = hotkeys.up.ToHotKey();
					var down = hotkeys.down.ToHotKey();
					if(up.Equals(key) || up.Equals(key2) || down.Equals(key) || down.Equals(key2))
						return true;
				}

				foreach(var value in this.serializableRegisteredDevices)
				{
					var hotkey = value.Key.ToHotKey();
					if(hotkey.Equals(key) || hotkey.Equals(key2))
						return true;
				}
				return false;
			}

			internal void AddRegisteredProcess(string processName, VolumeHotkeysPair hotkeys)
			{
				if(processName == null || hotkeys.VolumeUp == null || hotkeys.VolumeDown == null)
					return;
				this.serializableRegisteredSessions.Add(processName, (hotkeys.VolumeUp.ToSerializableHotkey(), hotkeys.VolumeDown.ToSerializableHotkey()));
			}

			internal void RemoveRegisteredSession(string sessionName)
			{
				if(string.IsNullOrEmpty(sessionName))
					return;
				this.serializableRegisteredSessions.Remove(sessionName);
			}

			[OptionalField]
			private Dictionary<SerializableHotkey, (string id, string name)> serializableRegisteredDevices;

			internal ObservableConcurrentDictionary<HotKey, DeviceInfo> GetRegisteredDevices()
			{
				var dict = new ObservableConcurrentDictionary<HotKey, DeviceInfo>();
				if(this.serializableRegisteredDevices == null)
				{
					this.serializableRegisteredDevices = new Dictionary<SerializableHotkey, (string id, string name)>();
					return dict;
				}

				if(this.serializableRegisteredDevices.Count == 0)
					return dict;

				foreach(var tuple in this.serializableRegisteredDevices)
					dict.Add(tuple.Key.ToHotKey(), new DeviceInfo(tuple.Value.id, tuple.Value.name));

				return dict;
			}

			internal void AddRegisteredDevice(HotKey hotkey, DeviceInfo deviceInfo)
			{
				this.serializableRegisteredDevices.Add(hotkey.ToSerializableHotkey(), (deviceInfo.ID, deviceInfo.Name));
			}

			internal void RemoveRegisteredDevice(HotKey hotkey)
			{
				this.serializableRegisteredDevices.Remove(hotkey.ToSerializableHotkey());
			}

			[OnDeserialized]
			private void OnDeserialized(StreamingContext context)
			{
				//convert values from the old version of settings class to the new one
				if(this.MusicAppName != null && !VolumeUpIsEmpty && !VolumeDownIsEmpty)
				{
					this.serializableRegisteredSessions ??= new Dictionary<string, (SerializableHotkey, SerializableHotkey)>();
					this.serializableRegisteredSessions.Add(MusicAppName, (new SerializableHotkey(this.VolumeUpKey, this.VolumeUpModifiers),
						                                         new SerializableHotkey(this.VolumeDownKey, this.VolumeDownModifiers)));
					LogManager.GetLogger(typeof(AppSettings)).Info($"Converted old hotkeys to a new version. App: [{this.MusicAppName}] +vol: [{this.VolumeUpKey}] -vol: [{this.VolumeDownKey}]");
					this.MusicAppName = null;
					this.VolumeUpKey = VolumeDownKey = Key.None;
					this.VolumeUpModifiers = VolumeDownModifiers = ModifierKeys.None;
				}

				if(RegisteredSystemHotkeys == null)
					RegisteredSystemHotkeys = new List<SystemHotkey>();
			}
		}

		[Serializable]
		public class NotificationsAppSettings : INotifyPropertyChanged
		{
			private bool enabled = false;
			public bool Enabled
			{
				get => enabled;
				set
				{
					this.enabled = value;
					OnPropertyChanged();
				}
			}

			private int verticalIndent = NotificationManagerHelper.MinIndent;
			public int VerticalIndent
			{
				get => this.verticalIndent;
				set
				{
					this.verticalIndent = value;
					OnPropertyChanged();
				}
			}

			private int horizontalIndent = NotificationManagerHelper.MinIndent;
			public int HorizontalIndent
			{
				get => this.horizontalIndent;
				set
				{
					this.horizontalIndent = value;
					OnPropertyChanged();
				}
			}

			private NotificationPosition position = NotificationPosition.TopLeft;
			public NotificationPosition Position
			{
				get => this.position;
				set
				{
					this.position = value;
					OnPropertyChanged();
				}
			}

			private int displayTimeInSeconds = 3;
			public int DisplayTimeInSeconds
			{
				get => this.displayTimeInSeconds;
				set
				{
					this.displayTimeInSeconds = value;
					OnPropertyChanged();
				}
			}

			[OptionalField]
			private bool reactToAllVolumeChanges;
			public bool ReactToAllVolumeChanges
			{
				get => this.reactToAllVolumeChanges;
				set
				{
					this.reactToAllVolumeChanges = value;
					OnPropertyChanged();
				}
			}

			[OptionalField]
			private int selectedScreenIndex;
			public int SelectedScreenIndex
			{
				get => selectedScreenIndex;
				set => selectedScreenIndex = value;
			}

			[field: NonSerialized]
			public event PropertyChangedEventHandler PropertyChanged;

			protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		[Serializable]
		internal readonly struct SerializableHotkey
		{
			private readonly Key _key;
			private readonly ModifierKeys _modifierKeys;
			internal HotKey ToHotKey() => new HotKey(this._key, this._modifierKeys);

			internal SerializableHotkey(Key key, ModifierKeys modifierKeys)
			{
				_key = key;
				_modifierKeys = modifierKeys;
			}
		}
	}
}
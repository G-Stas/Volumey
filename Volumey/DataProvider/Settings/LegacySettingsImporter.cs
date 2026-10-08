using System;
using System.Collections.Generic;
using System.Formats.Nrbf;
using System.IO;
using System.Windows.Input;
using Notification.Wpf.Controls;
using Volumey.Controls;

namespace Volumey.DataProvider.Settings
{
	/// <summary>
	/// Imports settings saved by Volumey 1.x (a BinaryFormatter snapshot of the old AppSettings class).
	/// The file is parsed as plain data with System.Formats.Nrbf, so none of the old types have to exist anymore.
	/// Member names below are field names of the old AppSettings classes and must not be changed.
	/// </summary>
	internal static class LegacySettingsImporter
	{
		internal static void Import(Stream legacyFile, VolumeySettings settings)
		{
			var root = (ClassRecord) NrbfDecoder.Decode(legacyFile);

			var general = settings.General;
			ReadEnum(root, "currentAppTheme", v => general.Theme = (AppTheme) v);
			ReadString(root, "appLanguage", v => general.Language = v);
			ReadInt(root, "volumeStep", v => general.VolumeStep = v);
			ReadBool(root, "volumeLimitIsOn", v => general.VolumeLimitEnabled = v);
			ReadInt(root, "volumeLimit", v => general.VolumeLimit = v);
			ReadBool(root, "syncAppVolumeIsOn", v => general.AppVolumeSyncEnabled = v);
			ReadString(root, "systemSoundsName", v => general.SystemSoundsName = v);
			ReadString(root, "systemLanguage", v => general.SystemSoundsLanguage = v);

			var window = settings.Window;
			ReadBool(root, "popupEnabled", v => window.PopupMode = v);
			ReadBool(root, "alwaysOnTop", v => window.AlwaysOnTop = v);
			ReadBool(root, "deviceViewAtTheBottom", v => window.DeviceViewAtTheBottom = v);
			ReadBool(root, "displayDeviceIconAtTray", v => window.DisplayDeviceIconAtTray = v);
			ReadDouble(root, "windowLeft", v => window.Left = v);
			ReadDouble(root, "windowTop", v => window.Top = v);
			ReadInt(root, "selectedScreenIndex", v => window.ScreenIndex = v);

			if(GetClass(root, "hotkeysSettings") is ClassRecord hotkeys)
				ImportHotkeys(hotkeys, settings.Hotkeys);

			if(GetClass(root, "notificationSettings") is ClassRecord notifications)
			{
				var n = settings.Notifications;
				ReadBool(notifications, "enabled", v => n.Enabled = v);
				ReadInt(notifications, "verticalIndent", v => n.VerticalIndent = v);
				ReadInt(notifications, "horizontalIndent", v => n.HorizontalIndent = v);
				ReadEnum(notifications, "position", v => n.Position = (NotificationPosition) v);
				ReadInt(notifications, "displayTimeInSeconds", v => n.DisplayTimeSeconds = v);
				ReadBool(notifications, "reactToAllVolumeChanges", v => n.ReactToAllVolumeChanges = v);
				ReadInt(notifications, "selectedScreenIndex", v => n.ScreenIndex = v);
			}
		}

		private static void ImportHotkeys(ClassRecord record, HotkeySettings hotkeys)
		{
			var volumeUp = ReadHotkey(record, "VolumeUpKey", "VolumeUpModifiers");
			var volumeDown = ReadHotkey(record, "VolumeDownKey", "VolumeDownModifiers");
			var apps = new List<AppHotkeysEntry>();

			//the oldest versions stored a single app's hotkeys in VolumeUp/VolumeDown together with MusicAppName
			string musicAppName = GetString(record, "<MusicAppName>k__BackingField");
			if(musicAppName != null && volumeUp != null && volumeDown != null)
				apps.Add(new AppHotkeysEntry { ProcessName = musicAppName, VolumeUp = volumeUp, VolumeDown = volumeDown });
			else
			{
				hotkeys.DeviceVolumeUp = volumeUp;
				hotkeys.DeviceVolumeDown = volumeDown;
			}

			hotkeys.OpenMixer = ReadHotkey(record, "OpenMixerKey", "OpenMixerModifiers");
			hotkeys.DeviceMute = ReadHotkey(record, "DeviceMuteKey", "DeviceMuteModifiers");
			hotkeys.ForegroundVolumeUp = ReadHotkey(record, "ForegroundVolumeUpKey", "ForegroundVolumeUpModifiers");
			hotkeys.ForegroundVolumeDown = ReadHotkey(record, "ForegroundVolumeDownKey", "ForegroundVolumeDownModifiers");
			ReadBool(record, "PreventResettingVolumeBalance", v => hotkeys.PreventResettingVolumeBalance = v);

			//Dictionary<string, (SerializableHotkey up, SerializableHotkey down)>
			foreach(var pair in GetDictionaryPairs(record, "serializableRegisteredSessions"))
			{
				var tuple = pair.GetClassRecord("value");
				var up = ReadSerializableHotkey(tuple?.GetClassRecord("Item1"));
				var down = ReadSerializableHotkey(tuple?.GetClassRecord("Item2"));
				string processName = pair.GetString("key");
				if(processName != null && up != null && down != null)
					apps.Add(new AppHotkeysEntry { ProcessName = processName, VolumeUp = up, VolumeDown = down });
			}
			hotkeys.Apps = apps;

			//Dictionary<SerializableHotkey, (string id, string name)>
			var devices = new List<DeviceHotkeyEntry>();
			foreach(var pair in GetDictionaryPairs(record, "serializableRegisteredDevices"))
			{
				var hotkey = ReadSerializableHotkey(pair.GetClassRecord("key"));
				var tuple = pair.GetClassRecord("value");
				if(hotkey != null && tuple != null)
					devices.Add(new DeviceHotkeyEntry { Hotkey = hotkey, DeviceId = tuple.GetString("Item1"), DeviceName = tuple.GetString("Item2") });
			}
			hotkeys.DefaultDevices = devices;

			//List<SystemHotkey>
			var systemHotkeys = new List<SystemHotkeyEntry>();
			if(GetClass(record, "RegisteredSystemHotkeys") is ClassRecord list)
			{
				int size = list.GetInt32("_size");
				var items = GetRecords(list, "_items");
				for(int i = 0; i < size && i < items.Length; i++)
				{
					if(items[i] is ClassRecord item && ReadEnumValue(item, "_systemKey") is int systemKey)
					{
						systemHotkeys.Add(new SystemHotkeyEntry
						{
							SystemKey = (Key) systemKey,
							Replacement = ReadSerializableHotkey(GetClass(item, "_serializableReplacement"))
						});
					}
				}
			}
			hotkeys.System = systemHotkeys;
		}

		private static HotKey ReadHotkey(ClassRecord record, string keyMember, string modifiersMember)
			=> ToHotkey(ReadEnumValue(record, keyMember), ReadEnumValue(record, modifiersMember));

		private static HotKey ReadSerializableHotkey(ClassRecord record)
			=> record == null ? null : ToHotkey(ReadEnumValue(record, "_key"), ReadEnumValue(record, "_modifierKeys"));

		private static HotKey ToHotkey(int? key, int? modifiers)
		{
			var k = (Key) (key ?? 0);
			var m = (ModifierKeys) (modifiers ?? 0);
			if(k == Key.None && m == ModifierKeys.None)
				return null;
			return new HotKey(k, m);
		}

		private static IEnumerable<ClassRecord> GetDictionaryPairs(ClassRecord owner, string member)
		{
			//an empty dictionary is serialized without the KeyValuePairs member
			if(!(GetClass(owner, member) is ClassRecord dictionary))
				yield break;
			foreach(var pair in GetRecords(dictionary, "KeyValuePairs"))
			{
				if(pair is ClassRecord classRecord)
					yield return classRecord;
			}
		}

		private static SerializationRecord[] GetRecords(ClassRecord owner, string member)
		{
			if(owner.HasMember(member) && owner.GetArrayRecord(member) is SZArrayRecord<SerializationRecord> array)
				return array.GetArray();
			return Array.Empty<SerializationRecord>();
		}

		private static ClassRecord GetClass(ClassRecord owner, string member)
			=> owner.HasMember(member) ? owner.GetClassRecord(member) : null;

		private static string GetString(ClassRecord owner, string member)
			=> owner.HasMember(member) ? owner.GetString(member) : null;

		/// <summary>
		/// Enums are serialized as class records with a single value__ member.
		/// </summary>
		private static int? ReadEnumValue(ClassRecord owner, string member)
			=> GetClass(owner, member) is ClassRecord enumRecord ? enumRecord.GetInt32("value__") : (int?) null;

		private static void ReadEnum(ClassRecord owner, string member, Action<int> apply)
		{
			if(ReadEnumValue(owner, member) is int value)
				apply(value);
		}

		private static void ReadString(ClassRecord owner, string member, Action<string> apply)
		{
			if(GetString(owner, member) is string value)
				apply(value);
		}

		private static void ReadInt(ClassRecord owner, string member, Action<int> apply)
		{
			if(owner.HasMember(member))
				apply(owner.GetInt32(member));
		}

		private static void ReadBool(ClassRecord owner, string member, Action<bool> apply)
		{
			if(owner.HasMember(member))
				apply(owner.GetBoolean(member));
		}

		private static void ReadDouble(ClassRecord owner, string member, Action<double> apply)
		{
			if(owner.HasMember(member))
				apply(owner.GetDouble(member));
		}
	}
}

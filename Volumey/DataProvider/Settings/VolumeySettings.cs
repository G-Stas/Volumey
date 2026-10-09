using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Autosave;

namespace Volumey.DataProvider.Settings
{
	/// <summary>
	/// Root of the app settings, stored as settings.json. Sections are separate objects; a change of any section property
	/// is saved automatically once <c>EnableAutosave()</c> has been called (see <see cref="SettingsProvider"/>).
	/// </summary>
	[Autosave]
	public sealed class VolumeySettings : NotifiyingJsonSettings
	{
		/// <summary>
		/// Version of the settings layout. Bump it and add a migration step to <see cref="SettingsProvider"/>
		/// when a property is renamed, moved or changes its type. Adding new properties doesn't require it.
		/// </summary>
		/// <remarks>
		/// 1: the first settings.json. 2: screens are stored by device name (ScreenName) instead of their index (ScreenIndex).
		/// </remarks>
		public const int CurrentVersion = 2;

		/// <summary>Name of the removed screen index setting of version 1 (and of 1.x), read by the migration to version 2</summary>
		internal const string LegacyScreenIndex = "ScreenIndex";

		private readonly object saveLock = new object();

		public override string FileName { get; set; }

		public int Version { get; set; } = CurrentVersion;

		public GeneralSettings General { get; set; } = new GeneralSettings();
		public WindowSettings Window { get; set; } = new WindowSettings();
		public HotkeySettings Hotkeys { get; set; } = new HotkeySettings();
		public NotificationSettings Notifications { get; set; } = new NotificationSettings();

		protected override JsonSerializerSettings OverrideSerializerSettings { get; set; } = CreateSerializerSettings();

		public VolumeySettings() { }
		public VolumeySettings(string fileName) : base(fileName) { }

		//settings are changed from background threads as well, so don't let saves overlap
		public override void Save(string filename)
		{
			lock(this.saveLock)
				base.Save(filename);
		}

		private static JsonSerializerSettings CreateSerializerSettings()
		{
			var settings = new JsonSerializerSettings(JsonSettings.SerializationSettings)
			{
				//no .NET type names in the file
				TypeNameHandling = TypeNameHandling.None,
				//replace default lists/sections instead of merging into them
				ObjectCreationHandling = ObjectCreationHandling.Replace
			};
			//store enums by name (also Key/ModifierKeys inside hotkeys)
			settings.Converters.Add(new StringEnumConverter());
			return settings;
		}
	}
}

#nullable enable
using System;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Autosave;

namespace Volumey.DataProvider.Settings;

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
	public const int CurrentVersion = 1;

	private readonly object saveLock = new object();

	public override string FileName { get; set; } = null!;

	public int Version { get; set; } = CurrentVersion;

	public GeneralSettings General { get; set; } = new GeneralSettings();
	public WindowSettings Window { get; set; } = new WindowSettings();
	public HotkeySettings Hotkeys { get; set; } = new HotkeySettings();
	public NotificationSettings Notifications { get; set; } = new NotificationSettings();

	protected override JsonSerializerSettings? OverrideSerializerSettings { get; set; } = CreateSerializerSettings();

	public VolumeySettings() { }
	public VolumeySettings(string fileName) : base(fileName) { }

	/// <summary>
	/// How long a save waits for more changes. Every change saves the whole file, and changes come in bursts (e.g. a slider),
	/// so a burst is written once, on the thread pool, instead of once per change on the UI thread.
	/// </summary>
	internal static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(300);

	private Timer? saveTimer;
	private string? pendingFile;

	/// <summary>
	/// Called by autosave on every change: schedules the write (see <see cref="SaveDelay"/>). <see cref="Flush"/> writes it at once.
	/// </summary>
	public override void Save(string filename)
	{
		lock(saveLock)
		{
			pendingFile = filename;
			(saveTimer ??= new Timer(_ => Flush())).Change(SaveDelay, Timeout.InfiniteTimeSpan);
		}
	}

	/// <summary>
	/// Writes a scheduled save now, e.g. when the app exits. Returns once the file is written, also when the timer's write
	/// had already taken the change and is still running: the app exits right after, which would cut that write off.
	/// </summary>
	public void Flush()
	{
		lock(writeLock)
		{
			string? file;
			lock(saveLock)
			{
				file = pendingFile;
				pendingFile = null;
			}
			if(file == null)
				return;
			try
			{
				SaveNow(file);
			}
			catch(Exception e)
			{
				//also runs on a timer thread, where an exception would end the app
				log4net.LogManager.GetLogger(typeof(VolumeySettings)).Error("Failed to save the settings", e);
			}
		}
	}

	/// <summary>
	/// Writes the file at once (loading and migration need that).
	/// </summary>
	public void SaveNow(string? filename = null)
	{
		lock(writeLock)
			base.Save(filename ?? FileName);
	}

	//held while the file is written; taken before saveLock where both are needed (Save only takes saveLock)
	private readonly object writeLock = new object();

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

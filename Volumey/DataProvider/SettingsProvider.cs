using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using log4net;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Autosave;
using Nucs.JsonSettings.Modulation.Recovery;
using Volumey.Controls;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.DataProvider
{
    /// <summary>
    /// Provides the app settings, stored in settings.json and saved automatically on every change.
    /// </summary>
    public static class SettingsProvider
    {
        private const string ConfigFolderName = "Volumey";
        private const string SettingsFileName = "settings.json";
        /// <summary>BinaryFormatter settings file of Volumey 1.x, imported once when settings.json is created</summary>
        private const string LegacySettingsFileName = "appconfig";
        /// <summary>Overrides the settings folder, e.g. to run a dev build without touching the real settings</summary>
        private const string SettingsDirVariable = "VOLUMEY_SETTINGS_DIR";

        //use LocalApplicationData path to access local app data store instead of using Windows.Storage because
        //it's unstable when certain applications like GIMP are launched as described in this issue:
        //https://github.com/microsoft/ProjectReunion/issues/101
        private static readonly string SettingsDir = Environment.GetEnvironmentVariable(SettingsDirVariable) is string dir && dir.Length > 0
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigFolderName);

        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(SettingsProvider));

        private static VolumeySettings settings;
        private static readonly object SettingsLock = new object();

        public static VolumeySettings Settings
        {
            get
            {
                if(Volatile.Read(ref settings) is VolumeySettings s)
                    return s;
                lock(SettingsLock)
                    return settings ??= LoadSettings(SettingsDir);
            }
        }

        /// <summary>
        /// Replaces the settings, e.g. with a non-persistent <c>new VolumeySettings()</c> in tests.
        /// </summary>
        internal static void UseSettings(VolumeySettings volumeySettings)
        {
            lock(SettingsLock)
                settings = volumeySettings;
        }

        /// <summary>
        /// Loads settings.json from the folder (creating it, and importing 1.x settings on the first launch) and turns on autosave.
        /// </summary>
        internal static VolumeySettings LoadSettings(string settingsDir)
            => LoadSettings(settingsDir, () => new ScreenInfoProvider().GetAllScreensInfo().ToList());

        /// <param name="settingsDir"/>
        /// <param name="getScreens">The current screens, read only when an old settings file stores screens by index</param>
        internal static VolumeySettings LoadSettings(string settingsDir, Func<IReadOnlyList<ScreenInfo>> getScreens)
        {
            string path = Path.Combine(settingsDir, SettingsFileName);
            try
            {
                Directory.CreateDirectory(settingsDir);
                bool firstLaunch = !File.Exists(path);

                //a corrupted file is renamed to settings.<n>.json and defaults are loaded
                var loaded = JsonSettings.Configure<VolumeySettings>(path)
                                         .WithRecovery(RecoveryAction.RenameAndLoadDefault)
                                         .LoadNow();
                if(firstLaunch)
                    ImportLegacySettings(settingsDir, loaded);
                Migrate(loaded, getScreens);
                loaded.Save();
                return loaded.EnableAutosave();
            }
            catch(Exception e)
            {
                //keep the app working with default settings, changes just won't be saved
                Logger.Error("Failed to load settings", e);
                return new VolumeySettings();
            }
        }

        private static void Migrate(VolumeySettings volumeySettings, Func<IReadOnlyList<ScreenInfo>> getScreens)
        {
            //one step per version, each brings the layout to the next version
            if(volumeySettings.Version < 2)
            {
                //screens were stored by their index in the list of screens, which changes order when a monitor is detected late (#92).
                //The index meant the screens as they are now, so that's the best guess for the name
                int? windowIndex = volumeySettings.Window.TakeOtherValue<int?>(VolumeySettings.LegacyScreenIndex);
                int? notificationsIndex = volumeySettings.Notifications.TakeOtherValue<int?>(VolumeySettings.LegacyScreenIndex);
                if(windowIndex != null || notificationsIndex != null)
                {
                    var screens = GetScreensForMigration(getScreens);
                    volumeySettings.Window.ScreenName = ScreenNameAt(screens, windowIndex);
                    volumeySettings.Notifications.ScreenName = ScreenNameAt(screens, notificationsIndex);
                }
            }
            volumeySettings.Version = VolumeySettings.CurrentVersion;
        }

        private static IReadOnlyList<ScreenInfo> GetScreensForMigration(Func<IReadOnlyList<ScreenInfo>> getScreens)
        {
            try
            {
                return getScreens();
            }
            catch(Exception e)
            {
                //the primary screen is used then
                Logger.Error("Failed to read the screens to migrate the settings", e);
                return Array.Empty<ScreenInfo>();
            }
        }

        private static string ScreenNameAt(IReadOnlyList<ScreenInfo> screens, int? index)
            => index is int i && i >= 0 && i < screens.Count ? screens[i].Name : null;

        private static void ImportLegacySettings(string settingsDir, VolumeySettings volumeySettings)
        {
            string legacyPath = Path.Combine(settingsDir, LegacySettingsFileName);
            if(!File.Exists(legacyPath))
                return;
            try
            {
                using(var file = File.OpenRead(legacyPath))
                    LegacySettingsImporter.Import(file, volumeySettings);
                Logger.Info("Imported settings of the previous app version");
            }
            catch(Exception e)
            {
                Logger.Error("Failed to import settings of the previous app version", e);
            }
            //the legacy file is left untouched so a downgrade to 1.x keeps its settings
        }

        /// <summary>
        /// Checks if the hotkey is already used by the device, mixer or app volume hotkeys.
        /// </summary>
        internal static bool HotkeyExists(HotKey hotkey) => HotkeysExist(hotkey, null);

        internal static bool HotkeysExist(HotKey hotkey, HotKey hotkey2)
        {
            bool Matches(HotKey registered) => registered != null && (registered.Equals(hotkey) || registered.Equals(hotkey2));

            var hotkeys = Settings.Hotkeys;
            return Matches(hotkeys.DeviceVolumeUp) ||
                   Matches(hotkeys.DeviceVolumeDown) ||
                   Matches(hotkeys.OpenMixer) ||
                   Matches(hotkeys.DeviceMute) ||
                   hotkeys.Apps.Any(app => Matches(app.VolumeUp) || Matches(app.VolumeDown)) ||
                   hotkeys.DefaultDevices.Any(device => Matches(device.Hotkey));
        }
    }
}

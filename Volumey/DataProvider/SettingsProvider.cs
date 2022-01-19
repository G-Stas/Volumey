using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows.Input;
using Volumey.Localization;
using log4net;
using Volumey.Controls;

namespace Volumey.DataProvider
{
    public static class SettingsProvider
    {
        public static AppSettings Settings { get; private set; }
        public static AppSettings.HotkeysAppSettings HotkeysSettings => Settings.HotkeysSettings;
        public static AppSettings.NotificationsAppSettings NotificationsSettings => Settings.NotificationsSettings;

        private const string ConfigFolderName = "Volumey";
        private const string ConfigFileName = "appconfig";

        //use LocalApplicationData path to access local app data store instead of using Windows.Storage because
        //it's unstable when certain applications like GIMP are launched as described in this issue:
        //https://github.com/microsoft/ProjectReunion/issues/101
        private static readonly string SettingsDir = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigFolderName);
        private static readonly string SettingsFullPath = Path.Combine(SettingsDir, ConfigFileName);

        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(SettingsProvider));

        static SettingsProvider() => LoadSettings();

        private static void LoadSettings()
        {
            if(File.Exists(SettingsFullPath))
            {
                try
                {
                    using(var fileStream = new FileStream(SettingsFullPath, FileMode.Open))
                    {
                        fileStream.Position = 0;
                        Settings = (AppSettings) new BinaryFormatter().Deserialize(fileStream);
                    }
                    return;
                }
                catch(Exception e) { Logger.Error("Failed to load settings file", e); }
            }
        
            //write default values to file on first launch or if config file doesn't exist or deserialization failed
            Settings = new AppSettings { CurrentAppTheme = AppTheme.System, AppLanguage = TranslationSource.GetSystemLanguage() };
            SaveSettings().GetAwaiter().GetResult();
        }

        public static async Task SaveSettings()
        {
            try
            {
                if(!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);
            }
            catch(Exception e)
            {
                Logger.Error("Failed to create settings directory", e);
            }

            try
            {
                await using(var fileStream = new FileStream(SettingsFullPath, FileMode.OpenOrCreate))
                {
                    new BinaryFormatter().Serialize(fileStream, Settings);
                }
            }
            catch(Exception e)
            {
                Logger.Error("Failed to save settings file", e);
            }
        }

        internal static AppSettings.SerializableHotkey ToSerializableHotkey(this HotKey hotkey)
        {
            if(hotkey == null)
                return new AppSettings.SerializableHotkey(Key.None, ModifierKeys.None);
            return new AppSettings.SerializableHotkey(hotkey.Key, hotkey.ModifierKeys);
        }
        
        internal static (AppSettings.SerializableHotkey, AppSettings.SerializableHotkey) ToTuple(this Tuple<HotKey, HotKey> tuple)
        {
            if(tuple != null)
            {
                var (upHotkey, downHotkey) = tuple;
                return (upHotkey.ToSerializableHotkey(), downHotkey.ToSerializableHotkey());    
            }
            return(new AppSettings.SerializableHotkey(Key.None, ModifierKeys.None), new AppSettings.SerializableHotkey(Key.None, ModifierKeys.None));

        }
    }
}
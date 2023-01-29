using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel
{
    sealed class SettingsViewModel : INotifyPropertyChanged
    {
        public DeviceVolumeHotkeysViewModel DeviceVolumeHotkeysViewModel { get; }
        public AppsHotkeysViewModel HotkeysViewModel { get; }
        public OpenHotkeyViewModel OpenHotkeyViewModel { get; }
        public VolumeLimitViewModel VolumeLimitViewModel { get; }
        public AppVolumeSyncViewModel AppVolumeSyncViewModel { get; }
        public DefaultDeviceHotkeysViewModel DefaultDeviceHotkeysViewModel { get; }
        public NotificationViewModel NotificationsViewModel { get; }
        public ForegroundWindowVolumeViewModel ForegroundWindowVolumeViewModel { get; }
        public SystemHotkeysViewModel SystemHotkeysViewModel { get; }
        public LangSettings LangSettings { get; }
        public ICommand GitHubCommand { get; }
        public ICommand TipCommand { get; }

        public Tuple<string, string> TipSource { get; } = new Tuple<string, string>("Boosty", "https://boosty.to/stasg");

        private IntPtr windowHandle;
        
        // private bool blockHotkeysInSystem;
        // public bool BlockHotkeysInSystem
        // {
        //     get => this.blockHotkeysInSystem;
        //     set
        //     {
        //         this.blockHotkeysInSystem = value;
        //         SetupHotkeyManager();
        //         OnPropertyChanged();
        //
        //         if(value && this.AllowDuplicateHotkeys)
        //         {
        //             this.AllowDuplicateHotkeys = false;
        //             return;
        //         }
        //         
        //         SettingsProvider.Settings.BlockHotkeys = value;
        //         _ = SettingsProvider.SaveSettings();
        //     }
        // }

        // private bool allowDuplicateHotkeys;
        // Allows to set same hotkeys for different types of hotkeys.
        // Works only when blocking hotkeys in system is disabled.
        // public bool AllowDuplicateHotkeys
        // {
        //     get => this.allowDuplicateHotkeys;
        //     set
        //     {
        //         this.allowDuplicateHotkeys = value;
        //         OnPropertyChanged();
        //
        //         SettingsProvider.Settings.AllowDuplicates = value;
        //         _ = SettingsProvider.SaveSettings();
        //     }
        // }
        
        public SettingsViewModel()
        {
            this.LangSettings = new LangSettings();
            this.HotkeysViewModel = new AppsHotkeysViewModel();
            this.DeviceVolumeHotkeysViewModel = new DeviceVolumeHotkeysViewModel();
            this.OpenHotkeyViewModel = new OpenHotkeyViewModel();
            this.VolumeLimitViewModel = new VolumeLimitViewModel();
            this.AppVolumeSyncViewModel = new AppVolumeSyncViewModel();
            this.DefaultDeviceHotkeysViewModel = new DefaultDeviceHotkeysViewModel();
            this.ForegroundWindowVolumeViewModel = new ForegroundWindowVolumeViewModel();
            this.NotificationsViewModel = new NotificationViewModel();
            this.SystemHotkeysViewModel = new SystemHotkeysViewModel();
            this.GitHubCommand = new ActionCommand(async () => await OpenWebPage("https://github.com/G-Stas/Volumey"));
            this.TipCommand = new ActionCommand(async (param) => await OpenWebPage(param as string));

            // this.blockHotkeysInSystem = SettingsProvider.Settings.BlockHotkeys;
            // this.allowDuplicateHotkeys = SettingsProvider.Settings.AllowDuplicates;
        }

        /// <summary>
        /// Receives window handle from main view which is required for creating one of the hotkey managers.
        /// </summary>
        /// <param name="handle">Window handle</param>
        public void SetWindowHandle(IntPtr handle)
        {
            if(this.windowHandle != IntPtr.Zero)
                return;
            this.windowHandle = handle;
            // if(BlockHotkeysInSystem)
            SetupHotkeyManager();
        }

        private Action<int> HotkeyManagerMessageHandler;

        public Action<int> GetHotkeyMessageHandler()
            => this.WindowsHotkeyMessageHandlerBridge;

        /// <summary>
        /// Passes window hotkey messages from main view to hotkey manager.
        /// </summary>
        /// <param name="lParam">Contains pressed hotkey data.</param>
        private void WindowsHotkeyMessageHandlerBridge(int lParam)
        {
            this.HotkeyManagerMessageHandler?.Invoke(lParam);
        }

        private void SetupHotkeyManager()
        {
            var hotkeyManager = new HotkeyManager(this.windowHandle);
            this.HotkeyManagerMessageHandler = hotkeyManager.GetMessageHandler();
            HotkeysControl.SetHotkeyManager(hotkeyManager);
            
            // IHotkeyManager hm = null;
            // if(blockHotkeysInSystem)
            // {
            //     //Prevent creating manager right now since it's dependent on the window handle, which was not passed yet by window.
            //     if(this.windowHandle == IntPtr.Zero)
            //         return;
            //     var hotkeyManager = new HotkeyManager(this.windowHandle);
            //     this.HotkeyManagerMessageHandler = hotkeyManager.GetMessageHandler();
            //     hm = hotkeyManager;
            // }
            // else
            // {
            //     this.HotkeyManagerMessageHandler = null;
            //     hm = new HotkeyHookManager();
            // }
            // HotkeysControl.SetHotkeyManager(hm);
        }
        
        private async Task OpenWebPage(string url)
        {
            if(string.IsNullOrEmpty(url))
                return;
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
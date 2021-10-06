using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
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
        public DefaultDeviceHotkeysViewModel DefaultDeviceHotkeysViewModel { get; }
        public LangSettings LangSettings { get; }
        public ICommand GitHubCommand { get; }
        public ICommand TipCommand { get; }

        private IntPtr windowHandle;
        
        private bool blockHotkeysInSystem;
        public bool BlockHotkeysInSystem
        {
            get => this.blockHotkeysInSystem;
            set
            {
                this.blockHotkeysInSystem = value;
                SetupHotkeyManager();
                OnPropertyChanged();

                if(value && this.AllowDuplicateHotkeys)
                {
                    this.AllowDuplicateHotkeys = false;
                    return;
                }
                
                SettingsProvider.Settings.BlockHotkeys = value;
                _ = SettingsProvider.SaveSettings();
            }
        }

        private bool allowDuplicateHotkeys;
        /// <summary>
        /// Allows to set same hotkeys for different types of hotkeys.
        /// Works only when blocking hotkeys in system is disabled.
        /// </summary>
        public bool AllowDuplicateHotkeys
        {
            get => this.allowDuplicateHotkeys;
            set
            {
                this.allowDuplicateHotkeys = value;
                OnPropertyChanged();

                SettingsProvider.Settings.AllowDuplicates = value;
                _ = SettingsProvider.SaveSettings();
            }
        }
        
        public SettingsViewModel()
        {
            this.LangSettings = new LangSettings();
            this.HotkeysViewModel = new AppsHotkeysViewModel();
            this.DeviceVolumeHotkeysViewModel = new DeviceVolumeHotkeysViewModel();
            this.OpenHotkeyViewModel = new OpenHotkeyViewModel();
            this.VolumeLimitViewModel = new VolumeLimitViewModel();
            this.DefaultDeviceHotkeysViewModel = new DefaultDeviceHotkeysViewModel();
            this.GitHubCommand = new ActionCommand(async () => { await OpenWebPage("https://github.com/G-Stas/Volumey"); });
            this.TipCommand = new ActionCommand(async () => { await OpenWebPage("https://ko-fi.com/stasg"); });

            this.BlockHotkeysInSystem = SettingsProvider.Settings.BlockHotkeys;
            this.AllowDuplicateHotkeys = SettingsProvider.Settings.AllowDuplicates;
            
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
            if(BlockHotkeysInSystem)
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
            IHotkeyManager hm = null;
            if(blockHotkeysInSystem)
            {
                //Prevent creating manager right now since it's dependent on the window handle, which was not passed yet by window.
                if(this.windowHandle == IntPtr.Zero)
                    return;
                var hotkeyManager = new HotkeyManager(this.windowHandle);
                this.HotkeyManagerMessageHandler = hotkeyManager.GetMessageHandler();
                hm = hotkeyManager;
            }
            else
            {
                this.HotkeyManagerMessageHandler = null;
                hm = new HotkeyHookManager();
            }
            HotkeysControl.SetHotkeyManager(hm);
        }
        
        private async Task OpenWebPage(string url)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
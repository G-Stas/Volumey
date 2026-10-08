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
        
        private bool blockHotkeysInSystem;
        /// <summary>
        /// True: hotkeys block their keys system-wide (RegisterHotKey). False: a keyboard hook only listens.
        /// Changing it moves the registered hotkeys to the other hotkey manager.
        /// </summary>
        public bool BlockHotkeysInSystem
        {
            get => this.blockHotkeysInSystem;
            set
            {
                if(this.blockHotkeysInSystem == value)
                    return;
                this.blockHotkeysInSystem = value;
                SettingsProvider.Settings.Hotkeys.BlockInSystem = value;
                SetupHotkeyManager();
                OnPropertyChanged();
            }
        }
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

            this.blockHotkeysInSystem = SettingsProvider.Settings.Hotkeys.BlockInSystem;
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
            //the blocking manager needs the window handle, which the main view passes on startup
            if(this.windowHandle == IntPtr.Zero)
                return;
            IHotkeyManager manager;
            if(this.blockHotkeysInSystem)
            {
                var hotkeyManager = new HotkeyManager(this.windowHandle);
                this.HotkeyManagerMessageHandler = hotkeyManager.GetMessageHandler();
                manager = hotkeyManager;
            }
            else
            {
                this.HotkeyManagerMessageHandler = null;
                manager = new SharpHookHotkeyManager();
            }
            HotkeysControl.SetHotkeyManager(manager);
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
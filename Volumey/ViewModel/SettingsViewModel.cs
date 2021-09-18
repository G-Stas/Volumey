using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel
{
    sealed class SettingsViewModel
    {
        public DeviceVolumeHotkeysViewModel DeviceVolumeHotkeysViewModel { get; }
        public AppsHotkeysViewModel HotkeysViewModel { get; }
        public OpenHotkeyViewModel OpenHotkeyViewModel { get; }
        public VolumeLimitViewModel VolumeLimitViewModel { get; }
        public DefaultDeviceHotkeysViewModel DefaultDeviceHotkeysViewModel { get; }
        public LangSettings LangSettings { get; }
        public ICommand GitHubCommand { get; }
        public ICommand TipCommand { get; }
        
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
        }
        
        private async Task OpenWebPage(string url)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); }
            catch { }
        }
    }
}
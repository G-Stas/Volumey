using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel
{
    sealed class SettingsViewModel
    {
        public DeviceHotkeysViewModel DeviceHotkeysViewModel { get; }
        public AppsHotkeysViewModel HotkeysViewModel { get; }
        public OpenHotkeyViewModel OpenHotkeyViewModel { get; }
        public VolumeLimitViewModel VolumeLimitViewModel { get; }
        public LangSettings LangSettings { get; }
        public ICommand GitHubCommand { get; }
        public ICommand TipCommand { get; }
        
        public SettingsViewModel()
        {
            this.LangSettings = new LangSettings();
            this.HotkeysViewModel = new AppsHotkeysViewModel();
            this.DeviceHotkeysViewModel = new DeviceHotkeysViewModel();
            this.OpenHotkeyViewModel = new OpenHotkeyViewModel();
            this.VolumeLimitViewModel = new VolumeLimitViewModel();
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
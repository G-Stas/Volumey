using System;
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
        public ICommand GithubHyperlinkCommand { get; }
        
        public SettingsViewModel()
        {
            this.LangSettings = new LangSettings();
            this.HotkeysViewModel = new AppsHotkeysViewModel();
            this.DeviceHotkeysViewModel = new DeviceHotkeysViewModel();
            this.OpenHotkeyViewModel = new OpenHotkeyViewModel();
            this.VolumeLimitViewModel = new VolumeLimitViewModel();
            GithubHyperlinkCommand = new ActionCommand(GithubHyperlinkAction);
        }

        private async void GithubHyperlinkAction()
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/G-Stas/Volumey"));
            }
            catch { }
        }
    }
}
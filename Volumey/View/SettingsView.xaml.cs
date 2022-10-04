using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModernWpf.Controls;
using ModernWpf.Media.Animation;
using Volumey.View.SettingsPage;

namespace Volumey.View
{
    public partial class SettingsView : INotifyPropertyChanged
    {
        public class NavLink
        {
            public string LocalizationKey { get; }
            public Type PageType { get; }

            public NavLink(string localizationKey, Type pageType)
            {
                this.LocalizationKey = localizationKey;
                this.PageType = pageType;
            }
        }

        public static List<NavLink> NavLinks { get; } = new List<NavLink>();
        public NavLink SelectedNavLink { get; set; }

        static SettingsView()
        {
            NavLinks.Add(new NavLink("Settings_AppsHotkeys", typeof(AppsHotkeysPage)));
            NavLinks.Add(new NavLink("Settings_DeviceHotkeys", typeof(DeviceVolumeHotkeysPage)));
            NavLinks.Add(new NavLink("Settings_DefaultDeviceHotkey", typeof(DefaultDeviceHotkeysPage)));
            NavLinks.Add(new NavLink("Settings_MixerSettings", typeof(MixerSettingsPage)));
            NavLinks.Add(new NavLink("Notifications_Header", typeof(NotificationsPage)));
            NavLinks.Add(new NavLink("Settings_MediaHotkeys", typeof(SystemHotkeysPage)));
            NavLinks.Add(new NavLink("Settings_WindowBehavior", typeof(AppBehaviorPage)));
            NavLinks.Add(new NavLink("Settings_HeaderMisc", typeof(MiscPage)));
        }
        
        public SettingsView()
        {
            InitializeComponent();
            this.SelectedNavLink = NavLinks[0];
            this.PageContentFrame.Navigate(this.SelectedNavLink.PageType);
        }

        private NavigationTransitionInfo transition = new DrillInNavigationTransitionInfo();

        private void OnNavLinkItemClick(object sender, ItemClickEventArgs e)
        {
            if(this.SelectedNavLink == null)
                return;
            if(this.PageContentFrame.CurrentSourcePageType != this.SelectedNavLink.PageType)
                this.PageContentFrame.Navigate(this.SelectedNavLink.PageType, null, transition);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
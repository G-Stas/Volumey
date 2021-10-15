using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
    /// <summary>
    /// Represents data and the volume control of an output audio device
    /// </summary>
    public sealed class MasterSessionModel : INotifyPropertyChanged
    {
        private string deviceFriendlyName;
        public string DeviceFriendlyName
        {
            get => deviceFriendlyName;
            set
            {
                deviceFriendlyName = value;
                OnPropertyChanged();
            }
        }

        private string deviceDesc;
        public string DeviceDesc
        {
            get => deviceDesc;
            set
            {
                deviceDesc = value;
                OnPropertyChanged();
            }
        }

        private int volume;
        public int Volume
        {
            get => volume;
            set
            {
                if(this.volume != value)
                {
					if(value < 0)
						this.volume = 0;
					else if(value > 100)
						this.volume = 100;
                    else
						this.volume = value;
					this.SetVolume(this.volume, ref GuidValue.Internal.VolumeGUID);
                    OnPropertyChanged();
                }
            }
        }

        private bool isMuted;
        public bool IsMuted
        {
            get => isMuted;
            private set
            {
                if(this.isMuted != value)
                {
                    this.isMuted = value;
                    this.SetMute(value);
                    OnPropertyChanged();
                }
            }
        }

        private ImageSource deviceIcon;
        public ImageSource DeviceIcon
        {
            get => deviceIcon;
            set
            {
                deviceIcon = value;
                OnPropertyChanged();
            }
        }
        
        private HotKey volumeUp;
        private HotKey volumeDown;
        private HotKey muteKey;

        private bool volumeHotkeysRegistered;
        private bool muteHotkeyRegistered;

        public ICommand MuteCommand { get; }

        private IAudioSessionVolume masterVolume { get; }
        private IMasterVolumeNotificationHandler notificationHandler { get; }
        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(MasterSessionModel));
        
        public MasterSessionModel(string friendlyName, string desc, 
            int volume, bool muteState, ImageSource icon, 
            IAudioSessionVolume mVolume, IMasterVolumeNotificationHandler nHandler)
        {
            this.DeviceFriendlyName = friendlyName;
            this.DeviceDesc = desc;
            this.volume = volume;
            this.isMuted = muteState;
            this.DeviceIcon = icon;
            this.masterVolume = mVolume;
            this.notificationHandler = nHandler;

            this.MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);
            this.notificationHandler.VolumeChanged += OnVolumeChanged;
        }

        public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
        {
            try
            {
                if(volUp != null && volDown != null && HotkeysControl.RegisterHotkeysPair(volUp, volDown))
                {
                    this.volumeUp = volUp;
                    this.volumeDown = volDown;
                    HotkeysControl.HotkeyPressed += OnHotkeyPressed;
                    this.volumeHotkeysRegistered = true;
                    return true;
                }
            }
            catch { }
            return false;
        }

        internal void ResetVolumeHotkeys()
        {
            if(this.volumeUp != null && this.volumeDown != null)
                HotkeysControl.UnregisterHotkeysPair(this.volumeUp, this.volumeDown);
            if(!this.muteHotkeyRegistered)
                HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
            this.volumeHotkeysRegistered = false;
            this.volumeUp = this.volumeDown = null;
        }

        internal bool SetMuteHotkeys(HotKey key)
        {
            try
            {
                if(key != null && HotkeysControl.RegisterHotkey(key))
                {
                    this.muteKey = key;
                    HotkeysControl.HotkeyPressed += OnHotkeyPressed;
                    this.muteHotkeyRegistered = true;
                    return true;
                }
            }
            catch { }
            return false;
        }

        internal void ResetMuteHotkey()
        {
            if(this.muteKey != null)
                HotkeysControl.UnregisterHotkey(this.muteKey);
            if(!this.volumeHotkeysRegistered)
                HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
            this.muteHotkeyRegistered = false;
            this.muteKey = null;
        }
		
        private void OnHotkeyPressed(HotKey hotkey)
        {
            if(this.volumeHotkeysRegistered)
            {
                if(hotkey.Equals(this.volumeUp))
                {
                    this.SetVolume(this.Volume + HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty);
                }
                else if(hotkey.Equals(this.volumeDown))
                {
                    this.SetVolume(this.Volume - HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty);
                }
            }

            if(this.muteHotkeyRegistered && hotkey.Equals(this.muteKey))
                this.IsMuted = !this.isMuted;

        }
        
        private void SetVolume(int newVol, ref Guid guid)
        {
            try { this.masterVolume?.SetVolume(newVol, ref guid); }
            catch(COMException com)
            {
                LogStateException(com);
            }
            catch(Exception e)
            {
                LogStateException(e);
            }
        }

        private void SetMute(bool muteState)
        {
            try { this.masterVolume?.SetMute(muteState); }
            catch(COMException com)
            {
                LogStateException(com);
            }
            catch(Exception e)
            {
                LogStateException(e);
            }
        }

        private void OnVolumeChanged(VolumeChangedEventArgs e)
        {
            this.Volume = e.NewVolume;
            this.IsMuted = e.IsMuted;
        }

        private void LogStateException(COMException com)
        {
            Logger.Info($"Failed to change state of the master session. COM error message: [{com.ErrorCode}]", com);
        }

        private void LogStateException(Exception e)
        {
            Logger.Info("Failed to change state of the master session", e);
        }

        internal void Dispose()
        {
            this.notificationHandler.VolumeChanged -= OnVolumeChanged;
            this.notificationHandler.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
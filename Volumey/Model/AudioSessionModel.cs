using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using log4net;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
    /// <summary>
    /// Represents an audio session and the volume control of an application 
    /// </summary>
    public sealed class AudioSessionModel : INotifyPropertyChanged, IDisposable
    {
        private string name;
        public string Name
        {
            get => name;
            set
            {
                this.name = value;
                OnPropertyChanged();
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

        private int volume;
        public int Volume
        {
            get => volume;
            set
            {
                if (this.volume != value)
                {
                    if (value < 0)
                        this.volume = 0;
                    else if (value > 100)
                        this.volume = 100;
                    else
                        this.volume = value;

                    this.SetVolume(this.volume, ref GuidValue.Internal.VolumeGUID);
                }
                OnPropertyChanged();
            }
        }

        private ImageSource appIcon;
        public ImageSource AppIcon
        {
            get => appIcon;
            set
            {
                this.appIcon = value;
                OnPropertyChanged();
            }
        }

        public ICommand MuteCommand { get; }

        public event Action<AudioSessionModel> SessionEnded;

        private readonly IAudioSessionVolume sessionVolume;
        private readonly IAudioSessionStateNotifications sessionStateNotifications;
        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(AudioSessionModel));

        private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        public AudioSessionModel(bool isMuted, int volume, string name, ImageSource icon, IAudioSessionVolume aVolume,
            IAudioSessionStateNotifications sStateNotifications)
        {
            this.name = name;
            this.isMuted = isMuted;
            this.Volume = volume;
            this.sessionVolume = aVolume;
            this.sessionStateNotifications = sStateNotifications;
            this.AppIcon = icon;

            this.MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);

            this.sessionStateNotifications.VolumeChanged += OnVolumeChanged;
            this.sessionStateNotifications.SessionEnded += OnSessionEnded;
            this.sessionStateNotifications.IconPathChanged += OnIconChanged;
            this.sessionStateNotifications.NameChanged += OnNameChanged;
        }

        private HotKey volumeUp;
        private HotKey volumeDown;

        internal bool SetHotkeys(HotKey volUp, HotKey volDown)
        {
            try
            {
                if(volUp != null && volDown != null && HotkeysControl.RegisterVolumeHotkeys(volUp, volDown))
                {
                    this.volumeUp = volUp;
                    this.volumeDown = volDown;
                    HotkeysControl.HotkeyPressed += OnHotkeyPressed;
                    return true;
                }
            }
            catch { }
            return false; 
        }

        internal void ResetHotkeys()
        {
            this.UnregisterHotkeys();
        }
        
        private void UnregisterHotkeys()
        {
            if(this.volumeUp != null && this.volumeDown != null)
            {
                try
                {
                    HotkeysControl.UnregisterVolumeHotkeys(this.volumeUp, this.volumeDown);
                }
                catch { }
            }
            HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
            this.volumeUp = this.volumeDown = null;
        }

        private void OnHotkeyPressed(HotKey hotkey)
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

        private void OnSessionEnded()
            => this.SessionEnded?.Invoke(this);

        private void OnVolumeChanged(VolumeChangedEventArgs e)
        {
            this.Volume = e.NewVolume;
            this.IsMuted = e.IsMuted;
        }

        private void OnIconChanged(ImageSource newIcon) 
            => dispatcher.Invoke(() => { this.AppIcon = newIcon; });

        private void OnNameChanged(string newName)
            => dispatcher.Invoke(() => { this.Name = newName; });

        private void SetVolume(int newVol, ref Guid guid)
        {
            try { this.sessionVolume?.SetVolume(newVol, ref guid); }
            catch(COMException com)
            {
                LogStateException(com);
            }
            catch(Exception e)
            {
                LogStateException(e);
            }
        }

        private void SetMute(bool mute)
        {
            try { this.sessionVolume?.SetMute(mute); }
            catch(COMException com)
            {
                LogStateException(com);
            }
            catch(Exception e)
            {
                LogStateException(e);
            }
        }

        private void LogStateException(COMException com)
        {
            Logger.Info($"Failed to change state of the audio session. COM error message: [{com.ErrorCode.ToString(CultureInfo.InvariantCulture)}]", com);
        }

        private void LogStateException(Exception e)
        {
            Logger.Info("Failed to change state of the audio session", e);
        }

        public void Dispose()
        {
            this.sessionStateNotifications.VolumeChanged -= OnVolumeChanged;
            this.sessionStateNotifications.SessionEnded -= OnSessionEnded;
            this.sessionStateNotifications.IconPathChanged -= OnIconChanged;
            this.sessionStateNotifications.NameChanged -= OnNameChanged;
            this.sessionStateNotifications.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null) 
            => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
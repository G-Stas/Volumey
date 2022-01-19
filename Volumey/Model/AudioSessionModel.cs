using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
    /// <summary>
    /// Represents an audio session and the volume control of an application 
    /// </summary>
    public sealed class AudioSessionModel : BaseAudioSession
    {
        private string _name;
        public override string Name
        {
            get => _name;
            set
            {
                this._name = value;
                OnPropertyChanged();
            }
        }

        public override bool IsMuted
        {
            get => this._isMuted;
            set
            {
                if(this._isMuted != value)
                {
                    this._isMuted = value;
                    this.SetMute(value);
                    OnPropertyChanged();
                }
            }
        }

        public override int Volume
        {
            get => _volume;
            set
            {
                if (this._volume != value)
                {
                    if (value < 0)
                        this._volume = 0;
                    else if (value > 100)
                        this._volume = 100;
                    else
                        this._volume = value;

                    this.SetVolume(this._volume, ref GuidValue.Internal.VolumeGUID);
                }
                OnPropertyChanged();
            }
        }

        private readonly IAudioSessionStateNotifications sessionStateNotifications;
        private readonly IAudioSessionVolume sessionVolume;
        private HotKey volumeUp;
        private HotKey volumeDown;
        public event Action<AudioSessionModel> SessionEnded;

        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(AudioSessionModel));

        private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        public AudioSessionModel(bool isMuted, int volume, string name, string id, ImageSource icon, IAudioSessionVolume aVolume,
            IAudioSessionStateNotifications sStateNotifications) : base(volume, isMuted, id, icon)
        {
            this._name = name;
            this.sessionVolume = aVolume;
            this.sessionStateNotifications = sStateNotifications;
            this.sessionStateNotifications.VolumeChanged += OnVolumeChanged;
            this.sessionStateNotifications.SessionEnded += OnSessionEnded;
            this.sessionStateNotifications.IconPathChanged += OnIconChanged;
            this.sessionStateNotifications.NameChanged += OnNameChanged;
            this.sessionStateNotifications.Disconnected += OnDisconnected;
        }

        public override bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
        {
            try
            {
                if(volUp != null && volDown != null && HotkeysControl.RegisterHotkeysPair(volUp, volDown))
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

        public override void ResetVolumeHotkeys()
        {
            this.UnregisterHotkeys();
        }
        
        private void UnregisterHotkeys()
        {
            if(this.volumeUp != null && this.volumeDown != null)
            {
                try
                {
                    HotkeysControl.UnregisterHotkeysPair(this.volumeUp, this.volumeDown);
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
                this.AudioSessionStateNotificationMediator?.NotifyAudioStateChange(this);
            }
            else if(hotkey.Equals(this.volumeDown))
            {
                this.SetVolume(this.Volume - HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty);
                this.AudioSessionStateNotificationMediator?.NotifyAudioStateChange(this);
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
            => dispatcher.Invoke(() => { this.Icon = newIcon; });

        private void OnNameChanged(string newName)
            => dispatcher.Invoke(() => { this.Name = newName; });

        private void OnDisconnected(AudioSessionDisconnectReason reason)
            => OnSessionEnded();

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

        public override void Dispose()
        {
            base.Dispose();
            this.sessionStateNotifications.VolumeChanged -= OnVolumeChanged;
            this.sessionStateNotifications.SessionEnded -= OnSessionEnded;
            this.sessionStateNotifications.IconPathChanged -= OnIconChanged;
            this.sessionStateNotifications.NameChanged -= OnNameChanged;
            this.sessionStateNotifications.Disconnected -= OnDisconnected;
            this.sessionStateNotifications.Dispose();
        }
    }
}
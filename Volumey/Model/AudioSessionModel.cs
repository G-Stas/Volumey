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
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Model
{
    /// <summary>
    /// Represents an audio session and the volume control of an application 
    /// </summary>
    public sealed class AudioSessionModel : IManagedAudioSession
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                this._name = value;
                OnPropertyChanged();
            }
        }
        
        private readonly string _id;
        public string Id => this._id;

        public Guid GroupingParam { get; }

        private readonly uint _processId;
        public uint ProcessId => this._processId;
        
        public string FilePath { get; }

        private bool _isMuted;
        public bool IsMuted
        {
            get => this._isMuted;
            set
            {
                if(this._isMuted != value)
                {
                    this._isMuted = value;
                    this.SetMute(value);
                    this.MuteStateChanged?.Invoke(value, GuidValue.Internal.VolumeGUID);
                    OnPropertyChanged();
                }
            }
        }

        private int _volume;
        public int Volume
        {
            get => _volume;
            set
            {
                if(this._volume != value)
                {
                    if (value < 0)
                        this._volume = 0;
                    else if (value > 100)
                        this._volume = 100;
                    else
                        this._volume = value;

                    this.VolumeChanged?.Invoke(value, GuidValue.Internal.VolumeGUID);
                    this.SetVolume(this._volume, ref GuidValue.Internal.VolumeGUID);
                }
                OnPropertyChanged();
            }
        }
        
        public ImageSource IconSource { get; set; }
        
        public ICommand MuteCommand { get; set; }

        private AudioSessionState _state;

        private readonly IAudioSessionStateNotifications sessionStateNotifications;
        private readonly IAudioSessionVolume sessionVolume;
        public event Action<AudioSessionModel> SessionEnded;
        public event Action<int, Guid> VolumeChanged;
        public event Action<bool, Guid> MuteStateChanged;

        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(AudioSessionModel));

        private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        public AudioSessionModel(bool isMuted, int volume, string id, uint processId, string name, string filePath, Guid groupingParam, IAudioSessionVolume aVolume,
            IAudioSessionStateNotifications sStateNotifications)
        {
            _isMuted = isMuted;
            _volume = volume;
            _id = id;
            _processId = processId;
            GroupingParam = groupingParam;
            FilePath = filePath;
            Name = name;
            sessionVolume = aVolume;
            sessionStateNotifications = sStateNotifications;
            sessionStateNotifications.VolumeChanged += OnVolumeChanged;
            sessionStateNotifications.SessionEnded += OnSessionEnded;
            sessionStateNotifications.NameChanged += OnNameChanged;
            sessionStateNotifications.Disconnected += OnDisconnected;
            sessionStateNotifications.StateChanged += OnStateChanged;
            
            MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);
        }

        private void OnSessionEnded()
            => this.SessionEnded?.Invoke(this);

        private void OnVolumeChanged(VolumeChangedEventArgs e)
        {
            if(this.Volume != e.NewVolume)
                this.VolumeChanged?.Invoke(this.Volume, Guid.Empty);
            this.Volume = e.NewVolume;
            
            if(this.IsMuted != e.IsMuted)
                this.MuteStateChanged?.Invoke(this.IsMuted, Guid.Empty);
            this.IsMuted = e.IsMuted;
        }

        private void OnNameChanged(string newName)
            => dispatcher.Invoke(() => { this.Name = newName; });

        private void OnDisconnected(AudioSessionDisconnectReason reason)
            => OnSessionEnded();
        
        private void OnStateChanged(AudioSessionState newState)
            => this._state = newState;

        private void SetVolume(int newVol, ref Guid guid)
        {
            try
            {
                this.sessionVolume?.SetVolume(newVol, ref guid);
            }
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
        
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool _disposed;
        
        public void Dispose()
        {
            if(_disposed)
                return;
            _disposed = true;
            this.sessionStateNotifications.VolumeChanged -= OnVolumeChanged;
            this.sessionStateNotifications.SessionEnded -= OnSessionEnded;
            this.sessionStateNotifications.NameChanged -= OnNameChanged;
            this.sessionStateNotifications.Disconnected -= OnDisconnected;
            this.sessionStateNotifications.Dispose();
        }
    }
}
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
    /// <summary>
    /// Represents data and the volume control of an output audio device
    /// </summary>
    public sealed class MasterSessionModel : IManagedMasterAudioSession
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

        public string Name
        {
            get => this.deviceFriendlyName;
            set
            {
                this.DeviceFriendlyName = value;
                OnPropertyChanged();
            }
        }
        
        private readonly string _id;
        public string Id => this._id;

        private readonly uint _processId;
        public uint ProcessId => this._processId;

        public Guid GroupingParam { get; } = Guid.Empty;
        
        public string FilePath { get; }

        private int _volume;
        public int Volume
        {
            get => _volume;
            set
            {
                if(this._volume != value)
                {
                    if(value < 0)
                        this._volume = 0;
                    else if(value > 100)
                        this._volume = 100;
                    else
                        this._volume = value;
                    this.SetVolume(this._volume, ref GuidValue.Internal.VolumeGUID);
                    OnPropertyChanged();
                }
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if(this._isMuted != value)
                {
                    this._isMuted = value;
                    this.SetMute(value, GuidValue.Internal.VolumeGUID);
                    OnPropertyChanged();
                }
            }
        }
        
        private ImageSource _iconSource;
        public ImageSource IconSource
        {
            get => _iconSource;
            set
            {
                _iconSource = value;
                OnPropertyChanged();
            }
        }

        private Icon _icon;
        public Icon Icon
        {
        	get => _icon;
        	set
        	{
        		_icon = value;
        		OnPropertyChanged();
        	}
        }

        public ICommand MuteCommand { get; set; }

        private HotKey volumeUp;
        private HotKey volumeDown;
        private HotKey muteKey;
        
        private bool volumeHotkeysRegistered;
        private bool muteHotkeyRegistered;

        public bool AnyHotkeyRegistered => volumeHotkeysRegistered || muteHotkeyRegistered;
        
        public IAudioProcessStateMediator StateNotificationMediator { get; private set; }

        private IAudioSessionVolume masterVolume { get; }
        private IMasterVolumeNotificationHandler notificationHandler { get; }
        private static ILog logger;
        private static ILog Logger => logger ??= LogManager.GetLogger(typeof(MasterSessionModel));
        
        public MasterSessionModel(string friendlyName, string desc, 
            int volume, bool muteState, string id, Icon icon,
            IAudioSessionVolume mVolume, IMasterVolumeNotificationHandler nHandler)
        {
            this._volume = volume;
            this._isMuted = muteState;
            this._id = id;
            this._icon = icon;
            this._iconSource = icon.GetAsImageSource();
            this.Name = friendlyName;
            this.FilePath = this.Name;
            this.DeviceDesc = desc;
            this.masterVolume = mVolume;
            this.notificationHandler = nHandler;
            this.notificationHandler.VolumeChanged += OnVolumeChanged;
            
            MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);
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

        public void SetStateMediator(IAudioProcessStateMediator mediator)
        {
            StateNotificationMediator = mediator;
        }

        public void ResetStateMediator(bool force = false)
        {
            if(!force && SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges)
                return;
            StateNotificationMediator = null;
        }

        public void ResetVolumeHotkeys()
        {
            if(this.volumeUp != null && this.volumeDown != null)
                HotkeysControl.UnregisterHotkeysPair(this.volumeUp, this.volumeDown);
            if(!this.muteHotkeyRegistered)
                HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
            this.volumeHotkeysRegistered = false;
            this.volumeUp = this.volumeDown = null;
        }
        
        public bool SetMuteHotkey(HotKey key)
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

        public void ResetMuteHotkey()
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
                    if(SettingsProvider.HotkeysSettings.PreventResettingVolumeBalance && this.IsMuted)
                        this.SetMute(false, notify: false, ref GuidValue.Internal.Empty);
                    this.SetVolume(this.Volume + HotkeysControl.VolumeStep, notify:true, ref GuidValue.Internal.Empty);
                }
                else if(hotkey.Equals(this.volumeDown))
                {
                    int newValue = this.Volume - HotkeysControl.VolumeStep;
                    if(SettingsProvider.HotkeysSettings.PreventResettingVolumeBalance && newValue <= 0)
                    {
                        this.SetMute(true, notify: true, ref GuidValue.Internal.Empty);
                    }
                    else
                        this.SetVolume(newValue, notify: true, ref GuidValue.Internal.Empty);
                }
            }

            if(this.muteHotkeyRegistered && hotkey.Equals(this.muteKey))
            {
                this.SetMute(!this._isMuted, notify: true, ref GuidValue.Internal.Empty);
            }
        }

        public void SetVolume(int newVol, bool notify, ref Guid guid)
        {
            SetVolume(newVol, ref guid);
            if(notify)
                this.StateNotificationMediator?.NotifyAudioStateChange(this);
        }

        public void SetMute(bool muteState, bool notify, ref Guid context)
        {
            SetMute(muteState, context);
            if(notify)
                this.StateNotificationMediator?.NotifyAudioStateChange(this);
        }
        
        
        private void SetVolume(int newVol, ref Guid context)
        {
            try
            {
                this.masterVolume?.SetVolume(newVol, ref context);
                if(!context.Equals(GuidValue.Internal.VolumeGUID))
                    this.StateNotificationMediator?.NotifyAudioStateChange(this);
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

        private void SetMute(bool muteState, Guid context)
        {
            try
            {
                this.masterVolume?.SetMute(muteState); 
                if(!context.Equals(GuidValue.Internal.VolumeGUID))
                    this.StateNotificationMediator?.NotifyAudioStateChange(this);
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

        private void OnVolumeChanged(VolumeChangedEventArgs e)
        {
            if(SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges)
            {
                if(this.Volume != e.NewVolume || this.IsMuted != e.IsMuted)
                    this.StateNotificationMediator?.NotifyAudioStateChange(this);
            }
            
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
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            this.StateNotificationMediator?.NotifyOfDisposing(this);
            this.notificationHandler.VolumeChanged -= OnVolumeChanged;
            this.notificationHandler.Dispose();
        }
    }
}
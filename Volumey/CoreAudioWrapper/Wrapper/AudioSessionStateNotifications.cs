using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Helper;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notifications of session-related events such as changes in the volume level, display name, and session state.
    /// </summary>
    public sealed class AudioSessionStateNotifications : IAudioSessionStateNotifications
    {
        public event Action SessionEnded;
        public event Action<VolumeChangedEventArgs> VolumeChanged;
        public event Action<ImageSource> IconPathChanged;
        public event Action<string> NameChanged;
        public event Action<AudioSessionDisconnectReason> Disconnected;
        public event Action<AudioSessionState> StateChanged;
        private readonly IAudioSessionControl2 sessionControl;
        private readonly ISimpleAudioVolume sVolume;

        public AudioSessionStateNotifications(IAudioSessionControl2 sControl)
        {
            this.sessionControl = sControl;
            this.sVolume = (ISimpleAudioVolume)sControl;
        }

        public int OnChannelVolumeChanged(uint channelCount, float[] channelVolumes, int changedChannel,
            ref Guid eventContext) => 0;

        public int OnDisplayNameChanged(string newDisplayName, ref Guid eventContext)
        {
            this.NameChanged?.Invoke(newDisplayName);
            return 0;
        }

        public int OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext) => 0;

        //gets called when system process icon has changed
        public int OnIconPathChanged(string newIconPath, ref Guid eventContext)
        {
            if(string.IsNullOrEmpty(newIconPath))
                return 0;

            ImageSource icon = null;
            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string[] iconPathId = newIconPath.Split(",");
                    if(iconPathId.Length < 2)
                        throw new Exception();
                    string path = iconPathId[0];
                    int id = int.Parse(iconPathId[1]);
                    icon = IconHelper.GetFromDll(filePath: path, id).GetAsImageSource();
                }
                catch { }

                if(icon == null)
                {
                    try
                    {
                        icon = IconHelper.GetFromFilePath(newIconPath).GetAsImageSource();
                    }
                    catch { }
                }

                if(icon != null)
                    this.IconPathChanged?.Invoke(icon);
            });
            return 0;
        }

        public int OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            this.Disconnected?.Invoke(disconnectReason);
            return 0;
        }

        public int OnSimpleVolumeChanged(float newVolume, bool newMuteState, ref Guid eventContext)
        {
            //don't fire event if callback was called because of changes of volume inside the app
            if(eventContext.Equals(GuidValue.Internal.VolumeGUID))
                return 0;
            
            int newVolumeValue = Convert.ToInt32(newVolume * 100);

            //prevent invoking event if new volume value is not the same as the actual session volume value
            this.sVolume.GetMasterVolume(out float volume);
            var actualVolumeValue = Convert.ToInt32(volume * 100);
            if(newVolumeValue != actualVolumeValue)
                return 0;
            this.VolumeChanged?.Invoke(new VolumeChangedEventArgs(newVolumeValue, newMuteState));
            return 0;
        }

        public int OnStateChanged(AudioSessionState newState)
        {
            if(newState == AudioSessionState.Expired)
            {
                this.SessionEnded?.Invoke();
            }
            else
                this.StateChanged?.Invoke(newState);

            return 0;
        }

        public void RegisterNotifications()
            => Marshal.ThrowExceptionForHR(this.sessionControl.RegisterAudioSessionNotification(this));

        private void UnregisterNotifications()
            => this.sessionControl.UnregisterAudioSessionNotification(this);

        public void Dispose()
        {
            this.UnregisterNotifications();
        }
    }

    public readonly struct VolumeChangedEventArgs
    {
        public int NewVolume { get; }
        public bool IsMuted { get; }

        public VolumeChangedEventArgs(int newVol, bool newState)
        {
            NewVolume = newVol;
            IsMuted = newState;
        }
    }
}
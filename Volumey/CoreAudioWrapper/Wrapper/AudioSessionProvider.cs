using System;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notification when an audio session is created.
    /// </summary>
    class AudioSessionProvider : ISessionProvider
    {
        public event Action<object, SessionCreatedEventArgs> SessionCreated;
        private IAudioSessionManager2 sessionManager;

        internal AudioSessionProvider(IAudioSessionManager2 sessionManager)
        {
            this.sessionManager = sessionManager;
            this.RegisterSessionNotifications();
        }

        public int OnSessionCreated(IAudioSessionControl sessionControl)
        {
            var session = sessionControl.GetAudioSessionModel();
            if(session != null)
                this.SessionCreated?.Invoke(this, new SessionCreatedEventArgs(session, sessionControl));
            return 0;
        }

        private void RegisterSessionNotifications()
        {
            try { this.sessionManager?.RegisterSessionNotification(this); }
            catch(Exception e)
            {
                LogManager.GetLogger(typeof(AudioSessionProvider)).Error($"Failed to register session provider notifications", e);
            }
        }

        private void UnregisterSessionNotification()
        {
            this.sessionManager?.UnregisterSessionNotification(this);
        }

        public void Dispose()
        {
            this.UnregisterSessionNotification();
        }
    }
}

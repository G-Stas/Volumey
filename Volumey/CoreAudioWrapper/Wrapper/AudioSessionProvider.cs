using System;
using log4net;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notification when an audio session is created.
    /// </summary>
    class AudioSessionProvider : ISessionProvider
    {
        public event Action<object, SessionCreatedEventArgs> SessionCreated;
        private readonly AudioSessionManager sessionManager;

        internal AudioSessionProvider(AudioSessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
            this.sessionManager.OnSessionCreated += OnSessionCreated;
        }

        //NAudio raises it on a COM thread
        private void OnSessionCreated(object sender, AudioSessionControl sessionControl)
        {
            try
            {
                var session = AudioModelFactory.CreateSessionModel(sessionControl);
                if(session != null)
                    this.SessionCreated?.Invoke(this, new SessionCreatedEventArgs(session, sessionControl));
            }
            catch(Exception e)
            {
                LogManager.GetLogger(typeof(AudioSessionProvider)).Error("Failed to handle a new audio session", e);
            }
        }

        public void Dispose()
        {
            this.sessionManager.OnSessionCreated -= OnSessionCreated;
        }
    }
}

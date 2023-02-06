using System;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public class SessionCreatedEventArgs : EventArgs
	{
		public AudioSessionModel SessionModel;
		public IAudioSessionControl SessionControl;

		public SessionCreatedEventArgs(AudioSessionModel sessionModel, IAudioSessionControl sessionControl)
		{
			SessionModel = sessionModel;
			SessionControl = sessionControl;
		}
	}
}
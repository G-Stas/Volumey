using System;
using NAudio.CoreAudioApi;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public class SessionCreatedEventArgs : EventArgs
	{
		public AudioSessionModel SessionModel { get; }
		private readonly AudioSessionControl sessionControl;
		private readonly IHotkeyService hotkeys;

		internal SessionCreatedEventArgs(AudioSessionModel sessionModel, AudioSessionControl sessionControl, IHotkeyService hotkeys)
		{
			this.hotkeys = hotkeys;
			SessionModel = sessionModel;
			this.sessionControl = sessionControl;
		}

		/// <summary>
		/// Creates a process model for the new session when no existing process can hold it.
		/// </summary>
		public AudioProcessModel CreateProcessModel()
			=> AudioModelFactory.CreateProcessModel(this.SessionModel, this.sessionControl, this.hotkeys, out _);
	}
}

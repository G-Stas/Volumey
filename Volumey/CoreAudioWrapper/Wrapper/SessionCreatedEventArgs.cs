#nullable enable
using System;
using NAudio.CoreAudioApi;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.CoreAudioWrapper.Wrapper;

public sealed class SessionCreatedEventArgs : EventArgs
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
	/// Creates a process model for the new session when no existing process can hold it. Null if that fails.
	/// </summary>
	public AudioProcessModel? CreateProcessModel()
		=> AudioModelFactory.CreateProcessModel(SessionModel, sessionControl, hotkeys, out _);
}

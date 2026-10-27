#if DEBUG
#nullable enable
using System;
using System.Collections.Generic;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Volumey.ViewModel;
using Volumey.ViewModel.Settings;

namespace Volumey.Helper;

/// <summary>Injects silent, in-memory sessions for isolated mixer UI checks. Enabled only by an explicit Debug environment flag.</summary>
internal static class DebugMixerMockSessions
{
	internal static void Inject(DeviceProviderViewModel devices, IHotkeyService hotkeys)
	{
		if(devices.SelectedDevice is not { } device)
			return;

		//Keep the mixer UI limited to synthetic sources during evidence capture; releasing model wrappers never writes audio state.
		foreach(AudioProcessModel process in device.Processes)
			process.Dispose();
		device.Processes.Clear();

		var music = CreateProcess("Mock Music Player", 90001, "mock:music", hotkeys);
		var musicSession = CreateSession("mock:music:session", "Mock Music Player", 90001);
		musicSession.SetVisibilityDevice(device.Id);
		music.AddSession(musicSession);
		music.IconSource = null;
		device.Processes.Add(music);

		var chat = CreateProcess("Mock Voice Chat", 90002, "mock:chat", hotkeys);
		var chatSession = CreateSession("mock:chat:session", "Mock Voice Chat", 90002);
		chatSession.SetVisibilityDevice(device.Id);
		chat.AddSession(chatSession);
		chat.IconSource = null;
		device.Processes.Add(chat);
	}

	private static AudioProcessModel CreateProcess(string name, uint pid, string path, IHotkeyService hotkeys)
		=> new(50, false, name, pid, path, null, null, hotkeys);

	private static AudioSessionModel CreateSession(string id, string name, uint pid)
		=> new(false, 50, id, pid, name, "mock-session", Guid.Empty, new SilentSessionVolume(), new SilentSessionNotifications());

	private sealed class SilentSessionVolume : IAudioSessionVolume
	{
		public float GetVolume() => 0.5f;
		public bool GetMute() => false;
		public void SetVolume(int newVolume, Guid context) { }
		public void SetMute(bool newState, Guid context) { }
	}

	private sealed class SilentSessionNotifications : IAudioSessionStateNotifications
	{
		public event Action? SessionEnded { add { } remove { } }
		public event Action? Deactivated { add { } remove { } }
		public event Action<VolumeChangedEventArgs>? VolumeChanged { add { } remove { } }
		public event Action<string>? NameChanged { add { } remove { } }
		public event Action<AudioSessionDisconnectReason>? Disconnected { add { } remove { } }
		public void Dispose() { }
	}
}
#endif

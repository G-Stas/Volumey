using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// Reads real audio state from outside the app (NAudio), to check what a pressed hotkey did
/// </summary>
internal static class Audio
{
	public static bool HasOutputDevice
	{
		get
		{
			using var enumerator = new MMDeviceEnumerator();
			return enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		}
	}

	private static T WithDefaultDevice<T>(Func<MMDevice, T> read)
	{
		using var enumerator = new MMDeviceEnumerator();
		using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		return read(device);
	}

	/// <summary>The default device's volume, 0–100</summary>
	public static int DeviceVolume => WithDefaultDevice(d => (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100));

	public static bool DeviceMuted => WithDefaultDevice(d => d.AudioEndpointVolume.Mute);

	public static void SetDeviceVolume(int volume) => WithDefaultDevice(d => d.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100f);

	/// <summary>
	/// Remembers the default device's volume and mute and puts them back when disposed (the tests change them)
	/// </summary>
	public static IDisposable KeepDeviceState()
	{
		var (volume, mute, channels) = WithDefaultDevice(d => (d.AudioEndpointVolume.MasterVolumeLevelScalar, d.AudioEndpointVolume.Mute,
			Enumerable.Range(0, d.AudioEndpointVolume.Channels.Count).Select(i => d.AudioEndpointVolume.Channels[i].VolumeLevelScalar).ToArray()));
		return new Restore(() => WithDefaultDevice(d =>
		{
			d.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
			//the balance (the Devices page's balance test changes it)
			for(int i = 0; i < channels.Length && i < d.AudioEndpointVolume.Channels.Count; i++)
				d.AudioEndpointVolume.Channels[i].VolumeLevelScalar = channels[i];
			d.AudioEndpointVolume.Mute = mute;
			return 0;
		}));
	}

	/// <summary>The default device's channel levels, 0–100 (left, right, …)</summary>
	public static int[] ChannelVolumes => WithDefaultDevice(d => Enumerable.Range(0, d.AudioEndpointVolume.Channels.Count)
	                                                                       .Select(i => (int)Math.Round(d.AudioEndpointVolume.Channels[i].VolumeLevelScalar * 100))
	                                                                       .ToArray());

	/// <summary>The default device's format as Windows has it (PKEY_AudioEngine_DeviceFormat): sample rate and bits</summary>
	public static (int SampleRate, int Bits) DeviceFormat => WithDefaultDevice(d =>
	{
		var blob = (byte[])d.Properties[PropertyKeys.PKEY_AudioEngine_DeviceFormat].Value;
		return (BitConverter.ToInt32(blob, 4), BitConverter.ToInt16(blob, 14));
	});

	/// <summary>
	/// Remembers the volume and mute of every app (session) on the default device and puts them back when disposed:
	/// app volume sync changes all of them, other apps too
	/// </summary>
	public static IDisposable KeepSessionVolumes()
	{
		var saved = WithDefaultDevice(d =>
		{
			var states = new Dictionary<string, (float Volume, bool Mute)>();
			var sessions = d.AudioSessionManager.Sessions;
			for(int i = 0; i < sessions.Count; i++)
			{
				using var session = sessions[i];
				states[session.GetSessionInstanceIdentifier] = (session.SimpleAudioVolume.Volume, session.SimpleAudioVolume.Mute);
			}
			return states;
		});
		return new Restore(() => WithDefaultDevice(d =>
		{
			var sessions = d.AudioSessionManager.Sessions;
			for(int i = 0; i < sessions.Count; i++)
			{
				using var session = sessions[i];
				if(saved.TryGetValue(session.GetSessionInstanceIdentifier, out var state))
				{
					session.SimpleAudioVolume.Volume = state.Volume;
					session.SimpleAudioVolume.Mute = state.Mute;
				}
			}
			return 0;
		}));
	}

	/// <summary>The default device's name (Volumey's tray tooltip starts with it)</summary>
	public static string DefaultDeviceName => WithDefaultDevice(d => d.FriendlyName);

	/// <summary>The default device's endpoint ID (to check that a test didn't switch it)</summary>
	public static string DefaultDeviceId => WithDefaultDevice(d => d.ID);

	/// <summary>The names of the active output devices</summary>
	public static List<string> OutputDeviceNames()
	{
		using var enumerator = new MMDeviceEnumerator();
		var names = new List<string>();
		foreach(var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
			using(device)
				names.Add(device.FriendlyName);
		return names;
	}

	/// <summary>
	/// The volume (0–100) of a process's session on the default device, or null if it has none
	/// </summary>
	public static int? SessionVolume(int processId) => WithSession(processId, s => (int?)Math.Round(s.SimpleAudioVolume.Volume * 100));

	/// <summary>Whether a process's session on the default device is muted, or null if it has none</summary>
	public static bool? SessionMuted(int processId) => WithSession(processId, s => (bool?)s.SimpleAudioVolume.Mute);

	/// <summary>
	/// Changes a process's session volume (0–100) from outside the app, like the Windows volume mixer does
	/// </summary>
	public static void SetSessionVolume(int processId, int volume)
		=> WithSession(processId, s =>
		{
			s.SimpleAudioVolume.Volume = volume / 100f;
			return (bool?)true;
		});

	private static T? WithSession<T>(int processId, Func<AudioSessionControl, T?> read) => WithDefaultDevice(d =>
	{
		var sessions = d.AudioSessionManager.Sessions;
		for(int i = 0; i < sessions.Count; i++)
		{
			using var session = sessions[i];
			if(session.GetProcessID == processId)
				return read(session);
		}
		return default;
	});

	/// <summary>
	/// Waits until <paramref name="read"/> returns <paramref name="expected"/>; returns the last value
	/// </summary>
	public static T WaitFor<T>(Func<T> read, T expected, int timeoutMs = 3000)
	{
		var watch = Stopwatch.StartNew();
		T value = read();
		while(!EqualityComparer<T>.Default.Equals(value, expected) && watch.ElapsedMilliseconds < timeoutMs)
		{
			Thread.Sleep(100);
			value = read();
		}
		return value;
	}

	private sealed class Restore(Action restore) : IDisposable
	{
		public void Dispose() => restore();
	}
}

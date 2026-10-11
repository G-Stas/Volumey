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

	/// <summary>
	/// Remembers the default device's volume and mute and puts them back when disposed (the tests change them, owner's permission)
	/// </summary>
	public static IDisposable KeepDeviceState()
	{
		var (volume, mute) = WithDefaultDevice(d => (d.AudioEndpointVolume.MasterVolumeLevelScalar, d.AudioEndpointVolume.Mute));
		return new Restore(() => WithDefaultDevice(d =>
		{
			d.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
			d.AudioEndpointVolume.Mute = mute;
			return 0;
		}));
	}

	/// <summary>
	/// The volume (0–100) of a process's session on the default device, or null if it has none
	/// </summary>
	public static int? SessionVolume(int processId) => WithDefaultDevice(d =>
	{
		var sessions = d.AudioSessionManager.Sessions;
		for(int i = 0; i < sessions.Count; i++)
		{
			using var session = sessions[i];
			if(session.GetProcessID == processId)
				return (int?)Math.Round(session.SimpleAudioVolume.Volume * 100);
		}
		return null;
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

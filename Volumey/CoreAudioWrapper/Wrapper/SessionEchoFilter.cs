#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Recognizes audio session volume callbacks that were caused by the app's own volume and mute writes.
/// NAudio writes session volume with an empty event context and doesn't pass the context to session events,
/// so instead of comparing the context the filter remembers the state each own write leads to
/// and treats a callback that reports one of these states as an echo.
/// A state stays an echo for <see cref="EchoWindowMs"/>, because Windows can repeat a session event
/// (e.g. when several session controls of the same session were created in a process).
/// </summary>
internal sealed class SessionEchoFilter
{
	//an echo arrives within milliseconds, older writes are considered lost
	internal const long EchoWindowMs = 1000;

	private readonly Lock sync = new Lock();
	private readonly List<(int Volume, bool Mute, long Time)> pending = new List<(int, bool, long)>();
	private readonly Func<long> clock;
	private int volume;
	private bool mute;

	internal SessionEchoFilter(int volume, bool mute, Func<long>? clock = null)
	{
		this.volume = volume;
		this.mute = mute;
		this.clock = clock ?? (() => Environment.TickCount64);
	}

	internal void OnVolumeWritten(int newVolume)
	{
		lock(sync)
		{
			volume = newVolume;
			pending.Add((volume, mute, clock()));
		}
	}

	internal void OnMuteWritten(bool newMute)
	{
		lock(sync)
		{
			mute = newMute;
			pending.Add((volume, mute, clock()));
		}
	}

	/// <summary>
	/// Returns true if the reported state is the result of an own write. Otherwise remembers it as the current state.
	/// </summary>
	internal bool IsEcho(int newVolume, bool newMute)
	{
		if(IsOwnWrite(newVolume, newMute))
			return true;
		Remember(newVolume, newMute);
		return false;
	}

	/// <summary>
	/// Returns true if the reported state is the result of an own write, without remembering a state that isn't
	/// (a callback can be outdated: <see cref="Remember"/> it once it's known to be current)
	/// </summary>
	internal bool IsOwnWrite(int newVolume, bool newMute)
	{
		lock(sync)
		{
			long now = clock();
			for(int i = pending.Count - 1; i >= 0; i--)
			{
				if(now - pending[i].Time > EchoWindowMs)
					pending.RemoveAt(i);
			}

			for(int index = 0; index < pending.Count; index++)
			{
				if(pending[index].Volume == newVolume && pending[index].Mute == newMute)
				{
					//callbacks arrive in the order of the writes, so the earlier writes won't be reported anymore.
					//The matched write stays: Windows may deliver the same event several times to one handler
					pending.RemoveRange(0, index);
					return true;
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Remembers a state that wasn't caused by an own write as the current state
	/// </summary>
	internal void Remember(int newVolume, bool newMute)
	{
		lock(sync)
		{
			volume = newVolume;
			mute = newMute;
		}
	}
}

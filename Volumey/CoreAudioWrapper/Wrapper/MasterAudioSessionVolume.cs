#nullable enable
using System;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Controls the volume of a master session
/// </summary>
sealed class MasterAudioSessionVolume : IAudioSessionVolume, IDisposable
{
	private readonly AudioEndpointVolume audioVolume;
	//own lock object: locking the NAudio object could interfere with locks NAudio takes on it
	private readonly System.Threading.Lock writeLock = new();

	private readonly Action<int, Guid> volumeWriter;
	private readonly Action<bool, Guid> muteWriter;

	//whether writes are done by the worker or synchronously on the caller's thread
	private readonly bool background;

	//guards the fields below; never held during a device write
	private readonly System.Threading.Lock stateLock = new();
	//latest requested values waiting to be written (older ones are dropped)
	private (int Volume, Guid Context)? pendingVolume;
	private (bool Mute, Guid Context)? pendingMute;
	private Task? worker;
	//the latest worker, kept after it ends so it can be awaited
	private Task completion = Task.CompletedTask;
	//written under stateLock, read without it: the model asks for every notification on the UI thread
	private volatile bool busy;
	private volatile bool disposed;

	/// <summary>
	/// True while a write is queued or running
	/// </summary>
	public bool HasPendingWrites => busy;

	/// <summary>
	/// Raised on a worker thread each time the queued writes are done: the error of the last write that failed (null if none did)
	/// and what the device has now (null if it couldn't be read)
	/// </summary>
	public event Action<Exception?, VolumeChangedEventArgs?>? WritesCompleted;

	public MasterAudioSessionVolume(AudioEndpointVolume aVol)
	{
		audioVolume = aVol;
		volumeWriter = WriteVolume;
		muteWriter = WriteMute;
		background = true;
	}

	/// <param name="volumeWriter">Writes the volume to the device</param>
	/// <param name="muteWriter">Writes the mute state to the device</param>
	/// <param name="background">False writes synchronously on the caller's thread instead of on the worker (for tests)</param>
	internal MasterAudioSessionVolume(Action<int, Guid> volumeWriter, Action<bool, Guid> muteWriter, bool background)
	{
		//there is no device: only the writers are used
		audioVolume = null!;
		this.volumeWriter = volumeWriter;
		this.muteWriter = muteWriter;
		this.background = background;
	}

	/// <summary>
	/// Get's the master volume level as a uniform scaling factor.
	/// </summary>
	public float GetVolume() => audioVolume.MasterVolumeLevelScalar;

	/// <summary>
	/// Sets the master volume level, using a uniform scaling factor.
	/// </summary>
	public void SetVolume(int volumeLevel, Guid context)
		=> Enqueue((Math.Clamp(volumeLevel, 0, 100), context), null);

	private void WriteVolume(int volumeLevel, Guid context)
	{
		int sysVol = -1;
		try { sysVol = VolumePercent.FromScalar(GetVolume()); }
		catch { }

		//prevent setting volume in system if the master session already has the same value
		if(sysVol != volumeLevel)
		{
			//NAudio passes NotificationGuid as the event context of its writes
			lock(writeLock)
			{
				audioVolume.NotificationGuid = context;
				audioVolume.MasterVolumeLevelScalar = VolumePercent.ToScalar(volumeLevel);
			}
		}
	}

	/// <summary>
	/// Get the mute state for the master volume.
	/// </summary>
	public bool GetMute() => audioVolume.Mute;

	/// <summary>
	/// Set the mute state for the master volume.
	/// </summary>
	public void SetMute(bool muteState, Guid context)
		=> Enqueue(null, (muteState, context));

	private void WriteMute(bool muteState, Guid context)
	{
		lock(writeLock)
		{
			//the caller's context: hotkey changes must come back as notifications (the UI's own changes are skipped)
			audioVolume.NotificationGuid = context;
			audioVolume.Mute = muteState;
		}
	}

	private void Enqueue((int Volume, Guid Context)? volume, (bool Mute, Guid Context)? mute)
	{
		if(!background)
		{
			//exceptions reach the caller, as with the original synchronous writes
			if(mute is { } m) muteWriter(m.Mute, m.Context);
			if(volume is { } v) volumeWriter(v.Volume, v.Context);
			return;
		}

		lock(stateLock)
		{
			if(disposed)
				return;
			if(volume != null) pendingVolume = volume;
			if(mute != null) pendingMute = mute;
			busy = true;
			if(worker == null)
				completion = worker = Task.Run(Run);
		}
	}

	private void Run()
	{
		Exception? error = null;
		VolumeChangedEventArgs? state = null;
		while(true)
		{
			(int Volume, Guid Context)? volume;
			(bool Mute, Guid Context)? mute;
			bool idle;
			lock(stateLock)
			{
				volume = pendingVolume;
				mute = pendingMute;
				pendingVolume = null;
				pendingMute = null;
				if(disposed)
				{
					worker = null;
					busy = false;
					return;
				}
				idle = volume == null && mute == null;
				//from here on the model applies notifications again, so the state read below can't miss a change
				if(idle)
					busy = false;
			}

			if(idle)
			{
				//the queue is empty: read what the device has. A change made meanwhile by someone else
				//(e.g. the keyboard's mute key) was dropped as a possible echo, and nothing else would pick it up
				state = ReadDeviceState();
				lock(stateLock)
				{
					//nothing came in while we were reading: go idle
					if(disposed || (pendingVolume == null && pendingMute == null))
					{
						worker = null;
						break;
					}
				}
				continue;
			}

			//mute first: an unmute that precedes a volume step must not leave the step inaudible
			try { if(mute is { } m) muteWriter(m.Mute, m.Context); }
			catch(Exception e) { error = e; }
			try { if(volume is { } v) volumeWriter(v.Volume, v.Context); }
			catch(Exception e) { error = e; }
		}

		//reported once the worker is idle: the model only trusts the device's state when nothing newer is coming
		if(!disposed)
			WritesCompleted?.Invoke(error, state);
	}

	private VolumeChangedEventArgs? ReadDeviceState()
	{
		try { return new VolumeChangedEventArgs(VolumePercent.FromScalar(GetVolume()), GetMute()); }
		catch { return null; }
	}

	public Task WaitForWritesAsync()
	{
		lock(stateLock)
			return completion;
	}

	/// <summary>
	/// Drops the queued writes and doesn't write anything else. It doesn't wait for the write in progress (it can't be interrupted):
	/// await <see cref="WaitForWritesAsync"/> before the device is released
	/// </summary>
	public void Dispose()
	{
		lock(stateLock)
		{
			disposed = true;
			pendingVolume = null;
			pendingMute = null;
		}
	}
}

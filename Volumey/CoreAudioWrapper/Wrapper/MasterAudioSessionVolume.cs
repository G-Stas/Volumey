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
	private readonly Action<int, Guid> volumeWriter;
	private readonly Action<bool, Guid> muteWriter;
	private readonly Func<float> volumeReader;
	private readonly Func<bool> muteReader;

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
	/// Raised on a worker thread each time the queued writes are done: the error of the last write that failed (null if none did).
	/// The listener reads the device state itself when it handles it
	/// </summary>
	public event Action<Exception?>? WritesCompleted;

	public MasterAudioSessionVolume(AudioEndpointVolume aVol) : this(aVol, new System.Threading.Lock())
	{
	}

	//own lock object: locking the NAudio object could interfere with locks NAudio takes on it
	private MasterAudioSessionVolume(AudioEndpointVolume aVol, System.Threading.Lock writeLock)
		: this((level, context) => WriteVolume(aVol, writeLock, level, context),
			   (state, context) => WriteMute(aVol, writeLock, state, context),
			   () => aVol.MasterVolumeLevelScalar,
			   () => aVol.Mute)
	{
	}

	/// <param name="volumeWriter">Writes the volume to the device</param>
	/// <param name="muteWriter">Writes the mute state to the device</param>
	/// <param name="volumeReader">Reads the device's volume as a uniform scaling factor</param>
	/// <param name="muteReader">Reads the device's mute state</param>
	internal MasterAudioSessionVolume(Action<int, Guid> volumeWriter, Action<bool, Guid> muteWriter, Func<float> volumeReader, Func<bool> muteReader)
	{
		this.volumeWriter = volumeWriter;
		this.muteWriter = muteWriter;
		this.volumeReader = volumeReader;
		this.muteReader = muteReader;
	}

	/// <summary>
	/// Get's the master volume level as a uniform scaling factor.
	/// </summary>
	public float GetVolume() => volumeReader();

	/// <summary>
	/// Sets the master volume level, using a uniform scaling factor.
	/// </summary>
	public void SetVolume(int volumeLevel, Guid context)
		=> Enqueue((Math.Clamp(volumeLevel, 0, 100), context), null);

	private static void WriteVolume(AudioEndpointVolume audioVolume, System.Threading.Lock writeLock, int volumeLevel, Guid context)
	{
		int sysVol = -1;
		try { sysVol = VolumePercent.FromScalar(audioVolume.MasterVolumeLevelScalar); }
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
	public bool GetMute() => muteReader();

	/// <summary>
	/// Set the mute state for the master volume.
	/// </summary>
	public void SetMute(bool muteState, Guid context)
		=> Enqueue(null, (muteState, context));

	private static void WriteMute(AudioEndpointVolume audioVolume, System.Threading.Lock writeLock, bool muteState, Guid context)
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
		while(true)
		{
			(int Volume, Guid Context)? volume;
			(bool Mute, Guid Context)? mute;
			lock(stateLock)
			{
				volume = pendingVolume;
				mute = pendingMute;
				pendingVolume = null;
				pendingMute = null;
				//nothing came in while we were writing: go idle. From here on the model applies notifications again
				if(disposed || (volume == null && mute == null))
				{
					worker = null;
					busy = false;
					break;
				}
			}

			//mute first: an unmute that precedes a volume step must not leave the step inaudible
			try { if(mute is { } m) muteWriter(m.Mute, m.Context); }
			catch(Exception e) { error = e; }
			try { if(volume is { } v) volumeWriter(v.Volume, v.Context); }
			catch(Exception e) { error = e; }
		}

		//reported once the worker is idle: the model reads the device then, so it can't miss a change made while it was busy
		if(!disposed)
			WritesCompleted?.Invoke(error);
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

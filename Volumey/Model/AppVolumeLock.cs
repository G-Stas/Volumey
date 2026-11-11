#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Volumey.Helper;

namespace Volumey.Model;

/// <summary>A held volume: how it's held, the volume the app is held at and the volume it gets back when the lock is released on exit</summary>
internal sealed record VolumeLock(VolumeLockMode Mode, int Volume, int VolumeBefore);

/// <summary>
/// The volume lock of one app (<see cref="AudioProcessModel.Lock"/>). While it holds a <see cref="VolumeLock"/>, the app's sessions are set
/// to its volume and any later change to them is undone, also for a session added later. Which apps are held is kept by
/// <see cref="ViewModel.Settings.AppVolumeLockViewModel"/>, which a mode picked in the mixer goes to (<see cref="Requested"/>)
/// </summary>
public sealed class AppVolumeLock : INotifyPropertyChanged
{
	private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(AppVolumeLock));

	private readonly AudioProcessModel app;
	private readonly object sessionsLock;

	/// <summary>Null while the app is free; read on COM threads as well</summary>
	private volatile VolumeLock? held;

	private int passQueued;

	//taken for a put-back write and for changing the held lock: a write can't follow a release or a new volume
	private readonly object putBackLock = new();

	/// <param name="sessionsLock">The lock the app's <see cref="AudioProcessModel.Sessions"/> are changed under</param>
	internal AppVolumeLock(AudioProcessModel app, object sessionsLock)
	{
		this.app = app;
		this.sessionsLock = sessionsLock;
	}

	/// <summary>The lock that holds the app, null while it's free</summary>
	internal VolumeLock? Held => held;

	public bool IsOn => held != null;

	/// <summary>
	/// How the app is held. Setting it asks for that lock (<see cref="Requested"/>); it changes once the lock is made (<see cref="Hold"/>,
	/// <see cref="Release"/>)
	/// </summary>
	public VolumeLockMode Mode
	{
		get => held?.Mode ?? VolumeLockMode.Off;
		set
		{
			if(value == Mode)
				return;
			Requested?.Invoke(app, value);
			//a refused lock: the mixer goes back to the mode the app has
			if(value != Mode)
				OnPropertyChanged();
		}
	}

	/// <summary>A lock mode was picked in the app's row</summary>
	internal event Action<AudioProcessModel, VolumeLockMode>? Requested;

	/// <summary>
	/// Runs the pass that puts a held volume back, off the thread of the notification that reported the change: an audio notification must
	/// not call the audio API
	/// </summary>
	internal Action<Action> RunWork { get; set; } = work => Task.Run(work).Forget(Logger, "Failed to put a locked volume back");

	/// <summary>Holds the app at the lock's volume (clamped to 0..100) until <see cref="Release"/> or until it exits</summary>
	internal void Hold(VolumeLock volumeLock)
	{
		volumeLock = volumeLock with { Volume = Math.Clamp(volumeLock.Volume, 0, 100) };
		bool wasOn = IsOn;
		VolumeLockMode oldMode = Mode;
		lock(sessionsLock)
		{
			lock(putBackLock)
				held = volumeLock;
			foreach(AudioSessionModel session in app.Sessions)
				Subscribe(session);
		}
		app.ShowHeldVolume(volumeLock.Volume);
		PutBack();
		if(!wasOn)
			OnPropertyChanged(nameof(IsOn));
		if(oldMode != volumeLock.Mode)
			OnPropertyChanged(nameof(Mode));
	}

	/// <summary>Frees the app; with <paramref name="restore"/> it gets back <see cref="VolumeLock.VolumeBefore"/>. Does nothing while it's free</summary>
	internal void Release(bool restore = false)
	{
		VolumeLock? released;
		lock(sessionsLock)
		{
			lock(putBackLock)
			{
				released = held;
				held = null;
			}
			foreach(AudioSessionModel session in app.Sessions)
				Detach(session);
		}
		if(released == null)
			return;
		OnPropertyChanged(nameof(IsOn));
		OnPropertyChanged(nameof(Mode));
		if(restore)
			app.Volume = released.VolumeBefore;
	}

	/// <summary>A session added to the app: while it's held, the session gets the volume and is held too. Under the sessions' lock</summary>
	internal void Attach(AudioSessionModel session)
	{
		if(held is not { } current)
			return;
		Subscribe(session);
		session.Volume = current.Volume;
	}

	internal void Detach(AudioSessionModel session) => session.VolumeChanged -= OnSessionVolumeChanged;

	private void Subscribe(AudioSessionModel session)
	{
		session.VolumeChanged -= OnSessionVolumeChanged;
		session.VolumeChanged += OnSessionVolumeChanged;
	}

	//raised on a COM thread; also for the lock's own writes, which already have the held volume
	private void OnSessionVolumeChanged(int volume, Guid context)
	{
		if(held is not { } current || volume == current.Volume)
			return;
		//one pass puts back everything that changed before it runs
		if(Interlocked.Exchange(ref passQueued, 1) == 0)
			RunWork(RunPass);
	}

	private void RunPass()
	{
		//first: a change that arrives while the pass runs queues the next one
		Volatile.Write(ref passQueued, 0);
		PutBack();
	}

	private void PutBack()
	{
		if(held is not { } current || app.IsDisposed)
			return;
		foreach(AudioSessionModel session in app.SessionsSnapshot())
		{
			lock(putBackLock)
			{
				//released or held at another volume while this pass was running
				if(held?.Volume != current.Volume)
					return;
				if(session.Volume != current.Volume)
					session.Volume = current.Volume;
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model;

public sealed class AudioProcessModel : IManagedMasterAudioSession
{
	private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(AudioProcessModel));

	public event Action<AudioProcessModel>? Exited;

	/// <summary>
	/// A session of the process was disconnected because the device's format changed, see <see cref="AudioSessionModel.EndedByFormatChange"/>
	/// </summary>
	internal event Action<AudioProcessModel>? DeviceReconfigured;

	public ObservableCollection<AudioSessionModel> Sessions { get; }

	public readonly string RawProcessName = string.Empty;
	private string name = string.Empty;
	public string Name
	{
		get => name;
		set
		{
			if(name != value)
			{
				name = value;
				OnPropertyChanged();
			}
		}
	}

	private string? _id;
	public string Id => _id ??= _processId.ToString(CultureInfo.InvariantCulture);

	private readonly uint _processId;
	public uint ProcessId => _processId;

	public string FilePath { get; }

	public Guid GroupingParam { get; private set; } = Guid.Empty;

	public AppIdentity Identity => new(GroupingParam, FilePath);

	/// <summary>Windows' System Sounds: its session has no process</summary>
	public bool IsSystemSounds => _processId == 0;

	private AppRoute _route = new AppRoute.FollowsDefault();
	/// <summary>Where Windows plays the app; set by <see cref="AppRouting"/> on the UI thread</summary>
	public AppRoute Route
	{
		get => _route;
		private set
		{
			if(_route != value)
			{
				_route = value;
				OnPropertyChanged();
			}
		}
	}

	private string? _routeDeviceName;
	/// <summary>The name of the device the app is pinned to; null when it follows the default device or Windows can't name the device</summary>
	public string? RouteDeviceName
	{
		get => _routeDeviceName;
		private set
		{
			if(_routeDeviceName != value)
			{
				_routeDeviceName = value;
				OnPropertyChanged();
			}
		}
	}

	private bool _playsHere = true;
	/// <summary>The app's row belongs in its device's list (<see cref="AppRoutingRules.PlaysHere"/>)</summary>
	public bool PlaysHere
	{
		get => _playsHere;
		private set
		{
			if(_playsHere != value)
			{
				_playsHere = value;
				OnPropertyChanged();
			}
		}
	}

	internal void SetPlacement(AppRoute route, string? routeDeviceName, bool playsHere)
	{
		Route = route;
		RouteDeviceName = routeDeviceName;
		PlaysHere = playsHere;
	}

	/// <summary>A session of the app plays now</summary>
	internal bool PlaysNow => SessionsSnapshot().Any(s => s.IsActive);

	private void OnSessionActivityChanged(AudioSessionModel session) => ActivityChanged?.Invoke(this);

	/// <summary>A session started or stopped playing, or one that plays was added or removed; on any thread</summary>
	internal event Action<AudioProcessModel>? ActivityChanged;

	internal bool IsDisposed => _disposed;

	private bool _isMuted;
	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if(_isMuted != value)
			{
				//first: the tracked session reports the change back, and the model then already has it (see OnTrackedSessionStateChanged)
				_isMuted = value;
				lock(_sessionsLock)
				{
					foreach(AudioSessionModel session in Sessions)
						session.IsMuted = value;
				}
				OnPropertyChanged();
			}
		}
	}

	private int _volume;
	/// <summary>
	/// The app's volume. While it's locked (<see cref="IsVolumeLocked"/>) it can't be set, by the slider, a hotkey or anything else
	/// </summary>
	public int Volume
	{
		get => _volume;
		set
		{
			if(IsVolumeLocked)
				return;
			if(_volume != value)
			{
				if(value < 0)
					_volume = 0;
				else if(value > 100)
					_volume = 100;
				else
					_volume = value;

				lock(_sessionsLock)
				{
					foreach(AudioSessionModel session in Sessions)
						session.Volume = _volume;
				}
				OnPropertyChanged();
			}
		}
	}

	private const int NotLocked = -1;

	/// <summary>The volume the app is locked at, or <see cref="NotLocked"/>; read on COM threads as well</summary>
	private volatile int _lockedVolume = NotLocked;

	/// <summary>The volume is locked: <see cref="LockVolume"/></summary>
	public bool IsVolumeLocked => _lockedVolume != NotLocked;

	private volatile VolumeLockMode _lockMode;

	/// <summary>How the volume is locked: not at all, until the app exits, or also when it starts again</summary>
	public VolumeLockMode LockMode => _lockMode;

	/// <summary>The lock button of the app's row was pressed</summary>
	internal event Action<AudioProcessModel>? VolumeLockRequested;

	public ICommand ToggleVolumeLockCommand { get; }

	/// <summary>
	/// Runs the work that puts a locked volume back, off the thread of the notification that reported the change: an audio
	/// notification must not call the audio API
	/// </summary>
	internal Action<Action> RunLockWork { get; set; } = RunOnThreadPool;

	private static void RunOnThreadPool(Action work) => Task.Run(() =>
	{
		try
		{
			work();
		}
		catch(Exception e)
		{
			Logger.Error("Failed to put a locked volume back", e);
		}
	});

	private int _lockWorkQueued;
	private int _volumeBeforeLock;

	/// <summary>
	/// Holds the app at <paramref name="volume"/> until <see cref="UnlockVolume"/> or until it exits: the sessions are set to it, any
	/// later change to them is undone, and a session that is added gets it too. <paramref name="persistent"/> only tells
	/// <see cref="LockMode"/>; saving the lock is up to the caller
	/// </summary>
	internal void LockVolume(int volume, bool persistent = false)
	{
		volume = Math.Clamp(volume, 0, 100);
		bool changed = _volume != volume;
		lock(_sessionsLock)
		{
			if(!IsVolumeLocked)
				_volumeBeforeLock = _volume;
			_lockedVolume = volume;
			_lockMode = persistent ? VolumeLockMode.Persistent : VolumeLockMode.Session;
			_volume = volume;
			foreach(AudioSessionModel session in Sessions)
			{
				session.VolumeChanged -= OnLockedSessionVolumeChanged;
				session.VolumeChanged += OnLockedSessionVolumeChanged;
			}
		}
		PutLockedVolumeBack();
		OnPropertyChanged(nameof(IsVolumeLocked));
		OnPropertyChanged(nameof(LockMode));
		if(changed)
			OnPropertyChanged(nameof(Volume));
	}

	internal void UnlockVolume()
	{
		lock(_sessionsLock)
		{
			_lockedVolume = NotLocked;
			_lockMode = VolumeLockMode.None;
			foreach(AudioSessionModel session in Sessions)
				session.VolumeChanged -= OnLockedSessionVolumeChanged;
		}
		OnPropertyChanged(nameof(IsVolumeLocked));
		OnPropertyChanged(nameof(LockMode));
	}

	/// <summary>Releases the lock and gives the sessions the volume the app had before it was locked. Does nothing if it isn't locked</summary>
	internal void RestoreVolumeBeforeLock()
	{
		if(!IsVolumeLocked)
			return;
		UnlockVolume();
		Volume = _volumeBeforeLock;
	}

	//raised on a COM thread; also for the writes of this model, which already have the locked volume
	private void OnLockedSessionVolumeChanged(int volume, Guid context)
	{
		if(volume == _lockedVolume || !IsVolumeLocked)
			return;
		//one pass puts back everything that changed before it runs
		if(Interlocked.Exchange(ref _lockWorkQueued, 1) == 0)
			RunLockWork(RunLockPass);
	}

	private void RunLockPass()
	{
		//first: a change that arrives while the pass runs queues the next one
		Volatile.Write(ref _lockWorkQueued, 0);
		PutLockedVolumeBack();
	}

	private void PutLockedVolumeBack()
	{
		int locked = _lockedVolume;
		if(locked == NotLocked || _disposed)
			return;
		foreach(AudioSessionModel session in SessionsSnapshot())
		{
			//unlocked or restored while this pass was running
			if(_lockedVolume != locked)
				return;
			if(session.Volume != locked)
				session.Volume = locked;
		}
	}

	private ImageSource? _iconSource;
	public ImageSource? IconSource
	{
		get => _iconSource;
		set
		{
			_iconSource = value;
			OnPropertyChanged();
		}
	}

	public bool AnyHotkeyRegistered { get; private set; }

	private double _level;
	/// <summary>
	/// How loud the app plays (0..1, before its volume): its loudest unmuted session; set by the mixer's level meters
	/// </summary>
	public double Level
	{
		get => _level;
		set
		{
			if(_level != value)
			{
				_level = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>The sessions at this moment: they can be added or removed on other threads</summary>
	internal AudioSessionModel[] SessionsSnapshot()
	{
		lock(_sessionsLock)
		{
			return Sessions.ToArray();
		}
	}

	/// <summary>
	/// Session of the process which is used as reference to current process state i.e. its current volume and mute state 
	/// </summary>
	private AudioSessionModel? _trackedSession;

	public IAudioProcessStateMediator? StateNotificationMediator { get; private set; }

	private HotKey? _volumeUp;
	private HotKey? _volumeDown;
	private HotKey? _mute;
	//the app's models on all devices, which a hotkey press changes too
	private Func<IReadOnlyList<AudioProcessModel>>? _sameApp;

	public ICommand MuteCommand { get; set; }

	private object _sessionsLock = new object();

	private static Dispatcher _dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	private readonly IHotkeyService _hotkeys;

	private readonly Process? _process;

	/// <param name="iconSource">A frozen image, shared with the other models of the same app (see <see cref="IconHelper.GetIconSource"/>)</param>
	public AudioProcessModel(int volume, bool isMuted, string name, uint processId, string filePath, ImageSource? iconSource, Process? proc, IHotkeyService hotkeys, IAudioProcessStateMediator? stateNotificationMediator = null)
	{
		_hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
		_volume = volume;
		_isMuted = isMuted;
		_iconSource = iconSource;
		_processId = processId;
		Name = name;
		FilePath = filePath;

		//the model owns the process: watching it for its exit keeps its handle open (and this model alive) until Dispose
		//Might throw Access Denied if its a system process
		try
		{
			if(proc != null)
			{
				RawProcessName = proc.ProcessName;
				proc.EnableRaisingEvents = true;
				proc.Exited += OnProcessExited;
				_process = proc;
			}
		}
		catch
		{
			proc?.Dispose();
		}

		Sessions = new ObservableCollection<AudioSessionModel>();

		MuteCommand = new ActionCommand(() => IsMuted = !IsMuted);
		ToggleVolumeLockCommand = new ActionCommand(() => VolumeLockRequested?.Invoke(this));
		StateNotificationMediator = stateNotificationMediator;
	}

	public void AddSession(AudioSessionModel newSession)
	{
		bool addedPlaying = false;
		lock(_sessionsLock)
		{
			if(!Sessions.Contains(newSession))
			{
				newSession.SessionEnded += OnSessionEnded;
				newSession.Deactivated += OnSessionDeactivated;
				newSession.ActivityChanged += OnSessionActivityChanged;
				addedPlaying = newSession.IsActive;
				Sessions.Add(newSession);
				newSession.IconSource = IconSource;
				if(Sessions.Count == 1)
				{
					Volume = newSession.Volume;
					IsMuted = newSession.IsMuted;
					GroupingParam = newSession.GroupingParam;
					_trackedSession = newSession;
					_trackedSession.VolumeChanged += OnTrackedSessionVolumeChanged;
					_trackedSession.MuteStateChanged += OnTrackedSessionStateChanged;
				}
				else
				{
					newSession.Volume = Volume;
					newSession.IsMuted = IsMuted;
				}
				if(IsVolumeLocked)
				{
					newSession.VolumeChanged += OnLockedSessionVolumeChanged;
					newSession.Volume = _lockedVolume;
				}
			}
		}
		if(addedPlaying)
			ActivityChanged?.Invoke(this);
	}

	//also raised when this model's own setter changes the sessions: the setter raises PropertyChanged then
	private void OnTrackedSessionStateChanged(bool muteState, Guid context)
	{
		if(_isMuted != muteState)
		{
			_isMuted = muteState;
			OnPropertyChanged(nameof(IsMuted));
		}

		if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
		{
			if(!context.Equals(GuidValue.Internal.VolumeGUID))
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}
	}

	private void OnTrackedSessionVolumeChanged(int volume, Guid context)
	{
		//a locked volume doesn't follow the session: it's put back
		if(IsVolumeLocked)
			return;
		if(_volume != volume)
		{
			_volume = volume;
			OnPropertyChanged(nameof(Volume));
		}

		if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
		{
			if(!context.Equals(GuidValue.Internal.VolumeGUID))
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}
	}

	public bool SetVolumeHotkeys(HotKey? volUp, HotKey? volDown) => SetVolumeHotkeys(volUp, volDown, out _);

	/// <param name="failed">The hotkey the system refused, null otherwise</param>
	/// <summary>Registers both volume hotkeys, or neither</summary>
	public bool SetVolumeHotkeys(HotKey? volUp, HotKey? volDown, out HotKey? failed)
	{
		failed = null;
		if(volUp == null || volDown == null)
			return false;
		if(SetHotkeys(volUp, volDown, null, out var refused))
			return true;
		ResetVolumeHotkeys();
		failed = refused[0];
		return false;
	}

	/// <summary>
	/// Registers the app's hotkeys that are set, each on its own: one the system refuses doesn't keep the others from working
	/// </summary>
	/// <param name="failed">The hotkeys the system refused</param>
	/// <param name="sameApp">The models of this app on all devices, which a press changes to this model's new volume or mute;
	/// null for this model only</param>
	/// <returns>False if any was refused</returns>
	public bool SetHotkeys(HotKey? volUp, HotKey? volDown, HotKey? mute, out IReadOnlyList<HotKey> failed,
		Func<IReadOnlyList<AudioProcessModel>>? sameApp = null)
	{
		var refused = new List<HotKey>();
		HotKey? Register(HotKey? hotkey)
		{
			if(hotkey == null)
				return null;
			try
			{
				if(_hotkeys.RegisterHotkey(hotkey))
					return hotkey;
			}
			catch { }
			refused.Add(hotkey);
			return null;
		}

		_sameApp = sameApp;
		_volumeUp = Register(volUp);
		_volumeDown = Register(volDown);
		_mute = Register(mute);
		failed = refused;
		if(_volumeUp != null || _volumeDown != null || _mute != null)
		{
			_hotkeys.HotkeyPressed -= OnHotkeyPressed;
			_hotkeys.HotkeyPressed += OnHotkeyPressed;
			AnyHotkeyRegistered = true;
		}
		return refused.Count == 0;
	}

	public void ResetVolumeHotkeys()
	{
		UnregisterHotkeys();
	}

	private void UnregisterHotkeys()
	{
		try
		{
			_hotkeys.UnregisterHotkeys(new[] { _volumeUp, _volumeDown, _mute });
		}
		catch { }
		_hotkeys.HotkeyPressed -= OnHotkeyPressed;
		AnyHotkeyRegistered = false;
		_volumeUp = _volumeDown = _mute = null;
		_sameApp = null;
	}

	public void SetVolume(int newValue, bool notify, Guid guid)
	{
		Volume = newValue;
		if(notify)
			StateNotificationMediator?.NotifyAudioStateChange(this);
	}

	public void SetMute(bool newState, bool notify, Guid context)
	{
		IsMuted = newState;
		if(notify)
			StateNotificationMediator?.NotifyAudioStateChange(this);
	}

	public void SetStateMediator(IAudioProcessStateMediator mediator)
	{
		StateNotificationMediator = mediator;
	}

	public void ResetStateMediator(bool force = false)
	{
		if(!force && SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
			return;
		StateNotificationMediator = null;
	}

	private void OnHotkeyPressed(HotKey hotkey)
	{
		IReadOnlyList<AudioProcessModel> sameApp = _sameApp?.Invoke() ?? [];
		if(hotkey.Equals(_volumeUp))
			ApplyHotkey(1, sameApp);
		else if(hotkey.Equals(_volumeDown))
			ApplyHotkey(-1, sameApp);
		//the whole app: all its sessions, like the mixer's mute button
		else if(hotkey.Equals(_mute))
			ApplyHotkey(0, sameApp);
	}

	/// <summary>
	/// A hotkey press: the volume by the volume step (<paramref name="direction"/> 1 or -1) or the mute toggled (0), with the OSD for this
	/// model; the app's other models (<paramref name="sameApp"/>, on other devices) get the same volume or mute
	/// </summary>
	internal void ApplyHotkey(int direction, IReadOnlyList<AudioProcessModel> sameApp)
	{
		if(direction == 0)
			SetMute(!IsMuted, notify: true, GuidValue.Internal.Empty);
		else
			SetVolume(Volume + direction * _hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
		foreach(var other in sameApp)
		{
			if(other == this || other.IsDisposed)
				continue;
			if(direction == 0)
				other.IsMuted = IsMuted;
			else
				other.Volume = Volume;
		}
	}

	private int _exitedInvoked;

	private void OnProcessExited(object? sender, EventArgs e)
	{
		if(sender is Process process)
			process.Exited -= OnProcessExited;
		InvokeExited();
	}

	private static readonly TimeSpan ExitCheckInterval = TimeSpan.FromSeconds(5);
	private System.Threading.Timer? _exitCheck;

	/// <summary>
	/// For an app whose process can't be watched for its exit (it refuses even a handle to wait on, e.g. a game guarded by anti-cheat):
	/// looks for its sessions' processes in the system's process list every few seconds and raises <see cref="Exited"/> once none runs.
	/// Windows keeps an exited app's sessions (inactive), so they don't end it. A restarted app's new session joins this model
	/// (same executable), so the model lives on while any of its sessions' processes runs.
	/// </summary>
	internal void WatchExitInProcessList() => WatchExitInProcessList(ExitCheckInterval);

	/// <param name="interval">How often the process list is checked</param>
	internal void WatchExitInProcessList(TimeSpan interval)
	{
		if(_process != null || _processId == 0 || _exitCheck != null)
			return;
		_exitCheckInterval = interval;
		_exitCheck = new System.Threading.Timer(_ => CheckExit(), null, interval, interval);
	}

	private TimeSpan _exitCheckInterval;

	//a session goes inactive when its app stops playing, which is also the moment it exits: the next check comes right away
	//(after a moment: the process can still be in the list while it shuts down)
	private static readonly TimeSpan ExitCheckAfterDeactivation = TimeSpan.FromMilliseconds(500);

	private void OnSessionDeactivated(AudioSessionModel session)
	{
		try { _exitCheck?.Change(ExitCheckAfterDeactivation, _exitCheckInterval); }
		catch(ObjectDisposedException) { }
	}

	private void CheckExit()
	{
		//compared by path when it's known: process IDs are reused
		string? path = System.IO.Path.IsPathFullyQualified(FilePath) ? FilePath : null;
		uint[] processIds;
		lock(_sessionsLock)
		{
			processIds = Sessions.Select(s => s.ProcessId).Append(_processId).Where(id => id != 0).Distinct().ToArray();
		}
		foreach(uint processId in processIds)
		{
			if(NativeMethods.IsProcessRunning((int)processId, path))
				return;
		}
		InvokeExited();
	}

	private void OnSessionEnded(AudioSessionModel endedSession)
	{
		if(endedSession != null)
			ProcessEndedSession(endedSession).Forget(Logger, $"Failed to remove an ended session of [{Name}]");
	}

	private async Task ProcessEndedSession(AudioSessionModel endedSession)
	{
		endedSession.SessionEnded -= OnSessionEnded;
		endedSession.Deactivated -= OnSessionDeactivated;
		endedSession.ActivityChanged -= OnSessionActivityChanged;
		endedSession.VolumeChanged -= OnLockedSessionVolumeChanged;
		if(endedSession.IsActive)
			ActivityChanged?.Invoke(this);
		if(endedSession.EndedByFormatChange)
			DeviceReconfigured?.Invoke(this);
		await _dispatcher.InvokeAsync(() =>
		{
			lock(_sessionsLock)
			{
				Sessions.Remove(endedSession);
				if(Sessions.Count == 0)
					InvokeExited();
			}
		}).Task.ContinueWith(_ =>
		{
			//under the lock: the last session's end also ends the app, whose model is disposed on another thread meanwhile
			lock(_sessionsLock)
			{
				if(_trackedSession == endedSession)
				{
					endedSession.VolumeChanged -= OnTrackedSessionVolumeChanged;
					endedSession.MuteStateChanged -= OnTrackedSessionStateChanged;
					_trackedSession = Sessions.FirstOrDefault();
					if(_trackedSession != null)
					{
						_trackedSession.VolumeChanged += OnTrackedSessionVolumeChanged;
						_trackedSession.MuteStateChanged += OnTrackedSessionStateChanged;
					}
				}
			}
			endedSession.Dispose();
		});
	}

	//called from the thread pool (Process.Exited) and the UI thread (last session ended): raise Exited only once
	private void InvokeExited()
	{
		if(System.Threading.Interlocked.Exchange(ref _exitedInvoked, 1) == 1)
			return;
		Exited?.Invoke(this);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private bool _disposed;

	public void Dispose()
	{
		if(_disposed)
			return;
		_disposed = true;
		//stop reacting to presses; the hotkeys themselves are released by the view models (ResetVolumeHotkeys), not here
		_hotkeys.HotkeyPressed -= OnHotkeyPressed;
		StateNotificationMediator?.NotifyOfDisposing(this);

		//stops watching for the exit and closes the handle, so the app no longer keeps this model alive
		if(_process != null)
		{
			_process.Exited -= OnProcessExited;
			_process.Dispose();
		}
		_exitCheck?.Dispose();

		lock(_sessionsLock)
		{
			if(_trackedSession != null)
			{
				_trackedSession.VolumeChanged -= OnTrackedSessionVolumeChanged;
				_trackedSession.MuteStateChanged -= OnTrackedSessionStateChanged;
				_trackedSession = null;
			}
			foreach(AudioSessionModel session in Sessions)
			{
				session.ActivityChanged -= OnSessionActivityChanged;
				session.Dispose();
			}
		}

		Exited = null;
		ActivityChanged = null;
		DeviceReconfigured = null;
	}
}

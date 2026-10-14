using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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

	public event Action<AudioProcessModel> Exited;

	/// <summary>
	/// A session of the process was disconnected because the device's format changed, see <see cref="AudioSessionModel.EndedByFormatChange"/>
	/// </summary>
	internal event Action<AudioProcessModel> DeviceReconfigured;

	public ObservableCollection<AudioSessionModel> Sessions { get; }

	public readonly string RawProcessName = string.Empty;
	private string name;
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

	private string _id;
	public string Id => _id ??= _processId.ToString(CultureInfo.InvariantCulture);

	private readonly uint _processId;
	public uint ProcessId => _processId;

	public string FilePath { get; }

	public Guid GroupingParam { get; private set; } = Guid.Empty;

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
	public int Volume
	{
		get => _volume;
		set
		{
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

	private ImageSource _iconSource;
	public ImageSource IconSource
	{
		get => _iconSource;
		set
		{
			_iconSource = value;
			OnPropertyChanged();
		}
	}

	public bool AnyHotkeyRegistered { get; private set; }

	/// <summary>
	/// Session of the process which is used as reference to current process state i.e. its current volume and mute state 
	/// </summary>
	private AudioSessionModel _trackedSession;

	public IAudioProcessStateMediator StateNotificationMediator { get; private set; }

	private HotKey _volumeUp;
	private HotKey _volumeDown;

	public ICommand MuteCommand { get; set; }

	private object _sessionsLock = new object();

	private static Dispatcher _dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	private readonly IHotkeyService _hotkeys;

	private readonly Process _process;

	/// <param name="iconSource">A frozen image, shared with the other models of the same app (see <see cref="IconHelper.GetIconSource"/>)</param>
	public AudioProcessModel(int volume, bool isMuted, string name, uint processId, string filePath, ImageSource iconSource, Process proc, IHotkeyService hotkeys, IAudioProcessStateMediator stateNotificationMediator = null)
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
			proc.Dispose();
		}

		Sessions = new ObservableCollection<AudioSessionModel>();

		MuteCommand = new ActionCommand(() => IsMuted = !IsMuted);
		StateNotificationMediator = stateNotificationMediator;
	}

	public void AddSession(AudioSessionModel newSession)
	{
		lock(_sessionsLock)
		{
			if(!Sessions.Contains(newSession))
			{
				newSession.SessionEnded += OnSessionEnded;
				newSession.Deactivated += OnSessionDeactivated;
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
			}
		}
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

	public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown) => SetVolumeHotkeys(volUp, volDown, out _);

	/// <param name="failed">The hotkey the system refused, null otherwise</param>
	public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown, out HotKey failed)
	{
		failed = null;
		try
		{
			if(volUp != null && volDown != null && _hotkeys.RegisterHotkeysPair(volUp, volDown, out failed))
			{
				_volumeUp = volUp;
				_volumeDown = volDown;
				_hotkeys.HotkeyPressed += OnHotkeyPressed;
				AnyHotkeyRegistered = true;
				return true;
			}
		}
		catch { }
		return false;
	}

	public void ResetVolumeHotkeys()
	{
		UnregisterHotkeys();
	}

	private void UnregisterHotkeys()
	{
		if(_volumeUp != null && _volumeDown != null)
		{
			try
			{
				_hotkeys.UnregisterHotkeysPair(_volumeUp, _volumeDown);
			}
			catch { }
		}
		_hotkeys.HotkeyPressed -= OnHotkeyPressed;
		AnyHotkeyRegistered = false;
		_volumeUp = _volumeDown = null;
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
		if(hotkey.Equals(_volumeUp))
			SetVolume(Volume + _hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
		else if(hotkey.Equals(_volumeDown))
			SetVolume(Volume - _hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
	}

	private int _exitedInvoked;

	private void OnProcessExited(object sender, EventArgs e)
	{
		if(sender is Process process)
			process.Exited -= OnProcessExited;
		InvokeExited();
	}

	private static readonly TimeSpan ExitCheckInterval = TimeSpan.FromSeconds(5);
	private System.Threading.Timer _exitCheck;

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
		string path = System.IO.Path.IsPathFullyQualified(FilePath) ? FilePath : null;
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
			if(_trackedSession == endedSession)
			{
				_trackedSession.VolumeChanged -= OnTrackedSessionVolumeChanged;
				_trackedSession.MuteStateChanged -= OnTrackedSessionStateChanged;

				lock(_sessionsLock)
				{
					_trackedSession = Sessions.FirstOrDefault();
				}
				if(_trackedSession != null)
				{
					_trackedSession.VolumeChanged += OnTrackedSessionVolumeChanged;
					_trackedSession.MuteStateChanged += OnTrackedSessionStateChanged;
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

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
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

		if(_trackedSession != null)
		{
			_trackedSession.VolumeChanged -= OnTrackedSessionVolumeChanged;
			_trackedSession.MuteStateChanged -= OnTrackedSessionStateChanged;
		}

		lock(_sessionsLock)
		{
			foreach(AudioSessionModel session in Sessions)
				session.Dispose();
		}

		Exited = null;
		DeviceReconfigured = null;
	}
}

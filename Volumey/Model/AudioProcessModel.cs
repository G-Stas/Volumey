using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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

public class AudioProcessModel : IManagedMasterAudioSession
{
	private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(AudioProcessModel));

	public event Action<AudioProcessModel> Exited;

	/// <summary>
	/// A session of the process was disconnected because the device's format changed, see <see cref="AudioSessionModel.EndedByFormatChange"/>
	/// </summary>
	internal event Action<AudioProcessModel> DeviceReconfigured;

	public ObservableCollection<AudioSessionModel> Sessions { get; }

	public readonly string RawProcessName = string.Empty;
	public string Name { get; set; }

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
				lock(_sessionsLock)
				{
					foreach(AudioSessionModel session in Sessions)
						session.IsMuted = value;
				}
				_isMuted = value;
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

	private Icon _icon;
	public Icon Icon
	{
		get => _icon;
		set
		{
			_icon = value;
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
	// private HotKey _muteKey;

	public ICommand MuteCommand { get; set; }

	// private bool _muteKeyRegistered;
	private object _sessionsLock = new object();

	private static Dispatcher _dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	private readonly IHotkeyService _hotkeys;

	public AudioProcessModel(int volume, bool isMuted, string name, uint processId, string filePath, Icon icon, Process proc, IHotkeyService hotkeys, IAudioProcessStateMediator stateNotificationMediator = null)
	{
		_hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
		_volume = volume;
		_isMuted = isMuted;
		_icon = icon;
		_processId = processId;
		Name = name;
		FilePath = filePath;

		try { _iconSource = icon.GetAsImageSource(); }
		catch { }

		//Might throw Access Denied if its a system process
		try
		{
			if(proc != null)
			{
				RawProcessName = proc.ProcessName;
				proc.EnableRaisingEvents = true;
				proc.Exited += OnProcessExited;
			}
		}
		catch { }

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

	private void OnTrackedSessionStateChanged(bool muteState, Guid context)
	{
		_isMuted = muteState;
		OnPropertyChanged(nameof(IsMuted));

		if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
		{
			if(!context.Equals(GuidValue.Internal.VolumeGUID))
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}
	}

	private void OnTrackedSessionVolumeChanged(int volume, Guid context)
	{
		_volume = volume;
		OnPropertyChanged(nameof(Volume));

		if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
		{
			if(!context.Equals(GuidValue.Internal.VolumeGUID))
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}
	}

	public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
	{
		try
		{
			if(volUp != null && volDown != null && _hotkeys.RegisterHotkeysPair(volUp, volDown))
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

	public bool SetMuteHotkey(HotKey hotkey)
	{
		// try
		// {
		// 	if(hotkey != null && _hotkeys.RegisterHotkey(hotkey))
		// 	{
		// 		this._muteKey = hotkey;
		// 		_hotkeys.HotkeyPressed += OnHotkeyPressed;
		// 		this._muteKeyRegistered = true;
		// 		HotkeysRegistered = true;
		// 		return true;
		// 	}
		// }
		// catch { }
		return false;
	}

	public void ResetMuteHotkey()
	{
		// if(this._muteKey != null)
		// 	_hotkeys.UnregisterHotkey(this._muteKey);
		// if(!this._muteKeyRegistered)
		// 	_hotkeys.HotkeyPressed -= OnHotkeyPressed;
		// this._muteKeyRegistered = false;
		// this._muteKey = null;
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

	private void OnSessionEnded(AudioSessionModel endedSession)
	{
		if(endedSession != null)
			ProcessEndedSession(endedSession).Forget(Logger, $"Failed to remove an ended session of [{Name}]");
	}

	private async Task ProcessEndedSession(AudioSessionModel endedSession)
	{
		endedSession.SessionEnded -= OnSessionEnded;
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

		//the model owns its icon (see IconHelper); Dispose frees the handle
		_icon?.Dispose();

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

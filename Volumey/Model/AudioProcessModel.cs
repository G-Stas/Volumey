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

namespace Volumey.Model
{
	public class AudioProcessModel : IManagedMasterAudioSession
	{
		public event Action<AudioProcessModel> Exited;

		public ObservableCollection<AudioSessionModel> Sessions { get; }

		public readonly string RawProcessName = string.Empty;
		public string Name { get; set; }

		private string _id;
		public string Id => _id ??= this._processId.ToString(CultureInfo.InvariantCulture);

		private readonly uint _processId;
		public uint ProcessId => this._processId;

		public string FilePath { get; }

		public Guid GroupingParam { get; private set; } = Guid.Empty;

		private bool _isMuted;
		public bool IsMuted
		{
			get => this._isMuted;
			set
			{
				if(this._isMuted != value)
				{
					lock(_sessionsLock)
					{
						foreach(AudioSessionModel session in Sessions)
							session.IsMuted = value;
					}
					this._isMuted = value;
					OnPropertyChanged();
				}
			}
		}

		private int _volume;
		public int Volume
		{
			get => this._volume;
			set
			{
				if (this._volume != value)
				{
					if (value < 0)
						this._volume = 0;
					else if (value > 100)
						this._volume = 100;
					else
						this._volume = value;

					lock(_sessionsLock)
					{
						foreach(AudioSessionModel session in Sessions)
							session.Volume = this._volume;
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

		public AudioProcessModel(int volume, bool isMuted, string name, uint processId, string filePath, Icon icon, Process proc, IAudioProcessStateMediator stateNotificationMediator = null)
		{
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

			MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);
			StateNotificationMediator = stateNotificationMediator;
		}

		public void AddSession(AudioSessionModel newSession)
		{
			lock(_sessionsLock)
			{
				if(!Sessions.Contains(newSession))
				{
					newSession.SessionEnded += OnSessionEnded;
					this.Sessions.Add(newSession);
					newSession.IconSource = this.IconSource;
					if(Sessions.Count == 1)
					{
						this.Volume = newSession.Volume;
						this.IsMuted = newSession.IsMuted;
						this.GroupingParam = newSession.GroupingParam;
						_trackedSession = newSession;
						_trackedSession.VolumeChanged += OnTrackedSessionVolumeChanged;
						_trackedSession.MuteStateChanged += OnTrackedSessionStateChanged;
					}
					else
					{
						newSession.Volume = this.Volume;
						newSession.IsMuted = this.IsMuted;
					}
				}
			}
		}

		private void OnTrackedSessionStateChanged(bool muteState, Guid context)
		{
			this._isMuted = muteState;
			OnPropertyChanged(nameof(IsMuted));

			if(SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges)
			{
				if(!context.Equals(GuidValue.Internal.VolumeGUID))
					StateNotificationMediator?.NotifyAudioStateChange(this);
			}
		}

		private void OnTrackedSessionVolumeChanged(int volume, Guid context)
		{
			this._volume = volume;
			OnPropertyChanged(nameof(Volume));
			
			if(SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges)
			{
				if(!context.Equals(GuidValue.Internal.VolumeGUID))
					StateNotificationMediator?.NotifyAudioStateChange(this);
			}
		}

		public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
		{
			try
			{
				if(volUp != null && volDown != null && HotkeysControl.RegisterHotkeysPair(volUp, volDown))
				{
					this._volumeUp = volUp;
					this._volumeDown = volDown;
					HotkeysControl.HotkeyPressed += OnHotkeyPressed;
					AnyHotkeyRegistered = true;
					return true;
				}
			}
			catch { }
			return false;
		}

		public void ResetVolumeHotkeys()
		{
			this.UnregisterHotkeys();
		}

		public bool SetMuteHotkey(HotKey hotkey)
		{
			// try
			// {
			// 	if(hotkey != null && HotkeysControl.RegisterHotkey(hotkey))
			// 	{
			// 		this._muteKey = hotkey;
			// 		HotkeysControl.HotkeyPressed += OnHotkeyPressed;
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
			// 	HotkeysControl.UnregisterHotkey(this._muteKey);
			// if(!this._muteKeyRegistered)
			// 	HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
			// this._muteKeyRegistered = false;
			// this._muteKey = null;
		}

		private void UnregisterHotkeys()
		{
			if(this._volumeUp != null && this._volumeDown != null)
			{
				try
				{
					HotkeysControl.UnregisterHotkeysPair(this._volumeUp, this._volumeDown);
				}
				catch { }
			}
			HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
			AnyHotkeyRegistered = false;
			this._volumeUp = this._volumeDown = null;
		}

		public void SetVolume(int newValue, bool notify, ref Guid guid)
		{
			this.Volume = newValue;
			if(notify)
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}

		public void SetMute(bool newState, bool notify, ref Guid context)
		{
			this.IsMuted = newState;
			if(notify)
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}

		public void SetStateMediator(IAudioProcessStateMediator mediator)
		{
			StateNotificationMediator = mediator;
		}

		public void ResetStateMediator(bool force = false)
		{
			if(!force && SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges)
				return;
			StateNotificationMediator = null;
		}

		private void OnHotkeyPressed(HotKey hotkey)
		{
			if(hotkey.Equals(this._volumeUp))
				this.SetVolume(this.Volume + HotkeysControl.VolumeStep, notify: true, ref GuidValue.Internal.Empty);
			else if(hotkey.Equals(this._volumeDown))
				this.SetVolume(this.Volume - HotkeysControl.VolumeStep, notify: true, ref GuidValue.Internal.Empty);
		}

		private bool _exitedInvoked;

		private void OnProcessExited(object? sender, EventArgs e)
		{
			if(sender is Process process)
				process.Exited -= OnProcessExited;
			InvokeExited();
		}

		private async void OnSessionEnded(AudioSessionModel endedSession)
		{
			if(endedSession != null)
				await ProcessEndedSession(endedSession);
		}

		private async Task ProcessEndedSession(AudioSessionModel endedSession)
		{
			endedSession.SessionEnded -= OnSessionEnded;
			await _dispatcher.InvokeAsync(() =>
			{
				lock(_sessionsLock)
				{
					this.Sessions.Remove(endedSession);
					if(this.Sessions.Count == 0)
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
						_trackedSession = this.Sessions.FirstOrDefault();
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

		private void InvokeExited()
		{
			if(_exitedInvoked)
				return;
			_exitedInvoked = true;
			this.Exited?.Invoke(this);
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
			this.StateNotificationMediator?.NotifyOfDisposing(this);
			
			if(this._icon != null)
			{
				NativeMethods.DestroyIcon(this._icon.Handle);
				this._icon.Dispose();
			}
			
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
		}
	}
}
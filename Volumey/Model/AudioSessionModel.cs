using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Model;

/// <summary>
/// Represents an audio session and the volume control of an application 
/// </summary>
public sealed class AudioSessionModel : IManagedAudioSession
{
	private string _name;
	public string Name
	{
		get => _name;
		set
		{
			_name = value;
			OnPropertyChanged();
		}
	}

	private readonly string _id;
	public string Id => _id;

	public Guid GroupingParam { get; }

	private readonly uint _processId;
	public uint ProcessId => _processId;

	public string FilePath { get; }

	private bool _isMuted;
	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if(_isMuted != value)
			{
				_isMuted = value;
				SetMute(value);
				MuteStateChanged?.Invoke(value, GuidValue.Internal.VolumeGUID);
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
			int clamped = Math.Clamp(value, 0, 100);
			if(_volume != clamped)
			{
				_volume = clamped;
				VolumeChanged?.Invoke(_volume, GuidValue.Internal.VolumeGUID);
				SetVolume(_volume, GuidValue.Internal.VolumeGUID);
				OnPropertyChanged();
			}
		}
	}

	public ImageSource IconSource { get; set; }

	public ICommand MuteCommand { get; set; }

	private readonly IAudioSessionStateNotifications sessionStateNotifications;
	private readonly IAudioSessionVolume sessionVolume;
	public event Action<AudioSessionModel> SessionEnded;
	public event Action<int, Guid> VolumeChanged;
	public event Action<bool, Guid> MuteStateChanged;

	private static ILog logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(AudioSessionModel));

	private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	public AudioSessionModel(bool isMuted, int volume, string id, uint processId, string name, string filePath, Guid groupingParam, IAudioSessionVolume aVolume,
		IAudioSessionStateNotifications sStateNotifications)
	{
		_isMuted = isMuted;
		_volume = volume;
		_id = id;
		_processId = processId;
		GroupingParam = groupingParam;
		FilePath = filePath;
		Name = name;
		sessionVolume = aVolume;
		sessionStateNotifications = sStateNotifications;
		sessionStateNotifications.VolumeChanged += OnVolumeChanged;
		sessionStateNotifications.SessionEnded += OnSessionEnded;
		sessionStateNotifications.NameChanged += OnNameChanged;
		sessionStateNotifications.Disconnected += OnDisconnected;

		MuteCommand = new ActionCommand(() => IsMuted = !IsMuted);
	}

	private void OnSessionEnded()
		=> SessionEnded?.Invoke(this);

	/// <summary>
	/// A change made outside the app (the Windows mixer, the app itself). It's taken over without writing it back to Windows,
	/// and the process hears about it once, with the new values. It used to hear the old values first (so the OSD showed them),
	/// and the property setters then wrote the change back to Windows.
	/// </summary>
	private void OnVolumeChanged(VolumeChangedEventArgs e)
	{
		if(_volume != e.NewVolume)
		{
			_volume = e.NewVolume;
			OnPropertyChanged(nameof(Volume));
			VolumeChanged?.Invoke(_volume, Guid.Empty);
		}

		if(_isMuted != e.IsMuted)
		{
			_isMuted = e.IsMuted;
			OnPropertyChanged(nameof(IsMuted));
			MuteStateChanged?.Invoke(_isMuted, Guid.Empty);
		}
	}

	//raised on a COM thread, which must not wait for the UI thread
	private void OnNameChanged(string newName)
		=> dispatcher.InvokeAsync(() => { Name = newName; });

	/// <summary>
	/// True if Windows disconnected the session because the device's format changed (also when spatial sound is turned on or off):
	/// the device's objects don't work anymore then
	/// </summary>
	internal bool EndedByFormatChange { get; private set; }

	private void OnDisconnected(AudioSessionDisconnectReason reason)
	{
		Logger.Info($"Audio session disconnected: [{Name}], reason: {reason}");
		EndedByFormatChange = reason == AudioSessionDisconnectReason.FormatChanged;
		OnSessionEnded();
	}

	private void SetVolume(int newVol, Guid guid)
	{
		try
		{
			sessionVolume?.SetVolume(newVol, guid);
		}
		catch(Exception e)
		{
			LogStateException(e);
		}
	}

	private void SetMute(bool mute)
	{
		try { sessionVolume?.SetMute(mute); }
		catch(Exception e)
		{
			LogStateException(e);
		}
	}

	private void LogStateException(Exception e)
	{
		Logger.Info($"Failed to change state of the audio session, HRESULT 0x{e.HResult:X8}", e);
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
		sessionStateNotifications.VolumeChanged -= OnVolumeChanged;
		sessionStateNotifications.SessionEnded -= OnSessionEnded;
		sessionStateNotifications.NameChanged -= OnNameChanged;
		sessionStateNotifications.Disconnected -= OnDisconnected;
		sessionStateNotifications.Dispose();
	}
}

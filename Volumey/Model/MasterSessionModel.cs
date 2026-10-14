using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model;

/// <summary>
/// Represents data and the volume control of an output audio device
/// </summary>
public sealed class MasterSessionModel : IManagedMasterAudioSession
{
	private string deviceFriendlyName;
	public string DeviceFriendlyName
	{
		get => deviceFriendlyName;
		set
		{
			deviceFriendlyName = value;
			OnPropertyChanged();
		}
	}

	private string deviceDesc;
	public string DeviceDesc
	{
		get => deviceDesc;
		set
		{
			deviceDesc = value;
			OnPropertyChanged();
		}
	}

	public string Name
	{
		get => deviceFriendlyName;
		set
		{
			DeviceFriendlyName = value;
			OnPropertyChanged();
		}
	}

	private readonly string _id;
	public string Id => _id;

	//the endpoint volume doesn't belong to a process
	public uint ProcessId => 0;

	public Guid GroupingParam { get; } = Guid.Empty;

	public string FilePath { get; }

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
				SetVolume(_volume, GuidValue.Internal.VolumeGUID);
				OnPropertyChanged();
			}
		}
	}

	private bool _isMuted;
	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if(_isMuted != value)
			{
				_isMuted = value;
				SetMute(value, GuidValue.Internal.VolumeGUID);
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

	public ICommand MuteCommand { get; set; }

	private HotKey volumeUp;
	private HotKey volumeDown;
	private HotKey muteKey;

	private bool volumeHotkeysRegistered;
	private bool muteHotkeyRegistered;

	public bool AnyHotkeyRegistered => volumeHotkeysRegistered || muteHotkeyRegistered;

	public IAudioProcessStateMediator StateNotificationMediator { get; private set; }

	private readonly IHotkeyService hotkeys;
	private IAudioSessionVolume masterVolume { get; }
	private IMasterVolumeNotificationHandler notificationHandler { get; }
	private static ILog logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(MasterSessionModel));

	public MasterSessionModel(string friendlyName, string desc,
		int volume, bool muteState, string id, Icon icon,
		IAudioSessionVolume mVolume, IMasterVolumeNotificationHandler nHandler, IHotkeyService hotkeys)
	{
		this.hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
		_volume = volume;
		_isMuted = muteState;
		_id = id;
		_icon = icon;
		_iconSource = icon.GetAsImageSource();
		Name = friendlyName;
		FilePath = Name;
		DeviceDesc = desc;
		masterVolume = mVolume;
		notificationHandler = nHandler;
		notificationHandler.VolumeChanged += OnVolumeChanged;

		MuteCommand = new ActionCommand(() => IsMuted = !IsMuted);
	}

	public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown) => SetVolumeHotkeys(volUp, volDown, out _);

	/// <param name="failed">The hotkey the system refused, null otherwise</param>
	public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown, out HotKey failed)
	{
		failed = null;
		try
		{
			if(volUp != null && volDown != null && hotkeys.RegisterHotkeysPair(volUp, volDown, out failed))
			{
				volumeUp = volUp;
				volumeDown = volDown;
				hotkeys.HotkeyPressed += OnHotkeyPressed;
				volumeHotkeysRegistered = true;
				return true;
			}
		}
		catch { }
		return false;
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

	public void ResetVolumeHotkeys()
	{
		if(volumeUp != null && volumeDown != null)
			hotkeys.UnregisterHotkeysPair(volumeUp, volumeDown);
		if(!muteHotkeyRegistered)
			hotkeys.HotkeyPressed -= OnHotkeyPressed;
		volumeHotkeysRegistered = false;
		volumeUp = volumeDown = null;
	}

	public bool SetMuteHotkey(HotKey key)
	{
		try
		{
			if(key != null && hotkeys.RegisterHotkey(key))
			{
				muteKey = key;
				hotkeys.HotkeyPressed += OnHotkeyPressed;
				muteHotkeyRegistered = true;
				return true;
			}
		}
		catch { }
		return false;
	}

	public void ResetMuteHotkey()
	{
		if(muteKey != null)
			hotkeys.UnregisterHotkey(muteKey);
		if(!volumeHotkeysRegistered)
			hotkeys.HotkeyPressed -= OnHotkeyPressed;
		muteHotkeyRegistered = false;
		muteKey = null;
	}

	private void OnHotkeyPressed(HotKey hotkey)
	{
		if(volumeHotkeysRegistered)
		{
			if(hotkey.Equals(volumeUp))
			{
				if(SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance && IsMuted)
					SetMute(false, notify: false, GuidValue.Internal.Empty);
				SetVolume(Volume + hotkeys.VolumeStep, notify: true, GuidValue.Internal.Empty);
			}
			else if(hotkey.Equals(volumeDown))
			{
				int newValue = Volume - hotkeys.VolumeStep;
				if(SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance && newValue <= 0)
				{
					SetMute(true, notify: true, GuidValue.Internal.Empty);
				}
				else
					SetVolume(newValue, notify: true, GuidValue.Internal.Empty);
			}
		}

		if(muteHotkeyRegistered && hotkey.Equals(muteKey))
		{
			SetMute(!_isMuted, notify: true, GuidValue.Internal.Empty);
		}
	}

	/// <summary>
	/// Writes the volume (hotkeys, volume sync) and takes it over at once: the change doesn't wait for Windows' notification, which may come
	/// late or (with Volumey's own event context) not at all
	/// </summary>
	public void SetVolume(int newVol, bool notify, Guid guid)
	{
		newVol = Math.Clamp(newVol, 0, 100);
		if(SetVolume(newVol, guid) && _volume != newVol)
		{
			_volume = newVol;
			OnPropertyChanged(nameof(Volume));
		}
		if(notify)
			StateNotificationMediator?.NotifyAudioStateChange(this);
	}

	/// <summary>
	/// Writes the mute state (hotkeys) and takes it over at once. The mute hotkey toggles <see cref="IsMuted"/>, so it must be current:
	/// Windows' notification may not come (a write with Volumey's own context is skipped), and the hotkey would mute but never unmute
	/// </summary>
	public void SetMute(bool muteState, bool notify, Guid context)
	{
		if(SetMute(muteState, context) && _isMuted != muteState)
		{
			_isMuted = muteState;
			OnPropertyChanged(nameof(IsMuted));
		}
		if(notify)
			StateNotificationMediator?.NotifyAudioStateChange(this);
	}


	//only write (true if they did); the public overloads notify (when asked to). Notifying here as well would show the OSD twice for every hotkey press
	private bool SetVolume(int newVol, Guid context)
	{
		if(masterVolume == null)
			return false;
		try
		{
			masterVolume.SetVolume(newVol, context);
			return true;
		}
		catch(Exception e)
		{
			LogStateException(e);
			return false;
		}
	}

	private bool SetMute(bool muteState, Guid context)
	{
		if(masterVolume == null)
			return false;
		try
		{
			masterVolume.SetMute(muteState, context);
			return true;
		}
		catch(Exception e)
		{
			LogStateException(e);
			return false;
		}
	}

	private void OnVolumeChanged(VolumeChangedEventArgs e)
	{
		if(SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges)
		{
			if(Volume != e.NewVolume || IsMuted != e.IsMuted)
				StateNotificationMediator?.NotifyAudioStateChange(this);
		}

		Volume = e.NewVolume;
		IsMuted = e.IsMuted;
	}

	private void LogStateException(Exception e)
	{
		Logger.Info($"Failed to change state of the master session, HRESULT 0x{e.HResult:X8}", e);
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private bool disposed;

	public void Dispose()
	{
		if(disposed)
			return;
		disposed = true;
		//stop reacting to presses (and stop being kept alive by the hotkey service); the hotkeys themselves are released by
		//the view models through ResetVolumeHotkeys/ResetMuteHotkey, often after this, so they aren't unregistered here
		hotkeys.HotkeyPressed -= OnHotkeyPressed;
		StateNotificationMediator?.NotifyOfDisposing(this);
		notificationHandler.VolumeChanged -= OnVolumeChanged;
		notificationHandler.Dispose();
		_icon?.Dispose();
	}
}

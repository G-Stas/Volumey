using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel;

sealed partial class SettingsViewModel : ObservableObject
{
	public DeviceVolumeHotkeysViewModel DeviceVolumeHotkeysViewModel { get; }
	public AppsHotkeysViewModel HotkeysViewModel { get; }
	public OpenHotkeyViewModel OpenHotkeyViewModel { get; }
	public VolumeLimitViewModel VolumeLimitViewModel { get; }
	public AppVolumeSyncViewModel AppVolumeSyncViewModel { get; }
	public DefaultDeviceHotkeysViewModel DefaultDeviceHotkeysViewModel { get; }
	public NotificationViewModel NotificationsViewModel { get; }
	public ForegroundWindowVolumeViewModel ForegroundWindowVolumeViewModel { get; }
	public SystemHotkeysViewModel SystemHotkeysViewModel { get; }
	public LangSettings LangSettings { get; }

	public AboutViewModel About { get; }

	private IntPtr windowHandle;

	private bool blockHotkeysInSystem;
	/// <summary>
	/// True: hotkeys block their keys system-wide (RegisterHotKey). False: a keyboard hook only listens.
	/// Changing it moves the registered hotkeys to the other hotkey manager.
	/// </summary>
	public bool BlockHotkeysInSystem
	{
		get => blockHotkeysInSystem;
		set
		{
			if(blockHotkeysInSystem == value)
				return;
			blockHotkeysInSystem = value;
			SettingsProvider.Settings.Hotkeys.BlockInSystem = value;
			SetupHotkeyManager();
			OnPropertyChanged();
		}
	}
	private readonly IHotkeyService hotkeys;

	internal SettingsViewModel(IHotkeyService hotkeys, LangSettings langSettings, AppsHotkeysViewModel appsHotkeys,
		DeviceVolumeHotkeysViewModel deviceVolumeHotkeys, OpenHotkeyViewModel openHotkey, VolumeLimitViewModel volumeLimit,
		AppVolumeSyncViewModel appVolumeSync, DefaultDeviceHotkeysViewModel defaultDeviceHotkeys,
		ForegroundWindowVolumeViewModel foregroundWindowVolume, NotificationViewModel notifications, SystemHotkeysViewModel systemHotkeys,
		AboutViewModel about)
	{
		this.hotkeys = hotkeys;
		LangSettings = langSettings;
		HotkeysViewModel = appsHotkeys;
		DeviceVolumeHotkeysViewModel = deviceVolumeHotkeys;
		OpenHotkeyViewModel = openHotkey;
		VolumeLimitViewModel = volumeLimit;
		AppVolumeSyncViewModel = appVolumeSync;
		DefaultDeviceHotkeysViewModel = defaultDeviceHotkeys;
		ForegroundWindowVolumeViewModel = foregroundWindowVolume;
		NotificationsViewModel = notifications;
		SystemHotkeysViewModel = systemHotkeys;
		About = about;

		blockHotkeysInSystem = SettingsProvider.Settings.Hotkeys.BlockInSystem;
	}

	/// <summary>
	/// Receives window handle from main view which is required for creating one of the hotkey managers.
	/// </summary>
	/// <param name="handle">Window handle</param>
	public void SetWindowHandle(IntPtr handle)
	{
		if(windowHandle != IntPtr.Zero)
			return;
		windowHandle = handle;
		SetupHotkeyManager();
	}

	private Action<int> HotkeyManagerMessageHandler;

	public Action<int> GetHotkeyMessageHandler()
		=> WindowsHotkeyMessageHandlerBridge;

	/// <summary>
	/// Passes window hotkey messages from main view to hotkey manager.
	/// </summary>
	/// <param name="lParam">Contains pressed hotkey data.</param>
	private void WindowsHotkeyMessageHandlerBridge(int lParam)
	{
		HotkeyManagerMessageHandler?.Invoke(lParam);
	}

	private void SetupHotkeyManager()
	{
		//the blocking manager needs the window handle, which the main view passes on startup
		if(windowHandle == IntPtr.Zero)
			return;
		IHotkeyManager manager;
		if(blockHotkeysInSystem)
		{
			var hotkeyManager = new HotkeyManager(windowHandle);
			HotkeyManagerMessageHandler = hotkeyManager.GetMessageHandler();
			manager = hotkeyManager;
		}
		else
		{
			HotkeyManagerMessageHandler = null;
			manager = new SharpHookHotkeyManager();
		}
		hotkeys.SetHotkeyManager(manager);
	}
}

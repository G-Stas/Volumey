using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

public partial class DefaultDeviceHotkeysViewModel : HotkeyViewModel
{
	private OutputDeviceModel _selected;
	public OutputDeviceModel SelectedDevice
	{
		get => _selected;
		set
		{
			_selected = value;
			OnPropertyChanged();
			ShowRegisteredHotkey(value);
		}
	}

	//true while HotKey shows the registered hotkey of the selected device rather than one the user typed
	private bool hotkeyShowsRegistration;

	[ObservableProperty]
	public partial KeyValuePair<HotKey, DeviceInfo>? SelectedRegDev { get; set; }

	public ObservableConcurrentDictionary<HotKey, DeviceInfo> RegisteredDevices { get; }

	private HotKey _hotkey;
	public HotKey HotKey
	{
		get => _hotkey;
		set
		{
			_hotkey = value;
			hotkeyShowsRegistration = false;
			OnPropertyChanged();
		}
	}


	public IDeviceProvider DeviceProvider { get; }

	internal DefaultDeviceHotkeysViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys) : base(hotkeys)
	{
		DeviceProvider = deviceProvider;

		RegisteredDevices = LoadRegisteredDevices();
		if(RegisteredDevices.Keys.Count > 0)
		{
			RegisterWhenHotkeysActive(RegisterLoadedHotkeys);

			//Update registered devices names once on startup
			UpdateNamesOrIDs();

			//show a registration in the list, so the page doesn't look as if nothing was registered (#18)
			SelectedRegDev = RegisteredDevices.First();
		}
	}

	/// <summary>
	/// Shows the hotkey registered for the selected device and selects its registration in the list (#18)
	/// </summary>
	private void ShowRegisteredHotkey(OutputDeviceModel device)
	{
		KeyValuePair<HotKey, DeviceInfo>? registration = null;
		if(device != null)
		{
			foreach(var registered in RegisteredDevices)
			{
				if(device.CompareId(registered.Value.ID))
				{
					registration = registered;
					break;
				}
			}
		}

		if(registration is { } reg)
		{
			HotKey = reg.Key;
			hotkeyShowsRegistration = true;
			SelectedRegDev = reg;
		}
		else if(hotkeyShowsRegistration)
			//don't offer another device's hotkey for this one, but keep a hotkey the user typed
			HotKey = null;
	}

	private void RegisterLoadedHotkeys()
	{
		hotkeys.HotkeyPressed += OnHotkeyPressed;
		//shows an error instead of silently doing nothing; the hotkeys stay saved and are tried again on the next start
		bool allRegistered = true;
		foreach(var hotkeyDeviceInfoPair in RegisteredDevices)
			allRegistered &= hotkeys.RegisterHotkey(hotkeyDeviceInfoPair.Key);
		if(!allRegistered)
			SetErrorMessage(ErrorMessageType.OpenReg);
	}

	/// <summary>
	/// Check if registered devices have changed their names or IDs to update these values in settings,
	/// since IDs are critical for setting default device in system
	/// </summary>
	private void UpdateNamesOrIDs()
	{
		try
		{
			foreach(var activeDevice in DeviceProvider.ActiveDevices)
			{
				foreach(var registeredDevice in RegisteredDevices)
				{
					string id = registeredDevice.Value.ID;
					string name = registeredDevice.Value.Name;

					if(activeDevice.CompareId(id) || activeDevice.Master.Name.Equals(name))
					{
						var actualFriendlyName = activeDevice.Master.DeviceFriendlyName;
						var friendlyName = registeredDevice.Value.Name;
						if(!actualFriendlyName.Equals(friendlyName) || !activeDevice.CompareId(id))
						{
							DeviceInfo deviceInfo = new DeviceInfo(activeDevice.Id, actualFriendlyName);
							HotKey hotkey = registeredDevice.Key;
							RegisteredDevices[hotkey] = deviceInfo;
							SaveRegisteredDevices();
						}
					}
				}
			}
		}
		catch { }
	}

	[RelayCommand]
	private void AddDevice()
	{
		if(RegisteredDevices.Values.Any(deviceInfo => deviceInfo.ID.Equals(SelectedDevice.Id)))
			return;

		if(RegisteredDevices.ContainsKey(HotKey))
		{
			SetErrorMessage(ErrorMessageType.HotkeyExists);
			return;
		}

		if(!IsValid(hotkeys.HotkeyIsValid(HotKey)))
			return;

		SetErrorMessage(ErrorMessageType.None);

		if(RegisteredDevices.Keys.Count == 0)
			hotkeys.HotkeyPressed += OnHotkeyPressed;

		if(!hotkeys.RegisterHotkey(HotKey))
		{
			SetErrorMessage(ErrorMessageType.OpenReg);
			return;
		}

		DeviceInfo deviceInfo = new DeviceInfo(SelectedDevice.Id, SelectedDevice.Master.DeviceFriendlyName);
		var hotkey = HotKey;

		RegisteredDevices.Add(hotkey, deviceInfo);
		SelectedDevice = null;
		HotKey = null;
		SelectedRegDev = new KeyValuePair<HotKey, DeviceInfo>(hotkey, deviceInfo);
		Logger.Info($"Registered default device hotkey: {hotkey}, count: {RegisteredDevices.Keys.Count}");
		SaveRegisteredDevices();
	}

	[RelayCommand]
	private void RemoveDevice()
	{
		if(SelectedRegDev == null || !SelectedRegDev.HasValue)
			return;

		HotKey hotkey = SelectedRegDev.Value.Key;

		hotkeys.UnregisterHotkey(hotkey);
		RegisteredDevices.Remove(hotkey);

		if(RegisteredDevices.Keys.Count == 0)
			hotkeys.HotkeyPressed -= OnHotkeyPressed;

		SelectedRegDev = RegisteredDevices.Count > 0 ? RegisteredDevices.First() : null;
		SetErrorMessage(ErrorMessageType.None);

		try
		{
			SaveRegisteredDevices();
		}
		catch { }
	}

	private static ObservableConcurrentDictionary<HotKey, DeviceInfo> LoadRegisteredDevices()
	{
		var devices = new ObservableConcurrentDictionary<HotKey, DeviceInfo>();
		foreach(var entry in SettingsProvider.Settings.Hotkeys.DefaultDevices)
		{
			if(entry.Hotkey != null)
				devices[entry.Hotkey] = new DeviceInfo(entry.DeviceId, entry.DeviceName);
		}
		return devices;
	}

	private void SaveRegisteredDevices() => SaveRegisteredDevices(SettingsProvider.Settings.Hotkeys);

	private void SaveRegisteredDevices(HotkeySettings hotkeySettings)
	{
		var entries = RegisteredDevices
						  .Select(d => new DeviceHotkeyEntry { Hotkey = d.Key, DeviceId = d.Value.ID, DeviceName = d.Value.Name })
						  .ToArray();
		hotkeySettings.DefaultDevices = entries;
	}

	private void OnHotkeyPressed(HotKey pressedHotkey)
	{
		if(RegisteredDevices.TryGetValue(pressedHotkey, out DeviceInfo deviceInfo))
			DeviceProvider.SetDefaultDevice(deviceInfo.ID);
	}
}

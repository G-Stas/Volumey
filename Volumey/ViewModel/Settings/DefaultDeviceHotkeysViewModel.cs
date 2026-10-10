using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The Devices page's device list: every connected output device, plus the ones with a saved "make default" hotkey that aren't connected.
/// Each device can have one hotkey that makes it the default device (saved by ID and name, so it still works after a rename).
/// </summary>
public partial class DefaultDeviceHotkeysViewModel : HotkeyViewModel
{
	/// <summary>
	/// Connected devices first (in Windows' order), then the saved ones that aren't connected
	/// </summary>
	public ObservableCollection<DeviceEntry> Devices { get; } = new ObservableCollection<DeviceEntry>();

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SetAsDefaultCommand))]
	public partial DeviceEntry SelectedDevice { get; set; }

	public IDeviceProvider DeviceProvider { get; }

	internal DefaultDeviceHotkeysViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys) : base(hotkeys)
	{
		DeviceProvider = deviceProvider;

		foreach(var saved in SettingsProvider.Settings.Hotkeys.DefaultDevices)
		{
			if(saved.Hotkey == null || Devices.Any(d => d.Hotkey.Equals(saved.Hotkey)))
				continue;
			var entry = new DeviceEntry(this, saved.DeviceId, saved.DeviceName) { Hotkey = saved.Hotkey };
			entry.SetRegistered(true);
			Devices.Add(entry);
		}
		bool anySaved = Devices.Count > 0;

		foreach(var device in deviceProvider.ActiveDevices)
			Connect(device);
		UpdateDefault();
		//the names or IDs of saved devices may have changed (they're matched by either)
		if(anySaved)
			Save();

		deviceProvider.ActiveDevices.CollectionChanged += OnActiveDevicesChanged;
		deviceProvider.DefaultDeviceChanged += _ => UpdateDefault();
		ErrorDictionary.LanguageChanged += () =>
		{
			foreach(var entry in Devices)
			{
				entry.SetError(entry.ErrorType);
				entry.SetFormatError(entry.FormatErrorType);
			}
		};

		SelectedDevice = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();

		if(anySaved)
			RegisterWhenHotkeysActive(RegisterLoadedHotkeys);
	}

	private void RegisterLoadedHotkeys()
	{
		hotkeys.HotkeyPressed += OnHotkeyPressed;
		//a hotkey that fails now shows an error instead of silently doing nothing; it stays saved and is tried again on the next start
		foreach(var entry in Devices.Where(d => d.HotkeyRegistered))
		{
			if(!hotkeys.RegisterHotkey(entry.Hotkey))
				entry.SetError(ErrorMessageType.OpenReg);
		}
	}

	/// <summary>
	/// A device is connected: its saved entry (same ID, or same name for a device whose ID changed) gets the model, otherwise it gets a new entry
	/// </summary>
	private void Connect(OutputDeviceModel device)
	{
		var entry = Devices.FirstOrDefault(d => device.CompareId(d.Id))
					?? Devices.FirstOrDefault(d => d.Model == null && d.Name == device.Master.DeviceFriendlyName);
		if(entry == null)
		{
			entry = new DeviceEntry(this, device.Id, device.Master.DeviceFriendlyName);
			//connected devices stay before the ones that aren't
			int index = Devices.TakeWhile(d => d.IsConnected).Count();
			Devices.Insert(index, entry);
		}
		else
		{
			entry.Id = device.Id;
			entry.Name = device.Master.DeviceFriendlyName;
			if(!entry.IsConnected)
			{
				Devices.Move(Devices.IndexOf(entry), Devices.TakeWhile(d => d.IsConnected).Count());
			}
		}
		entry.Model = device;
	}

	/// <summary>
	/// A device went away: it stays listed as not connected if it has a hotkey, otherwise it's removed
	/// </summary>
	private void Disconnect(OutputDeviceModel device)
	{
		//a replaced device (#82) is connected again first, so its entry already has the new model
		var entry = Devices.FirstOrDefault(d => d.Model == device);
		if(entry == null)
			return;
		entry.Model = null;
		entry.IsDefault = false;
		if(entry.HotkeyRegistered)
		{
			Devices.Move(Devices.IndexOf(entry), Devices.Count - 1);
			return;
		}
		bool wasSelected = SelectedDevice == entry;
		Devices.Remove(entry);
		if(wasSelected)
			SelectedDevice = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();
	}

	private void OnActiveDevicesChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if(e.Action == NotifyCollectionChangedAction.Reset)
		{
			foreach(var entry in Devices.Where(d => d.IsConnected).ToList())
				Disconnect(entry.Model);
			foreach(var device in DeviceProvider.ActiveDevices)
				Connect(device);
		}
		else
		{
			foreach(OutputDeviceModel device in e.OldItems ?? new OutputDeviceModel[0])
				Disconnect(device);
			foreach(OutputDeviceModel device in e.NewItems ?? new OutputDeviceModel[0])
				Connect(device);
		}
		UpdateDefault();
		SelectedDevice ??= Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();
		SetAsDefaultCommand.NotifyCanExecuteChanged();
	}

	private void UpdateDefault()
	{
		var defaultDevice = DeviceProvider.DefaultDevice;
		foreach(var entry in Devices)
			entry.IsDefault = entry.Model != null && entry.Model == defaultDevice;
		SetAsDefaultCommand.NotifyCanExecuteChanged();
	}

	partial void OnSelectedDeviceChanged(DeviceEntry value) => LoadFormats(value);

	/// <summary>
	/// A device got a new model (plugged in, or rebuilt after a format change)
	/// </summary>
	internal void OnModelChanged(DeviceEntry entry)
	{
		if(entry == SelectedDevice)
			LoadFormats(entry);
	}

	/// <summary>
	/// Reads the device's supported formats off the UI thread (COM calls, one per candidate format)
	/// </summary>
	private void LoadFormats(DeviceEntry entry)
	{
		if(entry?.Model is not { } model || entry.CanChooseFormat)
			return;
		LoadFormatsAsync(entry, model).Forget(Logger, "Failed to read the device's formats");
	}

	private static async Task LoadFormatsAsync(DeviceEntry entry, OutputDeviceModel model)
	{
		var formats = await Task.Run(model.GetSupportedFormats);
		//unplugged or rebuilt meanwhile: the new model reads its own
		if(entry.Model != model)
			return;
		entry.SetSupportedFormats(formats ?? Array.Empty<DeviceFormatOption>());
	}

	/// <summary>
	/// The format picked in a device's list: Windows reconfigures the device, which rebuilds its model (and the list then shows the new format).
	/// If Windows refuses it, the device shows the error and the list goes back to the current format.
	/// </summary>
	internal void ChangeFormat(DeviceEntry entry, DeviceFormatOption format)
		=> ChangeFormatAsync(entry, format).Forget(Logger, "Failed to change the device format");

	private async Task ChangeFormatAsync(DeviceEntry entry, DeviceFormatOption format)
	{
		entry.IsChangingFormat = true;
		entry.SetFormatError(ErrorMessageType.None);
		bool changed = await DeviceProvider.SetDeviceFormatAsync(entry.Id, format);
		entry.IsChangingFormat = false;
		if(!changed)
		{
			entry.SetFormatError(ErrorMessageType.FormatRefused);
			entry.ShowCurrentFormat();
		}
	}

	private bool CanSetAsDefault() => SelectedDevice is { IsConnected: true, IsDefault: false };

	[RelayCommand(CanExecute = nameof(CanSetAsDefault))]
	private void SetAsDefault()
	{
		if(CanSetAsDefault())
			DeviceProvider.SetDefaultDevice(SelectedDevice.Id);
	}

	/// <summary>
	/// Turns a device's hotkey on; false (with the device's error set) if it can't be used
	/// </summary>
	internal bool TurnOn(DeviceEntry entry)
	{
		var hotkey = entry.Hotkey;
		var error = hotkey == null ? ErrorMessageType.Missing : hotkeys.HotkeyIsValid(hotkey);
		if(error == ErrorMessageType.None && !hotkeys.RegisterHotkey(hotkey))
			//the hotkey service logs why
			error = ErrorMessageType.OpenReg;
		entry.SetError(error);
		if(error != ErrorMessageType.None)
			return false;

		if(!Devices.Any(d => d != entry && d.HotkeyRegistered))
			hotkeys.HotkeyPressed += OnHotkeyPressed;
		Logger.Info($"Registered default device hotkey: [{hotkey}] for [{entry.Name}]");
		return true;
	}

	internal void TurnOff(DeviceEntry entry)
	{
		hotkeys.UnregisterHotkey(entry.Hotkey);
		entry.SetError(ErrorMessageType.None);
		if(!Devices.Any(d => d != entry && d.HotkeyRegistered))
			hotkeys.HotkeyPressed -= OnHotkeyPressed;
	}

	/// <summary>
	/// A device's hotkey was turned on or off (called after the entry stored its new state): saves, and removes a device
	/// that isn't connected once its hotkey is off (nothing is left to show for it)
	/// </summary>
	internal void OnHotkeyStateChanged(DeviceEntry entry)
	{
		Save();
		if(entry.HotkeyRegistered || entry.IsConnected)
			return;
		//later, not inside the switch's click: selecting another device here moved the switch to that device while its click
		//was still being handled, and turned that device's hotkey off too (owner found it)
		if(SynchronizationContext.Current is { } uiThread)
			uiThread.Post(_ => RemoveDisconnected(entry), null);
		else
			RemoveDisconnected(entry);
	}

	private void RemoveDisconnected(DeviceEntry entry)
	{
		//turned on again or plugged in meanwhile
		if(entry.HotkeyRegistered || entry.IsConnected || !Devices.Contains(entry))
			return;
		if(SelectedDevice == entry)
			SelectedDevice = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault(d => d != entry);
		Devices.Remove(entry);
	}

	private void Save()
	{
		SettingsProvider.Settings.Hotkeys.DefaultDevices = Devices
			.Where(d => d.HotkeyRegistered)
			.Select(d => new DeviceHotkeyEntry { Hotkey = d.Hotkey, DeviceId = d.Id, DeviceName = d.Name })
			.ToArray();
	}

	private void OnHotkeyPressed(HotKey pressedHotkey)
	{
		var entry = Devices.FirstOrDefault(d => d.HotkeyRegistered && pressedHotkey.Equals(d.Hotkey));
		if(entry != null)
			DeviceProvider.SetDefaultDevice(entry.Id);
	}
}

using System;
using Volumey.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
    public class DefaultDeviceHotkeysViewModel : HotkeyViewModel
    {
        private OutputDeviceModel _selected;
        public OutputDeviceModel SelectedDevice
        {
            get => this._selected;
            set
            {
                this._selected = value;
                OnPropertyChanged();
                ShowRegisteredHotkey(value);
            }
        }

        //true while HotKey shows the registered hotkey of the selected device rather than one the user typed
        private bool hotkeyShowsRegistration;

        private KeyValuePair<HotKey, DeviceInfo>? _selectedRegDev;
        public KeyValuePair<HotKey, DeviceInfo>? SelectedRegDev
        {
            get => this._selectedRegDev;
            set
            {
                this._selectedRegDev = value;
                OnPropertyChanged();
            }
        }

        public ObservableConcurrentDictionary<HotKey, DeviceInfo> RegisteredDevices { get; }

        private HotKey _hotkey;
        public HotKey HotKey
        {
            get => this._hotkey;
            set
            {
                this._hotkey = value;
                this.hotkeyShowsRegistration = false;
                OnPropertyChanged();
            }
        }

        public ICommand AddDeviceCommand { get; }
        public ICommand RemoveDeviceCommand { get; }

        public IDeviceProvider DeviceProvider { get; }

        private ILog _logger;
        private ILog Logger => this._logger ??= LogManager.GetLogger(typeof(DefaultDeviceHotkeysViewModel));

        private readonly IHotkeyService hotkeys;

        internal DefaultDeviceHotkeysViewModel(IDeviceProvider deviceProvider, IHotkeyService hotkeys)
        {
            this.hotkeys = hotkeys;
            
            this.DeviceProvider = deviceProvider;

            this.AddDeviceCommand = new ActionCommand(AddDevice);
            this.RemoveDeviceCommand = new ActionCommand(RemoveDevice);

            this.RegisteredDevices = LoadRegisteredDevices();
            if(this.RegisteredDevices.Keys.Count > 0)
            {
                if(this.hotkeys.IsActive)
                    this.RegisterLoadedHotkeys();
                else
                    this.hotkeys.Activated += this.RegisterLoadedHotkeys;

                //Update registered devices names once on startup
                UpdateNamesOrIDs();

                //show a registration in the list, so the page doesn't look as if nothing was registered (#18)
                this.SelectedRegDev = this.RegisteredDevices.First();
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
                foreach(var registered in this.RegisteredDevices)
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
                this.HotKey = reg.Key;
                this.hotkeyShowsRegistration = true;
                this.SelectedRegDev = reg;
            }
            else if(this.hotkeyShowsRegistration)
                //don't offer another device's hotkey for this one, but keep a hotkey the user typed
                this.HotKey = null;
        }

        private void RegisterLoadedHotkeys()
        {
            this.hotkeys.HotkeyPressed += OnHotkeyPressed;
            //shows an error instead of silently doing nothing; the hotkeys stay saved and are tried again on the next start
            bool allRegistered = true;
            foreach(var hotkeyDeviceInfoPair in this.RegisteredDevices)
                allRegistered &= this.hotkeys.RegisterHotkey(hotkeyDeviceInfoPair.Key);
            if(!allRegistered)
                this.SetErrorMessage(ErrorMessageType.OpenReg);
        }

        /// <summary>
        /// Check if registered devices have changed their names or IDs to update these values in settings,
        /// since IDs are critical for setting default device in system
        /// </summary>
        private void UpdateNamesOrIDs()
        {
            try
            {
                foreach(var activeDevice in this.DeviceProvider.ActiveDevices)
                {
                    foreach(var registeredDevice in this.RegisteredDevices)
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
                                this.RegisteredDevices[hotkey] = deviceInfo;
                                this.SaveRegisteredDevices();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void AddDevice()
        {
            if(this.RegisteredDevices.Values.Any(deviceInfo => deviceInfo.ID.Equals(this.SelectedDevice.Id)))
                return;
            
            if(this.RegisteredDevices.ContainsKey(this.HotKey))
            {
                this.SetErrorMessage(ErrorMessageType.HotkeyExists);
                return;
            }

            if(this.hotkeys.HotkeyIsValid(this.HotKey) is var error && error != ErrorMessageType.None)
            {
                this.SetErrorMessage(error);
                return;
            }

            this.SetErrorMessage(ErrorMessageType.None);

            if (RegisteredDevices.Keys.Count == 0)
                this.hotkeys.HotkeyPressed += OnHotkeyPressed;

            if(!this.hotkeys.RegisterHotkey(this.HotKey))
            {
                this.SetErrorMessage(ErrorMessageType.OpenReg);
                return;
            }
            
            DeviceInfo deviceInfo = new DeviceInfo(this.SelectedDevice.Id, this.SelectedDevice.Master.DeviceFriendlyName);
            var hotkey = this.HotKey;
            
            this.RegisteredDevices.Add(hotkey, deviceInfo);
            this.SelectedDevice = null;
            this.HotKey = null;
            this.SelectedRegDev = new KeyValuePair<HotKey, DeviceInfo>(hotkey, deviceInfo);
            this.Logger.Info($"Registered default device hotkey: {hotkey}, count: {this.RegisteredDevices.Keys.Count}");
            this.SaveRegisteredDevices();
        }

        private void RemoveDevice()
        {
            if(this.SelectedRegDev == null || !this.SelectedRegDev.HasValue)
                return;

            HotKey hotkey = this.SelectedRegDev.Value.Key;

            this.hotkeys.UnregisterHotkey(hotkey);
            this.RegisteredDevices.Remove(hotkey);

            if(this.RegisteredDevices.Keys.Count == 0)
                this.hotkeys.HotkeyPressed -= OnHotkeyPressed;

            this.SelectedRegDev = this.RegisteredDevices.Count > 0 ? this.RegisteredDevices.First() : null;
            this.SetErrorMessage(ErrorMessageType.None);

            try
            {
                this.SaveRegisteredDevices();
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

        private void SaveRegisteredDevices() => this.SaveRegisteredDevices(SettingsProvider.Settings.Hotkeys);

        private void SaveRegisteredDevices(HotkeySettings hotkeySettings)
        {
            var entries = this.RegisteredDevices
                              .Select(d => new DeviceHotkeyEntry { Hotkey = d.Key, DeviceId = d.Value.ID, DeviceName = d.Value.Name })
                              .ToArray();
            hotkeySettings.DefaultDevices = entries;
        }

        private void OnHotkeyPressed(HotKey pressedHotkey)
        {
            if(RegisteredDevices.TryGetValue(pressedHotkey, out DeviceInfo deviceInfo))
                this.DeviceProvider.SetDefaultDevice(deviceInfo.ID);
        }
    }
}
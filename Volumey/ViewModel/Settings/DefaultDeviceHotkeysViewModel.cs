using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
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
            }
        }

        private KeyValuePair<HotKey, Tuple<string, string>>? _selectedRegDev;
        public KeyValuePair<HotKey, Tuple<string, string>>? SelectedRegDev
        {
            get => this._selectedRegDev;
            set
            {
                this._selectedRegDev = value;
                OnPropertyChanged();
            }
        }

        public ObservableConcurrentDictionary<HotKey, Tuple<string, string>> RegisteredDevices { get; }

        private HotKey _hotkey;
        public HotKey HotKey
        {
            get => this._hotkey;
            set
            {
                this._hotkey = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddDeviceCommand { get; }
        public ICommand RemoveDeviceCommand { get; }

        public IDeviceProvider DeviceProvider { get; }

        private ILog _logger;
        private ILog Logger => this._logger ??= LogManager.GetLogger(typeof(DefaultDeviceHotkeysViewModel));

        public DefaultDeviceHotkeysViewModel()
        {
            ErrorDictionary.LanguageChanged += () => { this.SetErrorMessage(this.CurrentErrorType);};
            
            this.DeviceProvider = DataProvider.DeviceProvider.GetInstance();

            this.AddDeviceCommand = new ActionCommand(AddDevice);
            this.RemoveDeviceCommand = new ActionCommand(RemoveDevice);
            
            this.RegisteredDevices = SettingsProvider.HotkeysSettings.GetRegisteredDevices();
            if(this.RegisteredDevices.Keys.Count > 0)
            {
                if(HotkeysControl.IsActive)
                    this.RegisterLoadedHotkeys();
                else
                    HotkeysControl.Activated += this.RegisterLoadedHotkeys;

                //Update registered devices names once on startup
                Task.Run(UpdateNamesOrIDs);
            }
        }

        private void RegisterLoadedHotkeys()
        {
            HotkeysControl.HotkeyPressed += OnHotkeyPressed;
            foreach (var tuple in this.RegisteredDevices)
                HotkeysControl.RegisterHotkey(tuple.Key);
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
                        string id = registeredDevice.Value.Item1;
                        string name = registeredDevice.Value.Item2;
                        
                        if(activeDevice.CompareId(id) || activeDevice.Master.Name.Equals(name))
                        {
                            var actualFriendlyName = activeDevice.Master.DeviceFriendlyName;
                            var friendlyName = registeredDevice.Value.Item2;
                            if(!actualFriendlyName.Equals(friendlyName) || !activeDevice.CompareId(id))
                            {
                                this.RegisteredDevices[registeredDevice.Key] = new Tuple<string, string>(activeDevice.Id, actualFriendlyName);
                                SettingsProvider.HotkeysSettings.RemoveRegisteredDevice(registeredDevice.Key);
                                SettingsProvider.HotkeysSettings.AddRegisteredDevice(registeredDevice.Key, activeDevice.Id, actualFriendlyName);
                                _ = SettingsProvider.SaveSettings();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void AddDevice()
        {
            if(this.RegisteredDevices.Values.Any(val => val.Item1.Equals(this.SelectedDevice.Id)))
                return;
            
            if (this.RegisteredDevices.ContainsKey(this.HotKey))
            {
                this.SetErrorMessage(ErrorMessageType.HotkeyExists);
                return;
            }

            if (HotkeysControl.HotkeyIsValid(this.HotKey) is var error && error != ErrorMessageType.None)
            {
                this.SetErrorMessage(error);
                return;
            }

            this.SetErrorMessage(ErrorMessageType.None);

            if (RegisteredDevices.Keys.Count == 0)
                HotkeysControl.HotkeyPressed += OnHotkeyPressed;

            if (!HotkeysControl.RegisterHotkey(this.HotKey))
            {
                this.SetErrorMessage(ErrorMessageType.OpenReg);
                return;
            }
            var id = this.SelectedDevice.Id;
            var name = this.SelectedDevice.Master.DeviceFriendlyName;
            var hotkey = this.HotKey;
            this.RegisteredDevices.Add(hotkey, new Tuple<string, string>(id, name));
            this.SelectedDevice = null;
            this.HotKey = null;
            Task.Run(() =>
            {
                try
                {
                    this.Logger.Info($"Registered default device hotkey: {hotkey.ToString()}, count: {this.RegisteredDevices.Keys.Count.ToString()}");
                    SettingsProvider.HotkeysSettings.AddRegisteredDevice(hotkey, id, name);
                    _ = SettingsProvider.SaveSettings();
                }
                catch { }
            });
            
        }

        private void RemoveDevice()
        {
            if (this.SelectedRegDev == null || !this.SelectedRegDev.HasValue)
                return;

            var hotkey = this.SelectedRegDev.Value.Key;

            HotkeysControl.UnregisterHotkey(hotkey);
            this.RegisteredDevices.Remove(hotkey);

            if (this.RegisteredDevices.Keys.Count == 0)
                HotkeysControl.HotkeyPressed -= OnHotkeyPressed;

            this.SelectedRegDev = null;
            this.SetErrorMessage(ErrorMessageType.None);

            try
            {
                SettingsProvider.HotkeysSettings.RemoveRegisteredDevice(hotkey);
                _ = SettingsProvider.SaveSettings();
            }
            catch { }
        }

        private void OnHotkeyPressed(HotKey pressedHotkey)
        {
            if (RegisteredDevices.TryGetValue(pressedHotkey, out var value))
            {
                var id = value.Item1;
                this.DeviceProvider.SetDefaultDevice(id);
            }
        }
    }
}
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notifications when an audio endpoint device is added or removed,
    /// when the state or properties of an endpoint device change,
    /// or when there is a change in the default role assigned to an endpoint device. 
    /// </summary>
    public sealed class DeviceStateNotificationsHandler : IDeviceStateNotificationHandler
    { 
        public event Action<string> DefaultDeviceChanged;
        public event Action<string> NameChanged;
        public event Action<string> IconPathChanged;
        public event Action<string> FormatChanged;
        public event Action<string> DeviceDisabled;
        public event Action<OutputDeviceModel> ActiveDeviceAdded;

        private readonly IMMDeviceEnumerator deviceEnumerator;

        private ILog logger;
        private ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceStateNotificationsHandler));

        public DeviceStateNotificationsHandler(IMMDeviceEnumerator sEnum)
        {
            this.deviceEnumerator = sEnum;
            this.RegisterDeviceNotifications();
        }

        /// <summary>
        /// Called when the default Endpoint device for a given role changes
        /// </summary>
        /// <param name="flow">The dataflow direction</param>
        /// <param name="role">The role</param>
        /// <param name="deviceId">The ID of the Endpoint device that is now the default for the specified role</param>
        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string deviceId)
        {
            if(flow == EDataFlow.Render && role == ERole.Console)
            {
                this.DefaultDeviceChanged?.Invoke(deviceId);
            }
            return 0;
        }

        /// <summary>
        /// Called when a new Endpoint device is added to the system
        /// </summary>
        /// <param name="deviceId">The ID of the new Endpoint device</param>
        public int OnDeviceAdded(string deviceId)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    this.deviceEnumerator.GetDevice(deviceId, out IMMDevice newDevice);
                    var device = new MMDevice(newDevice);
                    if(device.GetDataFlow() == EDataFlow.Render && device.GetState() == DeviceState.Active)
                    {
                        var model = device.GetOutputDeviceModel(this);
                        if(model != null)
                            this.ActiveDeviceAdded?.Invoke(model);
                    }
                }
                catch(Exception e)
                {
                    Logger.Error("Failed to check/add new device", e);
                }
            });
            return 0;
        }

        /// <summary>
        /// Called when an Endpoint device is removed from the system
        /// </summary>
        /// <param name="deviceId">The ID of the Endpoint device that was removed</param>
        public int OnDeviceRemoved(string deviceId) => 0;

        /// <summary>
        /// Called when the state of an Endpoint device changes
        /// </summary>
        /// <param name="deviceId">The ID of the Endpoint device whose state has changed</param>
        /// <param name="newState">The new state of the device</param>
        public int OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    this.deviceEnumerator.GetDevice(deviceId, out IMMDevice newDevice);
                    var device = new MMDevice(newDevice);
                    if(device.GetDataFlow() == EDataFlow.Render)
                    {
                        if(newState != DeviceState.Active)
                            this.DeviceDisabled?.Invoke(deviceId);
                        else
                        {
                            var model = device.GetOutputDeviceModel(this);
                            if(model != null)
                                this.ActiveDeviceAdded?.Invoke(model);
                        }
                    }
                }
                catch { }
            });
            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PROPERTYKEY key)
        {
            var friendlyName = PROPERTYKEY.DeviceProperties.FriendlyName;
            if(key.Guid == friendlyName.Guid && (key.Id == friendlyName.Id || key.Id == PROPERTYKEY.DeviceProperties.Description.Id))
            {
                this.NameChanged?.Invoke(deviceId);
                return 0;
            }

            var iconPath = PROPERTYKEY.DeviceProperties.IconPath;
            if(key.Guid == iconPath.Guid && key.Id == iconPath.Id)
            {
                this.IconPathChanged?.Invoke(deviceId);
                return 0;
            }

            var format = PROPERTYKEY.DeviceProperties.DeviceFormat;
            if(key.Guid == format.Guid && key.Id == format.Id)
            {
                this.FormatChanged?.Invoke(deviceId);
            }
            return 0;
        }

        /// <summary>
        /// Registers the object to receive device notifications
        /// </summary>
        private void RegisterDeviceNotifications()
            => Marshal.ThrowExceptionForHR(this.deviceEnumerator.RegisterEndpointNotificationCallback(this));
        

        /// <summary>
        /// Unregisters the object that was registered in a previous call to RegisterDeviceNotifications
        /// </summary>
        private void UnregisterDeviceNotifications()
        {
            //Unregister on the thread pool because this call occasionally hangs and might hang the entire application when executed in the UI thread :\
            Task.Run(() => { this.deviceEnumerator.UnregisterEndpointNotificationCallback(this); });
        }

        public void Dispose()
        {
            this.UnregisterDeviceNotifications();
        }
    }
}

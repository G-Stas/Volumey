using System;
using System.Threading.Tasks;
using log4net;
using NAudio.CoreAudioApi;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    /// <summary>
    /// Provides notifications when an audio endpoint device is added or removed,
    /// when the state or properties of an endpoint device change,
    /// or when there is a change in the default role assigned to an endpoint device.
    /// NAudio raises the notifications on COM threads. They are passed to the UI thread without waiting:
    /// a waiting callback thread deadlocks with the UI thread when that releases a device's COM objects,
    /// because the release waits for running callbacks to return.
    /// </summary>
    public sealed class DeviceStateNotificationsHandler : IDeviceStateNotificationHandler
    {
        public event Action<string> DefaultDeviceChanged;
        public event Action<string> NameChanged;
        public event Action<string> IconPathChanged;
        public event Action<string> FormatChanged;
        public event Action<string> DeviceDisabled;
        public event Action<OutputDeviceModel> ActiveDeviceAdded;

        private readonly DeviceEnumerator enumerator;
        private readonly MMDeviceNotificationClient notificationClient;

        private ILog logger;
        private ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceStateNotificationsHandler));

        public DeviceStateNotificationsHandler(DeviceEnumerator enumerator)
        {
            this.enumerator = enumerator;
            this.notificationClient = enumerator.Enumerator.CreateNotificationClient(useSynchronizationContext: false);
            this.notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
            this.notificationClient.DeviceAdded += OnDeviceAdded;
            this.notificationClient.DeviceStateChanged += OnDeviceStateChanged;
            this.notificationClient.PropertyValueChanged += OnPropertyValueChanged;
        }

        /// <summary>
        /// Called when the default Endpoint device for a given role changes
        /// </summary>
        private void OnDefaultDeviceChanged(object sender, DefaultDeviceChangedEventArgs e)
        {
            if(e.Flow == DataFlow.Render && e.Role == Role.Console)
            {
                Post(() => this.DefaultDeviceChanged?.Invoke(e.DeviceId));
            }
        }

        /// <summary>
        /// Called when a new Endpoint device is added to the system
        /// </summary>
        private void OnDeviceAdded(object sender, DeviceNotificationEventArgs e)
        {
            Post(() =>
            {
                try
                {
                    this.AddIfActiveOutput(e.DeviceId);
                }
                catch(Exception ex)
                {
                    Logger.Error("Failed to check/add new device", ex);
                }
            });
        }

        /// <summary>
        /// Called when the state of an Endpoint device changes
        /// </summary>
        private void OnDeviceStateChanged(object sender, DeviceStateChangedEventArgs e)
        {
            Post(() =>
            {
                try
                {
                    //every output device model ignores other devices' IDs, so input devices need no check
                    //(opening the device for one used to release it on the UI thread)
                    if(e.NewState != DeviceState.Active)
                        this.DeviceDisabled?.Invoke(e.DeviceId);
                    else
                        this.AddIfActiveOutput(e.DeviceId);
                }
                catch(Exception ex)
                {
                    Logger.Error("Failed to handle a device state change", ex);
                }
            });
        }

        private void AddIfActiveOutput(string deviceId)
        {
            //null for input or inactive devices; their objects are released off the UI thread
            if(this.enumerator.CreateDeviceModel(deviceId, this) is OutputDeviceModel model)
                this.ActiveDeviceAdded?.Invoke(model);
        }

        private void OnPropertyValueChanged(object sender, DevicePropertyChangedEventArgs e)
        {
            var key = e.PropertyKey;
            if(IsKey(key, PropertyKeys.PKEY_Device_FriendlyName) || IsKey(key, PropertyKeys.PKEY_Device_DeviceDesc))
                Post(() => this.NameChanged?.Invoke(e.DeviceId));
            else if(IsKey(key, PropertyKeys.PKEY_Device_IconPath))
                Post(() => this.IconPathChanged?.Invoke(e.DeviceId));
            else if(IsKey(key, PropertyKeys.PKEY_AudioEngine_DeviceFormat))
                Post(() => this.FormatChanged?.Invoke(e.DeviceId));
        }

        /// <summary>
        /// Runs the action on the UI thread without blocking the calling COM thread. The dispatcher keeps the order of the notifications.
        /// </summary>
        private void Post(Action action)
        {
            try
            {
                App.Current?.Dispatcher.InvokeAsync(() =>
                {
                    try { action(); }
                    catch(Exception e) { Logger.Error("Failed to handle a device notification", e); }
                });
            }
            catch(Exception e)
            {
                Logger.Error("Failed to dispatch a device notification", e);
            }
        }

        private static bool IsKey(PropertyKey key, PropertyKey expected)
            => key.formatId == expected.formatId && key.propertyId == expected.propertyId;

        public void Dispose()
        {
            this.notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
            this.notificationClient.DeviceAdded -= OnDeviceAdded;
            this.notificationClient.DeviceStateChanged -= OnDeviceStateChanged;
            this.notificationClient.PropertyValueChanged -= OnPropertyValueChanged;
            //Unregister on the thread pool because this call occasionally hangs and might hang the entire application when executed in the UI thread :\
            Task.Run(() => this.notificationClient.Dispose());
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using log4net;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.DataProvider
{
	/// <summary>
	/// Provides data about active and default output devices.
	/// </summary>
	public sealed class DeviceProvider : IDeviceProvider, INotifyPropertyChanged
	{
		public ObservableCollection<OutputDeviceModel> ActiveDevices { get; } = new ObservableCollection<OutputDeviceModel>();
		public event Action<OutputDeviceModel> DeviceDisabled;
		public event Action<OutputDeviceModel> DefaultDeviceChanged; 
		public event Action<OutputDeviceModel, OutputDeviceModel> DeviceReplaced;

		private OutputDeviceModel defaultDevice;
		public OutputDeviceModel DefaultDevice
		{
			get => defaultDevice;
			set
			{
				this.defaultDevice = value;
				OnPropertyChanged();
			}
		}

		private readonly PolicyClient PolicyClient = new PolicyClient();
		
		private bool noOutputDevices;
		public bool NoOutputDevices
		{
			get => noOutputDevices;
			set
			{
				noOutputDevices = value;
				OnPropertyChanged();
			}
		}
		
		private readonly IDeviceStateNotificationHandler deviceStateNotificationHandler;
		private readonly IDeviceEnumerator deviceEnumerator;

		/// <summary>Delays of the attempts to rebuild a reconfigured device: Windows may still be busy right after the notification</summary>
		private static readonly TimeSpan[] RebuildDelays =
			{ TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
		private volatile bool disposed;
		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceProvider));

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		internal DeviceProvider(IDeviceEnumerator deviceEnumerator, IDeviceStateNotificationHandler deviceStateHandler)
		{
			this.deviceStateNotificationHandler = deviceStateHandler;
			this.deviceEnumerator = deviceEnumerator;
			this.deviceStateNotificationHandler.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.deviceStateNotificationHandler.ActiveDeviceAdded += OnActiveDeviceAdded;

			string defaultDeviceId = deviceEnumerator.GetDefaultDeviceId();
			var devices = deviceEnumerator.GetCurrentActiveDevices(this.deviceStateNotificationHandler);
			if(devices.Count > 0)
			{
				foreach(var device in devices)
				{
					device.Disabled += OnDeviceDisabled;
					device.Invalidated += OnDeviceInvalidated;
					this.ActiveDevices.Add(device);

					if(device.CompareId(defaultDeviceId))
					{
						this.DefaultDevice = device;
					}
				}

				if(this.DefaultDevice == null)
				{
					this.DefaultDevice = this.ActiveDevices[0];
				}
			}
			else
				NoOutputDevices = true;
		}

		private DeviceProvider() => this.NoOutputDevices = true;

		/// <summary>
		/// Creates the provider of the app's audio devices. If Windows audio can't be reached, the provider has no devices.
		/// </summary>
		internal static IDeviceProvider Create(IHotkeyService hotkeys)
		{
			try
			{
				var devEnum = new DeviceEnumerator(hotkeys);
				return new DeviceProvider(devEnum, new DeviceStateNotificationsHandler(devEnum));
			}
			catch(Exception e)
			{
				//everything depends on this object, so create an empty one which shows that there are no output devices
				Logger.Fatal("Failed to get device provider object", e);
				return new DeviceProvider();
			}
		}
		public void SetDefaultDevice(string id)
		{
			try
			{
				//Check if the device to set is enabled
				if(this.ActiveDevices.Any(device => device.CompareId(id)))
				{
					//Prevent if it's already the default device
					if(DefaultDevice != null && !DefaultDevice.CompareId(id))
					{
						this.PolicyClient.SetDefaultEndpointDevice(id);
					}
				}
			}
			catch(Exception e)
			{
				Logger.Error("Failed to change default device", e);
			}
		}

		private void OnDeviceDisabled(OutputDeviceModel device)
		{
			Logger.Info($"Output device disabled: [{device.Master.DeviceFriendlyName}]");
			dispatcher.Invoke(() =>
			{
				//display NoOutputDevice placeholder in mixer view before deleting the last element
				if(this.ActiveDevices.Count == 1)
					NoOutputDevices = true;

				this.DeviceDisabled?.Invoke(device);
				device.Disabled -= OnDeviceDisabled;
				device.Invalidated -= OnDeviceInvalidated;
				this.ActiveDevices.Remove(device);

				device.Dispose();
			});
		}

		private void OnActiveDeviceAdded(OutputDeviceModel newDevice)
		{
			Logger.Info($"Output device added: [{newDevice.Master.DeviceFriendlyName}]");
			dispatcher.Invoke(() =>
			{
				newDevice.Disabled += OnDeviceDisabled;
				newDevice.Invalidated += OnDeviceInvalidated;
				this.ActiveDevices.Add(newDevice);
				
				if(NoOutputDevices)
				{
					this.DefaultDevice = newDevice; 
					//invoke the event to display a new default device in mixer view before NoOutputDevices placeholder will be removed
					this.DefaultDeviceChanged?.Invoke(this.DefaultDevice);
					NoOutputDevices = false;
				}
			});
		}

		private void OnDefaultDeviceChanged(string deviceId)
		{
			dispatcher.Invoke(() =>
			{
				if(deviceId == null)
				{
					this.DefaultDevice = null;
					this.DefaultDeviceChanged?.Invoke(this.DefaultDevice);
					return;
				}

				OutputDeviceModel newDefaultDevice = null;
				try
				{
					Monitor.Enter(this.ActiveDevices);
					foreach(var device in this.ActiveDevices)
					{
						if(device.CompareId(deviceId))
						{
							newDefaultDevice = device;
							break;
						}
					}
				}
				catch
				{
					if(this.ActiveDevices.Count > 0)
						this.DefaultDevice = this.ActiveDevices[0];
				}
				finally
				{
					Monitor.Exit(this.ActiveDevices);
					if(newDefaultDevice != null && this.DefaultDevice != newDefaultDevice)
					{
						Logger.Info($"Default output device changed: [{newDefaultDevice.Master.DeviceFriendlyName}]");
						this.DefaultDevice = newDefaultDevice;						
						this.DefaultDeviceChanged?.Invoke(this.DefaultDevice);
					}
				}
			});
		}

		/// <summary>
		/// Windows reconfigured the device (format or spatial sound changed, #82, #85): its sessions are gone and its objects stopped working,
		/// so the model is built again from fresh objects and replaces the old one. Before 2.0 the whole app restarted instead.
		/// </summary>
		private async void OnDeviceInvalidated(OutputDeviceModel invalid)
		{
			try
			{
				foreach(var delay in RebuildDelays)
				{
					await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
					if(this.disposed)
						return;
					bool done = false;
					dispatcher.Invoke(() => done = TryReplaceDevice(invalid));
					if(done)
						return;
				}
				Logger.Error($"Failed to rebuild the reconfigured device: [{invalid.Master.DeviceFriendlyName}]");
			}
			catch(Exception e)
			{
				Logger.Error("Failed to rebuild a reconfigured device", e);
			}
		}

		/// <returns>False if the new model couldn't be built yet</returns>
		private bool TryReplaceDevice(OutputDeviceModel invalid)
		{
			int index = this.ActiveDevices.IndexOf(invalid);
			//it was disabled in the meantime
			if(index < 0)
				return true;

			var fresh = this.deviceEnumerator.CreateDeviceModel(invalid.Id, this.deviceStateNotificationHandler);
			if(fresh == null)
				return false;

			invalid.Disabled -= OnDeviceDisabled;
			invalid.Invalidated -= OnDeviceInvalidated;
			fresh.Disabled += OnDeviceDisabled;
			fresh.Invalidated += OnDeviceInvalidated;
			//add the new model before the listeners switch to it, and remove the old one after that:
			//replacing it in place makes a combo box that shows it lose its selection and write null back
			this.ActiveDevices.Insert(index, fresh);

			if(this.DefaultDevice == invalid)
			{
				this.DefaultDevice = fresh;
				//listeners move hotkeys, notifications etc. from the old model to the new one
				this.DefaultDeviceChanged?.Invoke(fresh);
			}
			this.DeviceReplaced?.Invoke(invalid, fresh);
			this.ActiveDevices.Remove(invalid);
			Logger.Info($"Rebuilt the reconfigured device: [{fresh.Master.DeviceFriendlyName}], {fresh.Processes.Count} apps");

			invalid.Dispose();
			return true;
		}

		public void Dispose()
		{
			//no rebuilds after the app has started to exit
			this.disposed = true;
			this.deviceStateNotificationHandler?.Dispose();
			foreach(var device in this.ActiveDevices)
				device.Dispose();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
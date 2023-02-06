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
		public event Action<OutputDeviceModel> DeviceFormatChanged;

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
		
		private static IDeviceProvider instance;
		private readonly IDeviceStateNotificationHandler deviceStateNotificationHandler;
		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(DeviceProvider));

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		private DeviceProvider(IDeviceEnumerator deviceEnumerator, IDeviceStateNotificationHandler deviceStateHandler)
		{
			this.deviceStateNotificationHandler = deviceStateHandler;
			this.deviceStateNotificationHandler.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.deviceStateNotificationHandler.ActiveDeviceAdded += OnActiveDeviceAdded;

			string defaultDeviceId = deviceEnumerator.GetDefaultDeviceId();
			var devices = deviceEnumerator.GetCurrentActiveDevices(this.deviceStateNotificationHandler);
			if(devices.Count > 0)
			{
				foreach(var device in devices)
				{
					device.Disabled += OnDeviceDisabled;
					device.FormatChanged += OnDeviceFormatChanged;
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

		public static IDeviceProvider GetInstance()
		{
			if(instance == null)
			{
				try
				{
					var devEnum = new DeviceEnumerator();
					return instance = new DeviceProvider(devEnum, new DeviceStateNotificationsHandler(devEnum.MMDeviceEnumerator));
				}
				catch(Exception e)
				{
					//create empty DeviceProvider object which is going to indicate that there are no output devices available
					//since everything depends on this object and there was an error while trying to get it from WinApi
					Logger.Fatal("Failed to get device provider object", e);
					return instance = new DeviceProvider();
				}
			}
			return instance;
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
			dispatcher.Invoke(() =>
			{
				//display NoOutputDevice placeholder in mixer view before deleting the last element
				if(this.ActiveDevices.Count == 1)
					NoOutputDevices = true;

				this.DeviceDisabled?.Invoke(device);
				device.Disabled -= OnDeviceDisabled;
				device.FormatChanged -= OnDeviceFormatChanged;
				this.ActiveDevices.Remove(device);

				device.Dispose();
			});
		}

		private void OnActiveDeviceAdded(OutputDeviceModel newDevice)
		{
			dispatcher.Invoke(() =>
			{
				newDevice.Disabled += OnDeviceDisabled;
				newDevice.FormatChanged += OnDeviceFormatChanged;
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
						this.DefaultDevice = newDefaultDevice;						
						this.DefaultDeviceChanged?.Invoke(this.DefaultDevice);
					}
				}
			});
		}

		private void OnDeviceFormatChanged(OutputDeviceModel sender)
			=> this.DeviceFormatChanged?.Invoke(sender);

		public void Dispose()
		{
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
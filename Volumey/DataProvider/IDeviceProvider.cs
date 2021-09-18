using System;
using System.Collections.ObjectModel;
using Volumey.Model;

namespace Volumey.DataProvider
{
	public interface IDeviceProvider : IDisposable
	{
		public ObservableCollection<OutputDeviceModel> ActiveDevices { get; }
		public OutputDeviceModel DefaultDevice { get; set; }
		
		event Action<OutputDeviceModel> DeviceDisabled;
		event Action<OutputDeviceModel> DefaultDeviceChanged;
		/// <summary>
		/// Occurrs when stream format of any active device changes
		/// </summary>
		event Action<OutputDeviceModel> DeviceFormatChanged;
		
		public bool NoOutputDevices { get; set; }

		public void SetDefaultDevice(string deviceId);
	}
}
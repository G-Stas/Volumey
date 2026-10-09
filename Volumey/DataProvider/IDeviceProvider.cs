using System;
using System.Collections.ObjectModel;
using Volumey.Model;

namespace Volumey.DataProvider;

public interface IDeviceProvider : IDisposable
{
	public ObservableCollection<OutputDeviceModel> ActiveDevices { get; }
	public OutputDeviceModel DefaultDevice { get; set; }

	event Action<OutputDeviceModel> DeviceDisabled;
	event Action<OutputDeviceModel> DefaultDeviceChanged;
	/// <summary>
	/// The model of a device was replaced by a new one (old, new), because Windows reconfigured the device (see <see cref="OutputDeviceModel.Invalidated"/>).
	/// If it's the default device, <see cref="DefaultDeviceChanged"/> is raised before this with the new model. The old model is disposed afterwards.
	/// </summary>
	event Action<OutputDeviceModel, OutputDeviceModel> DeviceReplaced;

	public bool NoOutputDevices { get; set; }

	public void SetDefaultDevice(string deviceId);
}

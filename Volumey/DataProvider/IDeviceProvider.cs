#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;

namespace Volumey.DataProvider;

public interface IDeviceProvider : IDisposable
{
	public ObservableCollection<OutputDeviceModel> ActiveDevices { get; }
	public OutputDeviceModel? DefaultDevice { get; set; }

	event Action<OutputDeviceModel> DeviceDisabled;
	event Action<OutputDeviceModel?> DefaultDeviceChanged;
	/// <summary>
	/// The model of a device was replaced by a new one (old, new), because Windows reconfigured the device (see <see cref="OutputDeviceModel.Invalidated"/>).
	/// If it's the default device, <see cref="DefaultDeviceChanged"/> is raised before this with the new model. The old model is disposed afterwards.
	/// </summary>
	event Action<OutputDeviceModel, OutputDeviceModel> DeviceReplaced;

	public bool NoOutputDevices { get; set; }

	/// <summary>
	/// True while the devices are read at start (also when that happens late: after Windows audio couldn't be reached,
	/// or a device Windows refused at start that is read again),
	/// so a <see cref="DefaultDeviceChanged"/> then is the start, not a device plugged in
	/// </summary>
	public bool IsReadingDevices { get; }

	public void SetDefaultDevice(string? deviceId);

	/// <summary>
	/// Sets the format the device runs at (on the thread pool: Windows reconfigures the device during the call). False if the change failed;
	/// the failure is logged. On success the device's model is rebuilt, see <see cref="DeviceReplaced"/>.
	/// </summary>
	public Task<bool> SetDeviceFormatAsync(string deviceId, DeviceFormatOption format);

	/// <summary>
	/// The device's exclusive mode switches (read on the thread pool)
	/// </summary>
	public Task<DeviceFeatureState> GetDeviceFeaturesAsync(string deviceId);

	/// <summary>
	/// Turns a switch of the device on or off (on the thread pool); false if it failed (logged).
	/// </summary>
	public Task<bool> SetDeviceSwitchAsync(string deviceId, DeviceSwitch setting, bool on);
}

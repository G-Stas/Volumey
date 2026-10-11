using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Volumey.CoreAudioWrapper.Wrapper;
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

	/// <summary>
	/// Sets the format the device runs at (on the thread pool: Windows reconfigures the device during the call). False if the change failed;
	/// the failure is logged. On success the device's model is rebuilt, see <see cref="DeviceReplaced"/>.
	/// </summary>
	public Task<bool> SetDeviceFormatAsync(string deviceId, DeviceFormatOption format);

	/// <summary>
	/// The device's enhancements and exclusive mode switches and its spatial sound (read on the thread pool)
	/// </summary>
	public Task<DeviceFeatureState> GetDeviceFeaturesAsync(string deviceId);

	/// <summary>
	/// Turns a switch of the device on or off (on the thread pool); false if it failed (logged). Audio enhancements reconfigure the device
	/// (its model is rebuilt).
	/// </summary>
	public Task<bool> SetDeviceSwitchAsync(string deviceId, DeviceSwitch setting, bool on);

	/// <summary>
	/// Sets the device's spatial sound format or turns it off (on the thread pool). On success Windows reconfigures the device (its model is
	/// rebuilt). A format Windows refuses for its license is left out of the device's list until Volumey restarts (owner's decision);
	/// failures are logged.
	/// </summary>
	public Task<SpatialSoundResult> SetSpatialFormatAsync(string deviceId, SpatialSoundFormat format);
}

#nullable enable
using System;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IDeviceStateNotificationHandler : IDisposable
{
	public event Action<string>? DefaultDeviceChanged;
	public event Action<string>? NameChanged;
	public event Action<string>? IconPathChanged;
	public event Action<string>? FormatChanged;
	public event Action<string>? DeviceDisabled;
	public event Action<OutputDeviceModel>? ActiveDeviceAdded;

	/// <summary>
	/// Reads an active output device that Windows refused for a moment again with growing delays,
	/// and raises <see cref="ActiveDeviceAdded"/> once it can be read
	/// </summary>
	/// <param name="atStart">The device was refused while the devices were read at start (not plugged in later)</param>
	void AddWhenReady(string deviceId, bool atStart = false);

	/// <summary>
	/// True while <see cref="ActiveDeviceAdded"/> runs for a device that Windows refused at start, so listeners can tell it from a device plugged in
	/// </summary>
	bool IsAddingStartDevice { get; }
}

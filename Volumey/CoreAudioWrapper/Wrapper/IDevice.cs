#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Volumey.CoreAudioWrapper.Wrapper;

public interface IDevice : IDisposable
{
	string GetId();

	string GetFriendlyName();

	string GetDeviceDesc();

	/// <summary>
	/// A new icon owned by the caller, or null if the device has none (or it can't be read)
	/// </summary>
	Icon? GetIcon();

	DeviceFormat? GetDeviceFormat();

	/// <summary>
	/// The formats the device can run at (Windows' list: what it takes in exclusive mode), by bits and rate; empty if they can't be read
	/// </summary>
	IReadOnlyList<DeviceFormatOption> GetSupportedFormats();

	/// <summary>
	/// The level of each channel (0..1, the endpoint's channel volumes: the balance); empty if they can't be read
	/// </summary>
	IReadOnlyList<float> GetChannelVolumes();

	/// <summary>
	/// Sets one channel's level (0..1); Windows keeps the device volume at the loudest channel
	/// </summary>
	void SetChannelVolume(int channel, float level);

	/// <summary>
	/// A channel level or the device volume changed (also by Volumey itself); raised on a COM thread or the UI thread
	/// </summary>
	event Action ChannelVolumesChanged;
}

#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// A property of an endpoint's store, read and written through IPolicyConfig
/// </summary>
public readonly record struct EndpointProperty(Guid FormatId, uint PropertyId)
{
	/// <summary>
	/// "Allow applications to take exclusive control of this device" (1 = allowed; undocumented key of the audio engine's properties)
	/// </summary>
	public static readonly EndpointProperty AllowExclusive = new(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 3);

	/// <summary>
	/// "Give exclusive mode applications priority" (1 = on; undocumented key of the audio engine's properties)
	/// </summary>
	public static readonly EndpointProperty ExclusivePriority = new(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 4);
}

/// <summary>
/// The on/off settings of a device on the Devices page
/// </summary>
public enum DeviceSwitch { AllowExclusive, ExclusivePriority }

/// <summary>
/// What the Devices page shows for a device's switches; a null value couldn't be read
/// </summary>
public sealed record DeviceFeatureState(bool? AllowExclusive, bool? ExclusivePriority)
{
	public static readonly DeviceFeatureState Unknown = new(null, null);
}

/// <summary>
/// Reads and changes a device's exclusive mode switches (IPolicyConfig, undocumented, as Windows' sound settings do).
/// Every call makes COM calls: run it off the UI thread. Checked on a real device (2026-10-11): works without admin rights.
/// </summary>
public sealed class DeviceFeatures
{
	private readonly PolicyClient policy;

	public DeviceFeatures(PolicyClient policy)
	{
		this.policy = policy;
	}

	/// <summary>
	/// The device's switches; a switch that can't be read stays null
	/// </summary>
	public DeviceFeatureState Read(string deviceId)
	{
		bool? Switch(EndpointProperty property)
		{
			try
			{
				//not set: Windows' default, which is on for both (1 = on)
				return (policy.GetEndpointValue(deviceId, property) ?? 1u) != 0;
			}
			catch
			{
				return null;
			}
		}

		return new DeviceFeatureState(
			AllowExclusive: Switch(EndpointProperty.AllowExclusive),
			ExclusivePriority: Switch(EndpointProperty.ExclusivePriority));
	}

	/// <exception cref="System.Runtime.InteropServices.COMException">Windows refused the change</exception>
	public void Set(string deviceId, DeviceSwitch setting, bool on)
	{
		var property = setting == DeviceSwitch.AllowExclusive ? EndpointProperty.AllowExclusive : EndpointProperty.ExclusivePriority;
		policy.SetEndpointValue(deviceId, property, on ? 1u : 0u);
	}
}

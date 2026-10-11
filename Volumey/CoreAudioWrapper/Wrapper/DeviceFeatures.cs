#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Media.Audio;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// A property of an endpoint's store, read and written through IPolicyConfig
/// </summary>
public readonly record struct EndpointProperty(Guid FormatId, uint PropertyId)
{
	/// <summary>
	/// PKEY_AudioEndpoint_Disable_SysFx: 1 turns the driver's audio enhancements off ("Audio enhancements" in Windows' sound settings)
	/// </summary>
	public static readonly EndpointProperty DisableSysFx = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 5);

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
public enum DeviceSwitch { Enhancements, AllowExclusive, ExclusivePriority }

/// <summary>
/// A spatial sound format (Windows Sonic, Dolby Atmos...); <see cref="Off"/> turns spatial sound off
/// </summary>
public readonly record struct SpatialSoundFormat(Guid Id)
{
	public static readonly SpatialSoundFormat Off = new(Guid.Empty);

	public bool IsOff => Id == Guid.Empty;
}

/// <summary>
/// How setting a spatial sound format went
/// </summary>
public enum SpatialSoundResult
{
	Set,
	/// <summary>
	/// The format is installed but not licensed for the device (Dolby Atmos or DTS without their license)
	/// </summary>
	NotLicensed,
	Failed
}

/// <summary>
/// What the Devices page shows for a device's switches and spatial sound; a null value couldn't be read
/// </summary>
public sealed record DeviceFeatureState(bool? Enhancements, bool? AllowExclusive, bool? ExclusivePriority,
	IReadOnlyList<SpatialSoundFormat> SpatialFormats, SpatialSoundFormat? SpatialFormat)
{
	public static readonly DeviceFeatureState Unknown = new(null, null, null, Array.Empty<SpatialSoundFormat>(), null);
}

/// <summary>
/// Reads and changes a device's audio enhancements, exclusive mode switches (IPolicyConfig, undocumented, as Windows' sound settings do)
/// and spatial sound (the documented SpatialAudioDeviceConfiguration). Every call makes COM/WinRT calls: run it off the UI thread.
/// Checked on a real device (2026-10-11): all of them work without admin rights; enhancements and spatial sound
/// make Windows reconfigure the device (its model is rebuilt).
/// </summary>
public sealed class DeviceFeatures
{
	private readonly PolicyClient policy;

	//the spatial sound formats Windows knows, in the order its list shows them
	private static readonly Guid[] KnownSpatialFormats =
	{
		Guid.Parse(SpatialAudioFormatSubtype.WindowsSonic),
		Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForHeadphones),
		Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForHomeTheater),
		Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForSpeakers),
		Guid.Parse(SpatialAudioFormatSubtype.DTSHeadphoneX),
		Guid.Parse(SpatialAudioFormatSubtype.DTSXUltra)
	};

	public DeviceFeatures(PolicyClient policy)
	{
		this.policy = policy;
	}

	/// <summary>
	/// The device's switches and spatial sound; a part that can't be read stays null (empty list for spatial sound)
	/// </summary>
	public DeviceFeatureState Read(string deviceId)
	{
		bool? Switch(EndpointProperty property, bool onWhenOne)
		{
			try
			{
				//not set: Windows' default, which is on for all three
				uint value = policy.GetEndpointValue(deviceId, property) ?? (onWhenOne ? 1u : 0u);
				return onWhenOne ? value != 0 : value == 0;
			}
			catch
			{
				return null;
			}
		}

		var formats = new List<SpatialSoundFormat>();
		SpatialSoundFormat? current = null;
		try
		{
			var spatial = SpatialAudioDeviceConfiguration.GetForDeviceId(WinRtId(deviceId));
			if(spatial.IsSpatialAudioSupported)
			{
				formats.Add(SpatialSoundFormat.Off);
				formats.AddRange(KnownSpatialFormats.Where(f => spatial.IsSpatialAudioFormatSupported(f.ToString("B"))).Select(f => new SpatialSoundFormat(f)));
				current = Guid.TryParse(spatial.DefaultSpatialAudioFormat, out var id) ? new SpatialSoundFormat(id) : SpatialSoundFormat.Off;
			}
		}
		catch
		{
			//not available (e.g. an older Windows): the page doesn't show it
			formats.Clear();
		}

		return new DeviceFeatureState(
			Enhancements: Switch(EndpointProperty.DisableSysFx, onWhenOne: false),
			AllowExclusive: Switch(EndpointProperty.AllowExclusive, onWhenOne: true),
			ExclusivePriority: Switch(EndpointProperty.ExclusivePriority, onWhenOne: true),
			SpatialFormats: formats,
			SpatialFormat: current);
	}

	/// <exception cref="System.Runtime.InteropServices.COMException">Windows refused the change</exception>
	public void Set(string deviceId, DeviceSwitch setting, bool on)
	{
		switch(setting)
		{
			case DeviceSwitch.Enhancements:
				policy.SetEndpointValue(deviceId, EndpointProperty.DisableSysFx, on ? 0u : 1u);
				break;
			case DeviceSwitch.AllowExclusive:
				policy.SetEndpointValue(deviceId, EndpointProperty.AllowExclusive, on ? 1u : 0u);
				break;
			case DeviceSwitch.ExclusivePriority:
				policy.SetEndpointValue(deviceId, EndpointProperty.ExclusivePriority, on ? 1u : 0u);
				break;
		}
	}

	/// <summary>
	/// Sets the device's spatial sound format (<see cref="SpatialSoundFormat.Off"/> turns it off).
	/// A format can be installed (its app, e.g. Dolby Access) and still not be licensed for the device: Windows then refuses it with
	/// <see cref="SpatialSoundResult.NotLicensed"/> (seen with Dolby Atmos and DTS on a real device, 2026-10-11); there's no call that
	/// tells it beforehand without switching.
	/// </summary>
	/// <exception cref="InvalidOperationException">Windows refused it for another reason (the status is in the message)</exception>
	public SpatialSoundResult SetSpatialFormat(string deviceId, SpatialSoundFormat format)
	{
		var spatial = SpatialAudioDeviceConfiguration.GetForDeviceId(WinRtId(deviceId));
		var result = spatial.SetDefaultSpatialAudioFormatAsync(format.Id.ToString("B")).AsTask().GetAwaiter().GetResult();
		return result.Status switch
		{
			SetDefaultSpatialAudioFormatStatus.Succeeded => SpatialSoundResult.Set,
			SetDefaultSpatialAudioFormatStatus.LicenseExpired or SetDefaultSpatialAudioFormatStatus.LicenseNotValidForAudioEndpoint => SpatialSoundResult.NotLicensed,
			_ => throw new InvalidOperationException($"Windows refused the spatial sound format: {result.Status}")
		};
	}

	/// <summary>
	/// The WinRT id of an endpoint ("\\?\SWD#MMDEVAPI#{id}#{KSCATEGORY_AUDIO render interface}"), what the WinRT audio APIs take
	/// </summary>
	internal static string WinRtId(string deviceId) => @"\\?\SWD#MMDEVAPI#" + deviceId + "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
}

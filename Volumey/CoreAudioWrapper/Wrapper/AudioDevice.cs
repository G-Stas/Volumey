#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using log4net;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Volumey.Helper;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// An audio endpoint device. Owns the NAudio device and releases it (with its endpoint volume and session manager) on dispose.
/// </summary>
public sealed class AudioDevice : IDevice
{
	internal MMDevice Device { get; }

	private readonly Lock propertiesLock = new Lock();
	private bool disposed;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(AudioDevice));

	internal AudioDevice(MMDevice device)
	{
		Device = device ?? throw new ArgumentNullException(nameof(device));
	}

	public string GetId()
	{
		try { return Device.ID ?? string.Empty; }
		catch { return string.Empty; }
	}

	public string GetFriendlyName() => GetProperty<string>(PropertyKeys.PKEY_Device_FriendlyName) ?? string.Empty;

	public string GetDeviceDesc() => GetProperty<string>(PropertyKeys.PKEY_Device_DeviceDesc) ?? string.Empty;

	/// <summary>
	/// A new icon owned by the caller, or null
	/// </summary>
	public Icon? GetIcon()
	{
		string? iconPath = GetProperty<string>(PropertyKeys.PKEY_Device_IconPath);
		try
		{
			return IconHelper.GetFromResourcePath(iconPath);
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to extract the device icon: [{iconPath}]", e);
			return null;
		}
	}

	public DataFlow GetDataFlow()
	{
		try { return Device.DataFlow; }
		catch { return DataFlow.All; }
	}

	public DeviceState GetState() => Device.State;

	public DeviceFormat? GetDeviceFormat()
	{
		try
		{
			var blob = GetProperty<byte[]>(PropertyKeys.PKEY_AudioEngine_DeviceFormat);
			if(blob != null)
				return DeviceFormat.FromWaveFormatEx(blob);
		}
		catch(Exception e)
		{
			Logger.Error("Couldn't read device format structure", e);
		}
		return null;
	}

	//the formats Windows' sound settings offer: 16 and 24 bit (24 also in 32-bit containers) and 32-bit integers,
	//at the common sample rates
	private static readonly (ushort bits, ushort container)[] CandidateSampleSizes = { (16, 16), (24, 24), (24, 32), (32, 32) };
	private static readonly uint[] CandidateSampleRates = { 44100, 48000, 88200, 96000, 176400, 192000, 352800, 384000 };

	public IReadOnlyList<DeviceFormatOption> GetSupportedFormats()
	{
		var supported = new List<DeviceFormatOption>();
		try
		{
			//the channels stay the device's: only the sample size and rate can be picked (as in Windows' list)
			var current = GetDeviceFormat();
			using var client = Device.CreateAudioClient();
			int channels = current?.Channels ?? client.MixFormat.Channels;
			int channelMask = (int)(current?.ChannelMask ?? 0);
			if(channelMask == 0 && client.MixFormat is WaveFormatExtensible mix)
				channelMask = mix.ChannelMask;
			foreach(var (bits, container) in CandidateSampleSizes)
			{
				foreach(uint rate in CandidateSampleRates)
				{
					//24 bit in 24- and in 32-bit containers are one choice; the first one the device takes is kept
					if(supported.Any(s => s.Bits == bits && s.SamplesPerSec == rate))
						continue;
					//what Windows' format list checks: integer PCM the device takes in exclusive mode
					var format = new WaveFormatExtensible((int)rate, container, channels, false, bits, channelMask);
					try
					{
						if(client.IsFormatSupported(AudioClientShareMode.Exclusive, format))
							supported.Add(new DeviceFormatOption(bits, container, rate));
					}
					catch(Exception e) when(e is not AudioExclusiveModeNotAllowedException)
					{
						//one format the driver can't check isn't a reason to drop the others
						Logger.Debug($"Couldn't check the format {bits} bit, {rate} Hz", e);
					}
				}
			}
		}
		catch(AudioExclusiveModeNotAllowedException)
		{
			//"Allow apps to take exclusive control" is off: Windows refuses every check, the page shows the current format only
			Logger.Info("The formats can't be checked while exclusive mode isn't allowed");
			supported.Clear();
		}
		catch(Exception e)
		{
			Logger.Error("Couldn't read the device's supported formats", e);
		}
		return supported.OrderBy(f => f.Bits).ThenBy(f => f.SamplesPerSec).ToList();
	}

	public IReadOnlyList<float> GetChannelVolumes()
	{
		try
		{
			var channels = Device.AudioEndpointVolume.Channels;
			var levels = new float[channels.Count];
			for(int i = 0; i < levels.Length; i++)
				levels[i] = channels[i].VolumeLevelScalar;
			return levels;
		}
		catch(Exception e)
		{
			Logger.Error("Couldn't read the device's channel volumes", e);
			return Array.Empty<float>();
		}
	}

	public void SetChannelVolume(int channel, float level)
	{
		var target = Device.AudioEndpointVolume.Channels[channel];
		//its own context, not VolumeGUID: the device volume listener must see it when it changes the device volume (see GuidValue)
		target.NotificationGuid = GuidValue.Internal.BalanceGUID;
		target.VolumeLevelScalar = Math.Clamp(level, 0f, 1f);
	}

	private Action? channelVolumesChanged;
	public event Action ChannelVolumesChanged
	{
		add
		{
			//listen to Windows only while someone listens here (the Devices page)
			if(channelVolumesChanged == null)
				Device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
			channelVolumesChanged += value;
		}
		remove
		{
			channelVolumesChanged -= value;
			if(channelVolumesChanged == null && !disposed)
				Device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
		}
	}

	private void OnVolumeNotification(AudioVolumeNotificationData data) => channelVolumesChanged?.Invoke();

	/// <summary>
	/// Reads a property from a freshly opened property store: NAudio keeps the store open, and a kept store doesn't show later changes
	/// (the device properties are read again when Windows reports that they changed).
	/// </summary>
	private T? GetProperty<T>(PropertyKey key) where T : class
	{
		lock(propertiesLock)
		{
			try
			{
				Device.Properties.Dispose();
				Device.GetPropertyInformation();
				return Device.Properties.TryGetValue(key, out T? value) ? value : null;
			}
			catch(Exception e)
			{
				Logger.Error($"Failed to read device property {key.formatId},{key.propertyId}", e);
				return null;
			}
		}
	}

	public void Dispose()
	{
		if(disposed)
			return;
		disposed = true;
		if(channelVolumesChanged != null)
		{
			channelVolumesChanged = null;
			try { Device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification; }
			catch { }
		}
		Device.Dispose();
	}
}

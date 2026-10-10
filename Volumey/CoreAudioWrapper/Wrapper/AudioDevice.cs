#nullable enable
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Media;
using log4net;
using NAudio.CoreAudioApi;
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

	public ImageSource? GetIconSource()
	{
		return GetIcon().GetAsImageSource();
	}

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
		Device.Dispose();
	}
}

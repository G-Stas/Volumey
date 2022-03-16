using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Helper;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public sealed class MMDevice : IDevice
	{
		public IMMDevice device { get; }

		private static ILog logger;
		private static ILog Logger => logger ??= LogManager.GetLogger(typeof(MMDevice));

		public MMDevice(IMMDevice dev)
		{
			this.device = dev;
		}
		
		public object Activate(Guid guid)
		{
			Marshal.ThrowExceptionForHR(this.device.Activate(ref guid, 0, IntPtr.Zero, out object comInterface));
			return comInterface;
		}

		public string GetId()
		{
			this.device.GetId(out string id);
			return id ?? string.Empty;
		}

		public string GetFriendlyName()
		{
			var key = PROPERTYKEY.DeviceProperties.FriendlyName;
			return this.GetProperty(ref key);
		}

		public string GetDeviceDesc()
		{
			var key = PROPERTYKEY.DeviceProperties.Description;
			return this.GetProperty(ref key);
		}

		public ImageSource GetIconSource()
		{
			return GetIcon().GetAsImageSource();
		}

		public Icon GetIcon()
		{
			Icon icon = null;
			PROPERTYKEY iconPathKey = PROPERTYKEY.DeviceProperties.IconPath;
			string iconPath = this.GetProperty(ref iconPathKey);

			if(string.IsNullOrEmpty(iconPath))
				return null;
			
			//try to extract the icon from DLL first since it's the most common location of the icons
			try
			{
				string[] endpointIconPath = iconPath.Split(",");
				
				if(endpointIconPath.Length < 2)
					throw new Exception("Resource path is invalid");
				
				string path = endpointIconPath[0];
				int id = int.Parse(endpointIconPath[1], CultureInfo.InvariantCulture);

				App.Current.Dispatcher.Invoke(() => icon = IconHelper.GetFromDll(path, id));
			}
			catch { }
			//try to extract the icon from file path
			if(icon == null)
			{
				try
				{
					App.Current.Dispatcher.Invoke(() => icon = IconHelper.GetFromFilePath(iconPath));
				}
				catch(Exception e)
				{
					Logger.Error($"Failed to extract device icon from the file path: [{iconPath}]", e);	
				}
			}
			return icon;
		}

		public EDataFlow GetDataFlow()
		{
			EDataFlow flow = EDataFlow.All;
			try
			{
				var guid = new Guid(GuidValue.External.IMMEndpoint);
				Marshal.QueryInterface(Marshal.GetIUnknownForObject(this.device), ref guid, out IntPtr endpointPtr);
				IMMEndpoint endpoint = (IMMEndpoint) Marshal.GetObjectForIUnknown(endpointPtr);
				endpoint.GetDataFlow(out flow);
			}
			catch {}
			return flow;
		}

		public DeviceState GetState()
		{
			this.device.GetState(out var state);
			return state;
		}

		private string GetProperty(ref PROPERTYKEY key)
		{
			string prop = null;
			try
			{
				device.OpenPropertyStore(STGM.READ, out IPropertyStore store);
				if(store != null)
				{
					store.GetValue(ref key, out PROPVARIANT propvariant);
					prop = propvariant.GetPwszValAsString();
				}
			}
			catch {}
			return prop ?? string.Empty;
		}
		
		public WAVEFORMATEX? GetDeviceFormat()
		{
			try
			{
				var key = PROPERTYKEY.DeviceProperties.DeviceFormat;
				device.OpenPropertyStore(STGM.READ, out IPropertyStore store);
				if(store != null)
				{
					store.GetValue(ref key, out PROPVARIANT propvariant);
					return Marshal.PtrToStructure<WAVEFORMATEX>(propvariant.blob.blobData);
				}
			}
			catch(Exception e)
			{
				Logger.Error("Couldn't read device format structure", e);
			}
			return null;
		}
	}
}
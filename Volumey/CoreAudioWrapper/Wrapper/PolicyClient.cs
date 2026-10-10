#nullable enable
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;

namespace Volumey.CoreAudioWrapper.Wrapper;

public class PolicyClient
{
	[ComImport, Guid(GuidValue.External.PolicyConfigCOM)]
	private class PolicyClientCOM { }

	private readonly IPolicyConfig instance = (IPolicyConfig)new PolicyClientCOM();

	/// <exception cref="COMException">Windows refused the switch (it used to fail silently)</exception>
	public void SetDefaultEndpointDevice(string deviceId)
	{
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Console));
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Multimedia));
		Marshal.ThrowExceptionForHR(instance.SetDefaultEndpoint(deviceId, ERole.Communications));
	}
}
